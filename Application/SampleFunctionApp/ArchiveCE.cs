// Default URL for triggering event grid function in the local environment.
// http://localhost:7071/runtime/webhooks/EventGrid?functionName=ArchiveCE

using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Host;
using Microsoft.Azure.EventGrid.Models;
using Microsoft.Azure.WebJobs.Extensions.EventGrid;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;
using System.Threading.Tasks;

namespace SampleFunctionApp
{
    public static class ArchiveCE
    {
        [FunctionName("ArchiveCE")]
        public static async Task Run([EventGridTrigger]EventGridEvent eventGridEvent, ILogger log, ExecutionContext context)
        {
            log.LogInformation($"ArchiveCE function was triggered with the following Event Data: {eventGridEvent.Data}");
            if (eventGridEvent.EventType != "Microsoft.Storage.BlobCreated")
            {
                log.LogWarning($"The Event Type ({eventGridEvent.EventType}) for this event was not Microsoft.Storage.BlobCreated.  Please configure your Event Subscription to only process Microsoft.Storage.BlobCreated events.  This event will be ignored.");
                return;
            }

            var config = new ConfigurationBuilder()
             .SetBasePath(context.FunctionAppDirectory)
             .AddJsonFile("local.settings.json", optional: true, reloadOnChange: true)
             .AddEnvironmentVariables()
             .Build();

            var continuousExportStorageConnectionString = config["ContinuousExportStorageConnectionString"];
            log.LogInformation($"CE export storage connection string: {continuousExportStorageConnectionString.Substring(1, 15)}");
            var archiveStorageConnectionString = config["ArchiveStorageConnectionString"];
            log.LogInformation($"Archive storage connection string: {archiveStorageConnectionString.Substring(1, 15)}");
            var archiveContainerName = config["ArchiveContainerName"];
            log.LogInformation($"Archive container name: {archiveContainerName}");
                        
            var jData = JObject.Parse(eventGridEvent.Data.ToString());

            log.LogInformation($"Event Type: {eventGridEvent.EventType}");
            log.LogInformation($"Subject: {eventGridEvent.Subject}");
            log.LogInformation($"api: {jData["api"]}");
            log.LogInformation($"url: {jData["url"]}");
            log.LogInformation($"blobType: {jData["blobType"]}");
            log.LogInformation($"contentLength: {jData["contentLength"]}");
 
            Uri u = new Uri(jData["url"].ToString());
            string ceContainerName = u.Segments[1].Substring(0, u.Segments[1].Length-1);
            log.LogInformation($"CE Container: {ceContainerName}");

            string blobName = "";
            for (int i = 2; i < u.Segments.Length-1; i++)
            {
                blobName += u.Segments[i];
            }
            log.LogInformation(($"Blob Name: {blobName}"));

            
            try
            {
                CloudStorageAccount ceStorageAccount = CloudStorageAccount.Parse(continuousExportStorageConnectionString);
                CloudBlobClient ceStorageClient = ceStorageAccount.CreateCloudBlobClient();
                CloudBlobContainer ceStorageContainer = ceStorageClient.GetContainerReference(ceContainerName);

                CloudStorageAccount archiveStorageAccount = CloudStorageAccount.Parse(archiveStorageConnectionString);
                CloudBlobClient archiveStorageClient = archiveStorageAccount.CreateCloudBlobClient();
                CloudBlobContainer archiveStorageContainer = archiveStorageClient.GetContainerReference("archive");
                if (await archiveStorageContainer.CreateIfNotExistsAsync())
                {
                    log.LogInformation(($"Archive container {archiveContainerName} did not exist and was created sucessfully."));
                }
                archiveStorageContainer.Metadata.Add("origin", "Create automatically by ArchiveCE Function");
                await archiveStorageContainer.SetMetadataAsync();

                CloudBlockBlob ceBlob = ceStorageContainer.GetBlockBlobReference(blobName);
                CloudBlockBlob archiveBlob = archiveStorageContainer.GetBlockBlobReference(blobName);
                log.LogInformation(($"Set storage tier for archive blob to 'archive'"));
                await archiveBlob.SetStandardBlobTierAsync(StandardBlobTier.Archive);
                log.LogInformation(($"copying blob from CE account {ceStorageAccount.BlobStorageUri}, CE container {ceContainerName}"));
                log.LogInformation(($"copying blob to archive account {archiveStorageAccount.BlobStorageUri}, archive container {archiveContainerName}"));
                string result = await archiveBlob.StartCopyAsync(ceBlob);
                log.LogInformation(($"Result of Blob copy operation: {result}."));
            }
            catch (Exception e)
            {
                log.LogError($"Unable to archive blob Name: {blobName}.  Exception was {e.Message}");
                log.LogTrace($"Unable to archive blob Name: {blobName}.  Exception was {e.StackTrace}");
                if (e.InnerException != null)
                {
                    log.LogError($"Unable to archive blob Name: {blobName}.  Inner Exception was {e.InnerException.Message}");
                    log.LogTrace($"Unable to archive blob Name: {blobName}.  Exception was {e.InnerException.StackTrace}");
                }
            }

           

        }
    }
}
