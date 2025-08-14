using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using HypeDemo.Models;
using HypeDemo.Plugins;
using HypeDemo.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Agents;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.AzureOpenAI;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using Microsoft.Extensions.AI;
using DotNetEnv;

namespace HypeDemo;

class Program
{
    static async Task Main(string[] args)
    {
        Console.WriteLine("🚀 HyPE-Enhanced Quantum Projects RAG Demo (.NET)");
        Console.WriteLine("=" + new string('=', 59));

        DotNetEnv.Env.Load();

        var configuration = new ConfigurationBuilder()
            .AddJsonFile("appsettings.json", optional: true)
            .AddEnvironmentVariables()
            .Build();

        var azureOpenAIConfig = configuration.GetSection("AzureOpenAI");
        var endpoint = Environment.GetEnvironmentVariable("AZURE_OPENAI_ENDPOINT") ?? azureOpenAIConfig["Endpoint"];
        var apiKey = Environment.GetEnvironmentVariable("AZURE_OPENAI_API_KEY") ?? azureOpenAIConfig["ApiKey"];
        var deploymentName = Environment.GetEnvironmentVariable("AZURE_OPENAI_DEPLOYMENT_NAME") ?? azureOpenAIConfig["DeploymentName"] ?? "gpt-4o-mini";
        var embeddingDeploymentName = Environment.GetEnvironmentVariable("AZURE_OPENAI_EMBEDDING_DEPLOYMENT_NAME") ?? azureOpenAIConfig["EmbeddingDeploymentName"] ?? "text-embedding-ada-002";

        if (string.IsNullOrEmpty(endpoint) || string.IsNullOrEmpty(apiKey))
        {
            Console.WriteLine("❌ Azure OpenAI configuration missing. Please set:");
            Console.WriteLine("   - AZURE_OPENAI_ENDPOINT");
            Console.WriteLine("   - AZURE_OPENAI_API_KEY");
            Console.WriteLine("   OR configure them in appsettings.json");
            return;
        }

        var kernelBuilder = Kernel.CreateBuilder()
            .AddAzureOpenAIChatCompletion(deploymentName, endpoint, apiKey)
            .AddAzureOpenAIEmbeddingGenerator(embeddingDeploymentName, endpoint, apiKey);

        var kernel = kernelBuilder.Build();
        var chatService = kernel.GetRequiredService<IChatCompletionService>();
        var embeddingService = kernel.GetRequiredService<IEmbeddingGenerator<string, Embedding<float>>>();

        Console.WriteLine("✅ Created chat completion service");
        Console.WriteLine("✅ Created embeddings service");

        // Initialize HyPE vector store
        var hyPEStore = new HyPEVectorStore();
        hyPEStore.SetServices(embeddingService, chatService);

        // Load and process the demo data
        var dataPath = Path.Combine(Directory.GetCurrentDirectory(), "..", "..", "shared-data", "projects.md");
        Console.WriteLine($"\n📄 Loading quantum projects data from {dataPath}");

        if (!File.Exists(dataPath))
        {
            Console.WriteLine($"❌ Data file not found: {dataPath}");
            return;
        }

        var documents = DocumentLoader.LoadAndChunkProjectsData(dataPath);
        Console.WriteLine($"📚 Created {documents.Count} document chunks");

        // HyPE indexing phase
        Console.WriteLine("\n🧠 Starting HyPE indexing phase...");
        Console.WriteLine("=" + new string('=', 59));

        var dataDir = Path.Combine(Directory.GetCurrentDirectory(), "data");
        Directory.CreateDirectory(dataDir);
        var indexFilePath = Path.Combine(dataDir, "hype_index.json");

        if (File.Exists(indexFilePath))
        {
            var existingHyPEStore = HyPEVectorStore.LoadHyPEIndex(indexFilePath);
            if (existingHyPEStore?.GetAllQuestions().Count > 0)
            {
                hyPEStore = existingHyPEStore;
                hyPEStore.SetServices(embeddingService, chatService);
                Console.WriteLine("✅ Loaded existing HyPE index");
            }
            else
            {
                Console.WriteLine("🔨 Creating new HyPE index...");
                await hyPEStore.AddDocumentsWithHyPE(documents);
                hyPEStore.SaveHyPEIndex(indexFilePath);
            }
        }
        else
        {
            Console.WriteLine("🔨 Creating new HyPE index...");
            await hyPEStore.AddDocumentsWithHyPE(documents);
            hyPEStore.SaveHyPEIndex(indexFilePath);
        }

        Console.WriteLine("=" + new string('=', 59));
        Console.WriteLine("✅ HyPE indexing complete!");

        // Create the HyPE-enhanced quantum projects plugin
        var quantumPlugin = new QuantumProjectsHyPEPlugin(hyPEStore, embeddingService);
        kernel.Plugins.AddFromObject(quantumPlugin, "QuantumProjects");

        // Configure function calling settings
        var executionSettings = new AzureOpenAIPromptExecutionSettings
        {
            ToolCallBehavior = ToolCallBehavior.AutoInvokeKernelFunctions,
            MaxTokens = 1000,
            Temperature = 0.7
        };

        // Create ChatCompletionAgent
        var agent = new ChatCompletionAgent
        {
            Name = "HyPE-Assistant",
            Instructions = """
                You are a helpful assistant specializing in quantum research projects. 
                You have access to information about various quantum computing projects through HyPE (Hypothetical Prompt Embeddings).
                
                Use the search_quantum_projects_hype function to find relevant information from the quantum projects database.
                This system uses advanced question-to-question matching for improved retrieval accuracy.
                
                Always base your answers on the retrieved information and cite which projects the information comes from.
                If you can't find relevant information, say so clearly.
                """,
            Kernel = kernel,
            Arguments = new KernelArguments(executionSettings)
        };

        Console.WriteLine("✅ Created HyPE-Enhanced Quantum Projects ChatCompletionAgent");

        // Test queries designed to showcase HyPE's strength in bridging the question-document style gap
        var testQueries = new[]
        {
            // Specific factual queries that benefit from question-to-question matching
            "What quantum project achieved 150 ion qubits with 0.005% error rate?",
            "Which project has the largest budget at $50 million and what is its focus?", 
            "What are the specific timeline milestones for reaching 500 qubits by 2030?",
            
            // Comparative queries that require understanding relationships between projects
            "Which projects focus on fault-tolerant quantum computation and error correction?",
            
            // Complex queries that test semantic understanding and style bridging
            "What challenges do ion-trap quantum systems face when scaling beyond 200 qubits?",
            "Which quantum technologies can handle real-time data processing at scale?"
        };

        Console.WriteLine("\n" + new string('=', 80));
        Console.WriteLine("🎯 HyPE RETRIEVAL DEMO - Question-to-Question Matching");
        Console.WriteLine("Demonstrating HyPE's advantages in bridging the query-document style gap");
        Console.WriteLine(new string('=', 80));

        var chatThread = new ChatHistoryAgentThread();

        for (int i = 0; i < testQueries.Length; i++)
        {
            var query = testQueries[i];
            Console.WriteLine($"\n{new string('=', 60)}");
            Console.WriteLine($"💬 Test Query {i + 1}/{testQueries.Length}: {query}");
            Console.WriteLine(new string('=', 60));

            try
            {
                await foreach (var response in agent.InvokeAsync(query, chatThread))
                {
                    if (response.Message.Content != null)
                    {
                        Console.WriteLine($"\n🤖 HyPE-Enhanced Response:\n{response.Message.Content}");
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Error processing query: {ex.Message}");
            }

            await Task.Delay(2000);
        }

        Console.WriteLine($"\n{new string('=', 80)}");
        Console.WriteLine("\nPress any key to exit...");
        Console.ReadKey();
    }
}
