namespace IdleOptimizer.Api.Models;

public class Research
{
    public string Name { get; set; } = string.Empty;
    
    // Legacy property for backward compatibility - will be migrated to TargetMultipliers
    [Obsolete("Use TargetMultipliers instead. This property is kept for backward compatibility.")]
    public double MultiplierValue { get; set; }
    
    // New property: Dictionary mapping generator name to multiplier value
    public Dictionary<string, double> TargetMultipliers { get; set; } = [];
    
    public double Cost { get; set; } // Calculated from ResourceCosts
    public Dictionary<string, double> ResourceCosts { get; set; } = []; // Resource name -> cost amount
    public List<string> TargetGenerators { get; set; } = [];
    public bool IsApplied { get; set; } = false;
    public List<string> RequiredGenerators { get; set; } = []; // Names of generators that must be purchased first
    public List<string> RequiredResearch { get; set; } = []; // Names of research that must be applied first
    public bool IsUnlocked { get; set; } = true; // Track unlock state (default true for backward compatibility)
    
    /// <summary>
    /// Gets the multiplier for a specific target generator.
    /// If the generator is not in TargetMultipliers, falls back to MultiplierValue for backward compatibility.
    /// </summary>
    public double GetMultiplier(string? generatorName = null)
    {
        // If generator name is provided and exists in TargetMultipliers, use it
        if (!string.IsNullOrEmpty(generatorName) && TargetMultipliers != null && TargetMultipliers.ContainsKey(generatorName))
        {
            return TargetMultipliers[generatorName];
        }
        
        // For backward compatibility, if TargetMultipliers is empty but MultiplierValue is set, use it
        if ((TargetMultipliers == null || TargetMultipliers.Count == 0) && MultiplierValue != 0)
        {
            return MultiplierValue;
        }
        
        // If no specific generator and no legacy value, return 1.0 (no multiplier)
        return 1.0;
    }

    public double GetTotalCost()
    {
        if (ResourceCosts != null && ResourceCosts.Count > 0)
        {
            return ResourceCosts.Values.Sum();
        }
        return Cost;
    }
}

