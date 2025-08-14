using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
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

        // todo
    }
}
