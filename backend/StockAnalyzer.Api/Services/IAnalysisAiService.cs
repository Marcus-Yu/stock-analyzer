using StockAnalyzer.Api.Models;

namespace StockAnalyzer.Api.Services;

public interface IAnalysisAiService
{
    bool IsConfigured { get; }
    Task<StockAnalysisResult> AnalyzeAsync(AggregatedFinancialData data);
    Task<PostMortemAiResponse?> GeneratePostMortemAsync(string originalAnalysisJson, decimal actualPrice, string timeframe, decimal lower, decimal mod, decimal higher);
}
