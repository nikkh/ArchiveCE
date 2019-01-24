// Default URL for triggering event grid function in the local environment.
// http://localhost:7071/runtime/webhooks/EventGrid?functionName=ArchiveCE

using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Host;
using Microsoft.Azure.EventGrid.Models;
using Microsoft.Azure.WebJobs.Extensions.EventGrid;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

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
        }
    }
}
