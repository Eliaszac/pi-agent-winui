namespace PiAgentGui.Utilities;

/// <summary>Maps known provider identities to their brand; custom providers keep their own names.</summary>
public static class ModelProviderPresentation
{
    public static int SortPriority(string provider) => provider switch
    {
        "anthropic" => 0,
        "openai" => 1,
        "openai-codex" => 2,
        "ollama" => 3,
        var id when id.StartsWith("ollama-remote-", StringComparison.Ordinal) => 3,
        _ => 4
    };

    public static (string Name, string? Icon) Get(string provider) => provider switch
    {
        var id when id.StartsWith("ollama-remote-", StringComparison.Ordinal) => ("Ollama · " + id[14..], "ollama"),
        "anthropic" => ("Anthropic", "anthropic"),
        "openai" => ("OpenAI", "openai"),
        "openai-codex" => ("OpenAI Codex", "openai"),
        "google" => ("Google", "google"),
        "google-vertex" => ("Google Vertex AI", "google"),
        "google-gemini-cli" => ("Gemini CLI", "gemini"),
        "google-antigravity" => ("Google Antigravity", "google"),
        "github-copilot" => ("GitHub Copilot", "githubcopilot"),
        "ollama" => ("Ollama", "ollama"),
        "lmstudio" or "lm-studio" => ("LM Studio", "lmstudio"),
        "openrouter" => ("OpenRouter", "openrouter"),
        "amazon-bedrock" => ("Amazon Bedrock", "bedrock"),
        "azure-openai-responses" or "azure" => ("Azure OpenAI", "azure"),
        "mistral" => ("Mistral", "mistral"),
        "groq" => ("Groq", "groq"),
        "deepseek" => ("DeepSeek", "deepseek"),
        "xai" => ("xAI", "xai"),
        "cerebras" => ("Cerebras", "cerebras"),
        "huggingface" => ("Hugging Face", "huggingface"),
        "minimax" or "minimax-cn" => (provider == "minimax" ? "MiniMax" : "MiniMax China", "minimax"),
        "moonshotai" or "moonshotai-cn" => (provider == "moonshotai" ? "Moonshot AI" : "Moonshot AI China", "moonshot"),
        "zai" => ("Z.ai", "zai"),
        "vercel-ai-gateway" => ("Vercel AI Gateway", "vercel"),
        "opencode" => ("OpenCode", "opencode"),
        _ => (provider, null)
    };
}
