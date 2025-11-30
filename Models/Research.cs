namespace IdleOptimizer.Models;

public class Research
{
    public string Name { get; set; } = string.Empty;
    public double MultiplierValue { get; set; }
    public double Cost { get; set; }
    public List<string> TargetGenerators { get; set; } = [];
    public double GetMultiplier()
    {
        return MultiplierValue; // e.g., 0.25 becomes 1.25 (25% increase)
    }
}

