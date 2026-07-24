using System.Text.Json.Serialization;

namespace StockAnalyzer.Api.Models;

public class FinnhubMetricsResponse
{
    [JsonPropertyName("metric")]
    public Dictionary<string, object?> Metric { get; set; } = new();

    [JsonPropertyName("metricType")]
    public string MetricType { get; set; } = string.Empty;

    [JsonPropertyName("symbol")]
    public string Symbol { get; set; } = string.Empty;
}

public class FinnhubQuote
{
    [JsonPropertyName("c")]
    public double Current { get; set; }

    [JsonPropertyName("d")]
    public double Change { get; set; }

    [JsonPropertyName("dp")]
    public double PercentChange { get; set; }

    [JsonPropertyName("h")]
    public double High { get; set; }

    [JsonPropertyName("l")]
    public double Low { get; set; }

    [JsonPropertyName("o")]
    public double Open { get; set; }

    [JsonPropertyName("pc")]
    public double PreviousClose { get; set; }
}

public class FinnhubProfile
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("ticker")]
    public string Ticker { get; set; } = string.Empty;

    [JsonPropertyName("logo")]
    public string Logo { get; set; } = string.Empty;

    [JsonPropertyName("finnhubIndustry")]
    public string Industry { get; set; } = string.Empty;

    [JsonPropertyName("marketCapitalization")]
    public double MarketCapitalization { get; set; }
}

public class FinnhubNewsItem
{
    [JsonPropertyName("category")]
    public string Category { get; set; } = string.Empty;

    [JsonPropertyName("datetime")]
    public long Datetime { get; set; }

    [JsonPropertyName("headline")]
    public string Headline { get; set; } = string.Empty;

    [JsonPropertyName("id")]
    public long Id { get; set; }

    [JsonPropertyName("image")]
    public string Image { get; set; } = string.Empty;

    [JsonPropertyName("related")]
    public string Related { get; set; } = string.Empty;

    [JsonPropertyName("source")]
    public string Source { get; set; } = string.Empty;

    [JsonPropertyName("summary")]
    public string Summary { get; set; } = string.Empty;

    [JsonPropertyName("url")]
    public string Url { get; set; } = string.Empty;
}

public class FinnhubSentimentResponse
{
    [JsonPropertyName("reddit")]
    public List<SentimentData> Reddit { get; set; } = new();

    [JsonPropertyName("twitter")]
    public List<SentimentData> Twitter { get; set; } = new();
}

public class SentimentData
{
    [JsonPropertyName("atTime")]
    public string AtTime { get; set; } = string.Empty;

    [JsonPropertyName("mention")]
    public int Mention { get; set; }

    [JsonPropertyName("positiveScore")]
    public double PositiveScore { get; set; }

    [JsonPropertyName("negativeScore")]
    public double NegativeScore { get; set; }

    [JsonPropertyName("positiveMention")]
    public int PositiveMention { get; set; }

    [JsonPropertyName("negativeMention")]
    public int NegativeMention { get; set; }

    [JsonPropertyName("score")]
    public double Score { get; set; }
}

public class CompetitorData
{
    public string Ticker { get; set; } = string.Empty;
    public Dictionary<string, object?> Metrics { get; set; } = new();
}

public class AggregatedFinancialData
{
    public string Ticker { get; set; } = string.Empty;
    public Dictionary<string, object?> Metrics { get; set; } = new();
    public List<FinnhubNewsItem> RecentNews { get; set; } = new();
    public double OverallSentimentScore { get; set; }
    public int TotalMentions { get; set; }
    public double PositiveSentimentRatio { get; set; }
    public List<CompetitorData> Competitors { get; set; } = new();
    public FinnhubQuote? Quote { get; set; }
    public FinnhubProfile? Profile { get; set; }
    public string? LearningContextJson { get; set; }
}
