using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.AI;
using Microsoft.SemanticKernel;
using HypeDemo.Models;
using HypeDemo.Services;

namespace HypeDemo.Plugins;

public class QuantumProjectsHyPEPlugin
{
    private readonly HyPEVectorStore _hyPEStore;
    private readonly IEmbeddingGenerator<string, Embedding<float>> _embeddingsService;

    public QuantumProjectsHyPEPlugin(HyPEVectorStore hyPEStore, IEmbeddingGenerator<string, Embedding<float>> embeddingsService)
    {
        _hyPEStore = hyPEStore;
        _embeddingsService = embeddingsService;
    }

    [KernelFunction("search_quantum_projects_hype")]
    [Description("Search quantum project information using HyPE (Hypothetical Prompt Embeddings) to answer questions about quantum research projects.")]
    public async Task<string> SearchQuantumProjectsHyPE(
        [Description("The search query about quantum projects")] string query)
    {
        Console.WriteLine($"\n🔍 HyPE Search: {query}");

        var queryEmbeddings = await _embeddingsService.GenerateAsync(new[] { query });
        var queryEmbedding = queryEmbeddings[0].Vector;

        // Find similar hypothetical questions
        var similarQuestions = _hyPEStore.SearchByQuestionSimilarity(queryEmbedding, topK: 3);

        if (similarQuestions.Count == 0)
        {
            return "No relevant quantum project information found.";
        }

        // Extract original chunks from matching questions
        var context = new List<string>();
        var seenChunks = new HashSet<string>(); // Avoid duplicate chunks

        Console.WriteLine("🎯 Question-to-Question Matches Found:");
        for (int i = 0; i < similarQuestions.Count; i++)
        {
            var hypQ = similarQuestions[i];
            var chunkId = hypQ.OriginalChunkId;
            
            if (!seenChunks.Contains(chunkId))
            {
                seenChunks.Add(chunkId);
                var project = hypQ.OriginalChunkMetadata.Project;
                var section = hypQ.OriginalChunkMetadata.Section;

                var displayQuestion = hypQ.Question.Length > 100 
                    ? hypQ.Question.Substring(0, 100) + "..." 
                    : hypQ.Question;
                    
                Console.WriteLine($"   {i + 1}. Matched Question: \"{displayQuestion}\"");
                Console.WriteLine($"      From: {project} → {section}");

                context.Add($"From {project} - {section}:\n{hypQ.OriginalChunkContent}");
            }
        }

        if (context.Count == 0)
        {
            return "No relevant quantum project information found.";
        }

        var result = string.Join("\n\n---\n\n", context);
        Console.WriteLine($"📊 Retrieved {context.Count} unique document chunks via HyPE");

        return result;
    }
}
