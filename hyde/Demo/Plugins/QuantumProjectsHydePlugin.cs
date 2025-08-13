using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using HydeDemo.Services;
using Microsoft.SemanticKernel;

namespace HydeDemo.Plugins;

public class QuantumProjectsHydePlugin
{
    private readonly HydeVectorStore _hydeStore;

    public QuantumProjectsHydePlugin(HydeVectorStore hydeStore)
    {
        _hydeStore = hydeStore ?? throw new ArgumentNullException(nameof(hydeStore));
    }

    [KernelFunction]
    [Description("Search quantum project information using HyDE (Hypothetical Document Embeddings) to answer questions about quantum research projects.")]
    public async Task<string> SearchQuantumProjectsHydeAsync(
        [Description("The search query about quantum projects")] string query)
    {
        const string taskInstruction = "Write a detailed scientific passage about specific quantum computing projects, including concrete details like project names, goals, technologies, budgets, timelines, and technical specifications. Focus on the specific details mentioned in the question. Use the same style and level of detail as the example provided.";

        var similarDocs = await _hydeStore.SearchWithHydeAsync(query, topK: 3, taskInstruction: taskInstruction);

        if (!similarDocs.Any())
        {
            return "No relevant quantum project information found.";
        }

        var context = new List<string>();
        for (int i = 0; i < similarDocs.Count; i++)
        {
            var doc = similarDocs[i];
            var project = doc.Metadata.GetValueOrDefault("project", "Unknown Project").ToString();
            var section = doc.Metadata.GetValueOrDefault("section", "Unknown Section").ToString();

            Console.WriteLine($"   {i + 1}. Retrieved: {project} → {section}");
            context.Add($"From {project} - {section}:\n{doc.Content}");
        }

        return string.Join("\n\n---\n\n", context);
    }
}
