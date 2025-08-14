using System;
using System.Collections.Generic;

namespace HypeDemo.Models;

public class ChunkMetadata
{
    public string Project { get; set; } = string.Empty;
    public string Section { get; set; } = string.Empty;
    public int ProjectIndex { get; set; }
    public int SectionIndex { get; set; }
}

public class Document
{
    public string Id { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public ChunkMetadata Metadata { get; set; } = new();
    public float[]? Embedding { get; set; }
}

public class HypotheticalQuestion
{
    public string Question { get; set; } = string.Empty;
    public float[] QuestionEmbedding { get; set; } = Array.Empty<float>();
    public string OriginalChunkId { get; set; } = string.Empty;
    public string OriginalChunkContent { get; set; } = string.Empty;
    public ChunkMetadata OriginalChunkMetadata { get; set; } = new();
}
