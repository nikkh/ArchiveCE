// Default URL for triggering event grid function in the local environment.
// http://localhost:7071/runtime/webhooks/EventGrid?functionName=ArchiveCE

using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Host;
using Microsoft.Azure.EventGrid.Models;
using Microsoft.Azure.WebJobs.Extensions.EventGrid;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;

namespace SampleFunctionApp
{
    public static class ArchiveCE
    {
        [FunctionName("ArchiveCE")]
        public static void Run([EventGridTrigger]EventGridEvent eventGridEvent, ILogger log)
        {
            
            log.LogInformation($"Event Type: {eventGridEvent.EventType}");
            log.LogInformation($"Subject: {eventGridEvent.Subject}");
                        
            var jData = JObject.Parse(eventGridEvent.Data.ToString());
            log.LogInformation($"api: {jData["api"]}");
            log.LogInformation($"url: {jData["url"]}");
            log.LogInformation($"blobType: {jData["blobType"]}");
            log.LogInformation($"contentLength: {jData["contentLength"]}");

            log.LogInformation($"Event Data: {eventGridEvent.Data}");
            Uri u = new Uri(jData["url"].ToString());
            // https://appinsightsexportxxx.blob.core.windows.net/current/uonss-dev-as_2f08282e057d41f4a06aa7c91141a3cd/Requests/2019-01-24/20/c6c6f146-21e8-414d-a2ba-83ebd51e9b63_20190124_205114.blob
            string blobName = u.Segments[u.Segments.Length-1];
            log.LogInformation(($"blobName: {blobName}"));
            string outputBlobName = "";
            for (int i = 2; i < u.Segments.Length-1; i++)
            {
                outputBlobName += u.Segments[i];
            }
            log.LogInformation(($"Output location is {outputBlobName}"));
            //CloudStorageAccount storageAccount = CloudStorageAccount.Parse(
            //   CloudConfigurationManager.GetSetting("ArchiveStorageConnectionString"));

            //CloudBlobClient client = storageAccount.CreateCloudBlobClient();

            //CloudBlobContainer container = client.GetContainerReference("archive");
            //container.CreateIfNotExistsAsync().RunSynchronously();

            //// Copy the message to a blob
            //string stringMessage = JsonConvert.SerializeObject(message);
            //CloudBlockBlob blockBlob = container.GetBlockBlobReference(message.Id.ToString());
            //blockBlob.UploadText(stringMessage);

        }
    }
}
