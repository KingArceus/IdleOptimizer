using IdleOptimizer.Models;

namespace IdleOptimizer.Services;

public interface ICalculationService
{
    List<Generator> Generators { get; }
    List<Research> Research { get; }
    
    Task InitializeAsync();
    Task AppliedPurchaseAsync(UpgradeResult upgrade);
    UpgradeResult EvaluateGeneratorPurchase(Generator g);
    UpgradeResult EvaluateResearchPurchase(Research r);
    List<UpgradeResult> GetRankedUpgrades();
    double GetTotalProduction();
    Task SaveStateAsync();
    Task LoadStateAsync();
}

