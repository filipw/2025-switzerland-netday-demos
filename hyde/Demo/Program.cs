using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using HydeDemo.Models;
using HydeDemo.Plugins;
using HydeDemo.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Agents;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.AzureOpenAI;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using Microsoft.Extensions.AI;
using DotNetEnv;

namespace HydeDemo;

class Program
{
    static async Task Main(string[] args)
    {
        Console.WriteLine("🚀 HyDE-Enhanced Quantum Projects RAG Demo (.NET)");
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

        // Initialize HyDE vector store
        var hydeStore = new HydeVectorStore();
        hydeStore.SetServices(embeddingService, chatService);

        // Load and process the demo data
        var dataPath = Path.Combine(Directory.GetCurrentDirectory(), "..", "shared-data", "projects.md");
        Console.WriteLine($"\n📄 Loading quantum projects data from {dataPath}");

        if (!File.Exists(dataPath))
        {
            Console.WriteLine($"❌ Data file not found: {dataPath}");
            return;
        }

        var documents = DocumentLoader.LoadAndChunkProjectsData(dataPath);
        Console.WriteLine($"📚 Created {documents.Count} document chunks");

        // HyDE indexing is same as regular RAG indexing (only documents, not hypothetical docs!)
        Console.WriteLine("\n🧠 Starting HyDE document indexing phase...");
        Console.WriteLine("=" + new string('=', 59));

        var dataDir = Path.Combine(Directory.GetCurrentDirectory(), "data");
        Directory.CreateDirectory(dataDir);
        var indexFilePath = Path.Combine(dataDir, "hyde_index.json");

        if (File.Exists(indexFilePath))
        {
            var existingHydeStore = await HydeVectorStore.LoadIndexAsync(indexFilePath);
            if (existingHydeStore?.Documents.Count > 0)
            {
                hydeStore = existingHydeStore;
                hydeStore.SetServices(embeddingService, chatService);
                Console.WriteLine("✅ Loaded existing HyDE document index");
            }
            else
            {
                Console.WriteLine("🔨 Creating new HyDE document index...");
                await hydeStore.AddDocumentsAsync(documents);
                await hydeStore.SaveIndexAsync(indexFilePath);
            }
        }
        else
        {
            Console.WriteLine("🔨 Creating new HyDE document index...");
            await hydeStore.AddDocumentsAsync(documents);
            await hydeStore.SaveIndexAsync(indexFilePath);
        }

        Console.WriteLine("=" + new string('=', 59));
        Console.WriteLine("✅ HyDE document indexing complete!");

        // Create the HyDE-enhanced quantum projects plugin
        var quantumPlugin = new QuantumProjectsHydePlugin(hydeStore);
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
            Name = "HyDE-Assistant",
            Instructions = """
                You are a helpful assistant specializing in quantum research projects. 
                You have access to information about various quantum computing projects through HyDE (Hypothetical Document Embeddings).
                
                Use the SearchQuantumProjectsHydeAsync function to find relevant information from the quantum projects database.
                This system uses advanced document-to-document matching via hypothetical document generation for improved retrieval accuracy.
                
                Always base your answers on the retrieved information and cite which projects the information comes from.
                If you can't find relevant information, say so clearly.
                """,
            Kernel = kernel,
            Arguments = new KernelArguments(executionSettings)
        };

        Console.WriteLine("✅ Created HyDE-Enhanced Quantum Projects ChatCompletionAgent");

        var testQueries = new[]
        {
            "Tell me about quantum key distribution research",
            "What are the main challenges in scaling quantum computing systems?",
            "What quantum technologies are being developed for secure communications?"
        };

        Console.WriteLine("\n" + new string('=', 80));


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
                        Console.WriteLine($"\n🤖 Assistant Response:\n{response.Message.Content}");
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Error processing query: {ex.Message}");
            }

            await Task.Delay(1000);
        }

        Console.WriteLine($"\n{new string('=', 80)}");
        Console.WriteLine("\nPress any key to exit...");
        Console.ReadKey();
    }
}
