using StockAnalyzer.Api.Data;
using StockAnalyzer.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace StockAnalyzer.Api.Services;

public interface IEvaluationService
{
    Task RunDailyEvaluationsAsync(CancellationToken stoppingToken);
}

public class EvaluationService : IEvaluationService
{
    private readonly AppDbContext _dbContext;
    private readonly IFinnhubService _finnhubService;
    private readonly IPostMortemEngine _postMortemEngine;
    private readonly ILogger<EvaluationService> _logger;

    public EvaluationService(
        AppDbContext dbContext,
        IFinnhubService finnhubService,
        IPostMortemEngine postMortemEngine,
        ILogger<EvaluationService> logger)
    {
        _dbContext = dbContext;
        _finnhubService = finnhubService;
        _postMortemEngine = postMortemEngine;
        _logger = logger;
    }

    public async Task RunDailyEvaluationsAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Starting daily evaluation of expired predictions.");

        var expiredTargets = await _dbContext.PredictionTargets
            .Include(t => t.Prediction)
            .Where(t => !t.IsEvaluated && t.ExpirationDate <= DateTime.UtcNow)
            .ToListAsync(stoppingToken);

        if (!expiredTargets.Any())
        {
            _logger.LogInformation("No expired targets to evaluate.");
            return;
        }

        _logger.LogInformation("Found {Count} targets to evaluate.", expiredTargets.Count);

        foreach (var target in expiredTargets)
        {
            try
            {
                if (target.Prediction == null) continue;

                var (price, _) = await _finnhubService.RefreshQuoteAsync(target.Prediction.Ticker);
                
                if (price <= 0)
                {
                    _logger.LogWarning("Could not fetch price for {Ticker} during evaluation.", target.Prediction.Ticker);
                    continue;
                }

                var evaluation = EvaluateTarget(target, (decimal)price);
                _dbContext.PredictionEvaluations.Add(evaluation);
                
                target.IsEvaluated = true;
                _dbContext.PredictionTargets.Update(target);

                await _postMortemEngine.ProcessExpiredTargetAsync(target, evaluation, target.Prediction);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to evaluate target {TargetId}", target.Id);
            }
        }

        await _dbContext.SaveChangesAsync(stoppingToken);
        _logger.LogInformation("Finished daily evaluation.");
    }

    private PredictionEvaluation EvaluateTarget(PredictionTarget target, decimal actualPrice)
    {
        int score = 0;
        string resultLabel = "Inaccurate";

        // Logic for score:
        // We compare the actual price against the ModerateEstimate.
        // And check if it fell within the Lower/Higher bounds.

        if (actualPrice >= target.LowerEstimate && actualPrice <= target.HigherEstimate)
        {
            // It hit the range. Max score 100 if exactly moderate, scales down to 80 at the bounds.
            decimal range = target.HigherEstimate - target.LowerEstimate;
            if (range == 0) range = 1; // prevent divide by zero
            
            decimal diffFromMod = Math.Abs(target.ModerateEstimate - actualPrice);
            decimal pctFromMod = diffFromMod / (range / 2m);
            
            score = 100 - (int)(20m * pctFromMod);
        }
        else
        {
            // Outside the range. Scales down quickly.
            decimal closestBound = actualPrice < target.LowerEstimate ? target.LowerEstimate : target.HigherEstimate;
            if (closestBound == 0) closestBound = 1;
            
            decimal percentMiss = Math.Abs(actualPrice - closestBound) / closestBound;
            
            // 5% miss = 75 score, 20% miss = 0
            score = Math.Max(0, 80 - (int)(percentMiss * 100m * 4m));
        }

        // Apply result label
        if (score >= 80) resultLabel = "Accurate";
        else if (score >= 50) resultLabel = "Partially Accurate";
        else resultLabel = "Inaccurate";

        return new PredictionEvaluation
        {
            Id = Guid.NewGuid(),
            PredictionTargetId = target.Id,
            ActualPrice = actualPrice,
            AccuracyScore = score,
            EvaluationResult = resultLabel,
            EvaluatedAt = DateTime.UtcNow
        };
    }
}

public class DailyEvaluationWorker : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<DailyEvaluationWorker> _logger;

    public DailyEvaluationWorker(IServiceProvider serviceProvider, ILogger<DailyEvaluationWorker> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _serviceProvider.CreateScope();
                var evaluationService = scope.ServiceProvider.GetRequiredService<IEvaluationService>();
                var learningEngine = scope.ServiceProvider.GetRequiredService<ILearningEngine>();
                
                await evaluationService.RunDailyEvaluationsAsync(stoppingToken);
                await learningEngine.ProcessLearningEvidenceAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred executing daily evaluation worker.");
            }

            // Run once per day, or shorter for testing. Let's do every 6 hours.
            await Task.Delay(TimeSpan.FromHours(6), stoppingToken);
        }
    }
}
