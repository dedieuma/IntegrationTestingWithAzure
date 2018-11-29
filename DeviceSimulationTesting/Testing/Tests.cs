using Microsoft.Azure.Devices.Client;
using Microsoft.Azure.Documents.Client;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using DeviceSimulation;
using DeviceSimulationTesting.Testing;
using Newtonsoft.Json;
using System.Threading;

namespace DeviceSimulationTesting
{
    [TestClass]
    class Tests
    {
        [TestInitialize]
        public void InitializationResources()
        {
            
            Program.InitializeVariables();
            Program.InitializeResources();
            Program.DeployTemplate();
            Program.CreateCosmosDB();
            Program.CreateFunctionApp();
            
        }

        #region TestMethods
        //-METHODS-------------------------------------------------------------------------------------
        [TestMethod]
        public async Task Send_MessageToIotAndCheckIfTheMessageWasSavedInCosmos()
        {
            // Arrange
            Arrange_Send_MessageToIotAndCheckIfTheMessageWasSavedInCosmos(out string idToSeek, out DeviceClient deviceClient, out DocumentClient documentClient, out Uri collectionToCosmosLink, out string json, out Message message);

            //Act
            await Act_Send_MessageToIotAndCheckIfTheMessageWasSavedInCosmos(json, deviceClient, message);

            // Arrange
            dynamic result = await Assert_Send_MessageToIotAndCheckIfTheMessageWasSavedInCosmos(idToSeek, documentClient, collectionToCosmosLink);

        }
        #endregion

        #region TestMethodManagement
        //-MANAGEMENT-----------------------------------------------------------------------------------------------------------------
        private async Task<dynamic> Assert_Send_MessageToIotAndCheckIfTheMessageWasSavedInCosmos(string idToSeek, DocumentClient documentClient, Uri collectionToCosmosLink)
        {
            FeedResponse<dynamic> docs = await documentClient.ReadDocumentFeedAsync(collectionToCosmosLink, new FeedOptions { MaxItemCount = 10 });
            IEnumerator<dynamic> docEnumerator = docs.GetEnumerator();

            while (!docEnumerator.MoveNext())
            {
                if(docEnumerator.Current["id"] == idToSeek)
                {
                    break;
                }
            }
            return JsonConvert.DeserializeObject(docEnumerator.Current.ToString());
        }

        private async Task Act_Send_MessageToIotAndCheckIfTheMessageWasSavedInCosmos(string json, DeviceClient deviceClient, Message message)
        {
            Console.WriteLine($"{DateTime.Now} > Sending Message {json}");
            await deviceClient.SendEventAsync(message);
            Thread.Sleep(3000);
        }

        private void Arrange_Send_MessageToIotAndCheckIfTheMessageWasSavedInCosmos(out string idToSeek, out DeviceClient deviceClient, out DocumentClient documentClient, out Uri collectionToCosmosLink, out string json, out Message message)
        {
            Random rand = new Random();
            double minTemperature = 20;
            double minHumidity = 60;
            idToSeek = (rand.NextDouble() * 100).ToString();
            DateTime date = DateTime.Now;
            deviceClient = DeviceClient.CreateFromConnectionString(Program.deviceConfigs.FirstOrDefault().Key, TransportType.Mqtt);
            documentClient = new DocumentClient(new Uri(Program.endPointCosmosDB), Program.accountKeyCosmosDB);
            collectionToCosmosLink = UriFactory.CreateDocumentCollectionUri(Program.cosmosDBName, "collection");

            Model modelToSend = new Model(minTemperature + rand.NextDouble() * 15, minHumidity + rand.NextDouble() * 20, idToSeek, date);

            json = JsonConvert.SerializeObject(modelToSend);
            message = new Message(Encoding.ASCII.GetBytes(json));
            message.Properties.Add("temperatureAlert", (modelToSend.temperature > 30) ? "true" : "false");


        }
        #endregion


        [TestCleanup]
        public void CleanUp()
        {
            Program.DeleteResourceGroup();
        }
    }
}
