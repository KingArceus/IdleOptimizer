using IdleOptimizer.Models;

namespace IdleOptimizer.Services;

public interface ICalculationService
{
    List<Generator> Generators { get; }
    List<Research> Research { get; }
    List<Resource> Resources { get; }
    
    Task InitializeAsync();
    void AppliedPurchase(UpgradeResult upgrade);
    List<UpgradeResult> GetRankedUpgrades();
    double GetTotalProduction();
    Dictionary<string, double> GetTotalProductionByResource();
    double GetTotalProductionByResourceName(string resourceName);
    Dictionary<string, double> GetResourceValues();
    void UpdateResourceTotalProduction();
    Resource? GetResource(string name);
    void AddResource(Resource resource);
    void RemoveResource(Resource resource);
    double ApplyAbbreviation(double value, string? abbr);
    Task SaveStateAsync();
    Task LoadStateAsync();
    Task ClearAllAsync();
    Task PerformPrestigeAsync(double productionMultiplier, double costMultiplier);
}

