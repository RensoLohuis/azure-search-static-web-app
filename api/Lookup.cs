using Azure;
using Azure.Core.Serialization;
using Azure.Search.Documents;
using Azure.Search.Documents.Models;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using System.Net;
using System.Text.Json;
using WebSearch.Models;

namespace WebSearch.Function
{
    public class Lookup
    {
        private static string searchApiKey = Environment.GetEnvironmentVariable("SearchApiKey", EnvironmentVariableTarget.Process);
        private static string searchServiceName = Environment.GetEnvironmentVariable("SearchServiceName", EnvironmentVariableTarget.Process);
        private static string searchIndexName = Environment.GetEnvironmentVariable("SearchIndexName", EnvironmentVariableTarget.Process) ?? "good-books";

        private readonly ILogger<Lookup> _logger;

        public Lookup(ILogger<Lookup> logger)
        {
            _logger = logger;
        }


        [Function("lookup")]
        public async Task<HttpResponseData> RunAsync(
    [HttpTrigger(AuthorizationLevel.Anonymous, "get", "post")] HttpRequestData req,
    FunctionContext executionContext)
        {
            // Get Document Id
            var query = System.Web.HttpUtility.ParseQueryString(req.Url.Query);
            string documentId = query["id"];
            if (string.IsNullOrEmpty(documentId))
            {
                var errorResponse = req.CreateResponse(HttpStatusCode.BadRequest);
                errorResponse.WriteString("The 'id' parameter is required.");
                return errorResponse;
            }

            // Azure AI Search
            Uri serviceEndpoint = new($"https://{searchServiceName}.search.windows.net/");
            SearchClient searchClient = new(
                serviceEndpoint,
                searchIndexName,
                new AzureKeyCredential(searchApiKey)
            );

            SearchDocument document;
            try
            {
                var getDocumentResponse = await searchClient.GetDocumentAsync<SearchDocument>(documentId);
                document = getDocumentResponse.Value;
            }
            catch (RequestFailedException ex)
            {
                var errorResponse = req.CreateResponse(HttpStatusCode.NotFound);
                errorResponse.WriteString($"Document with ID '{documentId}' was not found. Error: {ex.Message}");
                return errorResponse;
            }
            catch (Exception ex)
            {
                var errorResponse = req.CreateResponse(HttpStatusCode.InternalServerError);
                errorResponse.WriteString($"An unexpected error occurred. Error: {ex.Message}");
                return errorResponse;
            }

            // Data to return
            var output = new LookupOutput
            {
                Document = document
            };

            var response = req.CreateResponse(HttpStatusCode.OK);
            response.Headers.Add("Content-Type", "application/json; charset=utf-8");

            try
            {
                string jsonOutput = JsonSerializer.Serialize(output, new JsonSerializerOptions(JsonSerializerDefaults.Web));
                await response.WriteStringAsync(jsonOutput);
            }
            catch (Exception ex)
            {
                var errorResponse = req.CreateResponse(HttpStatusCode.InternalServerError);
                errorResponse.WriteString($"An error occurred during serialization. Error: {ex.Message}");
                return errorResponse;
            }


            return response;
        }

    }
}
