using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace SampleFunctionApp.Test
{
    public class Function1Test
    {
        private readonly ILogger logger = NullLoggerFactory.Instance.CreateLogger("Null Logger");

        [Fact]
        public async void HttpTriggerWithParams()
        {
            var request = TestFactory.CreateHttpRequest("name", "Bill");
            var response = (OkObjectResult)await Function1.Run(request, logger);
            Assert.Equal("Hello Bill! Welcome to Azure Functions!", response.Value);
        }

        [Fact]
        public async void HttpTriggerWithoutParams()
        {
            var request = TestFactory.CreateHttpRequest("", "");
            var response = (OkObjectResult)await Function1.Run(request, logger);
            Assert.Equal("Hello there! Welcome to Azure Functions!", response.Value);
        }
    }
    public class FnHttpTriggerAnonymousTest
    {
        private readonly ILogger logger = NullLoggerFactory.Instance.CreateLogger("Null Logger");

        [Fact]
        public async void HttpTriggerWithParams()
        {
            var request = TestFactory.CreateHttpRequest("name", "nick");
            var response = (OkObjectResult)await FnHttpTriggerAnonymous.Run(request, logger);
            Assert.Equal("Hello, nick", response.Value);
        }

        [Fact]
        public async void HttpTriggerWithoutParams()
        {
            var request = TestFactory.CreateHttpRequest("", "");
            var response = (BadRequestObjectResult)await FnHttpTriggerAnonymous.Run(request, logger);
            Assert.Equal("Please pass a name on the query string or in the request body", response.Value);
        }
    }
}
