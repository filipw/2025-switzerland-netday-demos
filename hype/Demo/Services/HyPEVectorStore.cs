using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using HypeDemo.Models;

namespace HypeDemo.Services;

public class HyPEVectorStore
{
    private readonly List<HypotheticalQuestion> _hypotheticalQuestions = new();
    private IEmbeddingGenerator<string, Embedding<float>>? _embeddingsService;
    private IChatCompletionService? _llmService;

    public void SetServices(IEmbeddingGenerator<string, Embedding<float>> embeddingsService, IChatCompletionService llmService)
    {
        _embeddingsService = embeddingsService;
        _llmService = llmService;
    }

    private async Task<List<string>> GenerateHypotheticalQuestions(string chunkContent, ChunkMetadata chunkMetadata)
    {
        try
        {
            var project = chunkMetadata.Project;
            var section = chunkMetadata.Section;
            
            Console.WriteLine($"🤖 Generating questions for {project}/{section} (content length: {chunkContent.Length})");
            
            // Create a kernel and add the LLM service
            var kernelBuilder = Kernel.CreateBuilder();
            kernelBuilder.Services.AddSingleton<IChatCompletionService>(_llmService!);
            var kernel = kernelBuilder.Build();
            
            var questionGeneratorFunction = kernel.CreateFunctionFromPrompt(
                """
                You are an expert at generating highly specific hypothetical questions from document content for information retrieval.

                Your task is to analyze the text and generate 3-5 distinctive questions that would lead someone to search for this EXACT content. Make each question uniquely identifying by including:

                SPECIFICITY REQUIREMENTS:
                - Include specific numbers, dollar amounts, percentages, dates, and technical metrics mentioned
                - Reference unique project names, technologies, and methodologies described
                - Capture distinctive challenges, achievements, or goals that set this content apart
                - Use comparative language when appropriate (e.g., "largest", "first to achieve", "most advanced")
                - Include technical terminology and domain-specific details

                STYLE REQUIREMENTS:
                - Phrase as natural questions a researcher or professional might ask
                - Start with question words: "What", "Which", "How", "When", "Where", "Why"
                - Make questions searchable - they should lead to THIS specific content, not generic information
                - Avoid generic questions that could apply to multiple projects or documents

                EXAMPLES OF GOOD vs BAD:
                ❌ BAD: "What is the project's budget?" (too generic)
                ✅ GOOD: "Which quantum project has a $50 million budget dedicated to QKD infrastructure?"

                ❌ BAD: "What are the project goals?" (too generic) 
                ✅ GOOD: "What quantum project aims to stabilize 200 ion qubits by 2025 with 0.005% error rate?"

                Text to analyze:
                {{$chunk_content}}

                Generate exactly 3-5 highly specific, distinctive questions (one per line, no numbering):
                """,
                functionName: "GenerateQuestions");

            var kernelArguments = new KernelArguments
            {
                ["chunk_content"] = chunkContent
            };

            Console.WriteLine($"📤 Invoking kernel function for {project}/{section}...");
            var response = await questionGeneratorFunction.InvokeAsync(kernel, kernelArguments);
            Console.WriteLine($"📥 Received response for {project}/{section}: {response?.ToString()?.Length ?? 0} chars");

            if (response?.ToString() == null || string.IsNullOrWhiteSpace(response.ToString()))
            {
                Console.WriteLine($"⚠️ Empty response from LLM for chunk {project}/{section}");
                Console.WriteLine($"⚠️ Response object: {response}");
                Console.WriteLine($"⚠️ Content: '{response?.ToString()}'");
                return [];
            }

            var questionsText = response.ToString()!.Trim();
            Console.WriteLine($"📝 Raw response: {questionsText.Substring(0, Math.Min(200, questionsText.Length))}...");
            
            var questions = questionsText.Split('\n')
                .Select(q => q.Trim())
                .Where(q => !string.IsNullOrEmpty(q) && 
                           !q.StartsWith("1.") && !q.StartsWith("2.") && !q.StartsWith("3.") && 
                           !q.StartsWith("4.") && !q.StartsWith("5.") && 
                           !q.StartsWith("-") && !q.StartsWith("*") && !q.StartsWith("•"))
                .ToList();

            // Clean up any remaining numbering or formatting
            var cleanedQuestions = new List<string>();
            foreach (var q in questions)
            {
                var cleanedQ = q.Trim();
                if (!string.IsNullOrEmpty(cleanedQ))
                {
                    // Remove common prefixes
                    var prefixes = new[] { "1. ", "2. ", "3. ", "4. ", "5. ", "- ", "* ", "• " };
                    foreach (var prefix in prefixes)
                    {
                        if (cleanedQ.StartsWith(prefix))
                        {
                            cleanedQ = cleanedQ.Substring(prefix.Length).Trim();
                        }
                    }

                    // Only add if it's a "reasonable" question
                    if (cleanedQ.Length > 10 && cleanedQ.Contains('?'))
                    {
                        cleanedQuestions.Add(cleanedQ);
                    }
                }
            }

            Console.WriteLine($"💡 Generated {cleanedQuestions.Count} hypothetical questions for chunk: {project}/{section}");
            for (int i = 0; i < Math.Min(cleanedQuestions.Count, 3); i++)
            {
                var question = cleanedQuestions[i];
                var displayQuestion = question.Length > 80 ? question.Substring(0, 80) + "..." : question;
                Console.WriteLine($"   {i + 1}. {displayQuestion}");
            }

            return cleanedQuestions.Take(5).ToList(); // 5 questions max in this sample
        }
        catch (Exception e)
        {
            Console.WriteLine($"⚠️ Error generating hypothetical questions: {e}");
            return [];
        }
    }

    public async Task AddDocumentsWithHyPE(List<Document> documents)
    {
        Console.WriteLine($"\n🔬 Starting HyPE indexing for {documents.Count} document chunks...");

        for (int i = 0; i < documents.Count; i++)
        {
            var doc = documents[i];
            var project = doc.Metadata.Project;
            var section = doc.Metadata.Section;
            
            Console.WriteLine($"\n📄 Processing chunk {i + 1}/{documents.Count}: {project}/{section}");

            // Generate hypothetical questions for this chunk
            var questions = await GenerateHypotheticalQuestions(doc.Content, doc.Metadata);

            if (questions.Count == 0)
            {
                Console.WriteLine($"⚠️ No questions generated for chunk {doc.Id}, skipping...");
                continue;
            }

            // Create an embedding for each question
            Console.WriteLine($"🔮 Embedding {questions.Count} hypothetical questions...");
            var embeddings = await _embeddingsService!.GenerateAsync(questions);

            // Store each question with its embedding and link to original content
            for (int j = 0; j < questions.Count; j++)
            {
                var question = questions[j];
                var embedding = embeddings[j].Vector.ToArray();

                var hypQuestion = new HypotheticalQuestion
                {
                    Question = question,
                    QuestionEmbedding = embedding,
                    OriginalChunkId = doc.Id,
                    OriginalChunkContent = doc.Content,
                    OriginalChunkMetadata = doc.Metadata
                };
                _hypotheticalQuestions.Add(hypQuestion);
            }

            // Add a small delay to avoid rate limiting
            await Task.Delay(500);
        }

        Console.WriteLine($"\n✅ HyPE indexing complete! Created {_hypotheticalQuestions.Count} hypothetical question embeddings");
    }

    public List<HypotheticalQuestion> SearchByQuestionSimilarity(ReadOnlyMemory<float> queryEmbedding, int topK = 3)
    {
        if (_hypotheticalQuestions.Count == 0)
        {
            return new List<HypotheticalQuestion>();
        }

        var similarities = new List<(float similarity, HypotheticalQuestion question)>();

        foreach (var hypQ in _hypotheticalQuestions)
        {
            var similarity = CosineSimilarity(queryEmbedding.Span, hypQ.QuestionEmbedding);
            similarities.Add((similarity, hypQ));
        }

        return similarities
            .OrderByDescending(x => x.similarity)
            .Take(topK)
            .Select(x => x.question)
            .ToList();
    }

    private static float CosineSimilarity(ReadOnlySpan<float> vector1, ReadOnlySpan<float> vector2)
    {
        if (vector1.Length != vector2.Length)
            throw new ArgumentException("Vectors must have the same length");

        float dotProduct = 0;
        float norm1 = 0;
        float norm2 = 0;

        for (int i = 0; i < vector1.Length; i++)
        {
            dotProduct += vector1[i] * vector2[i];
            norm1 += vector1[i] * vector1[i];
            norm2 += vector2[i] * vector2[i];
        }

        if (norm1 == 0 || norm2 == 0)
            return 0;

        return dotProduct / (MathF.Sqrt(norm1) * MathF.Sqrt(norm2));
    }

    public void SaveHyPEIndex(string filePath)
    {
        var indexData = _hypotheticalQuestions.Select(hypQ => new
        {
            question = hypQ.Question,
            question_embedding = hypQ.QuestionEmbedding,
            original_chunk_id = hypQ.OriginalChunkId,
            original_chunk_content = hypQ.OriginalChunkContent,
            original_chunk_metadata = new 
            {
                project = hypQ.OriginalChunkMetadata.Project,
                section = hypQ.OriginalChunkMetadata.Section,
                project_index = hypQ.OriginalChunkMetadata.ProjectIndex,
                section_index = hypQ.OriginalChunkMetadata.SectionIndex
            }
        }).ToList();

        var jsonOptions = new JsonSerializerOptions { WriteIndented = true };
        var jsonString = JsonSerializer.Serialize(indexData, jsonOptions);
        File.WriteAllText(filePath, jsonString);

        Console.WriteLine($"💾 Saved HyPE index with {indexData.Count} questions to {filePath}");
    }

    public static HyPEVectorStore? LoadHyPEIndex(string filePath)
    {
        if (!File.Exists(filePath))
        {
            return null;
        }

        try
        {
            var jsonString = File.ReadAllText(filePath);
            var indexData = JsonSerializer.Deserialize<JsonElement[]>(jsonString);

            if (indexData == null)
                return null;

            var hyPEStore = new HyPEVectorStore();

            foreach (var item in indexData)
            {
                var questionEmbedding = item.GetProperty("question_embedding")
                    .EnumerateArray()
                    .Select(e => e.GetSingle())
                    .ToArray();

                var metadataElement = item.GetProperty("original_chunk_metadata");
                var metadata = new ChunkMetadata
                {
                    Project = metadataElement.GetProperty("project").GetString() ?? string.Empty,
                    Section = metadataElement.GetProperty("section").GetString() ?? string.Empty,
                    ProjectIndex = metadataElement.GetProperty("project_index").GetInt32(),
                    SectionIndex = metadataElement.GetProperty("section_index").GetInt32()
                };

                var hypQ = new HypotheticalQuestion
                {
                    Question = item.GetProperty("question").GetString() ?? string.Empty,
                    QuestionEmbedding = questionEmbedding,
                    OriginalChunkId = item.GetProperty("original_chunk_id").GetString() ?? string.Empty,
                    OriginalChunkContent = item.GetProperty("original_chunk_content").GetString() ?? string.Empty,
                    OriginalChunkMetadata = metadata
                };
                hyPEStore._hypotheticalQuestions.Add(hypQ);
            }

            Console.WriteLine($"📥 Loaded HyPE index with {hyPEStore._hypotheticalQuestions.Count} questions from {filePath}");
            return hyPEStore;
        }
        catch (Exception e)
        {
            Console.WriteLine($"⚠️ Error loading HyPE index: {e}");
            return null;
        }
    }

    public List<HypotheticalQuestion> GetAllQuestions() => _hypotheticalQuestions.ToList();
}
