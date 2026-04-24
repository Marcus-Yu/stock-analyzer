using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using StockAnalyzer.Api.Data;
using StockAnalyzer.Api.Models;

namespace StockAnalyzer.Api.Services;

public class StockAnalysisService : IStockAnalysisService
{
    private readonly IFinnhubService _finnhubService;
    private readonly IOllamaService _ollamaService;
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
        IOllamaService ollamaService,
        AppDbContext dbContext,
        ILogger<StockAnalysisService> logger)
    {
        _finnhubService = finnhubService;
        _ollamaService = ollamaService;
        _dbContext = dbContext;
        _logger = logger;
    }

    // ─── AI Analysis (uses Ollama) ────────────────────────────────────

    public async Task<StockAnalysisResult> AnalyzeTickerAsync(string ticker)
    {
        ticker = ticker.ToUpper().Trim();
        _logger.LogInformation("Analyzing ticker {Ticker}", ticker);

        var cached = await GetCachedAnalysisAsync(ticker);
        if (cached != null)
        {
            _logger.LogInformation("Cache hit for {Ticker} — refreshing quote", ticker);
            try
            {
                var (price, change) = await _finnhubService.RefreshQuoteAsync(ticker);
                if (price > 0)
                {
                    cached.CurrentPrice = price;
                    cached.PriceChangePercent = change;
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to refresh quote for {Ticker}, using cached price", ticker);
            }
            return cached;
        }

        _logger.LogInformation("Cache miss for {Ticker}, fetching fresh data", ticker);
        var financialData = await _finnhubService.GetFinancialDataAsync(ticker);
        var analysis = await _ollamaService.AnalyzeAsync(financialData);
        var category = analysis.Rating < 40 ? "HighRisk" : "LowRisk";
        await CacheAnalysisAsync(ticker, analysis, category);
        return analysis;
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
                var analysis = await AnalyzeTickerAsync(ticker);
                results.Add(new StockQuoteSummary
                {
                    Ticker = analysis.Ticker,
                    CompanyName = analysis.CompanyName,
                    CurrentPrice = analysis.CurrentPrice ?? 0,
                    PriceChangePercent = analysis.PriceChangePercent ?? 0,
                    Rating = analysis.Rating,
                    RatingLabel = analysis.RatingLabel,
                    SummaryVerdict = analysis.SummaryVerdict,
                });
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
                var analysis = await AnalyzeTickerAsync(mover.Ticker);
                mover.Rating = analysis.Rating;
                mover.RatingLabel = analysis.RatingLabel;
                mover.SummaryVerdict = analysis.SummaryVerdict;
                // Update price from live analysis data
                if (analysis.CurrentPrice.HasValue && analysis.CurrentPrice > 0)
                    mover.CurrentPrice = analysis.CurrentPrice.Value;
                if (analysis.PriceChangePercent.HasValue)
                    mover.PriceChangePercent = analysis.PriceChangePercent.Value;
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
                var analysis = await AnalyzeTickerAsync(pick.Ticker);
                pick.Rating = analysis.Rating;
                pick.RatingLabel = analysis.RatingLabel;
                pick.SummaryVerdict = analysis.SummaryVerdict;
                if (analysis.CurrentPrice.HasValue && analysis.CurrentPrice > 0)
                    pick.CurrentPrice = analysis.CurrentPrice.Value;
                if (analysis.PriceChangePercent.HasValue)
                    pick.PriceChangePercent = analysis.PriceChangePercent.Value;
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
        >= 90 => "Near-Perfect",
        >= 70 => "Strong Buy",
        >= 40 => "Hold",
        >= 20 => "High Risk",
        _ => "Uninvestable"
    };

    private async Task<StockAnalysisResult?> GetCachedAnalysisAsync(string ticker)
    {
        var cached = await _dbContext.CachedAnalyses
            .Where(c => c.Ticker == ticker && c.ExpiresAt > DateTime.UtcNow)
            .OrderByDescending(c => c.CreatedAt)
            .FirstOrDefaultAsync();
        if (cached == null) return null;
        return JsonSerializer.Deserialize<StockAnalysisResult>(cached.AnalysisJson,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
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
