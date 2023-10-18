using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Azure.AI.OpenAI;

namespace cog_search_embeddings.Helpers
{
    public class EmbeddingGenerator
    {
        private readonly OpenAIClient _oaiClient;
        private readonly string _aoaiDeploymentName;

        public EmbeddingGenerator(string aoaiKey, string aoaiEndpoint, string aoaiDeploymentName)
        {
            _oaiClient = new OpenAIClient(new Uri(aoaiEndpoint), new Azure.AzureKeyCredential(aoaiKey));
            _aoaiDeploymentName = aoaiDeploymentName;
        }

        public async Task<IEnumerable<float>> GenerateEmbeddingAsync(string text) {
            var embeddings = await _oaiClient.GetEmbeddingsAsync(_aoaiDeploymentName, new EmbeddingsOptions(text));
            return embeddings.Value.Data[0].Embedding;
        }
    }
}