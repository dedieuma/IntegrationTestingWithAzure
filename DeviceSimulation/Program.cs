using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
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
using Newtonsoft.Json.Linq;
using Newtonsoft.Json;
using Microsoft.Azure.Devices.Client;

namespace DeviceSimulation
{
    public class Program
    {
        //https://docs.microsoft.com/en-us/azure/iot-hub/iot-hub-rm-template
        //https://azure.microsoft.com/fr-fr/resources/samples/resource-manager-dotnet-resources-and-groups/
        //https://stackoverflow.com/questions/31684821/how-to-add-application-to-azure-ad-programmatically
        //https://docs.microsoft.com/en-us/azure/virtual-machines/windows/csharp-template
        //https://docs.microsoft.com/en-us/azure/iot-dps/quick-setup-auto-provision-rm
        //https://github.com/Microsoft/iot-samples/tree/master/DeviceManagement/csharp
        //https://github.com/Azure/azure-libraries-for-net
        //https://github.com/Azure-Samples/cosmosdb-dotnet-create-documentdb-and-get-mongodb-connection-string/blob/master/Program.cs
        //https://github.com/Azure-Samples/app-service-dotnet-configure-deployment-sources-for-functions

        // Resource Group Name
        static string rgName = "";
        // Name of the deployment with the template
        static string deploymentName = "";
        // Location. West Europe Used
        static Region location = null;
        // Azure AD Application ID
        static string applicationId = "";
        // Azure Subscription ID
        static string subscriptionId = "";
        // Azure AD Tenant ID
        static string tenantId = "";
        // Azure AD Password 
        static string password = "";
        // Name of the cosmos DB
        public static string cosmosDBName = "";
        // Name of the IOT Hub
        static string iotHubName = "";
        // Url endpoint to cosmos DB
        public static string endPointCosmosDB = "";
        // Key access to cosmos DB
        public static string accountKeyCosmosDB = "";
        // Prefix of the function app
        static string functionAppPrefix = ""; 
        static RegistryManager _registryManager;
        // Connection stirng to the iot hub
        static string iotHubConnectionString;
        // Azure Manager object
        static IAzure azure;
        // SPIN TO WIN
        static Spinner spin;
        // List of the iot devices
        public static List<DeviceConfig> deviceConfigs;
        //Name of the storage account
        static string accountStorageName = "";

        //static void Main(string[] args)
        //{
        //    Console.WriteLine("Beginning... Press Any Key");
        //    Console.ReadLine();
        //    spin = new Spinner();

        //    if (!InitializeVariables())
        //        return;
        //    InitializeResources();
        //    DeployTemplate();

        //    CreateCosmosDB();
        //    CreateFunctionApp();
        //    CreateDevice(1);
        //    SendDataToDevices();
        //    Console.WriteLine("Done Main");
        //    Console.ReadLine();
        //    Console.WriteLine("Deleting Resource Group...");
        //    Console.ReadLine();
        //    DeleteResourceGroup();

        //}


        /// <summary>
        /// Initialize the program variables with the parameters from the file path from the parameter pathToParameters.
        /// Default file : deviceSimulationParameters.json
        /// Write some variables into other specific files.
        /// </summary>
        /// <param name="pathToParameters">The path to the json file containing the variable data</param>
        /// <returns>true if succeed, false if the parameters from the json file are invalid</returns>
        public static bool InitializeVariables(string pathToParameters = "..\\..\\deviceSimulationParameters.json")
        {
            spin.setMessage("Initilize variables...");
            spin.Start();
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

            spin.Stop();
            
            return true;

        }

        /// <summary>
        /// Write the variables into specific files :
        /// azureauth.properties : useful to authentificate our IAzure object
        /// parameters.json : useful for the template deployment
        /// </summary>
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
        /// <summary>
        /// check if the parameters are valid, aka they are not empty or null and they don't begin with a <
        /// </summary>
        /// <param name="parameters">the json dynamic object from deviceSimulationParameters.json</param>
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

        /// <summary>
        /// Initialize Resources on Azure. Create a resource group, a storage, a container inside the storage, 
        /// and update template.json and parameter.json onto it. They will be used for deployment.
        /// </summary>
        public static void InitializeResources()
        {
            Console.WriteLine("Initialize Resources...");
            // Getting the Azure AD application credentials
            AzureCredentials credentials = SdkContext.AzureCredentialsFactory.FromFile("../../azureauth.properties");
            // Logging to my Azure AD
            azure = Azure.Configure().WithLogLevel(HttpLoggingDelegatingHandler.Level.Basic).Authenticate(credentials).WithDefaultSubscription();


            // Creating a new ResourceGroup with the name of rgName
            spin.setMessage("Creating a resource Group...");
            spin.Start();
            var resourceGroup = azure.ResourceGroups.Define(rgName).WithRegion(location).Create();
            spin.Stop();
            Console.WriteLine($"Resource Group {rgName} created");

            // Creating the storage linked to the resource Group
            spin.setMessage($"Creating the storage {accountStorageName}...");
            spin.Start();
            var storage = azure.StorageAccounts.Define(accountStorageName).WithRegion(location).WithExistingResourceGroup(resourceGroup).Create();
            
           
            var storageKeys = storage.GetKeys();
            string storageConnectionString = "DefaultEndpointsProtocol=https;"
                + "AccountName=" + storage.Name
                + ";AccountKey=" + storageKeys[0].Value
                + ";EndpointSuffix=core.windows.net";

            //Connexion to our newly created storage account and setting up our service blob client
            CloudStorageAccount cloudStorageAccount = CloudStorageAccount.Parse(storageConnectionString);
            CloudBlobClient serviceClient = cloudStorageAccount.CreateCloudBlobClient();


            // Creating our container
            //Console.WriteLine("Creating container...");
            CloudBlobContainer container = serviceClient.GetContainerReference("templates");
            container.CreateIfNotExistsAsync().GetAwaiter().GetResult();
            BlobContainerPermissions containerPermissions = new BlobContainerPermissions() { PublicAccess = BlobContainerPublicAccessType.Container };
            container.SetPermissionsAsync(containerPermissions).GetAwaiter().GetResult();

            Console.WriteLine($"Storage {accountStorageName} created");
            spin.Stop();




            //Uploading to the container our template file
            spin.setMessage("Uploading template file...");
            spin.Start();
            CloudBlockBlob templateBlob = container.GetBlockBlobReference("template.json");
            templateBlob.UploadFromFileAsync("..\\..\\Templates\\template.json").GetAwaiter().GetResult();

            //uploading to the container our parameter file
            Console.WriteLine("Uploading parameter file...");
            CloudBlockBlob paramBlob = container.GetBlockBlobReference("parameters.json");
            paramBlob.UploadFromFileAsync("..\\..\\Templates\\parameters.json").GetAwaiter().GetResult();
            spin.Stop();


        }

        /// <summary>
        /// Deploy the template from the template file template.json. Write the keys returned by the deployment into files
        /// </summary>
        public static void DeployTemplate()
        {
            spin.setMessage("Deploying Template...");
            spin.Start();
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
            spin.Stop();
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

        /// <summary>
        /// write the keys from the deployed template into some files.
        /// function.json : useful for indicating the triggering of the function app. Writes the key to the event hub linked to the iot hub
        /// config.yaml : write the connection string of the iot hub, for connecting freshly created devices
        /// </summary>
        /// <param name="outputs">the outputs from the deployment template</param>
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

        /// <summary>
        /// Send a Message to each of the devices created into config.yaml
        /// </summary>
        public static void SendDataToDevices()
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

        /// <summary>
        /// get the freshly created cosmosDB from azure. The template deployment deployed the cosmos.
        /// Get the connection string to the cosmos and write into function.json, for the output of the function app
        /// </summary>
        public static void CreateCosmosDB()
        {
            spin.setMessage("Creating a Cosmos DB...");
            spin.Start();
            //ICosmosDBAccount cosmosDBAccount = await azure.CosmosDBAccounts.Define(cosmosDBName)
            //                                   .WithRegion(Region.EuropeWest)
            //                                   .WithExistingResourceGroup(rgName)
            //                                   .WithKind(DatabaseAccountKind.MongoDB)
            //                                   .WithEventualConsistency()
            //                                   .WithWriteReplication(Region.EuropeWest)
            //                                   .WithReadReplication(Region.EuropeWest)
            //                                   .CreateAsync();
            ICosmosDBAccount cosmosDBAccount = azure.CosmosDBAccounts.GetByResourceGroup(rgName, cosmosDBName);
            spin.Stop();

            Console.WriteLine("CosmosDB Successfully created : " + cosmosDBAccount.Name);
            spin.setMessage("Getting credentials for CosmosDB...");
            spin.Start();


            var databaseAccountListKeysResult = cosmosDBAccount.ListKeys();
            string masterKey = databaseAccountListKeysResult.PrimaryMasterKey;
            string endPoint = cosmosDBAccount.DocumentEndpoint;
            string primaryConnectionString = "AccountEndpoint="
                                             + endPoint
                                             + ";AccountKey="
                                             + masterKey
                                             + ";";
            endPointCosmosDB = endPoint;
            accountKeyCosmosDB = masterKey;

            //Console.WriteLine("Get the MongoDB connection string");
            //var databaseAccountListConnectionStringsResult = cosmosDBAccount.ListConnectionStrings();
            //Console.WriteLine("MongoDB connection string: "
            //+ databaseAccountListConnectionStringsResult.ConnectionStrings[0].ConnectionString);

            //string primaryConnectionString = databaseAccountListConnectionStringsResult.ConnectionStrings[0].ConnectionString;
            spin.Stop();
            Console.WriteLine($"CosmosDb {cosmosDBName} with the connection string {primaryConnectionString}");


            dynamic jsonFunction = JObject.Parse(File.ReadAllText("..\\..\\FunctionAppCore\\IoTHubTrigger\\function.json"));
            dynamic bindings = jsonFunction["bindings"];
            bindings[1]["databaseName"] = cosmosDBName;
            //bindings[1]["connection"] = primaryConnectionString;

            jsonFunction["bindings"] = bindings;
            string jsonUpdated = JsonConvert.SerializeObject(jsonFunction, Formatting.Indented);
            File.WriteAllText(@"..\..\FunctionAppCore\IoTHubTrigger\function.json", jsonUpdated);


        }

        /// <summary>
        /// Create a function app, upload the files for his running state. The files are run.csx, function.json and host.json
        /// DO NOT WORK ATM : the inputs / outputs of the function app does not works.
        /// </summary>
        public static void CreateFunctionApp()
        {
            string appName = SdkContext.RandomResourceName(functionAppPrefix, 20);
            string suffix = ".azurewebsites.net";
            string appUrl = appName + suffix;
            spin.setMessage("Creating function app " + appName + " in resource group " + rgName + "...");
            //Console.WriteLine("Creating function app " + appName + " in resource group " + rgName + "...");
            //Console.ReadLine();

            IFunctionApp app1 = azure.AppServices.FunctionApps.Define(appName)
                                .WithRegion(Region.EuropeWest)
                                .WithExistingResourceGroup(rgName)
                                .Create();

            spin.Stop();
            Console.WriteLine("Created Function App");
            Console.WriteLine(app1);


            Console.WriteLine("");
            spin.setMessage("Deploying to function app" + appName + " with FTP...");
            spin.Start();

            IPublishingProfile profile = app1.GetPublishingProfile();
            Utilities.UploadFileToFunctionApp(profile, Path.Combine(Utilities.ProjectPath, "FunctionAppCore", "host.json"));
            Utilities.UploadFileToFunctionApp(profile, Path.Combine(Utilities.ProjectPath, "FunctionAppCore", "IoTHubTrigger", "function.json"), "IoTHubTrigger/function.json");
            Utilities.UploadFileToFunctionApp(profile, Path.Combine(Utilities.ProjectPath, "FunctionAppCore", "IoTHubTrigger", "run.csx"), "IoTHubTrigger/run.csx");
            //sync triggers
            app1.SyncTriggers();
            spin.Stop();


            Console.WriteLine("Deployment iotHubTrigger to web app" + app1.Name + " completed");

            //warm up
            //Console.WriteLine("Warming up " + appUrl + "/api/IoTHubTrigger");
            //Utilities.PostAddress("http://" + appUrl + "/api/IoTHubTrigger", "toto");
            //SdkContext.DelayProvider.Delay(5000);
            //Console.WriteLine("Curling...");
            //Console.WriteLine(Utilities.PostAddress("http://" + appUrl + "/api/IoTHubTrigger", "toto"));

        }

        /// <summary>
        /// Create numberOfDevices devices linked to the iot hub
        /// </summary>
        /// <param name="numberOfDevices">the number of devices to create</param>
        public static void CreateDevice(int numberOfDevices)
        {
            const string configFilePath = @"../../config.yaml";
            IoTHubExamples.Core.Configuration config = configFilePath.GetIoTConfiguration();
            deviceConfigs = config.DeviceConfigs;
            AzureIoTHubConfig azureConfig = config.AzureIoTHubConfig;

            _registryManager = RegistryManager.CreateFromConnectionString(azureConfig.ConnectionString);

            deviceConfigs = config.DeviceConfigs = new System.Collections.Generic.List<DeviceConfig>();

            for (int deviceNumber = 0; deviceNumber < numberOfDevices; deviceNumber++)
            {
                var testDevice = new DeviceConfig()
                {
                    DeviceId = $"{"test"}{deviceNumber:0000}",
                    Nickname = $"{"test"}{deviceNumber:0000}",
                    Status = "Enabled"
                };
                deviceConfigs.Add(testDevice);

                Task<string> task = AddDeviceAsync(testDevice);
                task.Wait();

                testDevice.Key = task.Result;
            }

            if (configFilePath.UpdateIoTConfiguration(config).Item1)
            {
                foreach (var testDevice in deviceConfigs)
                {
                    Console.WriteLine(
                        $"DeviceId: {testDevice.DeviceId} has DeviceKey: {testDevice.Key} \r\nConfig file: {configFilePath} has been updated accordingly.");
                }
            }

        }


        /// <summary>
        /// Add the device to the registry manager
        /// </summary>
        /// <param name="deviceConfig">the device to add</param>
        /// <returns></returns>
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


        /// <summary>
        /// Deletes the resource group, and all the resources inside
        /// </summary>
        public static void DeleteResourceGroup()
        {
            azure.ResourceGroups.DeleteByName(rgName);
            spin.Dispose();
        }



    }
}
