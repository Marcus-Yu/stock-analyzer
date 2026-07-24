using StockAnalyzer.Api.Models;

namespace StockAnalyzer.Api.Services;

public interface ILearningEngine
{
    Task ProcessLearningEvidenceAsync(CancellationToken cancellationToken);
    Task<string> GetLearningContextAsync(string sector, string marketRegime);
}
