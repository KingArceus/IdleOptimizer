namespace IdleOptimizer.Models;

public class Resource
{
    public string Name { get; set; } = string.Empty;
    
    // TotalProduction is calculated from generators, not stored
    public double TotalProduction { get; private set; }
    
    public void UpdateTotalProduction(double totalProduction)
    {
        TotalProduction = totalProduction;
    }
}

