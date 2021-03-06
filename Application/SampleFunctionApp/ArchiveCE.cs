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
using System.Threading;

namespace SampleFunctionApp
{
    public static class ArchiveCE
    {
        [FunctionName("ArchiveCE")]
        public static async Task Run([EventGridTrigger]EventGridEvent eventGridEvent, ILogger log, Microsoft.Azure.WebJobs.ExecutionContext context)
        {
            
            

            log.LogInformation($"ArchiveCE function was triggered with the following Event Data: {eventGridEvent.Data}");
            if (eventGridEvent.EventType != "Microsoft.Storage.BlobCreated")
            {
                log.LogWarning($"{context.InvocationId} - The Event Type ({eventGridEvent.EventType}) for this event was not Microsoft.Storage.BlobCreated.  Please configure your Event Subscription to only process Microsoft.Storage.BlobCreated events.  This event will be ignored.");
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
                string m = $"{context.InvocationId} - Variable ContinuousExportStorageConnectionString is not set in configuration.  This function cannot run.";
                log.LogCritical(m);
                throw new Exception(m);
            }
            log.LogDebug($"{context.InvocationId} - CE export storage connection string: {continuousExportStorageConnectionString.Substring(0, 40)}");

            var archiveStorageConnectionString = config["ArchiveStorageConnectionString"];
            if (archiveStorageConnectionString == null)
            {
                string m = $"{context.InvocationId} - Variable ArchiveStorageConnectionString is not set in configuration.  This function cannot run.";
                log.LogCritical(m);
                throw new Exception(m);
            }
            log.LogDebug($"{context.InvocationId} - Archive storage connection string: {archiveStorageConnectionString.Substring(0, 40)}");


            var archiveContainerName = config["ArchiveContainerName"];
            if (archiveContainerName == null)
            {
                log.LogWarning($"{context.InvocationId} - Variable ArchiveContainerName is not set in configuration.  Using default value: 'archive' ");
                archiveContainerName = "archive";
            }

            log.LogInformation($"{context.InvocationId} - Archive container name: {archiveContainerName}");

            Uri u = null;
            try
            {
                var jData = JObject.Parse(eventGridEvent.Data.ToString());
                log.LogDebug($"{context.InvocationId} - Event Type: {eventGridEvent.EventType}");
                log.LogDebug($"{context.InvocationId} - Subject: {eventGridEvent.Subject}");
                log.LogDebug($"{context.InvocationId} - url: {jData["url"]}");
                log.LogDebug($"{context.InvocationId} - blobType: {jData["blobType"]}");
                log.LogDebug($"{context.InvocationId} - contentLength: {jData["contentLength"]}");
                u = new Uri(jData["url"].ToString());

            }
            catch (Exception e)
            {
                log.LogCritical($"{context.InvocationId} - Unable to parse Event Grid Event. {eventGridEvent.Data.ToString()}.  {e.Message}. Processing aborted for this event.");
                return;
            }
            
 
            
            string ceContainerName = u.Segments[1].Substring(0, u.Segments[1].Length-1);
            log.LogDebug($"{context.InvocationId} - CE Container: {ceContainerName}");

            string blobName = "";
            for (int i = 2; i < u.Segments.Length; i++)
            {
                blobName += u.Segments[i];
            }
            log.LogInformation($"{context.InvocationId} - Blob Name: {blobName}");

            
            try
            {
                log.LogDebug($"{context.InvocationId} - Get hold of ce storage container");
                CloudStorageAccount ceStorageAccount = CloudStorageAccount.Parse(continuousExportStorageConnectionString);
                CloudBlobClient ceStorageClient = ceStorageAccount.CreateCloudBlobClient();
                CloudBlobContainer ceStorageContainer = ceStorageClient.GetContainerReference(ceContainerName);
                log.LogDebug($"{context.InvocationId} - ceStorageContainer.Uri: {ceStorageContainer.Uri}");

                log.LogDebug($"{context.InvocationId} - Get hold of archive storage container");
                CloudStorageAccount archiveStorageAccount = CloudStorageAccount.Parse(archiveStorageConnectionString);
                CloudBlobClient archiveStorageClient = archiveStorageAccount.CreateCloudBlobClient();
                CloudBlobContainer archiveStorageContainer = archiveStorageClient.GetContainerReference(archiveContainerName);
                if (await archiveStorageContainer.CreateIfNotExistsAsync())
                {
                    log.LogInformation(($"{context.InvocationId} - Archive container {archiveContainerName} did not exist and was created sucessfully."));
                    archiveStorageContainer.Metadata.Add("origin", "Create automatically by ArchiveCE Function");
                    await archiveStorageContainer.SetMetadataAsync();
                }
                
                log.LogDebug($"{context.InvocationId} - archiveStorageContainer.Uri: {archiveStorageContainer.Uri}");

                CloudBlockBlob ceBlob = ceStorageContainer.GetBlockBlobReference(blobName);
                log.LogInformation($"{context.InvocationId} - Uri for Continuous Export Blob: {ceBlob.Uri}");
                var policy = new SharedAccessBlobPolicy
                {
                    Permissions = SharedAccessBlobPermissions.Read,
                    SharedAccessStartTime = DateTime.UtcNow.AddMinutes(-15),
                    SharedAccessExpiryTime = DateTime.UtcNow.AddDays(7)
                };
                log.LogDebug($"{context.InvocationId} - sharedAccessPolicy: {policy.ToString()}");
                var ceBlobToken = ceBlob.GetSharedAccessSignature(policy);
                log.LogDebug($"{context.InvocationId} - ceBlobToken: {ceBlobToken.ToString()}");
                var ceBlobSAS = string.Format("{0}{1}", ceBlob.Uri, ceBlobToken);
                log.LogDebug($"{context.InvocationId} - ceBlobSAS: {ceBlobSAS.ToString()}");

                CloudBlockBlob archiveBlob = archiveStorageContainer.GetBlockBlobReference(blobName);
                log.LogInformation($"{context.InvocationId} -archiveBlob.Uri: {archiveBlob.Uri}");
                
                if (await archiveBlob.DeleteIfExistsAsync())
                {
                    log.LogWarning($"{context.InvocationId} - Archive blob already existed and will was deleted: {archiveBlob.Uri}");
                }
                                
                log.LogInformation(($"{context.InvocationId} - Copying blob {blobName} to archive account {archiveStorageAccount.BlobStorageUri}, archive container {archiveContainerName}"));
                await archiveBlob.StartCopyAsync(new Uri(ceBlobSAS));

                if (await CopyComplete(log, context.InvocationId.ToString(), archiveStorageContainer, blobName, 1000, 20))
                {
                    
                    archiveBlob.Metadata.Add("origin", ceBlob.Uri.ToString());
                    archiveBlob.Metadata.Add("function", context.FunctionName);
                    archiveBlob.Metadata.Add("invocationId", context.InvocationId.ToString());
                    await archiveBlob.SetMetadataAsync();
                    log.LogInformation($"{context.InvocationId} - Archive metadata set on blob {blobName}");
                    await archiveBlob.SetStandardBlobTierAsync(StandardBlobTier.Archive);
                    log.LogInformation($"{context.InvocationId} - Archived blob set to archive storage tier {blobName}");
                }

                log.LogInformation(($"{context.InvocationId} - Function {context.FunctionName} completed sucessfully."));

            }
            catch (Exception e)
            {

                log.LogError($"{context.InvocationId} - Exception {e.Message}, Unable to archive blob Name: {blobName}.  ");
                log.LogError($"{context.InvocationId} - Stacktrace {e.StackTrace}, Unable to archive blob Name: {blobName}.");
                if (e.InnerException != null)
                {
                    log.LogError($"{context.InvocationId} - Inner Exception was {e.InnerException.Message}");
                    
                }
                throw e;
            }

            
        }

        private static async Task<bool> CopyComplete(ILogger log, string invocationId, CloudBlobContainer archiveStorageContainer, string blobName, int waitInMs, int attempts)
        {
            log.LogInformation(($"{invocationId} - Monitoring state of asynchronous blob copy {blobName}"));
            
            for (int i = 1; i < attempts; i++)
            {
                log.LogInformation(($"{invocationId} - Monitoring copy - attempt {i}"));
                var blob = archiveStorageContainer.GetBlockBlobReference(blobName);
                await blob.FetchAttributesAsync();
                if (blob.CopyState != null)
                {
                    
                    if (blob.CopyState.Status == CopyStatus.Success)
                    {
                        log.LogInformation(($"{invocationId} - Copy completed sucessfully!"));
                        return true;
                    }
                    
                    if ((blob.CopyState.Status == CopyStatus.Aborted) || (blob.CopyState.Status == CopyStatus.Failed) || (blob.CopyState.Status == CopyStatus.Invalid))
                    {
                        log.LogCritical($"{invocationId} - Copy did not complete sucessfully.  Status was {blob.CopyState.Status.ToString()}");
                        return false;
                    }
                    if (blob.CopyState.Status == CopyStatus.Pending)
                    {
                        log.LogDebug($"{invocationId} - Copy pending. Sleeping {waitInMs} ms.");
                        Thread.Sleep(waitInMs);
                    }

                }
                else
                {
                    
                    
                    log.LogError($"{invocationId} - Unexpected error - no copy state available for blob");
                    return false;
                }
            }
            log.LogWarning(($"{invocationId} - Asynchronous Copy had still not completed after {attempts} attempts, pausing for {waitInMs} ms between each attempt. This doesnt mean that the copy did not complete - but it might be worth checking"));
            return false;
        }
    }
}
