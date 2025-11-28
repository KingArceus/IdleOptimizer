namespace IdleOptimizer.Models;

public class Generator
{
    public string Name { get; set; } = string.Empty;
    public double BaseProduction { get; set; }
    public int Count { get; set; }
    public double Cost { get; set; } // Cost for purchasing 10 units

    public double GetCurrentProduction()
    {
        return BaseProduction * Count;
    }

    public double GetPurchaseCost()
    {
        return Cost; // Cost is always for 10 units
    }
}

