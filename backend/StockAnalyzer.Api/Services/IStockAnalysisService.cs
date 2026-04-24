using StockAnalyzer.Api.Models;

namespace StockAnalyzer.Api.Services;

public interface IStockAnalysisService
{
    Task<StockAnalysisResult> AnalyzeTickerAsync(string ticker);
    Task<List<StockAnalysisResult>> AnalyzeBatchAsync(List<string> tickers);
    Task<List<StockAnalysisResult>> GetHighlightsAsync();
    Task<CategorizedStocksResponse> GetCategorizedAsync();
    Task<List<StockQuoteSummary>> GetTopMoversAsync();
    Task<List<StockQuoteSummary>> GetSteadyPicksAsync();
    Task<List<StockQuoteSummary>> GetWatchlistAsync();
}
