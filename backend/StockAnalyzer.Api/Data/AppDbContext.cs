using Microsoft.EntityFrameworkCore;

namespace StockAnalyzer.Api.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<CachedAnalysis> CachedAnalyses { get; set; } = null!;

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
