# Integration Testing With Azure

The project is a POC of integration testing with Azure.

It creates a new Azure resource Group, deploys some resource, make tests about a mock message from beginning to the end of the pipeline, and destroys the resources.

The idea was to quickly create resources on Azure, spy the entries/outputs of the resources to see if the messages are well formed as intended, and delete this environment. 
It could be included in the end inside a Azure DevOps pipeline, as a no backwards step gate.

## Prerequisites

Please follow the instruction at the first part of this page, "Prepare your Visual Studio Project" :
https://docs.microsoft.com/en-us/azure/iot-hub/iot-hub-rm-template
It allows you to create a new Azure AD Application, and gives you the credentials. 

This credentials are needed inside the DeviceSimulation/deviceSimulationParameters.json. With those, you need to write the name of the future resource group, the name of the deployment, the name of the storage account, the name of the cosmosDB, the name of the iot hub, and the prefix of your Function App. Careful, some of them must be unique on Azure, you should gives some prefixes like your nickname.

## Pipeline

```sequence
DeviceSimulator-> IoT Hub : message
IoT Hub-> Function App : Trigger Event Hub
Function App-> CosmosDB: Redirecting
```

The project DeviceSimulation contains all the logic of creating, sending a message and destroying the resources. Uncomment the main function in Program.cs to test it.
## Tools

I'm using an hybrid mix between Azure ARM (for deploying IoT Hub and CosmosDB) and Azure SDK .NET. I had some troubles with one or other, the choice of each one was made with my sucess and findings of the net.

## Links and resources

Not Exhaustive

https://docs.microsoft.com/en-us/azure/iot-hub/iot-hub-rm-template

https://azure.microsoft.com/fr-fr/resources/samples/resource-manager-dotnet-resources-and-groups/

https://stackoverflow.com/questions/31684821/how-to-add-application-to-azure-ad-programmatically

https://docs.microsoft.com/en-us/azure/virtual-machines/windows/csharp-template

https://docs.microsoft.com/en-us/azure/iot-dps/quick-setup-auto-provision-rm

https://github.com/Microsoft/iot-samples/tree/master/DeviceManagement/csharp

https://github.com/Azure/azure-libraries-for-net

https://github.com/Azure-Samples/cosmosdb-dotnet-create-documentdb-and-get-mongodb-connection-string/blob/master/Program.cs

https://github.com/Azure-Samples/app-service-dotnet-configure-deployment-sources-for-functions


### TODO And Known Bugs

- The entry and output of the function app, from FunctionAppCore/IoTHubTrigger/function.json are not working. There is no links on azure portal. I'm still working on it, i'll try to make an empty Function App using VSCode. The problem can come from the missing local.settings.json, which i don't have.
-  Make all the deployment with only ARM or only .NET SDK
