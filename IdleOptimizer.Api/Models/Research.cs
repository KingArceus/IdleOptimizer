namespace IdleOptimizer.Api.Models;

public class Research
{
    public string Name { get; set; } = string.Empty;
    public double MultiplierValue { get; set; }
    public double Cost { get; set; } // Calculated from ResourceCosts
    public Dictionary<string, double> ResourceCosts { get; set; } = []; // Resource name -> cost amount
    public List<string> TargetGenerators { get; set; } = [];
    public bool IsApplied { get; set; } = false;
    public List<string> RequiredGenerators { get; set; } = []; // Names of generators that must be purchased first
    public List<string> RequiredResearch { get; set; } = []; // Names of research that must be applied first
    public bool IsUnlocked { get; set; } = true; // Track unlock state (default true for backward compatibility)
    
    public double GetMultiplier()
    {
        return MultiplierValue; // e.g., 0.25 becomes 1.25 (25% increase)
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

