# Advanced RAG Patterns for AI Applications

Session demos for .NET DAY SWITZERLAND 2025 - "Advanced RAG patterns for AI Applications" presentation.

## Session Information

**Speaker**: Filip W  
**Event**: .NET DAY SWITZERLAND 2025  
**Date**: August 26, 2025  
**Session URL**: https://dotnetday.ch/speakers/filip-w.html

## Demos

### 1. HyDE (Hypothetical Document Embeddings) - `/hyde`
Demonstrates the HyDE pattern which improves retrieval accuracy by generating hypothetical documents that would answer a query, then using those documents for similarity search instead of direct query-to-document matching.

**Key Features:**
- Generates hypothetical documents from queries using LLM
- Performs document-to-document similarity search
- Reduces semantic gap between queries and documents

### 2. HyPE (Hypothetical Prompt Embeddings) - `/hype`
Showcases the HyPE pattern which pre-generates hypothetical questions for document chunks during indexing, enabling question-to-question similarity matching at query time.

**Key Features:**
- Pre-generates hypothetical questions for each document chunk
- Performs question-to-question similarity matching
- Bridges query-document style gaps more effectively

### 3. Real-time Voice RAG - `/rag-voice`
Demonstrates voice-enabled RAG using OpenAI's Realtime API with function calling to search a product catalog using local vector search.

**Key Features:**
- Voice input/output using OpenAI Realtime API
- Function calling for local RAG search integration
- Real-time audio processing

## Prerequisites

- .NET 9.0 SDK
- Azure OpenAI access

## Environment Configuration

Create a `.env` file in the root directory with the following variables:

```env
AZURE_OPENAI_ENDPOINT=your_azure_openai_endpoint
AZURE_OPENAI_API_KEY=your_azure_openai_api_key
AZURE_OPENAI_DEPLOYMENT_NAME=gpt-4o-mini
AZURE_OPENAI_EMBEDDING_DEPLOYMENT_NAME=text-embedding-ada-002
```

## Running the Demos

### HyDE Demo
```bash
cd hyde/Demo
dotnet run
```

### HyPE Demo
```bash
cd hype/Demo
dotnet run
```

### Voice RAG Demo

On Mac:

```bash
cd rag-voice
chmod +x run.sh
./run.sh
```

on Windows:

```bash
cd rag-voice
dotnet run
```

Note that on Windows the audio playback is not done automatically - you can play the input `user-question.pcm` and `assistant-response.pcm` files using any PCM audio player.

## Shared Data

The `/shared-data` directory contains quantum computing project information used by both HyDE and HyPE demos for consistent comparison between the two approaches.

## License

MIT