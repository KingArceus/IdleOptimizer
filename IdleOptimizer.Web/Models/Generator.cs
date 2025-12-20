namespace IdleOptimizer.Models;

public class Generator
{
    public string Name { get; set; } = string.Empty;
    public double BaseProduction { get; set; } // Calculated from Resources
    public Dictionary<string, double> BaseResources { get; set; } = []; // Base version of Resources (immutable except during Prestige)
    public Dictionary<string, double> Resources { get; set; } = []; // Resource name -> base production per unit (mutable)
    public int Count { get; set; }
    public double Cost { get; set; } // Calculated from ResourceCosts
    public Dictionary<string, double> BaseResourceCosts { get; set; } = []; // Base version of ResourceCosts (immutable except during Prestige)
    public Dictionary<string, double> ResourceCosts { get; set; } = []; // Resource name -> cost amount (mutable)
    public double CostRatio { get; set; } // Cost increase ratio
    public List<string> RequiredGenerators { get; set; } = []; // Names of generators that must be purchased first
    public List<string> RequiredResearch { get; set; } = []; // Names of research that must be applied first
    public bool IsUnlocked { get; set; } = true; // Track unlock state (default true for backward compatibility)

    public double GetCurrentProduction()
    {
        if (Resources != null && Resources.Count > 0)
        {
            return Resources.Values.Sum() * Count;
        }
        return BaseProduction * Count;
    }

    public Dictionary<string, double> GetCurrentProductionByResource()
    {
        var result = new Dictionary<string, double>();
        if (Resources != null && Resources.Count > 0)
        {
            foreach (var resource in Resources)
            {
                result[resource.Key] = resource.Value * Count;
            }
        }
        else if (BaseProduction > 0)
        {
            result["Default"] = BaseProduction * Count;
        }
        return result;
    }

    public double GetPurchaseCost()
    {
        if (ResourceCosts != null && ResourceCosts.Count > 0)
        {
            return ResourceCosts.Values.Sum();
        }
        return Cost;
    }
}

