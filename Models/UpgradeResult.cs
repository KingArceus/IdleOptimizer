namespace IdleOptimizer.Models;

public class UpgradeResult
{
    public string ItemName { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty; // "Generator" or "Research"
    public double Cost { get; set; }
    public double Gain { get; set; } // Flat gain value instead of percentage
    public double ValueScore { get; set; }
    public string? TargetGenerators { get; set; } // For Research items - comma-separated list
    public object? SourceItem { get; set; } // Reference to Generator or Research for applying purchase
}

