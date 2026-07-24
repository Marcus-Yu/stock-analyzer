using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;

namespace StockAnalyzer.Api.Models;

public class Prediction
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required]
    [MaxLength(20)]
    public string Ticker { get; set; } = string.Empty;

    [MaxLength(200)]
    public string CompanyName { get; set; } = string.Empty;

    [MaxLength(100)]
    public string Sector { get; set; } = string.Empty;

    public DateTime Timestamp { get; set; } = DateTime.UtcNow;

    public int PredictionScore { get; set; }

    [MaxLength(50)]
    public string Recommendation { get; set; } = string.Empty;

    public int DataConfidenceScore { get; set; } // 0-100, driven by available fundamental data completeness/quality

    [Required]
    public string OriginalAnalysisJson { get; set; } = string.Empty;

    // Navigation properties
    public PredictionSnapshot? Snapshot { get; set; }
    public ICollection<PredictionTarget> Targets { get; set; } = new List<PredictionTarget>();
}

public class PredictionSnapshot
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid PredictionId { get; set; }

    [MaxLength(50)]
    public string PromptVersion { get; set; } = string.Empty;

    [MaxLength(50)]
    public string ModelVersion { get; set; } = string.Empty;

    public string LearningContextUsed { get; set; } = string.Empty;

    [MaxLength(50)]
    public string MarketRegime { get; set; } = string.Empty;

    // Navigation property
    [ForeignKey("PredictionId")]
    [JsonIgnore]
    public Prediction? Prediction { get; set; }
}

public class PredictionTarget
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid PredictionId { get; set; }

    [MaxLength(50)]
    public string Timeframe { get; set; } = string.Empty;

    public DateTime ExpirationDate { get; set; }

    public decimal LowerEstimate { get; set; }
    public decimal ModerateEstimate { get; set; }
    public decimal HigherEstimate { get; set; }

    public bool IsEvaluated { get; set; } = false;

    // Navigation properties
    [ForeignKey("PredictionId")]
    [JsonIgnore]
    public Prediction? Prediction { get; set; }

    public PredictionEvaluation? Evaluation { get; set; }
    public PredictionPostMortem? PostMortem { get; set; }
    public ICollection<LearningEvidence> Evidence { get; set; } = new List<LearningEvidence>();
}

public class PredictionEvaluation
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid PredictionTargetId { get; set; }

    public decimal ActualPrice { get; set; }

    public int AccuracyScore { get; set; } // 0-100

    [MaxLength(50)]
    public string EvaluationResult { get; set; } = string.Empty; // Accurate, Partially Accurate, Inaccurate

    public DateTime EvaluatedAt { get; set; } = DateTime.UtcNow;

    // Navigation property
    [ForeignKey("PredictionTargetId")]
    [JsonIgnore]
    public PredictionTarget? Target { get; set; }
}

public class PredictionPostMortem
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid PredictionTargetId { get; set; }

    public string WhatWasCorrect { get; set; } = string.Empty;
    public string WhatWasIncorrect { get; set; } = string.Empty;
    public string WhatWasMissed { get; set; } = string.Empty;
    public string SucceededAssumptions { get; set; } = string.Empty;
    public string FailedAssumptions { get; set; } = string.Empty;
    public string FutureImprovements { get; set; } = string.Empty;

    public DateTime GeneratedAt { get; set; } = DateTime.UtcNow;

    // Navigation property
    [ForeignKey("PredictionTargetId")]
    [JsonIgnore]
    public PredictionTarget? Target { get; set; }
}

public class LearningFactor
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required]
    [MaxLength(100)]
    public string FactorName { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    public bool IsPredefined { get; set; } = false;

    // Navigation property
    public ICollection<LearningWeight> Weights { get; set; } = new List<LearningWeight>();
    public ICollection<LearningEvidence> Evidence { get; set; } = new List<LearningEvidence>();
}

public class LearningEvidence
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid PredictionTargetId { get; set; }
    public Guid FactorId { get; set; }

    public bool IsPositiveEvidence { get; set; }
    public decimal WeightImpact { get; set; }

    public DateTime RecordedAt { get; set; } = DateTime.UtcNow;

    // Navigation properties
    [ForeignKey("PredictionTargetId")]
    [JsonIgnore]
    public PredictionTarget? Target { get; set; }

    [ForeignKey("FactorId")]
    [JsonIgnore]
    public LearningFactor? Factor { get; set; }
}

public class LearningWeight
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid FactorId { get; set; }

    [MaxLength(100)]
    public string Sector { get; set; } = string.Empty;

    [MaxLength(50)]
    public string MarketRegime { get; set; } = string.Empty;

    public decimal Weight { get; set; }

    public int ReliabilityScore { get; set; } // 0-100

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    // Navigation properties
    [ForeignKey("FactorId")]
    [JsonIgnore]
    public LearningFactor? Factor { get; set; }

    public ICollection<LearningAdjustment> Adjustments { get; set; } = new List<LearningAdjustment>();
}

public class LearningAdjustment
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid LearningWeightId { get; set; }

    public decimal PreviousWeight { get; set; }
    public decimal NewWeight { get; set; }

    public string Reason { get; set; } = string.Empty;

    public DateTime AdjustedAt { get; set; } = DateTime.UtcNow;

    // Navigation property
    [ForeignKey("LearningWeightId")]
    [JsonIgnore]
    public LearningWeight? Weight { get; set; }
}

public class SystemSetting
{
    [Key]
    [MaxLength(100)]
    public string SettingKey { get; set; } = string.Empty;

    public string SettingValue { get; set; } = string.Empty;
}
