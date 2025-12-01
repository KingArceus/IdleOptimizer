namespace IdleOptimizer.Models;

public class Generator
{
    public string Name { get; set; } = string.Empty;
    public double BaseProduction { get; set; }
    public int Count { get; set; }
    public double Cost { get; set; } // Cost for purchasing 1 unit
    public double CostRatio { get; set; } // Cost increase ratio

    public double GetCurrentProduction()
    {
        return BaseProduction * Count;
    }

    public double GetPurchaseCost()
    {
        return Cost;
    }
}

