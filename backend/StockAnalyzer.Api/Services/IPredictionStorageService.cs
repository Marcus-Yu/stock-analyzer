using StockAnalyzer.Api.Models;

namespace StockAnalyzer.Api.Services;

public interface IPredictionStorageService
{
    Task StorePredictionAsync(StockAnalysisResult analysis, string promptVersion, string modelVersion, string learningContext, string marketRegime, int dataConfidenceScore);
}
