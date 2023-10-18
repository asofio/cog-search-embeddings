using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace cog_search_embeddings.Models
{
    public class ChunkingAndEmbeddingResponse
    {
        public List<OutputRecord> Values { get; set; }

        public ChunkingAndEmbeddingResponse()
        {
            Values = new List<OutputRecord>();
        }
    }

    public class OutputRecord
    {
        public string RecordId { get; set; }
        public ChunksData Data { get; set; }
        public List<OutputRecordMessage> Errors { get; set; }
        public List<OutputRecordMessage> Warnings { get; set; }
    }

    public class ChunksData
    {
        public List<string> Chunks { get; set; }
    }
}