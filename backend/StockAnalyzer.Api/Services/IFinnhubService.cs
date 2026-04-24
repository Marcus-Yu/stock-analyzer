using StockAnalyzer.Api.Models;

namespace StockAnalyzer.Api.Services;

public interface IFinnhubService
{
    Task<AggregatedFinancialData> GetFinancialDataAsync(string ticker);
    Task<List<StockQuoteSummary>> GetQuoteSummariesAsync(List<string> tickers);
    Task<(double price, double changePercent)> RefreshQuoteAsync(string ticker);
}
