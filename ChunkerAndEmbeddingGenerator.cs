using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using cog_search_embeddings.Models;
using System.Collections.Generic;
using Microsoft.SemanticKernel.Text;
using Microsoft.SemanticKernel.Plugins.Memory;
using Microsoft.SemanticKernel.Connectors.Memory.AzureCognitiveSearch;
using Microsoft.SemanticKernel.Connectors.AI.OpenAI;
using Microsoft.SemanticKernel.Memory;
using Polly;
using Polly.Retry;

namespace cog_search_embeddings
{
    public static class Chunker
    {
        private static readonly ILoggerFactory _loggerFactory = LoggerFactory.Create(builder =>
            builder.AddSimpleConsole(options =>
            {
                options.IncludeScopes = true;
                options.SingleLine = true;
                options.TimestampFormat = "HH:mm:ss ";
            }));
        private static readonly ResiliencePipeline _pipeline = new ResiliencePipelineBuilder().AddRetry(new RetryStrategyOptions()).Build();
        private static readonly string _azureCognitiveSearchEndpoint = Environment.GetEnvironmentVariable("AZURE_COGNITIVE_SEARCH_ENDPOINT");
        private static readonly string _azureCognitiveSearchEndpointKey = Environment.GetEnvironmentVariable("AZURE_COGNITIVE_SEARCH_ENDPOINT_KEY");
        private static readonly string _azureOpenAITextEmbeddingDeploymentName = Environment.GetEnvironmentVariable("AZURE_OPENAI_TEXT_EMBEDDING_DEPLOYMENT_NAME");
        private static readonly string _azureOpenAIEndpoint = Environment.GetEnvironmentVariable("AZURE_OPENAI_ENDPOINT");
        private static readonly string _azureOpenAIEndpointKey = Environment.GetEnvironmentVariable("AZURE_OPENAI_ENDPOINT_KEY");
        private static readonly ISemanticTextMemory _memory;

        static Chunker()
        {
            _memory = new MemoryBuilder()
                .WithMemoryStore(new AzureCognitiveSearchMemoryStore(_azureCognitiveSearchEndpoint, _azureCognitiveSearchEndpointKey))
                .WithAzureTextEmbeddingGenerationService(_azureOpenAITextEmbeddingDeploymentName, _azureOpenAIEndpoint, _azureOpenAIEndpointKey)
                .WithLoggerFactory(_loggerFactory)
                .Build();
        }

        [FunctionName("ChunkerAndEmbeddingGenerator")]
        public static async Task<IActionResult> Run(
            [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = null)] HttpRequest req,
            ILogger log)
        {
            var response = new ChunkingAndEmbeddingResponse();

            // Deserialize the request body
            string requestBody = new StreamReader(req.Body).ReadToEnd();
            var data = JsonConvert.DeserializeObject<ChunkingAndEmbeddingRequest>(requestBody);

            // Read configuration options from the request headers
            string targetIndex = req.Headers.ContainsKey("TargetIndex") ? req.Headers["TargetIndex"].ToString() : null;
            int maxTokensPerLine = req.Headers.ContainsKey("MaxTokensPerLine") ? int.Parse(req.Headers["MaxTokensPerLine"].ToString()) : 50;
            int maxTokensPerParagraph = req.Headers.ContainsKey("MaxTokensPerParagraph") ? int.Parse(req.Headers["MaxTokensPerParagraph"].ToString()) : 1000;
            int overlapTokens = req.Headers.ContainsKey("OverlapTokens") ? int.Parse(req.Headers["OverlapTokens"].ToString()) : 50;

            // Loop through all of the input records that the Cognitive Search Skillset will send in via this POST request
            foreach (var record in data.Values)
            {
                if (record == null || record.RecordId == null) continue;

                OutputRecord responseRecord = new OutputRecord
                {
                    RecordId = record.RecordId
                };
                
                try
                {
                    // Use Semantic Kernel's text chunker to split the text into lines and paragraphs
                    // This could be swapped out with another text chunker if desired
                    var lines = TextChunker.SplitPlainTextLines(record.Data.Text, maxTokensPerLine);
                    var paragraphs = TextChunker.SplitPlainTextParagraphs(lines, maxTokensPerParagraph, overlapTokens);
                    
                    await _pipeline.ExecuteAsync(async context => { 
                        /*
                            Use Semantic Kernel to persist index records to another index.  This will be opinionated and create a schema
                            within the index that is setup by Semantic Kernel.  At this point, there is no configuration to adjust the schema 
                            that Semantic Kernel will create.  If a different schema is desired, do not use Semantic Kernel.  In that case, create
                            your own schema, use the Azure Open AI SDK to generate embeddings and then use the Azure Cognitive Search SDK to persist
                            data to the index.  An example of using the AOAI sdk to generate embeddings can be found in Helpers/EmbeddingGenerator.cs.
                        */
                        for (int i = 0; i < paragraphs.Count; i++) {
                            await _memory.SaveInformationAsync(targetIndex, paragraphs[i], $"{record.Data.Filename}-{i}");
                        }
                     });

                    responseRecord.Data = new ChunksData
                    {
                        Chunks = paragraphs
                    };
                }
                catch (Exception e)
                {
                    // Something bad happened, log the issue.
                    var error = new OutputRecordMessage
                    {
                        Message = e.Message
                    };

                    responseRecord.Errors = new List<OutputRecordMessage>
                    {
                        error
                    };
                }
                finally
                {
                    response.Values.Add(responseRecord);
                }
            }

            return new OkObjectResult(response);
        }
    }
}
