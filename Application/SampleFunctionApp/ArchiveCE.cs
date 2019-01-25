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
            if (continuousExportStorageConnectionString == null)
            {
                string m = @"Variable ContinuousExportStorageConnectionString is not set in configuration.  This function cannot run.";
                log.LogCritical(m);
                throw new Exception(m);
            }
            log.LogInformation($"CE export storage connection string: {continuousExportStorageConnectionString.Substring(0, 25)}");

            var archiveStorageConnectionString = config["ArchiveStorageConnectionString"];
            if (archiveStorageConnectionString == null)
            {
                string m = @"Variable ArchiveStorageConnectionString is not set in configuration.  This function cannot run.";
                log.LogCritical(m);
                throw new Exception(m);
            }
            log.LogInformation($"Archive storage connection string: {archiveStorageConnectionString.Substring(0, 25)}");


            var archiveContainerName = config["ArchiveContainerName"];
            if (archiveContainerName == null)
            {
                log.LogWarning(@"Variable ArchiveContainerName is not set in configuration.  Using default value: 'archive' ");
                archiveContainerName = "archive";
            }

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
            for (int i = 2; i < u.Segments.Length; i++)
            {
                blobName += u.Segments[i];
            }
            log.LogInformation($"Blob Name: {blobName}");

            
            try
            {
                log.LogInformation($"Get hold of ce storage container");
                CloudStorageAccount ceStorageAccount = CloudStorageAccount.Parse(continuousExportStorageConnectionString);
                CloudBlobClient ceStorageClient = ceStorageAccount.CreateCloudBlobClient();
                CloudBlobContainer ceStorageContainer = ceStorageClient.GetContainerReference(ceContainerName);
                log.LogInformation($"ceStorageContainer.Uri: {ceStorageContainer.Uri}");

                log.LogInformation($"Get hold of archive storage container");
                CloudStorageAccount archiveStorageAccount = CloudStorageAccount.Parse(archiveStorageConnectionString);
                CloudBlobClient archiveStorageClient = archiveStorageAccount.CreateCloudBlobClient();
                CloudBlobContainer archiveStorageContainer = archiveStorageClient.GetContainerReference(archiveContainerName);
                if (await archiveStorageContainer.CreateIfNotExistsAsync().ConfigureAwait(false))
                {
                    log.LogInformation(($"Archive container {archiveContainerName} did not exist and was created sucessfully."));
                }
                archiveStorageContainer.Metadata.Add("origin", "Create automatically by ArchiveCE Function");
                await archiveStorageContainer.SetMetadataAsync();
                log.LogInformation($"archiveStorageContainer.Uri: {ceStorageContainer.Uri}");

                CloudBlockBlob ceBlob = ceStorageContainer.GetBlockBlobReference(blobName);
                log.LogInformation($"ceBlob.Uri: {ceBlob.Uri}");
                var policy = new SharedAccessBlobPolicy
                {
                    Permissions = SharedAccessBlobPermissions.Read,
                    SharedAccessStartTime = DateTime.UtcNow.AddMinutes(-15),
                    SharedAccessExpiryTime = DateTime.UtcNow.AddDays(7)
                };
                log.LogInformation($"sharedAccessPolicy: {policy.ToString()}");
                var ceBlobToken = ceBlob.GetSharedAccessSignature(policy);
                log.LogInformation($"ceBlobToken: {ceBlobToken.ToString()}");
                var ceBlobSAS = string.Format("{0}{1}", ceBlob.Uri, ceBlobToken);
                log.LogInformation($"ceBlobSAS: {ceBlobSAS.ToString()}");

                CloudBlockBlob archiveBlob = archiveStorageContainer.GetBlockBlobReference(blobName);
                log.LogInformation($"archiveBlob.Uri: {archiveBlob.Uri}");
                log.LogInformation($"Set storage tier for archive blob to 'archive'");
                await archiveBlob.SetStandardBlobTierAsync(StandardBlobTier.Archive);

                log.LogInformation(($"set traceability in archive blob metadata"));
                archiveBlob.Metadata.Add("traceability", $"archived by ArchiveCE Azure function at {System.DateTime.UtcNow} UTC)");
                await archiveBlob.SetMetadataAsync();

                log.LogInformation(($"copying blob to archive account {archiveStorageAccount.BlobStorageUri}, archive container {archiveContainerName}"));
                string result = await archiveBlob.StartCopyAsync(new Uri(ceBlobSAS)).ConfigureAwait(false);
                log.LogInformation($"Result of Blob copy operation: {result}.");

                
                

            }
            catch (Exception e)
            {
                log.LogError($"Unable to archive blob Name: {blobName}.  Exception was {e.Message}");
                log.LogError($"Unable to archive blob Name: {blobName}.  Stacktrace was {e.StackTrace}");
                if (e.InnerException != null)
                {
                    log.LogError($"Unable to archive blob Name: {blobName}.  Inner Exception was {e.InnerException.Message}");
                    log.LogError($"Unable to archive blob Name: {blobName}.  Stacktrace was {e.InnerException.StackTrace}");
                }
                throw e;
            }

           

        }
    }
}
