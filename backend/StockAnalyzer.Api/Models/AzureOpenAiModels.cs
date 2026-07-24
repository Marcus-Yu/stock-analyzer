using System.Text.Json.Serialization;

namespace StockAnalyzer.Api.Models;

public class AzureOpenAiChatRequest
{
    [JsonPropertyName("messages")]
    public List<AzureOpenAiMessage> Messages { get; set; } = new();

    [JsonPropertyName("response_format")]
    public object? ResponseFormat { get; set; }

    [JsonPropertyName("max_completion_tokens")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? MaxCompletionTokens { get; set; }
}

public class AzureOpenAiResponsesRequest
{
    [JsonPropertyName("model")]
    public string Model { get; set; } = string.Empty;

    [JsonPropertyName("input")]
    public List<AzureOpenAiMessage> Input { get; set; } = new();

    [JsonPropertyName("text")]
    public object? Text { get; set; }

    [JsonPropertyName("max_output_tokens")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? MaxOutputTokens { get; set; }
}

public class AzureOpenAiMessage
{
    [JsonPropertyName("role")]
    public string Role { get; set; } = string.Empty;

    [JsonPropertyName("content")]
    public string Content { get; set; } = string.Empty;
}

public class AzureOpenAiChatResponse
{
    [JsonPropertyName("choices")]
    public List<AzureOpenAiChoice> Choices { get; set; } = new();

    [JsonPropertyName("usage")]
    public AzureOpenAiUsage? Usage { get; set; }
}

public class AzureOpenAiResponsesResponse
{
    [JsonPropertyName("output_text")]
    public string? OutputText { get; set; }

    [JsonPropertyName("output")]
    public List<AzureOpenAiOutputItem> Output { get; set; } = new();

    [JsonPropertyName("usage")]
    public AzureOpenAiUsage? Usage { get; set; }
}

public class AzureOpenAiOutputItem
{
    [JsonPropertyName("type")]
    public string Type { get; set; } = string.Empty;

    [JsonPropertyName("content")]
    public List<AzureOpenAiOutputContent> Content { get; set; } = new();
}

public class AzureOpenAiOutputContent
{
    [JsonPropertyName("type")]
    public string Type { get; set; } = string.Empty;

    [JsonPropertyName("text")]
    public string? Text { get; set; }
}

public class AzureOpenAiChoice
{
    [JsonPropertyName("message")]
    public AzureOpenAiMessage Message { get; set; } = new();

    [JsonPropertyName("finish_reason")]
    public string? FinishReason { get; set; }
}

public class AzureOpenAiUsage
{
    [JsonPropertyName("prompt_tokens")]
    public int PromptTokens { get; set; }

    [JsonPropertyName("completion_tokens")]
    public int CompletionTokens { get; set; }

    [JsonPropertyName("total_tokens")]
    public int TotalTokens { get; set; }
}
