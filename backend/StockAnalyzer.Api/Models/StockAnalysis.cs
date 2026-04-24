using System.Text.Json.Serialization;

namespace StockAnalyzer.Api.Models;

public class StockAnalysisResult
{
    public string Ticker { get; set; } = string.Empty;
    public string CompanyName { get; set; } = string.Empty;
    public double? CurrentPrice { get; set; }
    public double? PriceChangePercent { get; set; }
    public int Rating { get; set; }
    public string RatingLabel { get; set; } = string.Empty;

    [JsonPropertyName("technical_moat")]
    public string TechnicalMoat { get; set; } = string.Empty;

    [JsonPropertyName("moat_label")]
    public string MoatLabel { get; set; } = string.Empty;

    [JsonPropertyName("catalysts")]
    public string Catalysts { get; set; } = string.Empty;

    [JsonPropertyName("catalysts_label")]
    public string CatalystsLabel { get; set; } = string.Empty;

    [JsonPropertyName("price_asymmetry")]
    public string PriceAsymmetry { get; set; } = string.Empty;

    [JsonPropertyName("asymmetry_label")]
    public string AsymmetryLabel { get; set; } = string.Empty;

    [JsonPropertyName("financial_benchmarking")]
    public string FinancialBenchmarking { get; set; } = string.Empty;

    [JsonPropertyName("benchmarking_label")]
    public string BenchmarkingLabel { get; set; } = string.Empty;

    [JsonPropertyName("risk_assessment")]
    public string RiskAssessment { get; set; } = string.Empty;

    [JsonPropertyName("risk_label")]
    public string RiskLabel { get; set; } = string.Empty;

    [JsonPropertyName("summary_verdict")]
    public string SummaryVerdict { get; set; } = string.Empty;

    /// <summary>
    /// LLM-provided assessment of each key metric vs industry/peers.
    /// Keys: pe_ratio, pb_ratio, ps_ttm, ev_ebitda, gross_margin, revenue_growth,
    ///       debt_equity, current_ratio, roe, dividend_yield, beta, market_cap
    /// Values: "favorable", "unfavorable", or "neutral"
    /// </summary>
    [JsonPropertyName("metric_assessments")]
    public Dictionary<string, string> MetricAssessments { get; set; } = new();

    public KeyMetrics KeyMetrics { get; set; } = new();
    public DateTime AnalyzedAt { get; set; } = DateTime.UtcNow;
}

public class KeyMetrics
{
    public double? PeRatio { get; set; }
    public double? PbRatio { get; set; }
    public double? PsTtm { get; set; }
    public double? EvToEbitda { get; set; }
    public double? GrossMargin { get; set; }
    public double? RevenueGrowthYoy { get; set; }
    public double? DebtToEquity { get; set; }
    public double? CurrentRatio { get; set; }
    public double? RoePercent { get; set; }
    public double? DividendYieldPercent { get; set; }
    public double? Beta { get; set; }
    public double? Week52High { get; set; }
    public double? Week52Low { get; set; }
    public double? MarketCap { get; set; }
}

/// <summary>
/// Lightweight quote-only model for list displays (movers, steady picks).
/// Does not require Ollama analysis.
/// </summary>
public class StockQuoteSummary
{
    public string Ticker { get; set; } = string.Empty;
    public string CompanyName { get; set; } = string.Empty;
    public double CurrentPrice { get; set; }
    public double PriceChangePercent { get; set; }
    public int? Rating { get; set; }
    public string? RatingLabel { get; set; }
    public string? SummaryVerdict { get; set; }
}

public class CategorizedStocksResponse
{
    public List<StockQuoteSummary> HighRisk { get; set; } = new();
    public List<StockQuoteSummary> LowRisk { get; set; } = new();
}

public class BatchAnalysisRequest
{
    public List<string> Tickers { get; set; } = new();
}
