using Microsoft.AspNetCore.Mvc;
using StockAnalyzer.Api.Models;
using StockAnalyzer.Api.Services;

namespace StockAnalyzer.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class StocksController : ControllerBase
{
    private readonly IStockAnalysisService _analysisService;
    private readonly ILogger<StocksController> _logger;

    public StocksController(IStockAnalysisService analysisService, ILogger<StocksController> logger)
    {
        _analysisService = analysisService;
        _logger = logger;
    }

    /// <summary>Analyze a single stock ticker with full AI analysis</summary>
    [HttpGet("analyze/{ticker}")]
    public async Task<ActionResult<StockAnalysisResult>> AnalyzeTicker(string ticker)
    {
        if (string.IsNullOrWhiteSpace(ticker) || ticker.Length > 10)
            return BadRequest("Invalid ticker symbol");
        try
        {
            var result = await _analysisService.AnalyzeTickerAsync(ticker);
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error analyzing ticker {Ticker}", ticker);
            return StatusCode(500, new { error = $"Failed to analyze {ticker}", message = ex.Message });
        }
    }

    /// <summary>Analyze multiple tickers in batch</summary>
    [HttpPost("batch")]
    public async Task<ActionResult<List<StockAnalysisResult>>> AnalyzeBatch([FromBody] BatchAnalysisRequest request)
    {
        if (request?.Tickers == null || request.Tickers.Count == 0) return BadRequest("At least one ticker is required");
        if (request.Tickers.Count > 20) return BadRequest("Maximum 20 tickers per batch");
        try
        {
            var results = await _analysisService.AnalyzeBatchAsync(request.Tickers);
            return Ok(results);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in batch analysis");
            return StatusCode(500, new { error = "Batch analysis failed", message = ex.Message });
        }
    }

    /// <summary>Get top 10 highest-rated stocks from cache (for Watchlist)</summary>
    [HttpGet("watchlist")]
    public async Task<ActionResult<List<StockQuoteSummary>>> GetWatchlist()
    {
        try { return Ok(await _analysisService.GetWatchlistAsync()); }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching watchlist");
            return StatusCode(500, new { error = "Failed to fetch watchlist", message = ex.Message });
        }
    }

    /// <summary>Get top 5 biggest daily movers from a pool of volatile stocks</summary>
    [HttpGet("movers")]
    public async Task<ActionResult<List<StockQuoteSummary>>> GetTopMovers()
    {
        try { return Ok(await _analysisService.GetTopMoversAsync()); }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching top movers");
            return StatusCode(500, new { error = "Failed to fetch movers", message = ex.Message });
        }
    }

    /// <summary>Get top 5 best-performing ETFs/indexes</summary>
    [HttpGet("steady")]
    public async Task<ActionResult<List<StockQuoteSummary>>> GetSteadyPicks()
    {
        try { return Ok(await _analysisService.GetSteadyPicksAsync()); }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching steady picks");
            return StatusCode(500, new { error = "Failed to fetch steady picks", message = ex.Message });
        }
    }

    /// <summary>Get stocks flagged for high-conviction signals</summary>
    [HttpGet("highlights")]
    public async Task<ActionResult<List<StockAnalysisResult>>> GetHighlights()
    {
        try { return Ok(await _analysisService.GetHighlightsAsync()); }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching highlights");
            return StatusCode(500, new { error = "Failed to fetch highlights", message = ex.Message });
        }
    }

    /// <summary>Get categorized stocks (movers + steady via quote data)</summary>
    [HttpGet("categories")]
    public async Task<ActionResult<CategorizedStocksResponse>> GetCategories()
    {
        try { return Ok(await _analysisService.GetCategorizedAsync()); }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching categories");
            return StatusCode(500, new { error = "Failed to fetch categories", message = ex.Message });
        }
    }
}
