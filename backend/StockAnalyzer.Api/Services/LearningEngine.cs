using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using StockAnalyzer.Api.Data;
using StockAnalyzer.Api.Models;

namespace StockAnalyzer.Api.Services;

public class LearningEngine : ILearningEngine
{
    private readonly AppDbContext _db;
    private readonly ILogger<LearningEngine> _logger;

    public LearningEngine(AppDbContext db, ILogger<LearningEngine> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task ProcessLearningEvidenceAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Starting periodic Learning Engine weight recalculation.");

        var setting = await _db.SystemSettings.FirstOrDefaultAsync(s => s.SettingKey == "max_daily_weight_adjustment_percent", cancellationToken);
        decimal maxAdjustPercent = decimal.TryParse(setting?.SettingValue, out var m) ? m : 2.0m;
        decimal maxAdjust = maxAdjustPercent / 100m;

        // Process evidence recorded in the last 24 hours
        var recentEvidence = await _db.LearningEvidence
            .Include(e => e.Target)
            .ThenInclude(t => t.Prediction)
            .Where(e => e.RecordedAt >= DateTime.UtcNow.AddDays(-1))
            .ToListAsync(cancellationToken);

        // Filter out low confidence predictions as per requirements
        var validEvidence = recentEvidence
            .Where(e => e.Target?.Prediction != null && e.Target.Prediction.DataConfidenceScore >= 50)
            .ToList();

        if (!validEvidence.Any())
        {
            _logger.LogInformation("No valid high-confidence learning evidence to process.");
            return;
        }

        var evidenceByFactor = validEvidence.GroupBy(e => e.FactorId);

        foreach (var group in evidenceByFactor)
        {
            var factorId = group.Key;
            
            // Simplify for MVP to a Global sector/regime
            var weight = await _db.LearningWeights.FirstOrDefaultAsync(w => w.FactorId == factorId && w.Sector == "Global", cancellationToken);
            if (weight == null)
            {
                weight = new LearningWeight
                {
                    FactorId = factorId,
                    Sector = "Global",
                    MarketRegime = "All",
                    Weight = 1.0m, // Base neutral weight
                    ReliabilityScore = 50,
                    UpdatedAt = DateTime.UtcNow
                };
                _db.LearningWeights.Add(weight);
            }

            decimal netImpact = group.Sum(e => e.IsPositiveEvidence ? e.WeightImpact : -e.WeightImpact);
            // Average the impact across the observations
            decimal averageImpact = netImpact / group.Count();
            
            // Apply bounding limit (e.g. 2%)
            if (averageImpact > maxAdjust) averageImpact = maxAdjust;
            if (averageImpact < -maxAdjust) averageImpact = -maxAdjust;

            if (averageImpact == 0) continue;

            decimal prevWeight = weight.Weight;
            weight.Weight += averageImpact;
            
            // Adjust Reliability Score based on positive/negative ratio
            var positiveCount = group.Count(e => e.IsPositiveEvidence);
            var ratio = (double)positiveCount / group.Count();
            if (ratio > 0.6 && weight.ReliabilityScore < 100) weight.ReliabilityScore += 1;
            if (ratio < 0.4 && weight.ReliabilityScore > 0) weight.ReliabilityScore -= 1;

            weight.UpdatedAt = DateTime.UtcNow;

            _db.LearningAdjustments.Add(new LearningAdjustment
            {
                LearningWeightId = weight.Id,
                PreviousWeight = prevWeight,
                NewWeight = weight.Weight,
                Reason = $"Periodic aggregation of {group.Count()} evidence records. Bounded impact: {averageImpact:P2}",
                AdjustedAt = DateTime.UtcNow
            });
        }

        await _db.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("Completed Learning Engine weight recalculation.");
    }

    public async Task<string> GetLearningContextAsync(string sector, string marketRegime)
    {
        var topFactors = await _db.LearningWeights
            .Include(w => w.Factor)
            .OrderByDescending(w => w.ReliabilityScore)
            .Take(5)
            .Select(w => new
            {
                factorName = w.Factor!.FactorName,
                weight = w.Weight,
                reliabilityScore = w.ReliabilityScore,
                isPredefined = w.Factor.IsPredefined
            })
            .ToListAsync();

        if (!topFactors.Any()) return "{}";

        var contextObject = new
        {
            autonomous_learning_context = new
            {
                description = "The following factors have been empirically proven by the AI Learning Engine based on historical prediction accuracy. Strongly bias your analysis around these weighted factors.",
                proven_factors = topFactors
            }
        };

        return JsonSerializer.Serialize(contextObject, new JsonSerializerOptions { WriteIndented = true });
    }
}
