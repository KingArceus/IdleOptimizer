using IdleOptimizer.Models;

namespace IdleOptimizer.Services;

public interface ICalculationService
{
    List<Generator> Generators { get; }
    List<Research> Research { get; }
    
    Task InitializeAsync();
    Task AppliedPurchaseAsync(UpgradeResult upgrade);
    List<UpgradeResult> GetRankedUpgrades();
    double GetTotalProduction();
    double ApplyAbbreviation(double value, string? abbr);
    Task SaveStateAsync();
    Task LoadStateAsync();
    Task ClearAllAsync();
}

