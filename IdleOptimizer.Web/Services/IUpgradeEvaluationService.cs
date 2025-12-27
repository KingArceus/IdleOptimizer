using IdleOptimizer.Models;

namespace IdleOptimizer.Services;

public interface IUpgradeEvaluationService
{
    UpgradeResult EvaluateGeneratorPurchase(
        Generator generator,
        List<Generator> allGenerators,
        Dictionary<string, double> resourceValues,
        Dictionary<string, double> bottleneckWeights,
        Dictionary<string, double> currentProductionByResource);
    
    UpgradeResult EvaluateResearchPurchase(
        Research research,
        List<Generator> allGenerators,
        List<Research> allResearch,
        Dictionary<string, double> resourceValues,
        Dictionary<string, double> bottleneckWeights,
        Dictionary<string, double> currentProductionByResource);
    
    double CalculateCascadeScore(
        UpgradeResult upgrade,
        List<Generator> generators,
        List<Research> research,
        Dictionary<string, double> currentProductionByResource,
        Dictionary<string, double> newProductionByResource,
        double timeToPayback);
    
    double CalculateTimeToAffordWithResourceCosts(
        Dictionary<string, double> resourceCosts,
        Dictionary<string, double> productionByResource);
    
    double CalculateEffectiveProductionGain(
        Dictionary<string, double> productionIncrease,
        Dictionary<string, double> resourceValues,
        Dictionary<string, double> bottleneckWeights);
    
    double CalculateEffectiveCost(
        Dictionary<string, double> resourceCosts,
        Dictionary<string, double> resourceValues);
}