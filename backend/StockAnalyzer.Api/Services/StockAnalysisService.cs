using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using StockAnalyzer.Api.Data;
using StockAnalyzer.Api.Models;

namespace StockAnalyzer.Api.Services;

public class StockAnalysisService : IStockAnalysisService
{
    private readonly IFinnhubService _finnhubService;
    private readonly IAnalysisAiService _analysisAiService;
    private readonly IPredictionStorageService _predictionStorageService;
    private readonly ILearningEngine _learningEngine;
    private readonly AppDbContext _dbContext;
    private readonly ILogger<StockAnalysisService> _logger;

    // Pool of popular volatile stocks for "Big Movers"
    private static readonly string[] MoverPool = {
        "TSLA", "NVDA", "AMD", "GME", "PLTR", "COIN", "MARA", "SOFI",
        "RIVN", "NIO", "LCID", "HOOD", "SNAP", "ROKU", "DKNG",
        "MSTR", "SMCI", "ARM", "SOUN", "IONQ", "RGTI", "BBAI",
        "RBLX", "ABNB", "SHOP", "SQ", "SNOW", "META", "NFLX", "AMC"
    };

    // Pool of ETFs / indexes / funds for "Steady Picks"
    private static readonly string[] SteadyPool = {
        "SPY", "QQQ", "VOO", "VTI", "VGT", "ARKK", "XLK",
        "SCHD", "DIA", "IWM", "VUG", "VTV"
    };

    // Top 10 most popular stocks — analyzed with Gemma 4 for the Watchlist
    private static readonly string[] WatchlistPool = {
        "AAPL", "MSFT", "NVDA", "GOOG", "AMZN",
        "META", "TSLA", "NFLX", "AMD", "PLTR"
    };

    private static readonly TimeSpan CacheDuration = TimeSpan.FromHours(1);

    public StockAnalysisService(
        IFinnhubService finnhubService,
        IAnalysisAiService analysisAiService,
        IPredictionStorageService predictionStorageService,
        ILearningEngine learningEngine,
        AppDbContext dbContext,
        ILogger<StockAnalysisService> logger)
    {
        _finnhubService = finnhubService;
        _analysisAiService = analysisAiService;
        _predictionStorageService = predictionStorageService;
        _learningEngine = learningEngine;
        _dbContext = dbContext;
        _logger = logger;
    }

    // ─── AI Analysis (uses Azure OpenAI) ─────────────────────────────

    public async Task<StockAnalysisResult> AnalyzeTickerAsync(string ticker)
    {
        ticker = ticker.ToUpper().Trim();
        _logger.LogInformation("Analyzing ticker {Ticker}", ticker);

        var cached = await GetCachedAnalysisAsync(ticker);
        if (cached != null)
        {
            // Log a prediction for today even for cached results
            await _predictionStorageService.StorePredictionAsync(
                cached,
                promptVersion: "1.0",
                modelVersion: "GPT-4o (Cached)",
                learningContext: "{}",
                marketRegime: "Normal",
                dataConfidenceScore: 80
            );

            if (HasCurrentAnalysisContract(cached) || !_analysisAiService.IsConfigured)
            {
                _logger.LogInformation("Cache hit for {Ticker} — refreshing quote", ticker);
                await RefreshCachedQuoteAsync(cached);
                return cached;
            }

            _logger.LogInformation("Cache hit for {Ticker}, but cached analysis is missing the current prompt contract. Regenerating.", ticker);
        }

        if (!_analysisAiService.IsConfigured)
            throw new InvalidOperationException("Azure OpenAI is not configured. Set AZURE_OPENAI_KEY, AZURE_OPENAI_ENDPOINT, and AZURE_OPENAI_DEPLOYMENT to run new analyses.");

        _logger.LogInformation("Cache miss for {Ticker}, fetching fresh data", ticker);
        var financialData = await _finnhubService.GetFinancialDataAsync(ticker);

        // Calculate Data Confidence and Regime
        int confidence = CalculateDataConfidence(financialData);
        string regime = DetermineMarketRegime();

        // Inject Learning Context if mode is AUTO or ASSIST
        var modeSetting = await _dbContext.SystemSettings.FirstOrDefaultAsync(s => s.SettingKey == "learning_mode");
        var learningMode = modeSetting?.SettingValue ?? "AUTO";
        string learningContextJson = "Baseline";

        if (learningMode == "AUTO" || learningMode == "ASSIST")
        {
            var sector = financialData.Profile?.Industry ?? "Global";
            learningContextJson = await _learningEngine.GetLearningContextAsync(sector, regime);
            financialData.LearningContextJson = learningContextJson;
        }

        var analysis = await _analysisAiService.AnalyzeAsync(financialData);
        if (IsProviderFallback(analysis))
        {
            _logger.LogWarning("Analysis provider returned fallback for {Ticker}; skipping cache write", ticker);
            return analysis;
        }

        var category = analysis.Rating < 40 ? "HighRisk" : "LowRisk";
        await CacheAnalysisAsync(ticker, analysis, category);

        await _predictionStorageService.StorePredictionAsync(
            analysis,
            promptVersion: "1.0", // from appsettings ideally
            modelVersion: "GPT-4o", // from config
            learningContext: learningContextJson,
            marketRegime: regime,
            dataConfidenceScore: confidence
        );

        return analysis;
    }

    private int CalculateDataConfidence(AggregatedFinancialData data)
    {
        int score = 0;
        if (data.Metrics.Count > 10) score += 40;
        if (data.Quote != null && data.Quote.Current > 0) score += 20;
        if (data.Competitors.Count > 0) score += 20;
        if (data.RecentNews.Count > 0) score += 20;
        return score == 0 ? 10 : score; // Minimum confidence 10
    }

    private string DetermineMarketRegime()
    {
        // Simple placeholder. In reality, we'd fetch SPY SMA 50/200, VIX, and Fed Funds Rate.
        return "Normal";
    }

    public async Task<List<StockAnalysisResult>> AnalyzeBatchAsync(List<string> tickers)
    {
        var results = new List<StockAnalysisResult>();
        foreach (var ticker in tickers)
        {
            try { results.Add(await AnalyzeTickerAsync(ticker)); }
            catch (Exception ex) { _logger.LogError(ex, "Failed to analyze {Ticker} in batch", ticker); }
        }
        return results;
    }

    // ─── Watchlist: Gemma 4 analysis of top 10 popular stocks ────────

    public async Task<List<StockQuoteSummary>> GetWatchlistAsync()
    {
        _logger.LogInformation("Building watchlist — analyzing top 10 popular stocks with Gemma 4");

        var results = new List<StockQuoteSummary>();

        // Analyze each stock (cached results returned instantly)
        foreach (var ticker in WatchlistPool)
        {
            try
            {
                var cachedSummary = await GetCachedSummaryAsync(ticker);
                if (cachedSummary != null)
                {
                    results.Add(cachedSummary);
                    continue;
                }

                results.Add(ToQuoteSummary(await AnalyzeTickerAsync(ticker)));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to analyze watchlist ticker {Ticker}", ticker);
            }
        }

        // Sort by rating descending (highest AI scores first)
        return results.OrderByDescending(r => r.Rating ?? 0).ToList();
    }

    // ─── Big Movers: top 5 by absolute % change + cached ratings ─────

    public async Task<List<StockQuoteSummary>> GetTopMoversAsync()
    {
        _logger.LogInformation("Fetching top movers from pool of {Count} stocks", MoverPool.Length);

        // 1) Fast quote scan to find top movers
        var quotes = await _finnhubService.GetQuoteSummariesAsync(MoverPool.ToList());
        var topMovers = quotes
            .Where(q => Math.Abs(q.PriceChangePercent) >= 1.0)
            .OrderByDescending(q => Math.Abs(q.PriceChangePercent))
            .Take(5)
            .ToList();

        // 2) Run AI analysis on each to get ratings (cached after first run)
        foreach (var mover in topMovers)
        {
            try
            {
                var cached = await GetCachedAnalysisAsync(mover.Ticker);
                ApplyAnalysisToSummary(mover, cached ?? await AnalyzeTickerAsync(mover.Ticker), keepLiveQuote: cached != null);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to analyze mover {Ticker}", mover.Ticker);
            }
        }

        return topMovers;
    }

    // ─── Steady Picks: best-performing ETFs + AI analysis ────────────

    public async Task<List<StockQuoteSummary>> GetSteadyPicksAsync()
    {
        _logger.LogInformation("Fetching steady picks from pool of {Count} ETFs", SteadyPool.Length);

        // 1) Fast quote scan to find best performers
        var quotes = await _finnhubService.GetQuoteSummariesAsync(SteadyPool.ToList());
        var topSteady = quotes
            .OrderByDescending(q => q.PriceChangePercent)
            .Take(5)
            .ToList();

        // 2) Run AI analysis on each to get ratings (cached after first run)
        foreach (var pick in topSteady)
        {
            try
            {
                var cached = await GetCachedAnalysisAsync(pick.Ticker);
                ApplyAnalysisToSummary(pick, cached ?? await AnalyzeTickerAsync(pick.Ticker), keepLiveQuote: cached != null);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to analyze steady pick {Ticker}", pick.Ticker);
            }
        }

        return topSteady;
    }

    // ─── Legacy endpoints ────────────────────────────────────────────

    public async Task<List<StockAnalysisResult>> GetHighlightsAsync()
    {
        var cached = await _dbContext.CachedAnalyses
            .Where(c => c.ExpiresAt > DateTime.UtcNow && c.VolatilityScore >= 25)
            .OrderByDescending(c => c.VolatilityScore)
            .Take(10)
            .ToListAsync();

        return cached.Select(h =>
            JsonSerializer.Deserialize<StockAnalysisResult>(h.AnalysisJson,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true })!)
            .Where(r => r != null)
            .ToList();
    }

    public async Task<CategorizedStocksResponse> GetCategorizedAsync()
    {
        var movers = await GetTopMoversAsync();
        var steady = await GetSteadyPicksAsync();
        return new CategorizedStocksResponse { HighRisk = movers, LowRisk = steady };
    }

    // ─── Helpers ─────────────────────────────────────────────────────

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

    private async Task<StockAnalysisResult?> GetCachedAnalysisAsync(string ticker)
    {
        var cached = await _dbContext.CachedAnalyses
            .Where(c => c.Ticker == ticker && c.ExpiresAt > DateTime.UtcNow)
            .OrderByDescending(c => c.CreatedAt)
            .FirstOrDefaultAsync();
        if (cached == null) return null;
        var analysis = JsonSerializer.Deserialize<StockAnalysisResult>(cached.AnalysisJson,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        if (analysis != null)
            analysis.RatingLabel = DeriveRatingLabel(analysis.Rating);
        return analysis;
    }

    private static bool HasCurrentAnalysisContract(StockAnalysisResult analysis) =>
        analysis.PriceEstimates.Count >= 5
        && !string.IsNullOrWhiteSpace(analysis.MacroContext)
        && !string.IsNullOrWhiteSpace(analysis.FinalVerdict);

    private static bool IsProviderFallback(StockAnalysisResult analysis) =>
        analysis.PriceEstimates.Count == 0
        && analysis.SummaryVerdict.StartsWith("Analysis could not be completed", StringComparison.OrdinalIgnoreCase);

    private async Task RefreshCachedQuoteAsync(StockAnalysisResult analysis)
    {
        try
        {
            var (price, change) = await _finnhubService.RefreshQuoteAsync(analysis.Ticker);
            if (price > 0)
            {
                analysis.CurrentPrice = price;
                analysis.PriceChangePercent = change;
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to refresh quote for {Ticker}, using cached price", analysis.Ticker);
        }
    }

    private async Task<StockQuoteSummary?> GetCachedSummaryAsync(string ticker)
    {
        var cached = await GetCachedAnalysisAsync(ticker);
        if (cached == null) return null;

        await RefreshCachedQuoteAsync(cached);
        return ToQuoteSummary(cached);
    }

    private static StockQuoteSummary ToQuoteSummary(StockAnalysisResult analysis) => new()
    {
        Ticker = analysis.Ticker,
        CompanyName = string.IsNullOrWhiteSpace(analysis.CompanyName) ? FinnhubService.GetCompanyName(analysis.Ticker) : analysis.CompanyName,
        CurrentPrice = analysis.CurrentPrice ?? 0,
        PriceChangePercent = analysis.PriceChangePercent ?? 0,
        Rating = analysis.Rating,
        RatingLabel = analysis.RatingLabel,
        SummaryVerdict = analysis.SummaryVerdict,
    };

    private static void ApplyAnalysisToSummary(StockQuoteSummary summary, StockAnalysisResult analysis, bool keepLiveQuote)
    {
        summary.Rating = analysis.Rating;
        summary.RatingLabel = analysis.RatingLabel;
        summary.SummaryVerdict = analysis.SummaryVerdict;

        if (string.IsNullOrWhiteSpace(summary.CompanyName))
            summary.CompanyName = string.IsNullOrWhiteSpace(analysis.CompanyName) ? FinnhubService.GetCompanyName(summary.Ticker) : analysis.CompanyName;

        if (keepLiveQuote) return;

        if (analysis.CurrentPrice.HasValue && analysis.CurrentPrice > 0)
            summary.CurrentPrice = analysis.CurrentPrice.Value;
        if (analysis.PriceChangePercent.HasValue)
            summary.PriceChangePercent = analysis.PriceChangePercent.Value;
    }

    private async Task CacheAnalysisAsync(string ticker, StockAnalysisResult analysis, string category)
    {
        var signalStrength = Math.Abs(analysis.Rating - 50);
        var cached = new CachedAnalysis
        {
            Ticker = ticker,
            AnalysisJson = JsonSerializer.Serialize(analysis),
            Rating = analysis.Rating,
            VolatilityScore = signalStrength,
            Category = category,
            CreatedAt = DateTime.UtcNow,
            ExpiresAt = DateTime.UtcNow.Add(CacheDuration)
        };
        _dbContext.CachedAnalyses.Add(cached);
        await _dbContext.SaveChangesAsync();
        _logger.LogInformation("Cached analysis for {Ticker} (Rating: {Rating})", ticker, analysis.Rating);
    }
}
