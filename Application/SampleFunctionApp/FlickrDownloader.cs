using System;
using System.Net;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Host;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;
using Newtonsoft.Json.Linq;

namespace SampleFunctionApp
{
    public static class FlickrDownloader
    {
        [FunctionName("FlickrDownloader")]
        public static async Task Run([QueueTrigger("downloads", Connection = "FLICKR_STORAGE_ACCOUNT_CONNECTIONSTRING")]string myQueueItem, ILogger log, Microsoft.Azure.WebJobs.ExecutionContext context)
        {
            log.LogInformation($"FlickrDownloader function triggered by incoming queue message: {myQueueItem}");

            var config = new ConfigurationBuilder()
             .SetBasePath(context.FunctionAppDirectory)
             .AddJsonFile("local.settings.json", optional: true, reloadOnChange: true)
             .AddEnvironmentVariables()
             .Build();

            var flickrStorageConnectionString = config["FLICKR_STORAGE_ACCOUNT_CONNECTIONSTRING"];
            if (flickrStorageConnectionString == null)
            {
                string m = $"{context.InvocationId} - Variable flickrStorageConnectionString is not set in configuration.  This function cannot run.";
                log.LogCritical(m);
                throw new Exception(m);
            }
            log.LogDebug($"{context.InvocationId} - CE export storage connection string: {flickrStorageConnectionString.Substring(0, 40)}");

            var incomingDataContainerName = config["IncomingDataContainerName"];
            if (incomingDataContainerName == null)
            {
                log.LogWarning($"{context.InvocationId} - Variable ArchiveContainerName is not set in configuration.  Using default value: 'archive' ");
                incomingDataContainerName = "archive";
            }

            log.LogInformation($"{context.InvocationId} - Archive container name: {incomingDataContainerName}");

            Uri downloadUri=null;
            String photosetId="";
            String title="";
            try
            {
                var jData = JObject.Parse(myQueueItem);
                photosetId = jData["PhotoSetId"].ToString();
                log.LogDebug($"{context.InvocationId} - PhotoSetId: {photosetId}");
                title = jData["Title"].ToString();
                log.LogDebug($"{context.InvocationId} - Title: {title}");
                log.LogDebug($"{context.InvocationId} - Number of Photos: {jData["Data"]["Photos"]}");
                log.LogDebug($"{context.InvocationId} - Number of Videos: {jData["Data"]["Videos"]}");
                log.LogDebug($"{context.InvocationId} - Url: {jData["Data"]["Url"]}");
                downloadUri = new Uri(jData["Data"]["Url"].ToString());

            }
            catch (Exception e)
            {
                log.LogCritical($"{context.InvocationId} - Unable to parse incoming message. {myQueueItem}.  {e.Message}. Processing aborted.");
                return;
            }

            log.LogDebug($"{context.InvocationId} - Get hold of storage container");
            CloudStorageAccount storageAccount = CloudStorageAccount.Parse(flickrStorageConnectionString);
            CloudBlobClient storageClient = storageAccount.CreateCloudBlobClient();
            CloudBlobContainer storageContainer = storageClient.GetContainerReference(incomingDataContainerName);
            log.LogDebug($"{context.InvocationId} - storageContainer.Uri: {storageContainer.Uri}");

            if (await storageContainer.CreateIfNotExistsAsync())
            {
                log.LogInformation(($"{context.InvocationId} - Incoming data container {incomingDataContainerName} did not exist and was created sucessfully."));
                storageContainer.Metadata.Add("origin", "Create automatically by FlickrDownloader Function");
                await storageContainer.SetMetadataAsync();
            }

            CloudBlockBlob newBlob = storageContainer.GetBlockBlobReference($"{title}{photosetId}");
            log.LogInformation($"{context.InvocationId} -newBlob.Uri: {newBlob.Uri}");

            using (var client = new WebClient())

            {
                using (var stream = client.OpenRead(downloadUri))
                {
                    await newBlob.UploadFromStreamAsync(stream);
                }

            }
            log.LogInformation($"{context.InvocationId} {newBlob.Uri} was downloaded sucessfully");
        }

    }

}
