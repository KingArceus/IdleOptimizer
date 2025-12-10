namespace IdleOptimizer.Models;

public class UpgradeResult
{
    public string ItemName { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty; // "Generator" or "Research"
    public double Cost { get; set; }
    public Dictionary<string, double>? ResourceCosts { get; set; } // For Research items - resource name -> cost amount
    public double Gain { get; set; } // Flat gain value instead of percentage
    public Dictionary<string, double>? GainByResource { get; set; } // Production increase per resource
    public double BaseProduction { get; set; } // For Generator items - editable base production
    public int Count { get; set; } // For Generator items - editable count
    public double TimeToAfford { get; set; }
    public double TimeToPayback { get; set; }
    public double CascadeScore { get; set; }
    public string? TargetGenerators { get; set; } // For Research items - comma-separated list
    public object? SourceItem { get; set; } // Reference to Generator or Research for applying purchase
    public DateTime? AvailableAt { get; set; } // Timestamp when the purchase becomes available
}