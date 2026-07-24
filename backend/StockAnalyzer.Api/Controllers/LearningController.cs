using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using StockAnalyzer.Api.Data;

namespace StockAnalyzer.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class LearningController : ControllerBase
{
    private readonly AppDbContext _dbContext;

    public LearningController(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    [HttpGet("insights")]
    public async Task<IActionResult> GetInsights()
    {
        var activeWeights = await _dbContext.LearningWeights
            .Include(w => w.Factor)
            .OrderByDescending(w => w.ReliabilityScore)
            .Select(w => new
            {
                w.Id,
                w.FactorId,
                FactorName = w.Factor!.FactorName,
                IsPredefined = w.Factor.IsPredefined,
                w.Sector,
                w.MarketRegime,
                w.Weight,
                w.ReliabilityScore,
                w.UpdatedAt
            })
            .ToListAsync();

        var recentAdjustments = await _dbContext.LearningAdjustments
            .Include(a => a.Weight)
                .ThenInclude(w => w.Factor)
            .OrderByDescending(a => a.AdjustedAt)
            .Take(50)
            .Select(a => new
            {
                a.Id,
                FactorName = a.Weight!.Factor!.FactorName,
                a.PreviousWeight,
                a.NewWeight,
                a.Reason,
                a.AdjustedAt
            })
            .ToListAsync();
            
        var postMortems = await _dbContext.PredictionPostMortems
            .Include(p => p.Target)
                .ThenInclude(t => t.Prediction)
            .OrderByDescending(p => p.GeneratedAt)
            .Take(10)
            .Select(p => new
            {
                p.Id,
                Ticker = p.Target != null && p.Target.Prediction != null ? p.Target.Prediction.Ticker : "Unknown",
                Timeframe = p.Target != null ? p.Target.Timeframe : "Unknown",
                p.WhatWasCorrect,
                p.WhatWasIncorrect,
                p.WhatWasMissed,
                p.GeneratedAt
            })
            .ToListAsync();

        return Ok(new
        {
            ActiveWeights = activeWeights,
            RecentAdjustments = recentAdjustments,
            RecentPostMortems = postMortems
        });
    }
}
