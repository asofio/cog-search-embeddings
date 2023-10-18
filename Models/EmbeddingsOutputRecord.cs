using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace cog_search_embeddings.Models
{
    public class EmbeddingsOutputRecord
    {
        public string Text { get; set; }
        public IEnumerable<float> Embedding { get; set; }
    }
}