using Azure.AI.OpenAI;
using OpenAI.Realtime;
using System.ClientModel;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Azure;
using Azure.Search.Documents;
using Azure.Search.Documents.Indexes;
using Azure.Search.Documents.Models;

// bootstrap RealtimeConvetrsationClient 
var endpoint = Environment.GetEnvironmentVariable("AZURE_OPENAI_ENDPOINT") ??
               throw new Exception("'AZURE_OPENAI_ENDPOINT' must be set");
var key = Environment.GetEnvironmentVariable("AZURE_OPENAI_API_KEY") ??
          throw new Exception("'AZURE_OPENAI_API_KEY' must be set");
var deploymentName = Environment.GetEnvironmentVariable("AZURE_OPENAI_DEPLOYMENT") ?? "gpt-4o-realtime-preview";

var aoaiClient = new AzureOpenAIClient(new Uri(endpoint), new ApiKeyCredential(key));
var client = aoaiClient.GetRealtimeClient();

// bootstrap SearchClient
var searchEndpoint = Environment.GetEnvironmentVariable("AZURE_SEARCH_ENDPOINT") ??
                     throw new Exception("'AZURE_SEARCH_ENDPOINT' must be set");

var searchCredential = Environment.GetEnvironmentVariable("AZURE_SEARCH_API_KEY") ??
                       throw new Exception("'AZURE_SEARCH_API_KEY' must be set");
var indexName = Environment.GetEnvironmentVariable("AZURE_SEARCH_INDEX");
var indexClient = new SearchIndexClient(new Uri(searchEndpoint), new AzureKeyCredential(searchCredential));
var searchClient = indexClient.GetSearchClient(indexName);

// prepare audio input, this plays the role of mic input in this demo
var inputAudioPath = Path.Combine(Directory.GetCurrentDirectory(), "user-question.pcm");
await using var inputAudioStream = File.OpenRead(inputAudioPath);

// bootstrap voice conversation session
using var session = await client.StartConversationSessionAsync(deploymentName);
var sessionOptions = new ConversationSessionOptions()
{
    Instructions = 
        """
        You are a helpful voice-enabled customer assistant for a sports store.
        As the voice assistant, you answer questions very succinctly and friendly. 
        Only answer questions based on information available in the product search, accessible via the 'search' tool.
        Always use the 'search' tool before answering a question about products.
        When responding, produce a brief response. Do not use bullet points but refer to the products in a continuous fashion.
        If the 'search' tool does not yield any product results, respond that you are unable to answer the given question.
        """,
    Tools =
    {
        new ConversationFunctionTool("search")
        {
            Description = "Search the product catalog for product information",
            Parameters = BinaryData.FromString(
                """
                {
                  "type": "object",
                  "properties": {
                    "query": {
                      "type": "string",
                      "description": "The search query e.g. 'miami themed products'"
                    }
                  },
                  "required": ["query"]
                }
                """)
        }
    },
    InputAudioFormat = RealtimeAudioFormat.Pcm16,
    OutputAudioFormat = RealtimeAudioFormat.Pcm16,
    Temperature = 0.6f
};
await session.ConfigureConversationSessionAsync(sessionOptions);

// dispatch audio and start processing responses
await session.SendInputAudioAsync(inputAudioStream);
await Process(session);

async Task Process(RealtimeSession session)
{
    await using var outputAudioStream = File.Create("assistant-response.pcm");
    await foreach (var update in session.ReceiveUpdatesAsync())
    {
        switch (update)
        {
            // collecting audio chunks for playback
            case OutputDeltaUpdate audioDeltaUpdate when audioDeltaUpdate.AudioBytes != null:
                outputAudioStream.Write(audioDeltaUpdate.AudioBytes.ToArray());
                break;
            // collecting assistant response transcript to display in console
            case OutputDeltaUpdate outputTranscriptDeltaUpdate when outputTranscriptDeltaUpdate.AudioTranscript != null:
                Console.Write(outputTranscriptDeltaUpdate.AudioTranscript);
                break;
            // indicates assistant item streaming finished
            case OutputStreamingFinishedUpdate itemFinishedUpdate:
            {
                // if we have function call, we should invoke it and send back to the session
                if (itemFinishedUpdate.FunctionCallId is not null)
                {
                    Console.WriteLine($" -> Invoking: {itemFinishedUpdate.FunctionName}({itemFinishedUpdate.FunctionCallArguments})");
                    var functionResult = await InvokeFunction(itemFinishedUpdate.FunctionName, itemFinishedUpdate.FunctionCallArguments);
                    if (functionResult != "")
                    {
                        var functionOutputItem =
                            RealtimeItem.CreateFunctionCallOutput(callId: itemFinishedUpdate.FunctionCallId,
                                output: functionResult);
                        await session.AddItemAsync(functionOutputItem);
                    }
                }

                break;
            }
            // assistant turn ended
            case ResponseFinishedUpdate turnFinishedUpdate:
                // if we invoked a function, we skip the user turn
                if (turnFinishedUpdate.CreatedItems.Any(item => item.FunctionCallId is not null))
                {
                    Console.WriteLine($" -> Short circuit the client turn due to function invocation");
                    await session.StartResponseAsync();
                }
                else
                {
                    return;
                }

                break;
            case RealtimeErrorUpdate conversationErrorUpdate:
                Console.Error.WriteLine($"Error! {conversationErrorUpdate.Message}");
                return;
        }
    }
}

async Task<string> InvokeFunction(string functionName, string functionArguments)
{
    if (functionName == "search")
    {
        var doc = JsonDocument.Parse(functionArguments);
        var root = doc.RootElement;

        var query = root.GetProperty("query").GetString();

        var result = await InvokeSearch(query, searchClient);
        return result;
    }

    throw new Exception($"Unsupported tool '{functionName}'");
}

static async Task<string> InvokeSearch(string query, SearchClient searchClient)
{
    SearchResults<Product> response = await searchClient.SearchAsync<Product>(query, new SearchOptions
    {
        Size = 5
    });
    var results = new StringBuilder();
    var resultCount = 0;
    await foreach (var result in response.GetResultsAsync())
    {
        resultCount++;
        results.AppendLine($"Product: {result.Document.Name}, Description: {result.Document.Description}");
    }

    results.AppendLine($"Total results: {resultCount}");

    var documentation = results.ToString();
    Console.WriteLine($" -> Retrieved documentation:\n{documentation}");
    return documentation;
}

public record Product(
    [property: JsonPropertyName("description")]
    string Description,
    [property: JsonPropertyName("name")] string Name);