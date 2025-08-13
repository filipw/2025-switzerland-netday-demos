using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics.Tensors;
using System.Text.Json;
using System.Threading.Tasks;
using HydeDemo.Models;
using Microsoft.Extensions.AI;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;

namespace HydeDemo.Services;

public class HydeVectorStore
{
    private readonly List<Document> _documents = new();
    private readonly List<HypotheticalDocument> _hypotheticalDocuments = new();
    private IEmbeddingGenerator<string, Embedding<float>>? _embeddingService;
    private IChatCompletionService? _chatService;

    public IReadOnlyList<Document> Documents => _documents;
    public IReadOnlyList<HypotheticalDocument> HypotheticalDocuments => _hypotheticalDocuments;

    public void SetServices(IEmbeddingGenerator<string, Embedding<float>> embeddingService, IChatCompletionService chatService)
    {
        _embeddingService = embeddingService ?? throw new ArgumentNullException(nameof(embeddingService));
        _chatService = chatService ?? throw new ArgumentNullException(nameof(chatService));
    }

    public async Task AddDocumentsAsync(IEnumerable<Document> documents)
    {
        var documentList = documents.ToList();
        Console.WriteLine($"\n📄 Indexing {documentList.Count} document chunks...");

        foreach (var doc in documentList)
        {
            if (_embeddingService != null && doc.Embedding == null)
            {
                var embedding = await _embeddingService.GenerateAsync(doc.Content);
                doc.Embedding = new ReadOnlyMemory<float>(embedding.Vector.ToArray());
            }
            _documents.Add(doc);
        }

        Console.WriteLine($"✅ Indexed {documentList.Count} documents with embeddings");
    }

    public async Task<string> GenerateHypotheticalDocumentAsync(string query, string? taskInstruction = null)
    {
        if (_chatService == null)
            throw new InvalidOperationException("Chat service not set. Call SetServices first.");

        try
        {
            taskInstruction ??= "Write a detailed, specific passage that directly answers the question with concrete details, facts, and specific information. Avoid generic introductions.";

            var prompt = $@"{taskInstruction}

Example:
Question: What is Project Lighthouse and what is its budget?
Passage: Project Lighthouse is dedicated to developing a secure communication system using Quantum Key Distribution (QKD). The primary objective is to create a QKD system that can securely transmit encryption keys over distances of up to 500 kilometers by 2025. Project Lighthouse is the most well-funded quantum project, with a budget of $50 million for 2024. Approximately 60% of the budget is dedicated to building the infrastructure required for large-scale QKD networks, including optical fiber installations, ground stations, and communication satellites.

Question: {query}
Passage:";

            var chatHistory = new ChatHistory();
            chatHistory.AddUserMessage(prompt);

            var response = await _chatService.GetChatMessageContentAsync(chatHistory);
            var hypotheticalDoc = response.Content?.Trim() ?? "";
            
            if (string.IsNullOrWhiteSpace(hypotheticalDoc))
            {
                Console.WriteLine($"⚠️ Empty response from LLM for query: {query}");
                return query; // fallback to original query
            }

            Console.WriteLine($"📝 Generated hypothetical document ({hypotheticalDoc.Length} chars) for query: {query[..Math.Min(50, query.Length)]}...");
            Console.WriteLine($"🔍 Hypothetical document preview: {hypotheticalDoc[..Math.Min(200, hypotheticalDoc.Length)]}...");

            return hypotheticalDoc;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"⚠️ Error generating hypothetical document: {ex.Message}");
            return query; // fallback to original query
        }
    }

    public async Task<List<Document>> SearchWithHydeAsync(string query, int topK = 3, string? taskInstruction = null)
    {
        if (_embeddingService == null)
            throw new InvalidOperationException("Embedding service not set. Call SetServices first.");

        Console.WriteLine($"\n🔍 HyDE Search: {query}");

        // Step 1: Generate hypothetical document that would answer the query
        var hypotheticalDoc = await GenerateHypotheticalDocumentAsync(query, taskInstruction);

        // Step 2: Embed the hypothetical document
        Console.WriteLine("🔮 Embedding hypothetical document...");
        var hypEmbedding = await _embeddingService.GenerateAsync(hypotheticalDoc);
        var hypEmbeddingMemory = new ReadOnlyMemory<float>(hypEmbedding.Vector.ToArray());

        // Step 3: Search for similar real documents using document-document similarity
        Console.WriteLine("🎯 Searching for similar real documents...");
        var similarDocs = SearchByEmbedding(hypEmbeddingMemory, topK);

        // Store the hypothetical document for later analysis or debugging
        var hypDocEntry = new HypotheticalDocument
        {
            DocumentText = hypotheticalDoc,
            DocumentEmbedding = hypEmbeddingMemory,
            OriginalQuery = query,
            QueryContext = new Dictionary<string, object> { ["task_instruction"] = taskInstruction ?? "" }
        };
        _hypotheticalDocuments.Add(hypDocEntry);

        Console.WriteLine($"📊 Found {similarDocs.Count} similar documents via HyDE");
        return similarDocs;
    }

    private List<Document> SearchByEmbedding(ReadOnlyMemory<float> queryEmbedding, int topK = 3)
    {
        if (!_documents.Any())
            return new List<Document>();

        var similarities = new List<(float similarity, Document doc)>();

        foreach (var doc in _documents)
        {
            if (doc.Embedding.HasValue)
            {
                var similarity = TensorPrimitives.CosineSimilarity(queryEmbedding.Span, doc.Embedding.Value.Span);
                similarities.Add((similarity, doc));
            }
        }

        similarities.Sort((a, b) => b.similarity.CompareTo(a.similarity));

        // Show top similarities for illustration
        Console.WriteLine("🔍 Top similarities:");
        for (int i = 0; i < Math.Min(5, similarities.Count); i++)
        {
            var (sim, doc) = similarities[i];
            var project = doc.Metadata.GetValueOrDefault("project", "Unknown").ToString();
            var section = doc.Metadata.GetValueOrDefault("section", "Unknown").ToString();
            Console.WriteLine($"   {i + 1}. {sim:F4} - {project} → {section}");
        }

        return similarities.Take(topK).Select(x => x.doc).ToList();
    }

    public async Task SaveIndexAsync(string filePath)
    {
        var indexData = _documents.Select(doc => new
        {
            doc.Id,
            doc.Content,
            doc.Metadata,
            EmbeddingArray = doc.Embedding?.ToArray()
        }).ToList();

        var json = JsonSerializer.Serialize(indexData, new JsonSerializerOptions { WriteIndented = true });
        await File.WriteAllTextAsync(filePath, json);

        Console.WriteLine($"💾 Saved HyDE document index with {indexData.Count} documents to {filePath}");
    }

    public static async Task<HydeVectorStore?> LoadIndexAsync(string filePath)
    {
        if (!File.Exists(filePath))
            return null;

        try
        {
            var json = await File.ReadAllTextAsync(filePath);
            var indexData = JsonSerializer.Deserialize<List<JsonElement>>(json);

            if (indexData == null)
                return null;

            var hydeStore = new HydeVectorStore();

            foreach (var item in indexData)
            {
                var doc = new Document
                {
                    Id = item.GetProperty("Id").GetString() ?? "",
                    Content = item.GetProperty("Content").GetString() ?? "",
                    Metadata = JsonSerializer.Deserialize<Dictionary<string, object>>(
                        item.GetProperty("Metadata").GetRawText()) ?? new(),
                };

                if (item.TryGetProperty("EmbeddingArray", out var embeddingElement) && 
                    embeddingElement.ValueKind != JsonValueKind.Null)
                {
                    var embeddingArray = JsonSerializer.Deserialize<float[]>(embeddingElement.GetRawText());
                    if (embeddingArray != null)
                    {
                        doc.Embedding = new ReadOnlyMemory<float>(embeddingArray);
                    }
                }

                hydeStore._documents.Add(doc);
            }

            Console.WriteLine($"📥 Loaded HyDE document index with {hydeStore._documents.Count} documents from {filePath}");
            return hydeStore;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"⚠️ Error loading HyDE index: {ex.Message}");
            return null;
        }
    }
}
