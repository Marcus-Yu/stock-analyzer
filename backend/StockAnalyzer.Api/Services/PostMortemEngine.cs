using Microsoft.EntityFrameworkCore;
using StockAnalyzer.Api.Data;
using StockAnalyzer.Api.Models;

namespace StockAnalyzer.Api.Services;

public class PostMortemEngine : IPostMortemEngine
{
    private readonly AppDbContext _db;
    private readonly IAnalysisAiService _aiService;
    private readonly ILogger<PostMortemEngine> _logger;

    public PostMortemEngine(AppDbContext db, IAnalysisAiService aiService, ILogger<PostMortemEngine> logger)
    {
        _db = db;
        _aiService = aiService;
        _logger = logger;
    }

    public async Task ProcessExpiredTargetAsync(PredictionTarget target, PredictionEvaluation evaluation, Prediction prediction)
    {
        _logger.LogInformation("Generating post-mortem for Target {TargetId}", target.Id);

        var result = await _aiService.GeneratePostMortemAsync(
            prediction.OriginalAnalysisJson,
            evaluation.ActualPrice,
            target.Timeframe,
            target.LowerEstimate,
            target.ModerateEstimate,
            target.HigherEstimate
        );

        if (result == null)
        {
            _logger.LogWarning("Failed to generate post-mortem for Target {TargetId}", target.Id);
            return;
        }

        // Save the textual post-mortem
        var postMortem = new PredictionPostMortem
        {
            PredictionTargetId = target.Id,
            WhatWasCorrect = result.WhatWasCorrect ?? string.Empty,
            WhatWasIncorrect = result.WhatWasIncorrect ?? string.Empty,
            WhatWasMissed = result.WhatWasMissed ?? string.Empty,
            SucceededAssumptions = result.SucceededAssumptions ?? string.Empty,
            FailedAssumptions = result.FailedAssumptions ?? string.Empty,
            FutureImprovements = result.FutureImprovements ?? string.Empty,
            GeneratedAt = DateTime.UtcNow
        };
        _db.PredictionPostMortems.Add(postMortem);

        // Process factors and record evidence
        foreach (var extracted in result.ExtractedFactors)
        {
            if (string.IsNullOrWhiteSpace(extracted.FactorName)) continue;

            // Find existing factor or create a new AI-discovered factor
            var factorNameClean = extracted.FactorName.Trim();
            var factor = await _db.LearningFactors
                .FirstOrDefaultAsync(f => f.FactorName.ToLower() == factorNameClean.ToLower());

            if (factor == null)
            {
                factor = new LearningFactor
                {
                    FactorName = factorNameClean,
                    Description = "AI-discovered factor from post-mortem.",
                    IsPredefined = false
                };
                _db.LearningFactors.Add(factor);
                await _db.SaveChangesAsync(); // Save to generate ID for the evidence table
            }

            // Record Evidence
            var evidence = new LearningEvidence
            {
                PredictionTargetId = target.Id,
                FactorId = factor.Id,
                IsPositiveEvidence = extracted.IsPositiveEvidence,
                WeightImpact = extracted.WeightImpact,
                RecordedAt = DateTime.UtcNow
            };
            
            _db.LearningEvidence.Add(evidence);
        }

        await _db.SaveChangesAsync();
        _logger.LogInformation("Successfully generated post-mortem and recorded evidence for Target {TargetId}", target.Id);
    }
}
