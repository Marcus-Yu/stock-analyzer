using System.Text;
using System.Text.Json;
using StockAnalyzer.Api.Models;

namespace StockAnalyzer.Api.Services;

public class AzureOpenAiService : IAnalysisAiService
{
    private readonly HttpClient _httpClient;
    private readonly string _apiKey;
    private readonly string _endpoint;
    private readonly string _deployment;
    private readonly string _apiVersion;
    private readonly ILogger<AzureOpenAiService> _logger;

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

    private static readonly HashSet<string> KnownEtfOrIndexTickers = new(StringComparer.OrdinalIgnoreCase)
    {
        "SPY", "QQQ", "VOO", "VTI", "VGT", "ARKK", "XLK", "SCHD", "DIA", "IWM", "VUG", "VTV",
        "IVV", "VEA", "VWO", "EFA", "EEM", "AGG", "BND", "TLT", "HYG", "LQD", "XLF", "XLE",
        "XLI", "XLV", "XLY", "XLP", "XLU", "XLB", "XLRE", "VDY.TO", "XEI.TO", "XIC.TO", "ZSP.TO"
    };

    public AzureOpenAiService(HttpClient httpClient, IConfiguration config, ILogger<AzureOpenAiService> logger)
    {
        _httpClient = httpClient;
        _httpClient.Timeout = TimeSpan.FromMinutes(5);
        _apiKey = config["AZURE_OPENAI_KEY"] ?? config["AZURE_OPENAI_API_KEY"] ?? config["AzureOpenAI:ApiKey"] ?? string.Empty;
        _endpoint = (config["AZURE_OPENAI_ENDPOINT"] ?? config["AZURE_OPENAI_API_ENDPOINT"] ?? config["AzureOpenAI:Endpoint"] ?? string.Empty).TrimEnd('/');
        _deployment = config["AZURE_OPENAI_DEPLOYMENT"] ?? config["AZURE_OPENAI_API_DEPLOYMENT"] ?? config["AzureOpenAI:Deployment"] ?? string.Empty;
        _apiVersion = config["AZURE_OPENAI_API_VERSION"] ?? config["AzureOpenAI:ApiVersion"] ?? "2024-10-21";
        _logger = logger;
    }

    public bool IsConfigured => GetConfigurationError() == null;

    public async Task<StockAnalysisResult> AnalyzeAsync(AggregatedFinancialData data)
    {
        var analysisType = IsEtfOrIndex(data) ? "ETF/Index" : "Stock";
        var configurationError = GetConfigurationError();
        if (configurationError != null)
        {
            _logger.LogError("Azure OpenAI is not configured: {ConfigurationError}", configurationError);
            var fallback = CreateFallbackResult(data.Ticker, analysisType);
            fallback.SummaryVerdict = $"Analysis could not be completed because {configurationError}.";
            fallback.FinalVerdict = "Hold. Configure Azure OpenAI before relying on this analysis.";
            return fallback;
        }

        var prompt = BuildPrompt(data);
        _logger.LogInformation("Sending institutional analysis request to Azure OpenAI deployment {Deployment} for {Ticker}", _deployment, data.Ticker);

        try
        {
            var analysisJson = IsResponsesEndpoint()
                ? await SendResponsesRequestAsync(prompt, data.Ticker)
                : await SendChatCompletionsRequestAsync(prompt, data.Ticker);

            if (string.IsNullOrWhiteSpace(analysisJson))
            {
                _logger.LogError("Empty Azure OpenAI response for {Ticker}", data.Ticker);
                return CreateFallbackResult(data.Ticker, analysisType);
            }

            var result = JsonSerializer.Deserialize<StockAnalysisResult>(analysisJson,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            if (result == null)
            {
                _logger.LogError("Failed to parse Azure OpenAI analysis response for {Ticker}", data.Ticker);
                return CreateFallbackResult(data.Ticker, analysisType);
            }

            result.Ticker = data.Ticker.ToUpper();
            result.AnalysisType = string.IsNullOrWhiteSpace(result.AnalysisType) ? analysisType : result.AnalysisType;
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
            _logger.LogError(ex, "Azure OpenAI analysis failed for {Ticker}", data.Ticker);
            return CreateFallbackResult(data.Ticker, analysisType);
        }
    }

    public async Task<PostMortemAiResponse?> GeneratePostMortemAsync(string originalAnalysisJson, decimal actualPrice, string timeframe, decimal lower, decimal mod, decimal higher)
    {
        var configurationError = GetConfigurationError();
        if (configurationError != null) return null;

        var prompt = $@"
You are a ruthlessly objective AI post-mortem evaluator.
You previously generated the following stock analysis.
The timeframe '{timeframe}' has now expired.
Your predicted price estimates were:
- Lower: {lower}
- Moderate: {mod}
- Higher: {higher}

The ACTUAL market price at expiration was: {actualPrice}

ORIGINAL ANALYSIS JSON:
{originalAnalysisJson}

Analyze why the prediction was right or wrong, extracting:
- whatWasCorrect: What parts of the original thesis held true?
- whatWasIncorrect: Where was the analysis wrong?
- whatWasMissed: What external factors or risks were entirely missed?
- succeededAssumptions: Which core assumptions succeeded?
- failedAssumptions: Which core assumptions failed?
- futureImprovements: How can future analysis of this type of setup be improved?
- extractedFactors: List the factors (e.g., 'Valuation', 'Momentum', 'Macro', 'AI-Hype', etc.) that actually drove the outcome. 
For each, indicate if it was positive evidence (isPositiveEvidence = true if the factor behaved as expected/rewarded, false if it backfired or failed) and the weightImpact (a decimal, e.g., 1.0 or -1.0).

OUTPUT INSTRUCTIONS: Return exactly and ONLY a JSON object matching the requested schema.
";

        var schema = new
        {
            type = "object",
            properties = new
            {
                whatWasCorrect = new { type = "string" },
                whatWasIncorrect = new { type = "string" },
                whatWasMissed = new { type = "string" },
                succeededAssumptions = new { type = "string" },
                failedAssumptions = new { type = "string" },
                futureImprovements = new { type = "string" },
                extractedFactors = new
                {
                    type = "array",
                    items = new
                    {
                        type = "object",
                        properties = new
                        {
                            factorName = new { type = "string" },
                            isPositiveEvidence = new { type = "boolean" },
                            weightImpact = new { type = "number" }
                        },
                        required = new[] { "factorName", "isPositiveEvidence", "weightImpact" }
                    }
                }
            },
            required = new[] { "whatWasCorrect", "whatWasIncorrect", "whatWasMissed", "succeededAssumptions", "failedAssumptions", "futureImprovements", "extractedFactors" }
        };

        var request = new AzureOpenAiChatRequest
        {
            Messages = new List<AzureOpenAiMessage>
            {
                new() { Role = "system", Content = "You are an objective AI post-mortem evaluator. Return only valid JSON." },
                new() { Role = "user", Content = prompt }
            },
            MaxCompletionTokens = 4000,
            ResponseFormat = new { type = "json_schema", json_schema = new { name = "post_mortem", strict = false, schema = schema } }
        };

        try
        {
            using var httpRequest = CreateJsonRequest(BuildChatCompletionsUri(), request);
            var response = await _httpClient.SendAsync(httpRequest);
            response.EnsureSuccessStatusCode();

            var responseJson = await response.Content.ReadAsStringAsync();
            var azureResponse = JsonSerializer.Deserialize<AzureOpenAiChatResponse>(responseJson);
            var content = azureResponse?.Choices.FirstOrDefault()?.Message.Content;

            if (string.IsNullOrWhiteSpace(content)) return null;

            return JsonSerializer.Deserialize<PostMortemAiResponse>(content, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Post-mortem generation failed.");
            return null;
        }
    }

    private async Task<string?> SendChatCompletionsRequestAsync(string prompt, string ticker)
    {
        var request = new AzureOpenAiChatRequest
        {
            Messages = new List<AzureOpenAiMessage>
            {
                new()
                {
                    Role = "system",
                    Content = "You are a ruthless institutional analyst. Return only valid JSON that matches the requested analysis contract."
                },
                new()
                {
                    Role = "user",
                    Content = prompt
                }
            },
            MaxCompletionTokens = 8000,
            ResponseFormat = new
            {
                type = "json_schema",
                json_schema = new
                {
                    name = "stock_analysis_result",
                    strict = false,
                    schema = GetJsonSchema()
                }
            }
        };

        using var httpRequest = CreateJsonRequest(BuildChatCompletionsUri(), request);
        var response = await _httpClient.SendAsync(httpRequest);
        response.EnsureSuccessStatusCode();

        var responseJson = await response.Content.ReadAsStringAsync();
        var azureResponse = JsonSerializer.Deserialize<AzureOpenAiChatResponse>(responseJson);
        var usage = azureResponse?.Usage;
        _logger.LogInformation("Received Azure OpenAI chat completions response for {Ticker} ({TotalTokens} total tokens)",
            ticker, usage?.TotalTokens ?? 0);

        return azureResponse?.Choices.FirstOrDefault()?.Message.Content;
    }

    private async Task<string?> SendResponsesRequestAsync(string prompt, string ticker)
    {
        var request = new AzureOpenAiResponsesRequest
        {
            Model = _deployment,
            Input = new List<AzureOpenAiMessage>
            {
                new()
                {
                    Role = "system",
                    Content = "You are a ruthless institutional analyst. Return only valid JSON that matches the requested analysis contract."
                },
                new()
                {
                    Role = "user",
                    Content = prompt
                }
            },
            MaxOutputTokens = 8000,
            Text = new
            {
                format = new
                {
                    type = "json_schema",
                    name = "stock_analysis_result",
                    strict = false,
                    schema = GetJsonSchema()
                }
            }
        };

        using var httpRequest = CreateJsonRequest(BuildResponsesUri(), request);
        var response = await _httpClient.SendAsync(httpRequest);
        response.EnsureSuccessStatusCode();

        var responseJson = await response.Content.ReadAsStringAsync();
        var azureResponse = JsonSerializer.Deserialize<AzureOpenAiResponsesResponse>(responseJson);
        var usage = azureResponse?.Usage;
        _logger.LogInformation("Received Azure OpenAI responses response for {Ticker} ({TotalTokens} total tokens)",
            ticker, usage?.TotalTokens ?? 0);

        return azureResponse?.OutputText
            ?? azureResponse?.Output
                .SelectMany(item => item.Content)
                .FirstOrDefault(content => !string.IsNullOrWhiteSpace(content.Text))
                ?.Text;
    }

    private HttpRequestMessage CreateJsonRequest(string uri, object request)
    {
        var jsonContent = JsonSerializer.Serialize(request);
        var content = new StringContent(jsonContent, Encoding.UTF8, "application/json");
        var httpRequest = new HttpRequestMessage(HttpMethod.Post, uri)
        {
            Content = content
        };
        httpRequest.Headers.Add("api-key", _apiKey);
        return httpRequest;
    }

    private bool IsResponsesEndpoint()
    {
        if (!Uri.TryCreate(_endpoint, UriKind.Absolute, out var uri)) return false;
        return uri.AbsolutePath.Contains("/openai/responses", StringComparison.OrdinalIgnoreCase);
    }

    private string BuildChatCompletionsUri()
    {
        var endpoint = GetResourceEndpoint();
        var deployment = Uri.EscapeDataString(_deployment);
        var apiVersion = Uri.EscapeDataString(_apiVersion);
        return $"{endpoint}/openai/deployments/{deployment}/chat/completions?api-version={apiVersion}";
    }

    private string BuildResponsesUri()
    {
        if (Uri.TryCreate(_endpoint, UriKind.Absolute, out var uri)
            && uri.AbsolutePath.Contains("/openai/responses", StringComparison.OrdinalIgnoreCase))
        {
            return string.IsNullOrWhiteSpace(uri.Query)
                ? $"{_endpoint}?api-version={Uri.EscapeDataString(_apiVersion)}"
                : _endpoint;
        }

        return $"{GetResourceEndpoint()}/openai/responses?api-version={Uri.EscapeDataString(_apiVersion)}";
    }

    private string GetResourceEndpoint()
    {
        if (!Uri.TryCreate(_endpoint, UriKind.Absolute, out var uri)) return _endpoint.TrimEnd('/');
        var openAiIndex = uri.AbsoluteUri.IndexOf("/openai/", StringComparison.OrdinalIgnoreCase);
        return openAiIndex > 0 ? uri.AbsoluteUri[..openAiIndex].TrimEnd('/') : _endpoint.TrimEnd('/');
    }

    private string? GetConfigurationError()
    {
        if (string.IsNullOrWhiteSpace(_apiKey)) return "AZURE_OPENAI_KEY or AZURE_OPENAI_API_KEY is not configured";
        if (string.IsNullOrWhiteSpace(_endpoint)) return "AZURE_OPENAI_ENDPOINT or AZURE_OPENAI_API_ENDPOINT is not configured";
        if (string.IsNullOrWhiteSpace(_deployment)) return "AZURE_OPENAI_DEPLOYMENT or AZURE_OPENAI_API_DEPLOYMENT is not configured";
        return null;
    }

    // ─── Prompt Builder ──────────────────────────────────────────────

    private static string BuildPrompt(AggregatedFinancialData data)
    {
        var sb = new StringBuilder();
        var isEtfOrIndex = IsEtfOrIndex(data);
        var analysisType = isEtfOrIndex ? "ETF / Index" : "Individual Stock";

        sb.AppendLine($"ANALYSIS TYPE: {analysisType}");
        sb.AppendLine($"ANALYSIS DATE: {DateTime.UtcNow:yyyy-MM-dd}");
        sb.AppendLine();

        if (!string.IsNullOrWhiteSpace(data.LearningContextJson) && data.LearningContextJson != "Baseline")
        {
            sb.AppendLine("=== AUTONOMOUS LEARNING CONTEXT ===");
            sb.AppendLine(data.LearningContextJson);
            sb.AppendLine("===================================");
            sb.AppendLine();
        }

        if (isEtfOrIndex)
        {
            AppendEtfPrompt(sb);
        }
        else
        {
            AppendStockPrompt(sb);
        }

        AppendDataPayload(sb, data, analysisType);
        AppendOutputInstructions(sb, isEtfOrIndex);

        return sb.ToString();
    }

    private static void AppendStockPrompt(StringBuilder sb)
    {
        sb.AppendLine("You are a ruthless, highly critical, and objective institutional equity analyst. Evaluate the stock strictly from the provided financial data, recent news, competitor metrics, industry conditions, and current microeconomic and macroeconomic trends. Ignore market hype, social media narratives, emotional optimism, and unsupported growth assumptions.");
        sb.AppendLine("You are searching for asymmetric risk/reward setups where upside potential meaningfully outweighs downside risk.");
        sb.AppendLine("Core question: How buyable is this stock right now?");
        sb.AppendLine();
        sb.AppendLine("Analyze the stock against these 5 criteria:");
        sb.AppendLine("1. Technical Moat & Industry Competition: assess whether the company has a real moat or temporary momentum; whether technology, product, brand, network, data, scale, or distribution is difficult to replicate; whether competitors are catching up; pricing power; and whether the advantage is strengthening, stable, or deteriorating. Rate High for strong durable moat, Medium for some advantage but meaningful competition or margin pressure, Low for weak moat or severe competitive threats.");
        sb.AppendLine("2. Potential Catalysts: identify near-term and long-term price triggers including product launches, earnings surprises, margin expansion, revenue acceleration, regulatory changes, interest-rate shifts, industry growth, management changes, M&A, buybacks/dividends, sentiment changes, and competitive wins/losses. Rate High for clear credible upside catalysts, Medium for uncertain timing/impact, Low for few credible catalysts or fully priced-in catalysts.");
        sb.AppendLine("3. Price Asymmetry & Valuation: evaluate whether current price offers attractive upside versus downside; whether valuation is justified by growth, margins, ROE, and moat quality; whether downside is protected by cash, assets, recurring revenue, profitability, or balance sheet strength; and whether the market is pricing unrealistic expectations. Rate High for strong upside with limited downside and reasonable valuation, Medium for fair risk/reward, Low for poor asymmetry or excessive valuation.");
        sb.AppendLine("4. Financial Benchmarking vs. Competitors: compare the target against 1-3 peers using Price-to-Earnings, Price-to-Book, Price-to-Sales TTM, EV/EBITDA, Gross Margin, Revenue Growth YoY, Debt-to-Equity, Current Ratio, Return on Equity, Dividend Yield, Beta, and Market Cap. Evaluate whether the target is cheaper, growing faster, more profitable, more financially stable, and more attractive on a risk-adjusted basis. Rate High for superior or attractively valued metrics, Medium for mixed or average metrics, Low for weak, expensive, deteriorating, or inferior metrics.");
        sb.AppendLine("5. Risk Assessment & Red Flags: actively search for reasons not to buy, including accounting irregularities, weak or declining margins, customer concentration, insider selling, stock-based compensation, dilution, debt, liquidity, regulatory risk, lawsuits, management credibility, competitive threats, cyclicality, macro sensitivity, currency/geopolitical risk, and overdependence on one product, customer, region, or trend. Rate High when risks are limited, visible, and manageable; Medium when meaningful risks do not destroy the investment case; Low for major red flags or unacceptable downside risk.");
        sb.AppendLine();
        sb.AppendLine("Required macro and industry context: incorporate current world events, industry trends, interest rates, inflation, consumer demand, business spending, credit conditions, currency movements, commodity prices, supply chains, geopolitical tensions, regulatory changes, sector rotation, market liquidity, AI, automation, energy transition, healthcare, defense, and relevant company or industry news. State whether macro is a tailwind, neutral factor, or headwind.");
        sb.AppendLine();
        AppendScoringMethodology(sb, "stock");
    }

    private static void AppendEtfPrompt(StringBuilder sb)
    {
        sb.AppendLine("You are a ruthless, highly critical, and objective institutional portfolio analyst. Evaluate the ETF or index strictly from holdings quality, valuation, diversification, cost, liquidity, risk, macro exposure, and current market conditions.");
        sb.AppendLine("Ignore marketing claims, thematic hype, recent momentum narratives, backward-looking performance, and fund sponsor branding unless supported by fundamentals.");
        sb.AppendLine("Core question: How buyable is this ETF or index right now?");
        sb.AppendLine();
        sb.AppendLine("Analyze the ETF or index against these 5 criteria:");
        sb.AppendLine("1. Portfolio Quality & Diversification: assess whether exposure is genuinely diversified or secretly concentrated in a few stocks, sectors, countries, factors, or themes. Evaluate number of holdings, top 10 holdings weight, sector/country/factor exposure, single-company concentration, quality/profitability/balance-sheet strength of holdings, earnings durability, and whether the portfolio is broad and resilient or narrow and fragile. Rate High for broad high-quality diversification with limited concentration risk, Medium for reasonable exposure with some concentration or quality concerns, Low for concentrated, speculative, or narrow theme dependence.");
        sb.AppendLine("2. Valuation & Fundamental Attractiveness: evaluate whether the ETF/index is attractively valued relative to quality and growth. Analyze Price-to-Earnings, Price-to-Book, Price-to-Sales, Revenue Growth YoY, Dividend Yield, valuation versus similar ETFs/indexes, and whether holdings are priced for realistic or unrealistic growth. Rate High for reasonable or cheap valuation relative to growth, profitability, and risk; Medium for fair value; Low for expensive, overhyped, or unrealistic growth pricing.");
        sb.AppendLine("3. Growth, Profitability & Income Quality: for equity ETFs/indexes, focus on revenue growth, margins, ROE, earnings durability, dividend quality, and quality of underlying businesses. For bond or income ETFs/indexes, focus on yield quality, credit risk, duration risk, default risk, capital preservation, and interest-rate sensitivity. Rate High for strong growth/profitability or sustainable income quality, Medium for acceptable fundamentals, Low for weak growth, poor profitability, low-quality yield, or deteriorating fundamentals.");
        sb.AppendLine("4. Cost, Liquidity & Vehicle Efficiency: evaluate expense ratio, trading costs, average daily trading volume, bid-ask spreads, AUM, tracking quality, turnover, tax efficiency, fund structure, closure risk, and leverage/derivatives. Penalize high fees, poor liquidity, wide spreads, poor tracking, excessive turnover, small AUM, closure risk, or structural inefficiency. Rate High for low-cost, liquid, tax-efficient, well-tracking exposure; Medium for usable vehicle with drawbacks; Low for expensive, illiquid, inefficient, or structurally flawed exposure.");
        sb.AppendLine("5. Risk, Macro Sensitivity & Red Flags: search for concentration risk, valuation bubbles, rate sensitivity, credit risk, currency exposure, geopolitical risk, leverage, derivatives, methodology flaws, structural decay, sector overexposure, liquidity risk, thematic hype, severe underperformance risk, and prolonged drawdown risk. Rate High when risk is well controlled and appropriate for the ETF/index role; Medium when meaningful risks are visible and manageable; Low for high drawdown probability, decay, poor risk-adjusted returns, or prolonged underperformance.");
        sb.AppendLine();
        sb.AppendLine("Analyze exactly these 10 ETF/index metrics where available: Expense Ratio, Assets Under Management, Average Daily Trading Volume, Number of Holdings, Top 10 Holdings Weight, Price-to-Earnings Ratio, Price-to-Book Ratio, Price-to-Sales Ratio, Revenue Growth YoY, and Dividend Yield.");
        sb.AppendLine("Compare the target against 1-3 relevant alternatives or benchmarks, such as a broad market benchmark, lower-cost competitor ETF, more diversified alternative, similar sector/factor/thematic ETF, bond ETF, international ETF, or competing index.");
        sb.AppendLine("Required macro and market context: incorporate interest rates, inflation, credit spreads, currency movements, commodity prices, consumer demand, corporate earnings trends, sector rotation, geopolitical risk, regulatory changes, central bank policy, recession risk, market valuation levels, liquidity, country-specific risks, and industry-specific risks. State whether macro is a tailwind, neutral factor, or headwind.");
        sb.AppendLine();
        AppendScoringMethodology(sb, "ETF / index");
    }

    private static void AppendScoringMethodology(StringBuilder sb, string assetType)
    {
        sb.AppendLine("FINAL SCORING METHODOLOGY:");
        sb.AppendLine($"Provide a final integer rating from 0 to 100 representing current buyability for this {assetType}. Be harshly critical.");
        sb.AppendLine("* 90-100: Exceptional buy/allocation. Rare, high-quality opportunity with strong fundamentals or portfolio quality, attractive valuation, clear upside, and controlled risk.");
        sb.AppendLine("* 70-89: Strong buy/allocation. Compelling risk/reward, good catalysts or portfolio role, and manageable risks.");
        sb.AppendLine("* 40-69: Hold/Average. Usable or decent, but fairly valued, mixed, too risky, lacking catalysts, or not clearly superior to alternatives.");
        sb.AppendLine("* 20-39: High risk / weak buyability. Serious concerns with valuation, fundamentals, competitiveness, diversification, liquidity, fees, or downside risk.");
        sb.AppendLine("* 0-19: Uninvestable. Severe red flags, structural flaws, poor fundamentals or holdings, extreme valuation, unacceptable risk, or high probability of capital destruction.");
        sb.AppendLine();
    }

    private static void AppendDataPayload(StringBuilder sb, AggregatedFinancialData data, string analysisType)
    {
        sb.AppendLine("DATA PAYLOAD:");
        sb.AppendLine();
        sb.AppendLine($"=== TARGET {analysisType.ToUpperInvariant()}: {data.Ticker} ===");
        sb.AppendLine($"Company/Fund Name: {data.Profile?.Name ?? data.Ticker.ToUpper()}");
        sb.AppendLine($"Industry: {data.Profile?.Industry ?? "Unknown"}");
        if (data.Quote != null)
        {
            sb.AppendLine($"Current Price/Level: {data.Quote.Current}");
            sb.AppendLine($"Previous Close: {data.Quote.PreviousClose}");
            sb.AppendLine($"Daily Change: {data.Quote.Change}");
            sb.AppendLine($"Daily Change Percent: {data.Quote.PercentChange}");
            sb.AppendLine($"Day High: {data.Quote.High}");
            sb.AppendLine($"Day Low: {data.Quote.Low}");
        }
        sb.AppendLine();

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

        sb.AppendLine("Social Sentiment:");
        sb.AppendLine($"  Overall Score: {data.OverallSentimentScore:F3}");
        sb.AppendLine($"  Total Mentions: {data.TotalMentions}");
        sb.AppendLine($"  Positive Ratio: {data.PositiveSentimentRatio:P1}");
        sb.AppendLine();

        sb.AppendLine("=== PEER / COMPETITOR DATA ===");
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
            sb.AppendLine("  No peer data available. Evaluate based on available metrics, recent news, industry conditions, and relevant general market context.");
        }
        sb.AppendLine();
    }

    private static void AppendOutputInstructions(StringBuilder sb, bool isEtfOrIndex)
    {
        sb.AppendLine("OUTPUT INSTRUCTIONS:");
        sb.AppendLine("You must return your analysis ONLY as a valid JSON object. Do not include markdown formatting or conversational text.");
        sb.AppendLine("All section labels must be exactly one of 'High', 'Medium', or 'Low'. High is always favorable/strong, Medium is mixed, and Low is unfavorable/weak. Do not use High to mean high risk.");
        sb.AppendLine("Price estimates must be numeric values only, without currency symbols, commas, percent signs, or words.");
        sb.AppendLine("Required fields:");
        sb.AppendLine("- rating (integer 0-100)");
        sb.AppendLine($"- analysis_type (string: exactly '{(isEtfOrIndex ? "ETF/Index" : "Stock")}')");

        if (isEtfOrIndex)
        {
            sb.AppendLine("- technical_moat (string: Portfolio Quality & Diversification analysis)");
            sb.AppendLine("- moat_label (string: High/Medium/Low for portfolio quality and diversification)");
            sb.AppendLine("- catalysts (string: Valuation & Fundamental Attractiveness analysis)");
            sb.AppendLine("- catalysts_label (string: High/Medium/Low for valuation attractiveness)");
            sb.AppendLine("- price_asymmetry (string: Growth, Profitability & Income Quality analysis)");
            sb.AppendLine("- asymmetry_label (string: High/Medium/Low for growth, profitability, and income quality)");
            sb.AppendLine("- financial_benchmarking (string: Cost, Liquidity & Vehicle Efficiency analysis)");
            sb.AppendLine("- benchmarking_label (string: High/Medium/Low for vehicle efficiency)");
            sb.AppendLine("- risk_assessment (string: Risk, Macro Sensitivity & Red Flags analysis)");
            sb.AppendLine("- risk_label (string: High/Medium/Low for risk control; High means risk is well controlled)");
            sb.AppendLine("- metric_assessments (object): For exactly these ETF/index metrics where data exists, assess each as 'favorable', 'unfavorable', or 'neutral': expense_ratio, assets_under_management, average_daily_volume, number_of_holdings, top_10_holdings_weight, pe_ratio, pb_ratio, ps_ttm, revenue_growth, dividend_yield");
        }
        else
        {
            sb.AppendLine("- technical_moat (string: Technical Moat & Industry Competition analysis)");
            sb.AppendLine("- moat_label (string: High/Medium/Low for technical moat)");
            sb.AppendLine("- catalysts (string: Potential Catalysts analysis)");
            sb.AppendLine("- catalysts_label (string: High/Medium/Low for catalyst strength)");
            sb.AppendLine("- price_asymmetry (string: Price Asymmetry & Valuation analysis)");
            sb.AppendLine("- asymmetry_label (string: High/Medium/Low for upside/downside asymmetry)");
            sb.AppendLine("- financial_benchmarking (string: Financial Benchmarking vs. Competitors analysis)");
            sb.AppendLine("- benchmarking_label (string: High/Medium/Low for peer-relative financial attractiveness)");
            sb.AppendLine("- risk_assessment (string: Risk Assessment & Red Flags analysis)");
            sb.AppendLine("- risk_label (string: High/Medium/Low for risk control; High means risks are limited and manageable)");
            sb.AppendLine("- metric_assessments (object): For EACH key metric, assess whether the value is 'favorable', 'unfavorable', or 'neutral' COMPARED TO the stock's industry peers and key competitors. Required keys: pe_ratio, pb_ratio, ps_ttm, ev_ebitda, gross_margin, revenue_growth, debt_equity, current_ratio, roe, dividend_yield, beta, market_cap");
        }

        sb.AppendLine("- summary_verdict (string: 2-sentence harsh conclusion)");
        sb.AppendLine("- macro_context (string: explain whether current macro/market environment is a tailwind, neutral factor, or headwind)");
        sb.AppendLine("- financial_metrics_review (string: concise review of the required metrics and whether each is bullish, neutral, or bearish)");
        sb.AppendLine("- comparative_analysis (string: compare against 1-3 relevant peers, competitors, ETFs, indexes, or benchmarks)");
        sb.AppendLine("- final_verdict (string: exactly one of 'Strong Buy', 'Medium Buy', 'Weak Buy', 'Hold', 'Weak Sell', 'Medium Sell', or 'Strong Sell', followed by one concise sentence)");
        sb.AppendLine("- price_estimates (array of exactly 5 objects with timeframes '1 Week', '1 Month', '3 Months', '1 Year', and '5 Years'. Each object requires timeframe, lower_estimate, moderate_estimate, higher_estimate, and assumptions. Estimates must be grounded in the 5 criteria, financial metrics, peer comparison, valuation, macro conditions, recent price action, news, catalysts, and downside risks.)");
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
                analysis_type = new { type = "string" },
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
                macro_context = new { type = "string" },
                financial_metrics_review = new { type = "string" },
                comparative_analysis = new { type = "string" },
                final_verdict = new { type = "string" },
                price_estimates = new
                {
                    type = "array",
                    items = new
                    {
                        type = "object",
                        properties = new
                        {
                            timeframe = new { type = "string" },
                            lower_estimate = new { type = "number" },
                            moderate_estimate = new { type = "number" },
                            higher_estimate = new { type = "number" },
                            assumptions = new { type = "string" }
                        },
                        required = new[] { "timeframe", "lower_estimate", "moderate_estimate", "higher_estimate", "assumptions" }
                    }
                },
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
                        market_cap = new { type = "string" },
                        expense_ratio = new { type = "string" },
                        assets_under_management = new { type = "string" },
                        average_daily_volume = new { type = "string" },
                        number_of_holdings = new { type = "string" },
                        top_10_holdings_weight = new { type = "string" }
                    }
                }
            },
            required = new[] { "rating", "analysis_type", "technical_moat", "moat_label", "catalysts", "catalysts_label", "price_asymmetry", "asymmetry_label", "financial_benchmarking", "benchmarking_label", "risk_assessment", "risk_label", "summary_verdict", "macro_context", "financial_metrics_review", "comparative_analysis", "final_verdict", "price_estimates", "metric_assessments" }
        };
    }

    // ─── Helpers ──────────────────────────────────────────────────────

    private static bool IsEtfOrIndex(AggregatedFinancialData data)
    {
        var ticker = data.Ticker.ToUpperInvariant();
        if (KnownEtfOrIndexTickers.Contains(ticker)) return true;

        var name = data.Profile?.Name ?? string.Empty;
        var industry = data.Profile?.Industry ?? string.Empty;
        var combined = $"{name} {industry}";
        var pattern = @"\b(ETF|Index|Fund|iShares|Vanguard|SPDR|Invesco|ARK |Schwab|Select Sector|S&P 500|Nasdaq 100|Russell 2000|Dow Jones|Exchange Traded)\b";
        return System.Text.RegularExpressions.Regex.IsMatch(combined, pattern, System.Text.RegularExpressions.RegexOptions.IgnoreCase);
    }

    private static string DeriveRatingLabel(int rating) => rating switch
    {
        >= 85 => "Strong Buy",
        >= 70 => "Medium Buy",
        >= 55 => "Weak Buy",
        >= 45 => "Hold",
        >= 30 => "Weak Sell",
        >= 15 => "Medium Sell",
        _ => "Strong Sell"
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

    private static StockAnalysisResult CreateFallbackResult(string ticker, string analysisType = "Stock")
    {
        return new StockAnalysisResult
        {
            Ticker = ticker.ToUpper(),
            AnalysisType = analysisType,
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
            SummaryVerdict = "Analysis could not be completed. Retry or verify that the Azure OpenAI GPT-5.4 deployment is configured and reachable.",
            MacroContext = "Macro context unavailable because the analysis engine did not return a response.",
            FinancialMetricsReview = "Financial metrics review unavailable.",
            ComparativeAnalysis = "Comparative analysis unavailable.",
            FinalVerdict = "Hold. Retry the analysis before making a decision.",
            KeyMetrics = new KeyMetrics(),
            AnalyzedAt = DateTime.UtcNow
        };
    }
}
