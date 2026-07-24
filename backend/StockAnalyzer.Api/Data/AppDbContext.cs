using Microsoft.EntityFrameworkCore;

using StockAnalyzer.Api.Models;

namespace StockAnalyzer.Api.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<CachedAnalysis> CachedAnalyses { get; set; } = null!;

    public DbSet<Prediction> Predictions { get; set; } = null!;
    public DbSet<PredictionSnapshot> PredictionSnapshots { get; set; } = null!;
    public DbSet<PredictionTarget> PredictionTargets { get; set; } = null!;
    public DbSet<PredictionEvaluation> PredictionEvaluations { get; set; } = null!;
    public DbSet<PredictionPostMortem> PredictionPostMortems { get; set; } = null!;
    public DbSet<LearningFactor> LearningFactors { get; set; } = null!;
    public DbSet<LearningEvidence> LearningEvidence { get; set; } = null!;
    public DbSet<LearningWeight> LearningWeights { get; set; } = null!;
    public DbSet<LearningAdjustment> LearningAdjustments { get; set; } = null!;
    public DbSet<SystemSetting> SystemSettings { get; set; } = null!;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<CachedAnalysis>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => new { e.Ticker, e.CreatedAt });
            entity.Property(e => e.Ticker).HasMaxLength(20).IsRequired();
            entity.Property(e => e.AnalysisJson).IsRequired();
            entity.Property(e => e.Category).HasMaxLength(20);
        });

        modelBuilder.Entity<Prediction>(entity =>
        {
            entity.HasIndex(e => e.Ticker);
            entity.HasIndex(e => e.Timestamp);
        });

        modelBuilder.Entity<PredictionTarget>(entity =>
        {
            entity.HasIndex(e => e.ExpirationDate);
            entity.HasIndex(e => e.IsEvaluated);
        });
    }
}

public class CachedAnalysis
{
    public int Id { get; set; }
    public string Ticker { get; set; } = string.Empty;
    public string AnalysisJson { get; set; } = string.Empty;
    public int Rating { get; set; }
    public int VolatilityScore { get; set; }
    public string Category { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime ExpiresAt { get; set; }
}
