namespace IdleOptimizer.Models;

public class Research
{
    public string Name { get; set; } = string.Empty;
    public double MultiplierValue { get; set; }
    public double Cost { get; set; } // Calculated from ResourceCosts
    public Dictionary<string, double> ResourceCosts { get; set; } = new(); // Resource name -> cost amount
    public List<string> TargetGenerators { get; set; } = [];
    
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

