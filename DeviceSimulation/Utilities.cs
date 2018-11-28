using Microsoft.Azure.Management.AppService.Fluent;
using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using CoreFtp;
using System.Net.Http;
using Microsoft.Azure.Management.Network.Fluent;
using System.Text;

namespace DeviceSimulation
{
    class Utilities
    {
        public static bool IsRunningMocked { get; set; }
        public static string ProjectPath { get; set; }

        static Utilities()
        {
            ProjectPath = "..\\..";
        }

        public static void UploadFileToFunctionApp(IPublishingProfile profile, string filePath, string fileName = null)
        {
            if (!IsRunningMocked)
            {
                string host = profile.FtpUrl.Split(new char[] { '/' }, 2)[0];

                using (var ftpClient = new FtpClient(new FtpClientConfiguration
                {
                    Host = host,
                    Username = profile.FtpUsername,
                    Password = profile.FtpPassword
                }))
                {
                    var fileinfo = new FileInfo(filePath);
                    ftpClient.LoginAsync().GetAwaiter().GetResult();
                    if (!ftpClient.ListDirectoriesAsync().GetAwaiter().GetResult().Any(fni => fni.Name == "site"))
                    {
                        ftpClient.CreateDirectoryAsync("site").GetAwaiter().GetResult();
                    }
                    ftpClient.ChangeWorkingDirectoryAsync("./site").GetAwaiter().GetResult();
                    if (!ftpClient.ListDirectoriesAsync().GetAwaiter().GetResult().Any(fni => fni.Name == "wwwroot"))
                    {
                        ftpClient.CreateDirectoryAsync("wwwroot").GetAwaiter().GetResult();
                    }
                    ftpClient.ChangeWorkingDirectoryAsync("./wwwroot").GetAwaiter().GetResult();

                    if (fileName == null)
                    {
                        fileName = Path.GetFileName(filePath);
                    }
                    while (fileName.Contains("/"))
                    {
                        int slash = fileName.IndexOf("/");
                        string subDir = fileName.Substring(0, slash);
                        ftpClient.CreateDirectoryAsync(subDir).GetAwaiter().GetResult();
                        ftpClient.ChangeWorkingDirectoryAsync("./" + subDir).GetAwaiter().GetResult();
                        fileName = fileName.Substring(slash + 1);
                    }

                    using (var writeStream = ftpClient.OpenFileWriteStreamAsync(fileName).GetAwaiter().GetResult())
                    {
                        var fileReadStream = fileinfo.OpenRead();
                        fileReadStream.CopyToAsync(writeStream).GetAwaiter().GetResult();
                    }
                }
            }
        }

        public static string PostAddress(string url, string body, IDictionary<string, string> headers = null)
        {
            if (!IsRunningMocked)
            {
                try
                {
                    using (var client = new HttpClient())
                    {
                        if (headers != null)
                        {
                            foreach (var header in headers)
                            {
                                client.DefaultRequestHeaders.Add(header.Key, header.Value);
                            }
                        }
                        return client.PostAsync(url, new StringContent(body)).Result.ToString();
                    }
                }
                catch (Exception ex)
                {
                    Console.Error.WriteLine(ex);
                }
            }

            return "[Running in PlaybackMode]";
        }

        public static void PrintAppGateway(IApplicationGateway resource)
        {
            var info = new StringBuilder();
            info.Append("App gateway: ").Append(resource.Id)
                    .Append("Name: ").Append(resource.Name)
                    .Append("\n\tResource group: ").Append(resource.ResourceGroupName)
                    .Append("\n\tRegion: ").Append(resource.Region)
                    .Append("\n\tTags: ").Append(resource.Tags.ToString())
                    .Append("\n\tSKU: ").Append(resource.Sku.ToString())
                    .Append("\n\tOperational state: ").Append(resource.OperationalState)
                    .Append("\n\tInternet-facing? ").Append(resource.IsPublic)
                    .Append("\n\tInternal? ").Append(resource.IsPrivate)
                    .Append("\n\tDefault private IP address: ").Append(resource.PrivateIPAddress)
                    .Append("\n\tPrivate IP address allocation method: ").Append(resource.PrivateIPAllocationMethod)
                    .Append("\n\tDisabled SSL protocols: ").Append(resource.DisabledSslProtocols);

            // Show IP configs
            var ipConfigs = resource.IPConfigurations;
            info.Append("\n\tIP configurations: ").Append(ipConfigs.Count);
            foreach (var ipConfig in ipConfigs.Values)
            {
                info.Append("\n\t\tName: ").Append(ipConfig.Name)
                    .Append("\n\t\t\tNetwork id: ").Append(ipConfig.NetworkId)
                    .Append("\n\t\t\tSubnet name: ").Append(ipConfig.SubnetName);
            }

            // Show frontends
            var frontends = resource.Frontends;
            info.Append("\n\tFrontends: ").Append(frontends.Count);
            foreach (var frontend in frontends.Values)
            {
                info.Append("\n\t\tName: ").Append(frontend.Name)
                    .Append("\n\t\t\tPublic? ").Append(frontend.IsPublic);

                if (frontend.IsPublic)
                {
                    // Show public frontend info
                    info.Append("\n\t\t\tPublic IP address ID: ").Append(frontend.PublicIPAddressId);
                }

                if (frontend.IsPrivate)
                {
                    // Show private frontend info
                    info.Append("\n\t\t\tPrivate IP address: ").Append(frontend.PrivateIPAddress)
                        .Append("\n\t\t\tPrivate IP allocation method: ").Append(frontend.PrivateIPAllocationMethod)
                        .Append("\n\t\t\tSubnet name: ").Append(frontend.SubnetName)
                        .Append("\n\t\t\tVirtual network ID: ").Append(frontend.NetworkId);
                }
            }

            // Show backends
            var backends = resource.Backends;
            info.Append("\n\tBackends: ").Append(backends.Count);
            foreach (var backend in backends.Values)
            {
                info.Append("\n\t\tName: ").Append(backend.Name)
                    .Append("\n\t\t\tAssociated NIC IP configuration IDs: ").Append(string.Join(", ", backend.BackendNicIPConfigurationNames.Keys.ToArray()));

                // Show addresses
                var addresses = backend.Addresses;
                info.Append("\n\t\t\tAddresses: ").Append(addresses.Count);
                foreach (var address in addresses)
                {
                    info.Append("\n\t\t\t\tFQDN: ").Append(address.Fqdn)
                        .Append("\n\t\t\t\tIP: ").Append(address.IpAddress);
                }
            }

            // Show backend HTTP configurations
            var httpConfigs = resource.BackendHttpConfigurations;
            info.Append("\n\tHTTP Configurations: ").Append(httpConfigs.Count);
            foreach (var httpConfig in httpConfigs.Values)
            {
                info.Append("\n\t\tName: ").Append(httpConfig.Name)
                    .Append("\n\t\t\tCookie based affinity: ").Append(httpConfig.CookieBasedAffinity)
                    .Append("\n\t\t\tPort: ").Append(httpConfig.Port)
                    .Append("\n\t\t\tRequest timeout in seconds: ").Append(httpConfig.RequestTimeout)
                    .Append("\n\t\t\tProtocol: ").Append(httpConfig.Protocol.ToString())
                    .Append("\n\t\tHost header: ").Append(httpConfig.HostHeader)
                    .Append("\n\t\tHost header comes from backend? ").Append(httpConfig.IsHostHeaderFromBackend)
                    .Append("\n\t\tConnection draining timeout in seconds: ").Append(httpConfig.ConnectionDrainingTimeoutInSeconds)
                    .Append("\n\t\tAffinity cookie name: ").Append(httpConfig.AffinityCookieName)
                    .Append("\n\t\tPath: ").Append(httpConfig.Path);

                var probe = httpConfig.Probe;
                if (probe != null)
                {
                    info.Append("\n\t\tProbe: " + probe.Name);
                }
            }

            // Show SSL certificates
            var sslCerts = resource.SslCertificates;
            info.Append("\n\tSSL certificates: ").Append(sslCerts.Count);
            foreach (var cert in sslCerts.Values)
            {
                info.Append("\n\t\tName: ").Append(cert.Name)
                    .Append("\n\t\t\tCert data: ").Append(cert.PublicData);
            }

            // Show redirect configurations
            var redirects = resource.RedirectConfigurations;
            info.Append("\n\tRedirect configurations: ").Append(redirects.Count);
            foreach (IApplicationGatewayRedirectConfiguration redirect in redirects.Values)
            {
                info.Append("\n\t\tName: ").Append(redirect.Name)
                    .Append("\n\t\tTarget URL: ").Append(redirect.Type)
                    .Append("\n\t\tTarget URL: ").Append(redirect.TargetUrl)
                    .Append("\n\t\tTarget listener: ").Append(redirect.TargetListener?.Name)
                    .Append("\n\t\tIs path included? ").Append(redirect.IsPathIncluded)
                    .Append("\n\t\tIs query string included? ").Append(redirect.IsQueryStringIncluded)
                    .Append("\n\t\tReferencing request routing rules: ").Append(string.Join(", ", redirect.RequestRoutingRules.Keys.ToArray()));
            }

            // Show HTTP listeners
            var listeners = resource.Listeners;
            info.Append("\n\tHTTP listeners: ").Append(listeners.Count);
            foreach (var listener in listeners.Values)
            {
                info.Append("\n\t\tName: ").Append(listener.Name)
                    .Append("\n\t\t\tHost name: ").Append(listener.HostName)
                    .Append("\n\t\t\tServer name indication required? ").Append(listener.RequiresServerNameIndication)
                    .Append("\n\t\t\tAssociated frontend name: ").Append(listener.Frontend.Name)
                    .Append("\n\t\t\tFrontend port name: ").Append(listener.FrontendPortName)
                    .Append("\n\t\t\tFrontend port number: ").Append(listener.FrontendPortNumber)
                    .Append("\n\t\t\tProtocol: ").Append(listener.Protocol.ToString());
                if (listener.SslCertificate != null)
                {
                    info.Append("\n\t\t\tAssociated SSL certificate: ").Append(listener.SslCertificate.Name);
                }
            }

            // Show request routing rules
            var rules = resource.RequestRoutingRules;
            info.Append("\n\tRequest routing rules: ").Append(rules.Count);
            foreach (var rule in rules.Values)
            {
                info.Append("\n\t\tName: ").Append(rule.Name)
                    .Append("\n\t\t\tType: ").Append(rule.RuleType.ToString())
                    .Append("\n\t\t\tPublic IP address ID: ").Append(rule.PublicIPAddressId)
                    .Append("\n\t\t\tHost name: ").Append(rule.HostName)
                    .Append("\n\t\t\tServer name indication required? ").Append(rule.RequiresServerNameIndication)
                    .Append("\n\t\t\tFrontend port: ").Append(rule.FrontendPort)
                    .Append("\n\t\t\tFrontend protocol: ").Append(rule.FrontendProtocol.ToString())
                    .Append("\n\t\t\tBackend port: ").Append(rule.BackendPort)
                    .Append("\n\t\t\tCookie based affinity enabled? ").Append(rule.CookieBasedAffinity)
                    .Append("\n\t\tRedirect configuration: ").Append(rule.RedirectConfiguration?.Name ?? "(none)");

                // Show backend addresses
                var addresses = rule.BackendAddresses;
                info.Append("\n\t\t\tBackend addresses: ").Append(addresses.Count);
                foreach (var address in addresses)
                {
                    info.Append("\n\t\t\t\t")
                        .Append(address.Fqdn)
                        .Append(" [").Append(address.IpAddress).Append("]");
                }


                info
                    // Show SSL cert
                    .Append("\n\t\t\tSSL certificate name: ").Append(rule.SslCertificate?.Name ?? "(none)")

                    // Show backend
                    .Append("\n\t\t\tAssociated backend address pool: ").Append(rule.Backend?.Name ?? "(none)")

                    // Show backend HTTP settings config
                    .Append("\n\t\t\tAssociated backend HTTP settings configuration: ").Append(rule.BackendHttpConfiguration?.Name ?? "(none)")

                    // Show frontend listener
                    .Append("\n\t\t\tAssociated frontend listener: ").Append(rule.Listener?.Name ?? "(none)");
            }

            // Show probes  
            var probes = resource.Probes;
            info.Append("\n\tProbes: ").Append(probes.Count);
            foreach (var probe in probes.Values)
            {
                info.Append("\n\t\tName: ").Append(probe.Name)
                    .Append("\n\t\tProtocol:").Append(probe.Protocol.ToString())
                    .Append("\n\t\tInterval in seconds: ").Append(probe.TimeBetweenProbesInSeconds)
                    .Append("\n\t\tRetries: ").Append(probe.RetriesBeforeUnhealthy)
                    .Append("\n\t\tTimeout: ").Append(probe.TimeoutInSeconds)
                    .Append("\n\t\tHost: ").Append(probe.Host)
                    .Append("\n\t\tHealthy HTTP response status code ranges: ").Append(probe.HealthyHttpResponseStatusCodeRanges)
                    .Append("\n\t\tHealthy HTTP response body contents: ").Append(probe.HealthyHttpResponseBodyContents);
            }

            Console.WriteLine(info.ToString());
        }

    }
}
