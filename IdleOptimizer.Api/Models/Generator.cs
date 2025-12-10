namespace IdleOptimizer.Api.Models;

public class Generator
{
    public string Name { get; set; } = string.Empty;
    public double BaseProduction { get; set; } // Calculated from Resources
    public Dictionary<string, double> Resources { get; set; } = new(); // Resource name -> base production per unit
    public int Count { get; set; }
    public double Cost { get; set; } // Calculated from ResourceCosts
    public Dictionary<string, double> ResourceCosts { get; set; } = new(); // Resource name -> cost amount
    public double CostRatio { get; set; } // Cost increase ratio

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

