using StockAnalyzer.Api.Models;

namespace StockAnalyzer.Api.Services;

public interface IPostMortemEngine
{
    Task ProcessExpiredTargetAsync(PredictionTarget target, PredictionEvaluation evaluation, Prediction prediction);
}

public class PostMortemAiResponse
{
    public string WhatWasCorrect { get; set; } = string.Empty;
    public string WhatWasIncorrect { get; set; } = string.Empty;
    public string WhatWasMissed { get; set; } = string.Empty;
    public string SucceededAssumptions { get; set; } = string.Empty;
    public string FailedAssumptions { get; set; } = string.Empty;
    public string FutureImprovements { get; set; } = string.Empty;
    public List<AiExtractedFactor> ExtractedFactors { get; set; } = new();
}

public class AiExtractedFactor
{
    public string FactorName { get; set; } = string.Empty; // e.g. Valuation, Momentum, Regulatory Risk
    public bool IsPositiveEvidence { get; set; }
    public decimal WeightImpact { get; set; } // e.g. 1.0 or -1.0
}
