using System.Text;
using System.Text.Json;
using StockAnalyzer.Api.Models;

namespace StockAnalyzer.Api.Services;

public class OllamaService : IOllamaService
{
    private readonly HttpClient _httpClient;
    private readonly string _model;
    private readonly ILogger<OllamaService> _logger;

    // Metric keys we extract from Finnhub for the prompt
    private static readonly string[] BenchmarkMetrics = {
        "psTTM", "psAnnual", "enterpriseValueEBITDATTM",
        "grossMarginTTM", "revenueGrowthTTMYoy",
        "peBasicExclExtraTTM", "pbAnnual",
        "roeTTM", "roaTTM", "currentRatioAnnual", "debtEquityAnnual",
        "epsGrowthTTMYoy", "operatingMarginTTM", "netProfitMarginTTM",
        "beta", "52WeekHigh", "52WeekLow", "marketCapitalization",
        "dividendYieldIndicatedAnnual"
    };

    public OllamaService(HttpClient httpClient, IConfiguration config, ILogger<OllamaService> logger)
    {
        _httpClient = httpClient;
        _httpClient.BaseAddress = new Uri(config["Ollama:BaseUrl"] ?? "http://localhost:11434");
        _httpClient.Timeout = TimeSpan.FromMinutes(5);
        _model = config["Ollama:Model"] ?? "gemma4:e2b";
        _logger = logger;
    }

    public async Task<StockAnalysisResult> AnalyzeAsync(AggregatedFinancialData data)
    {
        var prompt = BuildPrompt(data);
        _logger.LogInformation("Sending institutional analysis request to Ollama for {Ticker}", data.Ticker);

        var request = new OllamaGenerateRequest
        {
            Model = _model,
            Prompt = prompt,
            Stream = false,
            Format = GetJsonSchema()
        };

        var jsonContent = JsonSerializer.Serialize(request);
        var content = new StringContent(jsonContent, Encoding.UTF8, "application/json");

        try
        {
            var response = await _httpClient.PostAsync("/api/generate", content);
            response.EnsureSuccessStatusCode();

            var responseJson = await response.Content.ReadAsStringAsync();
            var ollamaResponse = JsonSerializer.Deserialize<OllamaGenerateResponse>(responseJson);

            if (ollamaResponse == null || string.IsNullOrEmpty(ollamaResponse.Response))
            {
                _logger.LogError("Empty response from Ollama for {Ticker}", data.Ticker);
                return CreateFallbackResult(data.Ticker);
            }

            _logger.LogInformation("Received Ollama response for {Ticker} ({EvalCount} tokens)",
                data.Ticker, ollamaResponse.EvalCount);

            var result = JsonSerializer.Deserialize<StockAnalysisResult>(ollamaResponse.Response,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            if (result == null)
            {
                _logger.LogError("Failed to parse Ollama analysis response for {Ticker}", data.Ticker);
                return CreateFallbackResult(data.Ticker);
            }

            result.Ticker = data.Ticker.ToUpper();
            result.RatingLabel = DeriveRatingLabel(result.Rating);
            result.AnalyzedAt = DateTime.UtcNow;

            // Populate price and company data from Finnhub
            result.CompanyName = data.Profile?.Name ?? data.Ticker.ToUpper();
            result.CurrentPrice = data.Quote?.Current > 0 ? data.Quote.Current : null;
            result.PriceChangePercent = data.Quote?.PercentChange != 0 ? data.Quote?.PercentChange : null;

            // Populate KeyMetrics from the raw Finnhub data
            result.KeyMetrics = ExtractKeyMetrics(data.Metrics);

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ollama analysis failed for {Ticker}", data.Ticker);
            return CreateFallbackResult(data.Ticker);
        }
    }

    // ─── Prompt Builder ──────────────────────────────────────────────

    private static string BuildPrompt(AggregatedFinancialData data)
    {
        var sb = new StringBuilder();

        // ── System Instruction ──
        sb.AppendLine("You are a ruthless, highly critical, and objective institutional equity analyst. Your goal is to evaluate a stock based strictly on the provided financial data, news, and competitor metrics, ignoring market hype. You are specifically searching for asymmetric risk/reward setups.");
        sb.AppendLine();
        sb.AppendLine("Analyze the provided stock data against the following 5 criteria:");
        sb.AppendLine();
        sb.AppendLine("1. Technical Moat & Industry Competition: Assess the durability of the company's competitive advantage. Is the technology easily replicable? Who is eating their market share?");
        sb.AppendLine("2. Potential Catalysts: Identify near-term triggers for price action (e.g., product launches, FDA approvals, earnings surprises, regulatory shifts).");
        sb.AppendLine("3. Price Asymmetry: Evaluate the valuation against the growth ceiling. Is the downside protected by cash/assets, while the upside is exponential?");
        sb.AppendLine("4. Financial Benchmarking (vs. Competitors): Compare the target stock against its 1-3 main competitors using the provided metrics: P/S (TTM and Forward), EV/EBITDA, Gross Margin, and YoY Revenue Growth. Calculate and evaluate the Value/Growth Score (P/S TTM / Revenue Growth %).");
        sb.AppendLine("5. Risk Assessment: Actively search for red flags. Focus heavily on accounting irregularities, high customer concentration, and severe competitive threats.");
        sb.AppendLine();

        // ── Scoring Methodology ──
        sb.AppendLine("SCORING METHODOLOGY (1-100):");
        sb.AppendLine("Provide a final integer rating from 0 to 100. Be harshly critical.");
        sb.AppendLine("* 90-100: A near-perfect setup. Massive moat, extreme price asymmetry, stellar financials, and near-zero existential risk. (Extremely rare).");
        sb.AppendLine("* 70-89: Strong buy. Solid fundamentals with clear catalysts and manageable risk.");
        sb.AppendLine("* 40-69: Hold/Average. Fully valued, or the risks perfectly offset the potential upside.");
        sb.AppendLine("* 20-39: High risk. Deteriorating financials, weak moat, or significant competitive threats.");
        sb.AppendLine("* 0-19: Uninvestable. Glaring accounting red flags, imminent bankruptcy risk, or total loss of market share.");
        sb.AppendLine();

        // ── Target Stock Data ──
        sb.AppendLine("DATA PAYLOAD:");
        sb.AppendLine();
        sb.AppendLine($"=== TARGET STOCK: {data.Ticker} ===");
        sb.AppendLine();

        // Financial Metrics
        sb.AppendLine("Financial Metrics:");
        if (data.Metrics.Count > 0)
        {
            foreach (var key in BenchmarkMetrics)
            {
                if (data.Metrics.TryGetValue(key, out var value) && value != null)
                {
                    sb.AppendLine($"  {key}: {value}");
                }
            }
        }
        else
        {
            sb.AppendLine("  No financial metrics available.");
        }
        sb.AppendLine();

        // Recent News
        sb.AppendLine("Recent News Headlines (Last 7 Days):");
        if (data.RecentNews.Count > 0)
        {
            foreach (var news in data.RecentNews)
            {
                var date = DateTimeOffset.FromUnixTimeSeconds(news.Datetime).ToString("yyyy-MM-dd");
                sb.AppendLine($"  [{date}] {news.Headline} (Source: {news.Source})");
                if (!string.IsNullOrEmpty(news.Summary))
                {
                    sb.AppendLine($"    Summary: {news.Summary[..Math.Min(200, news.Summary.Length)]}...");
                }
            }
        }
        else
        {
            sb.AppendLine("  No recent news available.");
        }
        sb.AppendLine();

        // Sentiment
        sb.AppendLine("Social Sentiment:");
        sb.AppendLine($"  Overall Score: {data.OverallSentimentScore:F3}");
        sb.AppendLine($"  Total Mentions: {data.TotalMentions}");
        sb.AppendLine($"  Positive Ratio: {data.PositiveSentimentRatio:P1}");
        sb.AppendLine();

        // ── Competitor Data ──
        sb.AppendLine("=== COMPETITOR DATA ===");
        if (data.Competitors.Count > 0)
        {
            foreach (var comp in data.Competitors)
            {
                sb.AppendLine($"--- {comp.Ticker} ---");
                if (comp.Metrics.Count > 0)
                {
                    foreach (var key in BenchmarkMetrics)
                    {
                        if (comp.Metrics.TryGetValue(key, out var value) && value != null)
                        {
                            sb.AppendLine($"  {key}: {value}");
                        }
                    }
                }
                else
                {
                    sb.AppendLine("  No metrics available.");
                }
                sb.AppendLine();
            }
        }
        else
        {
            sb.AppendLine("  No competitor data available. Evaluate based on general industry knowledge.");
        }
        sb.AppendLine();

        // ── Output Instructions ──
        sb.AppendLine("OUTPUT INSTRUCTIONS:");
        sb.AppendLine("You must return your analysis ONLY as a valid JSON object. Do not include markdown formatting or conversational text.");
        sb.AppendLine("Required fields:");
        sb.AppendLine("- rating (integer 0-100)");
        sb.AppendLine("- technical_moat (string: detailed analysis)");
        sb.AppendLine("- moat_label (string: 2-4 word characterization, e.g. 'Moderate and Evolving', 'Deep and Durable', 'Narrow and Eroding')");
        sb.AppendLine("- catalysts (string: detailed analysis)");
        sb.AppendLine("- catalysts_label (string: 2-4 word summary, e.g. 'Strong Near-Term', 'Limited Upside Triggers', 'Multiple Catalysts')");
        sb.AppendLine("- price_asymmetry (string: detailed analysis)");
        sb.AppendLine("- asymmetry_label (string: 2-4 word summary, e.g. 'Favorable Upside', 'Balanced Risk/Reward', 'Downside Skewed')");
        sb.AppendLine("- financial_benchmarking (string: detailed analysis)");
        sb.AppendLine("- benchmarking_label (string: 2-4 word summary, e.g. 'Premium Valuation', 'Undervalued Relative', 'Fairly Priced')");
        sb.AppendLine("- risk_assessment (string: detailed analysis)");
        sb.AppendLine("- risk_label (string: 2-4 word summary, e.g. 'Elevated Concerns', 'Manageable Risk', 'Critical Red Flags')");
        sb.AppendLine("- summary_verdict (string: 2-sentence harsh conclusion)");
        sb.AppendLine("- metric_assessments (object): For EACH key metric below, assess whether the value is 'favorable', 'unfavorable', or 'neutral' COMPARED TO the stock's industry peers and key competitors. Do NOT use absolute thresholds — compare to the sector median and the competitors provided above.");
        sb.AppendLine("  Required keys: pe_ratio, pb_ratio, ps_ttm, ev_ebitda, gross_margin, revenue_growth, debt_equity, current_ratio, roe, dividend_yield, beta, market_cap");
        sb.AppendLine("  Each value must be exactly one of: 'favorable', 'unfavorable', 'neutral'");

        return sb.ToString();
    }

    // ─── JSON Schema ─────────────────────────────────────────────────

    private static object GetJsonSchema()
    {
        return new
        {
            type = "object",
            properties = new
            {
                rating = new { type = "integer" },
                technical_moat = new { type = "string" },
                moat_label = new { type = "string" },
                catalysts = new { type = "string" },
                catalysts_label = new { type = "string" },
                price_asymmetry = new { type = "string" },
                asymmetry_label = new { type = "string" },
                financial_benchmarking = new { type = "string" },
                benchmarking_label = new { type = "string" },
                risk_assessment = new { type = "string" },
                risk_label = new { type = "string" },
                summary_verdict = new { type = "string" },
                metric_assessments = new
                {
                    type = "object",
                    properties = new
                    {
                        pe_ratio = new { type = "string" },
                        pb_ratio = new { type = "string" },
                        ps_ttm = new { type = "string" },
                        ev_ebitda = new { type = "string" },
                        gross_margin = new { type = "string" },
                        revenue_growth = new { type = "string" },
                        debt_equity = new { type = "string" },
                        current_ratio = new { type = "string" },
                        roe = new { type = "string" },
                        dividend_yield = new { type = "string" },
                        beta = new { type = "string" },
                        market_cap = new { type = "string" }
                    }
                }
            },
            required = new[] { "rating", "technical_moat", "moat_label", "catalysts", "catalysts_label", "price_asymmetry", "asymmetry_label", "financial_benchmarking", "benchmarking_label", "risk_assessment", "risk_label", "summary_verdict", "metric_assessments" }
        };
    }

    // ─── Helpers ──────────────────────────────────────────────────────

    private static string DeriveRatingLabel(int rating) => rating switch
    {
        >= 90 => "Near-Perfect",
        >= 70 => "Strong Buy",
        >= 40 => "Hold",
        >= 20 => "High Risk",
        _ => "Uninvestable"
    };

    private static KeyMetrics ExtractKeyMetrics(Dictionary<string, object?> metrics)
    {
        return new KeyMetrics
        {
            PeRatio = TryGetDoubleMultiKey(metrics, "peBasicExclExtraTTM", "peTTM", "peNormalizedAnnual"),
            PbRatio = TryGetDoubleMultiKey(metrics, "pbAnnual", "pbQuarterly"),
            PsTtm = TryGetDoubleMultiKey(metrics, "psTTM", "psAnnual"),
            EvToEbitda = TryGetDoubleMultiKey(metrics, "evEbitdaTTM", "enterpriseValueEBITDATTM"),
            GrossMargin = TryGetDoubleMultiKey(metrics, "grossMarginTTM", "grossMarginAnnual", "grossMargin5Y"),
            RevenueGrowthYoy = TryGetDoubleMultiKey(metrics, "revenueGrowthTTMYoy", "revenueGrowthQuarterlyYoy", "revenueGrowth3Y"),
            DebtToEquity = TryGetDoubleMultiKey(metrics, "totalDebt/totalEquityAnnual", "totalDebt/totalEquityQuarterly", "debtEquityAnnual"),
            CurrentRatio = TryGetDoubleMultiKey(metrics, "currentRatioAnnual", "currentRatioQuarterly"),
            RoePercent = TryGetDoubleMultiKey(metrics, "roeTTM", "roeRfy", "roeAnnual"),
            DividendYieldPercent = TryGetDoubleMultiKey(metrics, "dividendYieldIndicatedAnnual", "currentDividendYieldTTM"),
            Beta = TryGetDouble(metrics, "beta"),
            Week52High = TryGetDouble(metrics, "52WeekHigh"),
            Week52Low = TryGetDouble(metrics, "52WeekLow"),
            MarketCap = TryGetDouble(metrics, "marketCapitalization"),
        };
    }

    /// <summary>Try multiple Finnhub key aliases, return the first non-null value.</summary>
    private static double? TryGetDoubleMultiKey(Dictionary<string, object?> metrics, params string[] keys)
    {
        foreach (var key in keys)
        {
            var val = TryGetDouble(metrics, key);
            if (val.HasValue) return val;
        }
        return null;
    }

    private static double? TryGetDouble(Dictionary<string, object?> metrics, string key)
    {
        if (!metrics.TryGetValue(key, out var value) || value == null) return null;
        if (value is JsonElement je)
        {
            if (je.ValueKind == JsonValueKind.Number) return je.GetDouble();
            if (je.ValueKind == JsonValueKind.String && double.TryParse(je.GetString(), out var d)) return d;
        }
        if (double.TryParse(value.ToString(), out var result)) return result;
        return null;
    }

    private static StockAnalysisResult CreateFallbackResult(string ticker)
    {
        return new StockAnalysisResult
        {
            Ticker = ticker.ToUpper(),
            Rating = 50,
            RatingLabel = "Hold",
            TechnicalMoat = "Unable to analyze — AI engine did not respond.",
            MoatLabel = "Unknown",
            Catalysts = "Unable to analyze.",
            CatalystsLabel = "Unknown",
            PriceAsymmetry = "Unable to analyze.",
            AsymmetryLabel = "Unknown",
            FinancialBenchmarking = "Unable to analyze — no competitor comparison available.",
            BenchmarkingLabel = "Unknown",
            RiskAssessment = "Unable to analyze. The AI engine failed to produce a response. Retry recommended.",
            RiskLabel = "Unknown",
            SummaryVerdict = "Analysis could not be completed. Retry or verify that Ollama is running with the gemma4:e2b model.",
            KeyMetrics = new KeyMetrics(),
            AnalyzedAt = DateTime.UtcNow
        };
    }
}
