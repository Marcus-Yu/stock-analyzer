using System.Text.Json;
using StockAnalyzer.Api.Models;

namespace StockAnalyzer.Api.Services;

public class FinnhubService : IFinnhubService
{
    private readonly HttpClient _httpClient;
    private readonly string _apiKey;
    private readonly ILogger<FinnhubService> _logger;
    private static readonly SemaphoreSlim _rateLimiter = new(30, 30);

    // Static ticker → company name mapping for fast list views (avoids profile API calls)
    private static readonly Dictionary<string, string> TickerNames = new(StringComparer.OrdinalIgnoreCase)
    {
        // Tech / Growth
        { "TSLA", "Tesla" }, { "NVDA", "NVIDIA" }, { "AMD", "AMD" }, { "AAPL", "Apple" },
        { "MSFT", "Microsoft" }, { "GOOG", "Alphabet" }, { "GOOGL", "Alphabet" }, { "AMZN", "Amazon" },
        { "META", "Meta Platforms" }, { "NFLX", "Netflix" }, { "CRM", "Salesforce" },
        { "SHOP", "Shopify" }, { "SQ", "Block Inc" }, { "SNOW", "Snowflake" },
        { "PLTR", "Palantir" }, { "ARM", "Arm Holdings" }, { "SMCI", "Super Micro" },
        // Volatile / Meme
        { "GME", "GameStop" }, { "AMC", "AMC Entertainment" }, { "MARA", "Marathon Digital" },
        { "COIN", "Coinbase" }, { "SOFI", "SoFi Technologies" }, { "RIVN", "Rivian" },
        { "NIO", "NIO Inc" }, { "LCID", "Lucid Group" }, { "HOOD", "Robinhood" },
        { "SNAP", "Snap Inc" }, { "ROKU", "Roku" }, { "DKNG", "DraftKings" },
        { "MSTR", "MicroStrategy" }, { "IONQ", "IonQ" }, { "RGTI", "Rigetti Computing" },
        { "SOUN", "SoundHound AI" }, { "BBAI", "BigBear.ai" }, { "RBLX", "Roblox" },
        { "ABNB", "Airbnb" },
        // ETFs / Indexes / Funds
        { "SPY", "S&P 500 ETF" }, { "QQQ", "Nasdaq 100 ETF" }, { "VOO", "Vanguard S&P 500" },
        { "VTI", "Vanguard Total Market" }, { "VGT", "Vanguard Info Tech" },
        { "ARKK", "ARK Innovation" }, { "XLK", "Technology Select" },
        { "SCHD", "Schwab US Dividend" }, { "DIA", "Dow Jones ETF" }, { "IWM", "Russell 2000 ETF" },
        { "VUG", "Vanguard Growth" }, { "VTV", "Vanguard Value" },
        { "VDY.TO", "Vanguard FTSE CDN Div" }, { "XEI.TO", "iShares Core Equity" },
        { "XIC.TO", "iShares Core S&P/TSX" }, { "ZSP.TO", "BMO S&P 500" },
        // Defensive / Value
        { "PG", "Procter & Gamble" }, { "KO", "Coca-Cola" }, { "JNJ", "Johnson & Johnson" },
        { "PEP", "PepsiCo" }, { "WMT", "Walmart" }, { "COST", "Costco" },
        { "MCD", "McDonald's" }, { "UNH", "UnitedHealth" },
    };

    public FinnhubService(HttpClient httpClient, IConfiguration config, ILogger<FinnhubService> logger)
    {
        _httpClient = httpClient;
        _apiKey = config["Finnhub:ApiKey"] ?? throw new ArgumentException("Finnhub:ApiKey not configured");
        _logger = logger;
        _httpClient.BaseAddress = new Uri("https://finnhub.io/api/v1/");
        _httpClient.DefaultRequestHeaders.Add("X-Finnhub-Token", _apiKey);
    }

    // ─── Lightweight batch quote fetch for list displays ──────────────

    public async Task<List<StockQuoteSummary>> GetQuoteSummariesAsync(List<string> tickers)
    {
        var results = new List<StockQuoteSummary>();

        // Fetch quotes in batches to respect rate limits
        foreach (var ticker in tickers)
        {
            try
            {
                var quote = await GetQuoteAsync(ticker);
                if (quote.Current <= 0) continue; // Skip tickers with no data

                results.Add(new StockQuoteSummary
                {
                    Ticker = ticker.ToUpper(),
                    CompanyName = TickerNames.GetValueOrDefault(ticker.ToUpper(), ticker.ToUpper()),
                    CurrentPrice = Math.Round(quote.Current, 2),
                    PriceChangePercent = Math.Round(quote.PercentChange, 2),
                });
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to fetch quote for {Ticker}, skipping", ticker);
            }
        }

        return results;
    }

    // ─── Lightweight quote refresh (for updating cached results) ─────

    public async Task<(double price, double changePercent)> RefreshQuoteAsync(string ticker)
    {
        var quote = await GetQuoteAsync(ticker);
        return (Math.Round(quote.Current, 2), Math.Round(quote.PercentChange, 2));
    }

    // ─── Full data fetch for AI analysis ──────────────────────────────

    public async Task<AggregatedFinancialData> GetFinancialDataAsync(string ticker)
    {
        var result = new AggregatedFinancialData { Ticker = ticker.ToUpper() };

        var metricsTask = GetMetricsAsync(ticker);
        var newsTask = GetNewsAsync(ticker);
        var sentimentTask = GetSentimentAsync(ticker);
        var peersTask = GetPeersAsync(ticker);
        var quoteTask = GetQuoteAsync(ticker);
        var profileTask = GetProfileAsync(ticker);

        await Task.WhenAll(metricsTask, newsTask, sentimentTask, peersTask, quoteTask, profileTask);

        result.Metrics = (await metricsTask).Metric;
        result.RecentNews = await newsTask;
        result.Quote = await quoteTask;
        result.Profile = await profileTask;

        var sentiment = await sentimentTask;
        AggregateSentiment(result, sentiment);

        var peers = await peersTask;
        foreach (var peer in peers.Take(3))
        {
            try
            {
                var peerMetrics = await GetMetricsAsync(peer);
                result.Competitors.Add(new CompetitorData { Ticker = peer, Metrics = peerMetrics.Metric });
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to fetch competitor metrics for {Peer}", peer);
            }
        }

        return result;
    }

    // ─── Individual API calls ─────────────────────────────────────────

    public static string GetCompanyName(string ticker) =>
        TickerNames.GetValueOrDefault(ticker.ToUpper(), ticker.ToUpper());

    private async Task<FinnhubQuote> GetQuoteAsync(string ticker)
    {
        await _rateLimiter.WaitAsync();
        try
        {
            var response = await _httpClient.GetAsync($"quote?symbol={ticker}");
            response.EnsureSuccessStatusCode();
            var json = await response.Content.ReadAsStringAsync();
            return JsonSerializer.Deserialize<FinnhubQuote>(json) ?? new FinnhubQuote();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to fetch quote for {Ticker}", ticker);
            return new FinnhubQuote();
        }
        finally { _ = Task.Delay(1000).ContinueWith(_ => _rateLimiter.Release()); }
    }

    private async Task<FinnhubProfile> GetProfileAsync(string ticker)
    {
        await _rateLimiter.WaitAsync();
        try
        {
            var response = await _httpClient.GetAsync($"stock/profile2?symbol={ticker}");
            response.EnsureSuccessStatusCode();
            var json = await response.Content.ReadAsStringAsync();
            return JsonSerializer.Deserialize<FinnhubProfile>(json) ?? new FinnhubProfile();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to fetch profile for {Ticker}", ticker);
            return new FinnhubProfile();
        }
        finally { _ = Task.Delay(1000).ContinueWith(_ => _rateLimiter.Release()); }
    }

    private async Task<FinnhubMetricsResponse> GetMetricsAsync(string ticker)
    {
        await _rateLimiter.WaitAsync();
        try
        {
            var response = await _httpClient.GetAsync($"stock/metric?symbol={ticker}&metric=all");
            response.EnsureSuccessStatusCode();
            var json = await response.Content.ReadAsStringAsync();
            return JsonSerializer.Deserialize<FinnhubMetricsResponse>(json) ?? new FinnhubMetricsResponse();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to fetch metrics for {Ticker}", ticker);
            return new FinnhubMetricsResponse();
        }
        finally { _ = Task.Delay(1000).ContinueWith(_ => _rateLimiter.Release()); }
    }

    private async Task<List<FinnhubNewsItem>> GetNewsAsync(string ticker)
    {
        await _rateLimiter.WaitAsync();
        try
        {
            var to = DateTime.UtcNow.ToString("yyyy-MM-dd");
            var from = DateTime.UtcNow.AddDays(-7).ToString("yyyy-MM-dd");
            var response = await _httpClient.GetAsync($"company-news?symbol={ticker}&from={from}&to={to}");
            response.EnsureSuccessStatusCode();
            var json = await response.Content.ReadAsStringAsync();
            var news = JsonSerializer.Deserialize<List<FinnhubNewsItem>>(json) ?? new List<FinnhubNewsItem>();
            return news.Take(10).ToList();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to fetch news for {Ticker}", ticker);
            return new List<FinnhubNewsItem>();
        }
        finally { _ = Task.Delay(1000).ContinueWith(_ => _rateLimiter.Release()); }
    }

    private async Task<FinnhubSentimentResponse> GetSentimentAsync(string ticker)
    {
        await _rateLimiter.WaitAsync();
        try
        {
            var response = await _httpClient.GetAsync($"stock/social-sentiment?symbol={ticker}&from={DateTime.UtcNow.AddDays(-7):yyyy-MM-dd}&to={DateTime.UtcNow:yyyy-MM-dd}");
            response.EnsureSuccessStatusCode();
            var json = await response.Content.ReadAsStringAsync();
            return JsonSerializer.Deserialize<FinnhubSentimentResponse>(json) ?? new FinnhubSentimentResponse();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to fetch sentiment for {Ticker}", ticker);
            return new FinnhubSentimentResponse();
        }
        finally { _ = Task.Delay(1000).ContinueWith(_ => _rateLimiter.Release()); }
    }

    private async Task<List<string>> GetPeersAsync(string ticker)
    {
        await _rateLimiter.WaitAsync();
        try
        {
            var response = await _httpClient.GetAsync($"stock/peers?symbol={ticker}");
            response.EnsureSuccessStatusCode();
            var json = await response.Content.ReadAsStringAsync();
            var peers = JsonSerializer.Deserialize<List<string>>(json) ?? new List<string>();
            return peers.Where(p => !string.IsNullOrWhiteSpace(p) && !p.Equals(ticker, StringComparison.OrdinalIgnoreCase)).Take(5).ToList();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to fetch peers for {Ticker}", ticker);
            return new List<string>();
        }
        finally { _ = Task.Delay(1000).ContinueWith(_ => _rateLimiter.Release()); }
    }

    private static void AggregateSentiment(AggregatedFinancialData result, FinnhubSentimentResponse sentiment)
    {
        var allSentiment = sentiment.Reddit.Concat(sentiment.Twitter).ToList();
        if (allSentiment.Count == 0) { result.OverallSentimentScore = 0; result.TotalMentions = 0; result.PositiveSentimentRatio = 0.5; return; }
        result.TotalMentions = allSentiment.Sum(s => s.Mention);
        var totalPositive = allSentiment.Sum(s => s.PositiveMention);
        var totalNegative = allSentiment.Sum(s => s.NegativeMention);
        var total = totalPositive + totalNegative;
        result.PositiveSentimentRatio = total > 0 ? (double)totalPositive / total : 0.5;
        result.OverallSentimentScore = allSentiment.Average(s => s.Score);
    }
}
