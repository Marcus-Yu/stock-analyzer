using StockAnalyzer.Api.Models;

namespace StockAnalyzer.Api.Services;

public interface IOllamaService
{
    Task<StockAnalysisResult> AnalyzeAsync(AggregatedFinancialData data);
}
