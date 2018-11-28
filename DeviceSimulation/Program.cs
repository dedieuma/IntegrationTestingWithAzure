using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Azure.Management.Compute.Fluent;
using Microsoft.Azure.Management.Compute.Fluent.Models;
using Microsoft.Azure.Management.Fluent;
using Microsoft.Azure.Management.ResourceManager.Fluent;
using Microsoft.Azure.Management.ResourceManager.Fluent.Authentication;
using Microsoft.Azure.Management.ResourceManager.Fluent.Core;
using Microsoft.IdentityModel.Clients.ActiveDirectory;
using Microsoft.Rest;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;
using Microsoft.Azure.Management.ResourceManager;
using Microsoft.Azure.Management.ResourceManager.Models;
using Microsoft.Azure.Devices;
using Microsoft.Azure.Devices.Common.Exceptions;
using IoTHubExamples.Core;
using Microsoft.Azure.Management.AppService.Fluent;
using System.IO;
using Microsoft.Azure.Management.CosmosDB.Fluent;
using Microsoft.Azure.Management.CosmosDB.Fluent.Models;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json;
using Microsoft.Azure.Management.Network.Fluent;
using Microsoft.Azure.Devices.Client;

namespace DeviceSimulation
{
    class Program
    {
        //https://github.com/Azure/azure-libraries-for-net
        //https://github.com/Azure-Samples/cosmosdb-dotnet-create-documentdb-and-get-mongodb-connection-string/blob/master/Program.cs
        //https://github.com/Azure-Samples/app-service-dotnet-configure-deployment-sources-for-functions
        static string rgName = "";
        static string deploymentName = "";
        static Region location = null;
        static string applicationId = "";
        static string subscriptionId = "";
        public static string tenantId = "";
        static string password = "";
        static string cosmosDBName = "";
        static string iotHubName = "";
        static string functionAppPrefix = ""; 
        static RegistryManager _registryManager;
        static string iotHubConnectionString;
        static IAzure azure;
        static Spinner spin;

        //Name of the storage account
        static string accountStorageName = "";

        static void Main(string[] args)
        {
            Console.WriteLine("Beginning... Press Any Key");
            Console.ReadLine();

            if (!InitializeVariables())
                return;
            InitializeResources();
            DeployTemplate();

            CreateCosmosDB();
            //CreateFunctionApp();
            //CreateDevice(1);
            //SendDataToDevices();
            Console.WriteLine("Done Main");
            Console.ReadLine();




        }

        private static bool InitializeVariables(string pathToParameters = "..\\..\\deviceSimulationParameters.json")
        {
            dynamic parameters = JObject.Parse(File.ReadAllText(pathToParameters));
            try
            {
                ParametersInvalid(parameters);
            }
            catch (ArgumentException e)
            {
                Console.Error.WriteLine(e.Message);
                Console.WriteLine("\nPress any key...");
                Console.ReadLine();
                return false;
            }

            dynamic credentials = parameters["credentials"];
            subscriptionId = credentials["subscription"];
            applicationId = credentials["client"];
            password = credentials["key"];
            tenantId = credentials["tenant"];
            rgName = parameters["rgName"];
            deploymentName = parameters["deploymentName"];
            accountStorageName = parameters["accountStorageName"];
            cosmosDBName = parameters["cosmosDBName"];
            iotHubName = parameters["iotHubName"];
            functionAppPrefix = parameters["functionAppNamePrefix"];


            // Change the location here
            location = Region.EuropeWest;
            WriteVariableIntoFiles();

            
            return true;

        }

        private static void WriteVariableIntoFiles()
        {

            // Writing azureauth.properties
            string credentialsToAzureAuth = "subscription=" +
                                                        subscriptionId +
                                                        "\r\nclient=" +
                                                        applicationId +
                                                        "\r\nkey=" +
                                                        password +
                                                        "\r\ntenant=" +
                                                        tenantId +
                                                        "\r\nmanagementURI=https://management.core.windows.net/" +
                                                        "\r\nbaseURL=https://management.azure.com/" +
                                                        "\r\nauthURL=https://login.windows.net/" +
                                                        "\r\ngraphURL=https://graph.windows.net/";

            File.WriteAllText("..\\..\\azureauth.properties", credentialsToAzureAuth);


            // Writing parameters for the deployment template
            dynamic templateJson = JObject.Parse(File.ReadAllText("..\\..\\Templates\\parameters.json"));
            templateJson["parameters"]["iotHubName"]["value"] = iotHubName;
            templateJson["parameters"]["cosmosName"]["value"] = cosmosDBName;
            templateJson["parameters"]["accountStorageName"]["value"] = accountStorageName;
            string templateUpdated = JsonConvert.SerializeObject(templateJson, Formatting.Indented);
            File.WriteAllText("..\\..\\Templates\\parameters.json", templateUpdated);
        }

        //TODO : enhance this method
        private static void ParametersInvalid(dynamic parameters)
        {

            dynamic credentials = parameters["credentials"];
            string current = Convert.ToString(credentials["subscription"]);
            if (String.IsNullOrWhiteSpace(current) || current.StartsWith("<"))
                throw new ArgumentException("subscription is invalid, inside the deviceSimulationParameters.json");

            current = Convert.ToString(credentials["client"]);
            if (String.IsNullOrWhiteSpace(current) || current.StartsWith("<"))
                throw new ArgumentException("client is invalid, inside the deviceSimulationParameters.json");

            current = Convert.ToString(credentials["key"]);
            if (String.IsNullOrWhiteSpace(current) || current.StartsWith("<"))
                throw new ArgumentException("key is invalid, inside the deviceSimulationParameters.json");

            current = Convert.ToString(credentials["tenant"]);
            if (String.IsNullOrWhiteSpace(current) || current.StartsWith("<"))
                throw new ArgumentException("tenant is invalid, inside the deviceSimulationParameters.json");

            current = parameters["rgName"];
            if (String.IsNullOrWhiteSpace(current) || current.StartsWith("<"))
                throw new ArgumentException("rgName is invalid, inside the deviceSimulationParameters.json");

            current = parameters["deploymentName"];
            if (String.IsNullOrWhiteSpace(current) || current.StartsWith("<"))
                throw new ArgumentException("deploymentName is invalid, inside the deviceSimulationParameters.json");

            current = parameters["accountStorageName"];
            if (String.IsNullOrWhiteSpace(current) || current.StartsWith("<"))
                throw new ArgumentException("accountStorageName is invalid, inside the deviceSimulationParameters.json");

            current = parameters["cosmosDBName"];
            if (String.IsNullOrWhiteSpace(current) || current.StartsWith("<"))
                throw new ArgumentException("cosmosDBName is invalid, inside the deviceSimulationParameters.json");

            current = parameters["iotHubName"];
            if (String.IsNullOrWhiteSpace(current) || current.StartsWith("<"))
                throw new ArgumentException("iotHubName is invalid, inside the deviceSimulationParameters.json");

            current = parameters["functionAppNamePrefix"];
            if (String.IsNullOrWhiteSpace(current) || current.StartsWith("<"))
                throw new ArgumentException("functionAppNamePrefix is invalid, inside the deviceSimulationParameters.json");

        }

        private static void InitializeResources()
        {
            AzureCredentials credentials = SdkContext.AzureCredentialsFactory.FromFile("../../azureauth.properties");

            azure = Azure.Configure().WithLogLevel(HttpLoggingDelegatingHandler.Level.Basic).Authenticate(credentials).WithDefaultSubscription();
            spin = new Spinner();


            // Getting the Azure AD application credentials
            //AzureCredentials credentials = SdkContext.AzureCredentialsFactory.FromFile("../../azureauth.properties");

            // Logging to my Azure AD
            //azure = Azure.Configure().WithLogLevel(HttpLoggingDelegatingHandler.Level.Basic).Authenticate(credentials).WithDefaultSubscription();

            // Creating a new ResourceGroup with the name of rgName
            var resourceGroup = azure.ResourceGroups.Define(rgName).WithRegion(location).Create();


            Console.WriteLine($"Resource Group {rgName} created");
            // Creating the storage linked to the resource Group
            var storage = azure.StorageAccounts.Define(accountStorageName).WithRegion(location).WithExistingResourceGroup(resourceGroup).Create();
            Console.WriteLine($"Storage {accountStorageName} created");
            var storageKeys = storage.GetKeys();
            string storageConnectionString = "DefaultEndpointsProtocol=https;"
                + "AccountName=" + storage.Name
                + ";AccountKey=" + storageKeys[0].Value
                + ";EndpointSuffix=core.windows.net";

            //Connexion to our newly created storage account and setting up our service blob client
            CloudStorageAccount cloudStorageAccount = CloudStorageAccount.Parse(storageConnectionString);
            CloudBlobClient serviceClient = cloudStorageAccount.CreateCloudBlobClient();


            // Creating our container
            Console.WriteLine("Creating container...");
            CloudBlobContainer container = serviceClient.GetContainerReference("templates");
            container.CreateIfNotExistsAsync().GetAwaiter().GetResult();
            BlobContainerPermissions containerPermissions = new BlobContainerPermissions() { PublicAccess = BlobContainerPublicAccessType.Container };
            container.SetPermissionsAsync(containerPermissions).GetAwaiter().GetResult();
            //spin.Stop();



            //Uploading to the container our template file
            //spin.setMessage("Uploading template file...");
            //spin.Start();
            Console.WriteLine("Uploading template file...");
            CloudBlockBlob templateBlob = container.GetBlockBlobReference("template.json");
            templateBlob.UploadFromFileAsync("..\\..\\Templates\\template.json").GetAwaiter().GetResult();

            //spin.Stop();
            //uploading to the container our parameter file
            //spin.setMessage("Uploading parameter file...");
            //spin.Start();
            Console.WriteLine("Uploading parameter file...");
            CloudBlockBlob paramBlob = container.GetBlockBlobReference("parameters.json");
            paramBlob.UploadFromFileAsync("..\\..\\Templates\\parameters.json").GetAwaiter().GetResult();


        }

        private static void DeployTemplate()
        {
            Console.WriteLine("Deploying Template...");
            // string templatePath = "https://" + acccountStorageName + ".blob.core.windows.net/templates/templateIotHub.json";
            // string paramPath = "https://" + acccountStorageName + ".blob.core.windows.net/templates/parameters.json";
            // var deployment = azure.Deployments.Define("Deployment_01")
            //                     .WithExistingResourceGroup(resourceGroup)
            //                     .WithTemplateLink(templatePath, "1.0.0.0")
            //                     .WithParametersLink(paramPath, "1.0.0.0")
            //                     .WithMode(DeploymentMode.Incremental)
            //                     .Create();

            // IDeployment toto = await azure.Deployments.GetByNameAsync("Deployment_01");
            Microsoft.Azure.Management.ResourceManager.ResourceManagementClient client;

            var authContext = new AuthenticationContext(string.Format("https://login.microsoftonline.com/{0}", tenantId));

            var credential = new ClientCredential(applicationId, password);
            AuthenticationResult token = authContext.AcquireTokenAsync("https://management.core.windows.net/", credential).Result;


            if (token == null)
            {
                Console.WriteLine("Failed to obtain the token");
                return;
            }

            var creds = new TokenCredentials(token.AccessToken);
            client = new Microsoft.Azure.Management.ResourceManager.ResourceManagementClient(creds);
            client.SubscriptionId = subscriptionId;


            var createResponse = client.Deployments.CreateOrUpdate(rgName, deploymentName, new Microsoft.Azure.Management.ResourceManager.Models.Deployment()
            {
                Properties = new DeploymentProperties
                {
                    Mode = Microsoft.Azure.Management.ResourceManager.Models.DeploymentMode.Incremental,
                    TemplateLink = new Microsoft.Azure.Management.ResourceManager.Models.TemplateLink
                    {
                        Uri = "https://" + accountStorageName + ".blob.core.windows.net/templates/template.json"
                    },
                    ParametersLink = new Microsoft.Azure.Management.ResourceManager.Models.ParametersLink
                    {
                        Uri = "https://" + accountStorageName + ".blob.core.windows.net/templates/parameters.json"
                    }
                }
            });

            string state = createResponse.Properties.ProvisioningState;
            Console.WriteLine("Deployment state: {0}", state);

            if (state != "Succeeded")
            {
                Console.WriteLine("Failed to deploy the template");
            }
            Console.WriteLine(createResponse.Properties.Outputs);
            WriteKeysIntoFiles(createResponse.Properties.Outputs);
            Console.WriteLine("Done");
            //Console.ReadLine();
        }

        private static void WriteKeysIntoFiles(object outputs)
        {
            dynamic jsonOutputs = JObject.Parse(outputs.ToString());

            // Write the Event Hub key into the function.json of the azure function app
            dynamic jsonFunction = JObject.Parse(File.ReadAllText("..\\..\\FunctionAppCore\\IoTHubTrigger\\function.json"));
            dynamic bindings = jsonFunction["bindings"];
            //bindings[0]["connection"] = outputs[""];
            //bindings[0]["connection"] = jsonOutputs["eventHubEndPoint"]["value"] + ";EntityPath=dedieumaiothubcreation";
            jsonFunction["bindings"] = bindings;
            string jsonUpdated = JsonConvert.SerializeObject(jsonFunction, Formatting.Indented);
            File.WriteAllText(@"..\..\FunctionAppCore\IoTHubTrigger\function.json", jsonUpdated);

            // Write the iot hub connection string into the config.yaml, used for device creation
            const string configFilePath = @"../../config.yaml";
            iotHubConnectionString = jsonOutputs["hubKeys"]["value"];
            IoTHubExamples.Core.Configuration config = configFilePath.GetIoTConfiguration();
            config.AzureIoTHubConfig.ConnectionString = iotHubConnectionString;
            string[] connectionStrSplitted = iotHubConnectionString.Split(new char[] { ';', '=' });
            config.AzureIoTHubConfig.IoTHubUri = connectionStrSplitted[1];
            if (configFilePath.UpdateIoTConfiguration(config).Item1)
            {
                Console.WriteLine($"Saved iot hub connection string {jsonOutputs["hubKeys"]["value"]}");
            }


        }


        private static void SendDataToDevices()
        {
            const string configFilePath = @"../../config.yaml";
            IoTHubExamples.Core.Configuration config = configFilePath.GetIoTConfiguration();
            List<DeviceConfig> testDevices = config.DeviceConfigs;

            foreach (DeviceConfig device in testDevices)
            {
                string connectionDevice = "HostName="
                                            + config.AzureIoTHubConfig.IoTHubUri
                                            + ";DeviceId="
                                            + device.DeviceId
                                            + ";SharedAccessKey="
                                            + device.Key;

                Random rand = new Random();
                double temperature = 20 + rand.NextDouble() * 15;
                double humidity = 60 + rand.NextDouble() * 20;

                var telemetryDataPoint = new
                {
                    temperature,
                    humidity,
                    date = DateTime.Now
                };

                string messageString = JsonConvert.SerializeObject(telemetryDataPoint);
                Microsoft.Azure.Devices.Client.Message message = new Microsoft.Azure.Devices.Client.Message(Encoding.ASCII.GetBytes(messageString));

                message.Properties.Add("temperatureAlert", (temperature) > 30 ? "true" : "false");

                DeviceClient.CreateFromConnectionString(connectionDevice, Microsoft.Azure.Devices.Client.TransportType.Mqtt).SendEventAsync(message);
                Console.WriteLine("Sending Message : {0}", messageString);
            }






        }

        private static void CreateCosmosDB()
        {

            Console.WriteLine("Creating a Cosmos DB...");
            //ICosmosDBAccount cosmosDBAccount = await azure.CosmosDBAccounts.Define(cosmosDBName)
            //                                   .WithRegion(Region.EuropeWest)
            //                                   .WithExistingResourceGroup(rgName)
            //                                   .WithKind(DatabaseAccountKind.MongoDB)
            //                                   .WithEventualConsistency()
            //                                   .WithWriteReplication(Region.EuropeWest)
            //                                   .WithReadReplication(Region.EuropeWest)
            //                                   .CreateAsync();
            ICosmosDBAccount cosmosDBAccount = azure.CosmosDBAccounts.GetByResourceGroup(rgName, cosmosDBName);

            Console.WriteLine("CosmosDB Successfully created : " + cosmosDBAccount.Name);
            Console.WriteLine("Getting credentials for CosmosDB...");

            var databaseAccountListKeysResult = cosmosDBAccount.ListKeys();
            string masterKey = databaseAccountListKeysResult.PrimaryMasterKey;
            string endPoint = cosmosDBAccount.DocumentEndpoint;
            string primaryConnectionString = "AccountEndpoint="
                                             + endPoint
                                             + ";AccountKey="
                                             + masterKey
                                             + ";";
            //mongodb://dedieumadocdb458:1A6qwNUSsKS3Yk4Ste19DAYL6MZa17szLzyIFOg3jm08bTaI4lzqqN0LJtgVp0qyMAyzTYP8UJGzFzEXPaIZLw==@dedieumadocdb458.documents.azure.com:10255/?ssl=true

            //Console.WriteLine("Get the MongoDB connection string");
            //var databaseAccountListConnectionStringsResult = cosmosDBAccount.ListConnectionStrings();
            //Console.WriteLine("MongoDB connection string: "
            //+ databaseAccountListConnectionStringsResult.ConnectionStrings[0].ConnectionString);

            //string primaryConnectionString = databaseAccountListConnectionStringsResult.ConnectionStrings[0].ConnectionString;
            Console.WriteLine($"CosmosDb {cosmosDBName} with the connection string {primaryConnectionString}");
            //string url = "dedieumadocdb458.documents.azure.com";
            //string[] splitted = primaryConnectionString.Split(new string[] { url }, StringSplitOptions.None);
            //string port = splitted[splitted.Length - 1].Substring(0, splitted[splitted.Length - 1].IndexOf('/'));
            //port = port.TrimStart(new char[] { ':' });
            //splitted = splitted[0].Split(':');
            //string primaryKey = splitted[splitted.Length - 1].TrimEnd(new char[] { '@' });
            //Console.WriteLine($"url : {url} port : {port} primaryKey : {primaryKey}");

            dynamic jsonFunction = JObject.Parse(File.ReadAllText("..\\..\\FunctionAppCore\\IoTHubTrigger\\function.json"));
            dynamic bindings = jsonFunction["bindings"];
            bindings[1]["databaseName"] = cosmosDBName;
            //bindings[1]["connection"] = primaryConnectionString;

            jsonFunction["bindings"] = bindings;
            string jsonUpdated = JsonConvert.SerializeObject(jsonFunction, Formatting.Indented);
            File.WriteAllText(@"..\..\FunctionAppCore\IoTHubTrigger\function.json", jsonUpdated);


        }

        private static void CreateFunctionApp()
        {
            string appName = SdkContext.RandomResourceName(functionAppPrefix, 20);
            string suffix = ".azurewebsites.net";
            string appUrl = appName + suffix;
            Console.WriteLine("Creating function app " + appName + " in resource group " + rgName + "...");
            //Console.ReadLine();

            IFunctionApp app1 = azure.AppServices.FunctionApps.Define(appName)
                                .WithRegion(Region.EuropeWest)
                                .WithExistingResourceGroup(rgName)
                                .Create();


            Console.WriteLine("Created Function App");
            Console.WriteLine(app1);
            app1.Deploy();
            // Deploy

            Console.WriteLine("");
            Console.WriteLine("Deploying to function app" + appName + " with FTP...");

            IPublishingProfile profile = app1.GetPublishingProfile();
            Utilities.UploadFileToFunctionApp(profile, Path.Combine(Utilities.ProjectPath, "FunctionAppCore", "host.json"));
            Utilities.UploadFileToFunctionApp(profile, Path.Combine(Utilities.ProjectPath, "FunctionAppCore", "IoTHubTrigger", "function.json"), "IoTHubTrigger/function.json");
            Utilities.UploadFileToFunctionApp(profile, Path.Combine(Utilities.ProjectPath, "FunctionAppCore", "IoTHubTrigger", "run.csx"), "IoTHubTrigger/run.csx");
            //sync triggers
            app1.SyncTriggers();


            Console.WriteLine("Deployment iotHubTrigger to web app" + app1.Name + " completed");

            //warm up
            //Console.WriteLine("Warming up " + appUrl + "/api/IoTHubTrigger");
            //Utilities.PostAddress("http://" + appUrl + "/api/IoTHubTrigger", "toto");
            //SdkContext.DelayProvider.Delay(5000);
            //Console.WriteLine("Curling...");
            //Console.WriteLine(Utilities.PostAddress("http://" + appUrl + "/api/IoTHubTrigger", "toto"));

        }

        private static void CreateDevice(int numberOfDevices)
        {
            const string configFilePath = @"../../config.yaml";
            IoTHubExamples.Core.Configuration config = configFilePath.GetIoTConfiguration();
            List<DeviceConfig> testDevices = config.DeviceConfigs;
            AzureIoTHubConfig azureConfig = config.AzureIoTHubConfig;

            _registryManager = RegistryManager.CreateFromConnectionString(azureConfig.ConnectionString);

            testDevices = config.DeviceConfigs = new System.Collections.Generic.List<DeviceConfig>();

            for (int deviceNumber = 0; deviceNumber < numberOfDevices; deviceNumber++)
            {
                var testDevice = new DeviceConfig()
                {
                    DeviceId = $"{"test"}{deviceNumber:0000}",
                    Nickname = $"{"test"}{deviceNumber:0000}",
                    Status = "Enabled"
                };
                testDevices.Add(testDevice);

                Task<string> task = AddDeviceAsync(testDevice);
                task.Wait();

                testDevice.Key = task.Result;
            }

            if (configFilePath.UpdateIoTConfiguration(config).Item1)
            {
                foreach (var testDevice in testDevices)
                {
                    Console.WriteLine(
                        $"DeviceId: {testDevice.DeviceId} has DeviceKey: {testDevice.Key} \r\nConfig file: {configFilePath} has been updated accordingly.");
                }
            }

        }

        private static async Task<string> AddDeviceAsync(DeviceConfig deviceConfig)
        {
            Device device;
            try
            {
                DeviceStatus status;
                if (!Enum.TryParse(deviceConfig.Status, true, out status))
                    status = DeviceStatus.Disabled;

                var d = new Device(deviceConfig.DeviceId)
                {
                    Status = status
                };
                device = await _registryManager.AddDeviceAsync(d);

                Console.WriteLine($"Device: {deviceConfig.DeviceId} created");
            }
            catch (DeviceAlreadyExistsException)
            {
                device = await _registryManager.GetDeviceAsync(deviceConfig.DeviceId);

                Console.WriteLine($"Device: {deviceConfig.DeviceId} already exist");

            }
            return device.Authentication.SymmetricKey.PrimaryKey;
        }

        

        


     
    }
}
