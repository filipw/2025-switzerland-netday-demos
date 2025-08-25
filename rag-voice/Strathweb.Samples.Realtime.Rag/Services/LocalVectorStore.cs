using System.Text.Json;
using System.Text.Json.Serialization;
using Azure.AI.OpenAI;
using System.ClientModel;

namespace Strathweb.Samples.Realtime.Rag.Services;

public class LocalVectorStore
{
    private readonly List<Product> _products;
    private readonly AzureOpenAIClient _openAiClient;
    private readonly string _embeddingDeployment;

    public LocalVectorStore(List<Product> products, AzureOpenAIClient openAiClient, string embeddingDeployment)
    {
        _products = products;
        _openAiClient = openAiClient;
        _embeddingDeployment = embeddingDeployment;
    }

    public static async Task<LocalVectorStore> LoadFromFileAsync(string filePath, AzureOpenAIClient openAiClient, string embeddingDeployment)
    {
        var jsonContent = await File.ReadAllTextAsync(filePath);
        var products = JsonSerializer.Deserialize<List<Product>>(jsonContent) ?? new List<Product>();
        return new LocalVectorStore(products, openAiClient, embeddingDeployment);
    }

    public async Task<List<Product>> SearchAsync(string query, int topK = 5)
    {
        // Generate embedding for the query
        var embeddingClient = _openAiClient.GetEmbeddingClient(_embeddingDeployment);
        var queryEmbeddingResponse = await embeddingClient.GenerateEmbeddingAsync(query);
        var queryEmbedding = queryEmbeddingResponse.Value.ToFloats();

        // Calculate cosine similarity for each product
        var similarities = new List<(Product Product, float Similarity)>();
        
        foreach (var product in _products)
        {
            if (product.DescriptionVector != null && product.DescriptionVector.Length > 0)
            {
                var similarity = CosineSimilarity(queryEmbedding, product.DescriptionVector);
                similarities.Add((product, similarity));
            }
        }

        // Sort by similarity (highest first) and return top K
        return similarities
            .OrderByDescending(x => x.Similarity)
            .Take(topK)
            .Select(x => x.Product)
            .ToList();
    }

    private static float CosineSimilarity(ReadOnlyMemory<float> vector1, float[] vector2)
    {
        var a = vector1.Span;
        var b = vector2.AsSpan();
        
        if (a.Length != b.Length)
            return 0f;

        float dotProduct = 0f;
        float normA = 0f;
        float normB = 0f;

        for (int i = 0; i < a.Length; i++)
        {
            dotProduct += a[i] * b[i];
            normA += a[i] * a[i];
            normB += b[i] * b[i];
        }

        if (normA == 0f || normB == 0f)
            return 0f;

        return dotProduct / (MathF.Sqrt(normA) * MathF.Sqrt(normB));
    }
}

public record Product(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("image_blob_path")] string ImageBlobPath,
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("category")] string Category,
    [property: JsonPropertyName("price")] string Price,
    [property: JsonPropertyName("description")] string Description,
    [property: JsonPropertyName("image_vector")] float[]? ImageVector,
    [property: JsonPropertyName("description_vector")] float[]? DescriptionVector
);
