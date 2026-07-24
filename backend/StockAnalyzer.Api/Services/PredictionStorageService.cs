using System.Text.Json;
using StockAnalyzer.Api.Data;
using StockAnalyzer.Api.Models;

namespace StockAnalyzer.Api.Services;

public class PredictionStorageService : IPredictionStorageService
{
    private readonly AppDbContext _dbContext;
    private readonly ILogger<PredictionStorageService> _logger;

    public PredictionStorageService(AppDbContext dbContext, ILogger<PredictionStorageService> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    public async Task StorePredictionAsync(
        StockAnalysisResult analysis, 
        string promptVersion, 
        string modelVersion, 
        string learningContext, 
        string marketRegime, 
        int dataConfidenceScore)
    {
        var predictionId = Guid.NewGuid();
        var timestamp = DateTime.UtcNow;

        var prediction = new Prediction
        {
            Id = predictionId,
            Ticker = analysis.Ticker,
            CompanyName = analysis.CompanyName,
            Sector = "", // Sector is not currently easily accessible from StockAnalysisResult directly without Finnhub profile caching, will leave empty for now or fetch later
            Timestamp = timestamp,
            PredictionScore = analysis.Rating,
            Recommendation = analysis.RatingLabel,
            DataConfidenceScore = dataConfidenceScore,
            OriginalAnalysisJson = JsonSerializer.Serialize(analysis)
        };

        var snapshot = new PredictionSnapshot
        {
            Id = Guid.NewGuid(),
            PredictionId = predictionId,
            PromptVersion = promptVersion,
            ModelVersion = modelVersion,
            LearningContextUsed = learningContext,
            MarketRegime = marketRegime
        };

        var targets = new List<PredictionTarget>();
        foreach (var estimate in analysis.PriceEstimates)
        {
            DateTime expirationDate = CalculateExpirationDate(timestamp, estimate.Timeframe);
            
            targets.Add(new PredictionTarget
            {
                Id = Guid.NewGuid(),
                PredictionId = predictionId,
                Timeframe = estimate.Timeframe,
                ExpirationDate = expirationDate,
                LowerEstimate = (decimal)(estimate.LowerEstimate ?? 0),
                ModerateEstimate = (decimal)(estimate.ModerateEstimate ?? 0),
                HigherEstimate = (decimal)(estimate.HigherEstimate ?? 0),
                IsEvaluated = false
            });
        }

        _dbContext.Predictions.Add(prediction);
        _dbContext.PredictionSnapshots.Add(snapshot);
        _dbContext.PredictionTargets.AddRange(targets);

        await _dbContext.SaveChangesAsync();
        _logger.LogInformation("Stored prediction {PredictionId} for {Ticker} with {TargetCount} targets", predictionId, analysis.Ticker, targets.Count);
    }

    private static DateTime CalculateExpirationDate(DateTime start, string timeframe)
    {
        var tf = timeframe.ToLowerInvariant();
        if (tf.Contains("week")) return start.AddDays(7);
        if (tf.Contains("month") && tf.Contains("1")) return start.AddMonths(1);
        if (tf.Contains("month") && tf.Contains("3")) return start.AddMonths(3);
        if (tf.Contains("year") && tf.Contains("1")) return start.AddYears(1);
        if (tf.Contains("year") && tf.Contains("5")) return start.AddYears(5);
        
        // fallback
        return start.AddDays(30);
    }
}
