using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace HydeDemo.Models;

/// <summary>
/// Represents a document chunk with embedding
/// </summary>
public class Document
{
    public string Id { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public Dictionary<string, object> Metadata { get; set; } = new();
    
    [JsonIgnore]
    public ReadOnlyMemory<float>? Embedding { get; set; }
    
    // For JSON serialization
    public float[]? EmbeddingArray
    {
        get => Embedding?.ToArray();
        set => Embedding = value != null ? new ReadOnlyMemory<float>(value) : null;
    }
}

/// <summary>
/// Represents a hypothetical document generated during HyDE search
/// </summary>
public class HypotheticalDocument
{
    public string DocumentText { get; set; } = string.Empty;
    public ReadOnlyMemory<float> DocumentEmbedding { get; set; }
    public string OriginalQuery { get; set; } = string.Empty;
    public Dictionary<string, object> QueryContext { get; set; } = new();
}
