using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using StockAnalyzer.Api.Data;
using StockAnalyzer.Api.Models;

namespace StockAnalyzer.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class PredictionsController : ControllerBase
{
    private readonly AppDbContext _dbContext;

    public PredictionsController(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    [HttpGet("dashboard")]
    public async Task<IActionResult> GetDashboard()
    {
        var totalPredictions = await _dbContext.Predictions.CountAsync();
        
        var evaluatedTargets = await _dbContext.PredictionEvaluations.Include(e => e.Target).ToListAsync();
        int evaluatedCount = evaluatedTargets.Count;

        int accurateCount = evaluatedTargets.Count(e => e.EvaluationResult == "Accurate");
        int partialCount = evaluatedTargets.Count(e => e.EvaluationResult == "Partially Accurate");
        int inaccurateCount = evaluatedTargets.Count(e => e.EvaluationResult == "Inaccurate");

        // Simple accuracy logic
        var dashboard = new
        {
            TotalPredictions = totalPredictions,
            EvaluatedCount = evaluatedCount,
            OverallAccuracyPercent = evaluatedCount > 0 ? Math.Round((double)accurateCount / evaluatedCount * 100, 2) : 0,
            AccurateCount = accurateCount,
            PartialCount = partialCount,
            InaccurateCount = inaccurateCount,
            HorizonAccuracy = new Dictionary<string, object>
            {
                { "1 Week", GetHorizonAccuracy(evaluatedTargets, "1 Week") },
                { "1 Month", GetHorizonAccuracy(evaluatedTargets, "1 Month") },
                { "3 Months", GetHorizonAccuracy(evaluatedTargets, "3 Months") },
                { "1 Year", GetHorizonAccuracy(evaluatedTargets, "1 Year") },
                { "5 Years", GetHorizonAccuracy(evaluatedTargets, "5 Years") }
            }
        };

        return Ok(dashboard);
    }

    private object GetHorizonAccuracy(List<PredictionEvaluation> targets, string timeframe)
    {
        var subset = targets.Where(t => t.Target != null && t.Target.Timeframe == timeframe).ToList();
        if (!subset.Any()) return new { Count = 0, AccuracyPercent = 0 };

        int accurate = subset.Count(t => t.EvaluationResult == "Accurate");
        return new
        {
            Count = subset.Count,
            AccuracyPercent = Math.Round((double)accurate / subset.Count * 100, 2)
        };
    }

    [HttpGet]
    public async Task<IActionResult> GetPredictions([FromQuery] int page = 1, [FromQuery] int pageSize = 50)
    {
        var rawPredictions = await _dbContext.Predictions
            .OrderByDescending(p => p.Timestamp)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(p => new
            {
                p.Id,
                p.Ticker,
                p.CompanyName,
                p.Timestamp,
                p.PredictionScore,
                p.Recommendation,
                p.DataConfidenceScore,
                p.OriginalAnalysisJson
            })
            .ToListAsync();

        // Parse analysis_type in memory — EF Core cannot reliably translate JSON string matching to SQL
        var predictions = rawPredictions.Select(p =>
        {
            string analysisType = "Stock";
            try
            {
                using var doc = System.Text.Json.JsonDocument.Parse(p.OriginalAnalysisJson);
                if (doc.RootElement.TryGetProperty("analysis_type", out var at))
                    analysisType = at.GetString() ?? "Stock";
            }
            catch { /* malformed JSON — default to Stock */ }

            bool isEtf = analysisType.Contains("ETF", StringComparison.OrdinalIgnoreCase)
                      || analysisType.Contains("Index", StringComparison.OrdinalIgnoreCase)
                      || analysisType.Contains("Fund", StringComparison.OrdinalIgnoreCase);

            return new
            {
                p.Id,
                p.Ticker,
                p.CompanyName,
                p.Timestamp,
                p.PredictionScore,
                p.Recommendation,
                p.DataConfidenceScore,
                IsEtf = isEtf
            };
        }).ToList();

        return Ok(predictions);
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetPrediction(Guid id)
    {
        var prediction = await _dbContext.Predictions
            .Include(p => p.Snapshot)
            .Include(p => p.Targets)
                .ThenInclude(t => t.Evaluation)
            .Include(p => p.Targets)
                .ThenInclude(t => t.PostMortem)
            .FirstOrDefaultAsync(p => p.Id == id);

        if (prediction == null) return NotFound();

        return Ok(prediction);
    }
}
