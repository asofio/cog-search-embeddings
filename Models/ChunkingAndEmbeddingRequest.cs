using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace cog_search_embeddings.Models
{
    public class ChunkingAndEmbeddingRequest
    {
        public List<InputRecord> Values { get; set; }
    }

    public class InputRecord
    {
        public string RecordId { get; set; }
        public FileData Data { get; set; }
    }

    public class FileData
    {
        public string Text { get; set; }
        public string Filename { get; set; }
    }
}