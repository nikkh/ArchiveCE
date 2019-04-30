using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Host;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;

namespace SampleFunctionApp
{
    public static class ExchangeMessageTrackingToSplunk
    {
        [FunctionName("ExchangeMessageTrackingToSplunk")]
        public static async Task Run([QueueTrigger("traces", Connection = "TracesStorageConnectionString")]string traceMessage, ILogger log, Microsoft.Azure.WebJobs.ExecutionContext context)
        {
            log.LogInformation($"ExchangeMessageTrackingToSplunk function was triggered with the following message: {traceMessage}");
            var config = new ConfigurationBuilder()
             .SetBasePath(context.FunctionAppDirectory)
             .AddJsonFile("local.settings.json", optional: true, reloadOnChange: true)
             .AddEnvironmentVariables()
             .Build();

            var traceStorageConnectionString = config["TracesStorageConnectionString"];
            if (traceStorageConnectionString == null)
            {
                string m = $"{context.InvocationId} - Variable TracesStorageConnectionString is not set in configuration.  This function cannot run.";
                log.LogCritical(m);
                throw new Exception(m);
            }
            log.LogDebug($"{context.InvocationId} - TracesStorageConnectionString: {traceStorageConnectionString.Substring(0, 40)}");

            var archiveContainerName = config["TraceArchiveContainerName"];
            if (archiveContainerName == null)
            {
                log.LogWarning($"{context.InvocationId} - Variable TraceArchiveContainerName is not set in configuration.  Using default value: 'trace-archive' ");
                archiveContainerName = "trace-archive";
            }

            try
            {

                log.LogDebug($"{context.InvocationId} - Get hold of archive storage container");
                CloudStorageAccount archiveStorageAccount = CloudStorageAccount.Parse(traceStorageConnectionString);
                CloudBlobClient archiveStorageClient = archiveStorageAccount.CreateCloudBlobClient();
                CloudBlobContainer archiveStorageContainer = archiveStorageClient.GetContainerReference(archiveContainerName);
                if (await archiveStorageContainer.CreateIfNotExistsAsync())
                {
                    log.LogInformation(($"{context.InvocationId} - Archive container {archiveContainerName} did not exist and was created sucessfully."));
                    archiveStorageContainer.Metadata.Add("origin", "Created automatically by ExchangeMessageTrackingToSplunk Function");
                    await archiveStorageContainer.SetMetadataAsync();
                }
                log.LogInformation($"{context.InvocationId} - Archive container name: {archiveContainerName}");

                var blobName = Guid.NewGuid().ToString();
                CloudBlockBlob archiveBlob = archiveStorageContainer.GetBlockBlobReference(blobName);
                log.LogInformation($"{context.InvocationId} -archiveBlob.Uri: {archiveBlob.Uri}");

                using (var stream = new MemoryStream(Encoding.UTF8.GetBytes(traceMessage)))
                {
                    await archiveBlob.UploadFromStreamAsync(stream);
                }

                log.LogInformation($"{context.InvocationId} -archiveBlob.Uri: {archiveBlob.Uri} was created for message");

            }
            catch (Exception e)
            {
                log.LogError($"{context.InvocationId} - Exception {e.Message}");
                log.LogError($"{context.InvocationId} - Stacktrace {e.StackTrace}");
                if (e.InnerException != null)
                {
                    log.LogError($"{context.InvocationId} - Inner Exception was {e.InnerException.Message}");

                }
                throw e;
            }
        }
    }
}
