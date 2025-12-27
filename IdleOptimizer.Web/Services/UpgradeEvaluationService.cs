using IdleOptimizer.Models;

namespace IdleOptimizer.Services;

public class UpgradeEvaluationService(
    IProductionService productionService,
    IValuationService valuationService) : IUpgradeEvaluationService
{
    private readonly IProductionService _productionService = productionService;
    private readonly IValuationService _valuationService = valuationService;

    /// <summary>
    /// Calculates effective production gain using resource values and bottleneck weights.
    /// Formula: Σ(Production increase for resourceᵢ × Resource Valueᵢ × Bottleneck Weightᵢ)
    /// </summary>
    public double CalculateEffectiveProductionGain(
        Dictionary<string, double> productionIncrease,
        Dictionary<string, double> resourceValues,
        Dictionary<string, double> bottleneckWeights)
    {
        double effectiveGain = 0;
        
        foreach (var increase in productionIncrease)
        {
            string resource = increase.Key;
            double increaseAmount = increase.Value;
            double resourceValue = resourceValues.ContainsKey(resource) ? resourceValues[resource] : 0;
            double bottleneckWeight = bottleneckWeights.ContainsKey(resource) ? bottleneckWeights[resource] : 1.0;
            
            // Handle infinite resource values (no production)
            if (double.IsInfinity(resourceValue))
            {
                // If resource has infinite value but we're producing it, use a large finite value
                resourceValue = 1e10;
            }
            
            effectiveGain += increaseAmount * resourceValue * bottleneckWeight;
        }
        
        return effectiveGain;
    }

    /// <summary>
    /// Calculates effective cost using resource valuations.
    /// Formula: Σ(Cost in resourceᵢ × Resource Valueᵢ)
    /// </summary>
    public double CalculateEffectiveCost(
        Dictionary<string, double> resourceCosts,
        Dictionary<string, double> resourceValues)
    {
        if (resourceCosts == null || resourceCosts.Count == 0)
            return 0;
        
        double effectiveCost = 0;
        
        foreach (var cost in resourceCosts)
        {
            double resourceValue = resourceValues.ContainsKey(cost.Key) ? resourceValues[cost.Key] : 0;
            
            // Handle infinite resource values
            if (double.IsInfinity(resourceValue))
            {
                resourceValue = 1e10;
            }
            
            effectiveCost += cost.Value * resourceValue;
        }
        
        return effectiveCost;
    }

    public double CalculateTimeToAffordWithResourceCosts(
        Dictionary<string, double> resourceCosts,
        Dictionary<string, double> productionByResource)
    {
        if (resourceCosts == null || resourceCosts.Count == 0)
            return double.MaxValue;
        
        double maxTime = 0;
        bool allResourcesAvailable = true;
        
        foreach (var cost in resourceCosts)
        {
            if (productionByResource.ContainsKey(cost.Key))
            {
                double resourceProduction = productionByResource[cost.Key];
                if (resourceProduction > 0)
                {
                    double resourceTime = cost.Value / resourceProduction;
                    if (resourceTime > maxTime)
                    {
                        maxTime = resourceTime;
                    }
                }
                else
                {
                    allResourcesAvailable = false;
                    break; // Resource not being produced
                }
            }
            else
            {
                allResourcesAvailable = false;
                break; // Resource not being produced
            }
        }
        
        return allResourcesAvailable ? maxTime : double.MaxValue;
    }

    /// <summary>
    /// Calculates cascade score using the new multi-resource formula:
    /// 1 + (Total Future Time Saved / Baseline) + 0.5 × (Bottleneck Shifts Caused)
    /// </summary>
    public double CalculateCascadeScore(
        UpgradeResult upgrade,
        List<Generator> generators,
        List<Research> research,
        Dictionary<string, double> currentProductionByResource,
        Dictionary<string, double> newProductionByResource)
    {
        // Baseline = Current WaitTime for the upgrade being evaluated
        double baseline = upgrade.TimeToAfford;
        if (baseline <= 0 || double.IsInfinity(baseline) || baseline == double.MaxValue)
        {
            baseline = 1.0; // Fallback to avoid division by zero
        }
        
        // Get all unpurchased upgrades
        var futureUpgrades = new List<(Dictionary<string, double>? ResourceCosts, double Cost, object SourceItem)>();
        
        foreach (var generator in generators)
        {
            if (generator.ResourceCosts != null && generator.ResourceCosts.Count > 0)
            {
                futureUpgrades.Add((generator.ResourceCosts, generator.GetPurchaseCost(), generator));
            }
            else if (generator.Cost > 0)
            {
                futureUpgrades.Add((null, generator.Cost, generator));
            }
        }
        
        foreach (var researchItem in research)
        {
            if (researchItem.ResourceCosts != null && researchItem.ResourceCosts.Count > 0)
            {
                futureUpgrades.Add((researchItem.ResourceCosts, researchItem.GetTotalCost(), researchItem));
            }
            else if (researchItem.Cost > 0)
            {
                futureUpgrades.Add((null, researchItem.Cost, researchItem));
            }
        }
        
        // Calculate Total Future Time Saved and Bottleneck Shifts
        double totalFutureTimeSaved = 0;
        int bottleneckShifts = 0;
        
        foreach (var (resourceCosts, cost, sourceItem) in futureUpgrades)
        {
            // Skip the upgrade we're currently evaluating
            if (sourceItem == upgrade.SourceItem)
                continue;
            
            Dictionary<string, double>? effectiveCosts = resourceCosts;
            
            // Handle legacy costs
            if (effectiveCosts == null || effectiveCosts.Count == 0)
            {
                effectiveCosts = [];
                double totalProduction = currentProductionByResource.Values.Sum();
                foreach (var resource in currentProductionByResource)
                {
                    if (totalProduction > 0)
                    {
                        double resourceShare = resource.Value / totalProduction;
                        effectiveCosts[resource.Key] = cost * resourceShare;
                    }
                }
            }
            
            // Calculate bottleneck resource before upgrade
            string? bottleneckBefore = _valuationService.IdentifyBottleneckResource(effectiveCosts, currentProductionByResource);
            double bottleneckWaitTimeBefore = 0;
            if (bottleneckBefore != null && effectiveCosts.ContainsKey(bottleneckBefore))
            {
                double production = currentProductionByResource.ContainsKey(bottleneckBefore) 
                    ? currentProductionByResource[bottleneckBefore] 
                    : 0;
                if (production > 0)
                {
                    bottleneckWaitTimeBefore = effectiveCosts[bottleneckBefore] / production;
                }
            }
            
            // Calculate bottleneck resource after upgrade
            string? bottleneckAfter = _valuationService.IdentifyBottleneckResource(effectiveCosts, newProductionByResource);
            double bottleneckWaitTimeAfter = 0;
            if (bottleneckAfter != null && effectiveCosts.ContainsKey(bottleneckAfter))
            {
                double production = newProductionByResource.ContainsKey(bottleneckAfter) 
                    ? newProductionByResource[bottleneckAfter] 
                    : 0;
                if (production > 0)
                {
                    bottleneckWaitTimeAfter = effectiveCosts[bottleneckAfter] / production;
                }
            }
            
            // Calculate time saved (reduction in bottleneck wait time)
            if (bottleneckWaitTimeBefore > 0 && bottleneckWaitTimeAfter > 0 && 
                !double.IsInfinity(bottleneckWaitTimeBefore) && !double.IsInfinity(bottleneckWaitTimeAfter))
            {
                double timeSaved = bottleneckWaitTimeBefore - bottleneckWaitTimeAfter;
                if (timeSaved > 0)
                {
                    totalFutureTimeSaved += timeSaved;
                }
            }
            
            // Check if bottleneck shifted
            if (bottleneckBefore != null && bottleneckAfter != null && bottleneckBefore != bottleneckAfter)
            {
                bottleneckShifts++;
            }
        }
        
        // Calculate cascade multiplier: 1 + (Total Future Time Saved / Baseline) + 0.5 × (Bottleneck Shifts)
        double cascadeMultiplier = 1.0 + (totalFutureTimeSaved / baseline) + (0.5 * bottleneckShifts);
        
        return cascadeMultiplier;
    }

    public UpgradeResult EvaluateGeneratorPurchase(
        Generator generator,
        List<Generator> allGenerators,
        Dictionary<string, double> resourceValues,
        Dictionary<string, double> bottleneckWeights,
        Dictionary<string, double> currentProductionByResource)
    {
        double currentTotal = _productionService.GetTotalProduction(allGenerators);
        double cost = generator.GetPurchaseCost();
        int purchaseAmount = 1;
        
        var tempGenerator = new Generator
        {
            Name = generator.Name,
            BaseProduction = generator.BaseProduction,
            Count = generator.Count + purchaseAmount,
            Cost = generator.Cost,
            Resources = generator.Resources != null ? new Dictionary<string, double>(generator.Resources) : []
        };
        
        // Calculate production per resource separately (do not sum different resources)
        var newProductionByResource = new Dictionary<string, double>();
        foreach (var gen in allGenerators)
        {
            Dictionary<string, double> generatorResources;
            if (gen.Name == generator.Name)
            {
                generatorResources = tempGenerator.GetCurrentProductionByResource();
            }
            else
            {
                generatorResources = gen.GetCurrentProductionByResource();
            }
            
            foreach (var resource in generatorResources)
            {
                if (newProductionByResource.ContainsKey(resource.Key))
                {
                    newProductionByResource[resource.Key] += resource.Value;
                }
                else
                {
                    newProductionByResource[resource.Key] = resource.Value;
                }
            }
        }
        
        // Calculate production increase per resource
        var productionIncrease = new Dictionary<string, double>();
        
        foreach (var resource in newProductionByResource)
        {
            double currentValue = currentProductionByResource.ContainsKey(resource.Key) 
                ? currentProductionByResource[resource.Key] 
                : 0;
            productionIncrease[resource.Key] = resource.Value - currentValue;
        }
        
        // Also account for resources that were removed
        foreach (var resource in currentProductionByResource)
        {
            if (!productionIncrease.ContainsKey(resource.Key))
            {
                productionIncrease[resource.Key] = -resource.Value;
            }
        }
        
        // Calculate effective production gain using resource values and bottleneck weights
        double effectiveGain = CalculateEffectiveProductionGain(productionIncrease, resourceValues, bottleneckWeights);
        
        // For total calculation, use the same logic as GetTotalProduction
        double newTotal = newProductionByResource.Count == 1 
            ? newProductionByResource.Values.First() 
            : 0;

        // Calculate time-based efficiency metrics using resource costs
        double timeToAfford;
        double secondsToAdd;

        if (generator.ResourceCosts != null && generator.ResourceCosts.Count > 0)
        {
            // Multiple resource costs - find the bottleneck (longest time to afford)
            timeToAfford = CalculateTimeToAffordWithResourceCosts(generator.ResourceCosts, currentProductionByResource);
            secondsToAdd = CalculateTimeToAffordWithResourceCosts(generator.ResourceCosts, currentProductionByResource);
        }
        else
        {
            timeToAfford = double.MaxValue;
            secondsToAdd = double.MaxValue;
        }

        // Calculate time to payback (using effective gain if available, otherwise simple gain)
        double timeToPayback = double.MaxValue;
        if (effectiveGain > 0)
        {
            // Use effective cost for payback calculation
            double effectiveCost = CalculateEffectiveCost(generator.ResourceCosts ?? [], resourceValues);
            if (effectiveCost > 0)
            {
                timeToPayback = effectiveCost / effectiveGain;
            }
        }
        else
        {
            // Fallback to simple calculation
            double extraProduction = newTotal - currentTotal;
            timeToPayback = extraProduction > 0 
                ? cost / extraProduction 
                : double.MaxValue;
        }
        
        // Calculate cascade score using resource dictionaries
        var upgradeResult = new UpgradeResult 
        { 
            Cost = cost, 
            Gain = effectiveGain, // Use effective gain
            ResourceCosts = generator.ResourceCosts != null && generator.ResourceCosts.Count > 0 
                ? new Dictionary<string, double>(generator.ResourceCosts) 
                : null,
            TimeToAfford = timeToAfford
        };
        double cascadeScore = CalculateCascadeScore(
            upgradeResult,
            allGenerators,
            [], // Research list not needed for generator evaluation
            currentProductionByResource,
            newProductionByResource
        );
        
        // Clamp to a safe maximum value (approximately 100 years in seconds)
        // But don't clamp if it's actually MaxValue (can't afford)
        const double maxSafeSeconds = 100.0 * 365.25 * 24 * 60 * 60; // ~3,155,760,000 seconds
        DateTime? availableAt;

        if (double.IsInfinity(secondsToAdd) || double.IsNaN(secondsToAdd))
        {
            availableAt = null; // Can't afford
        }
        else if (secondsToAdd == double.MaxValue)
        {
            availableAt = null; // Can't afford
        }
        else if (secondsToAdd > maxSafeSeconds)
        {
            availableAt = DateTime.Now.AddSeconds(maxSafeSeconds);
        }
        else
        {
            availableAt = DateTime.Now.AddSeconds(secondsToAdd);
        }
        
        return new UpgradeResult
        {
            ItemName = generator.Name,
            Type = "Generator",
            Cost = cost,
            ResourceCosts = generator.ResourceCosts != null && generator.ResourceCosts.Count > 0 ? new Dictionary<string, double>(generator.ResourceCosts) : null,
            Gain = effectiveGain, // Use effective gain
            GainByResource = productionIncrease.Count > 0 ? new Dictionary<string, double>(productionIncrease) : null,
            TimeToAfford = timeToAfford,
            TimeToPayback = timeToPayback,
            CascadeScore = cascadeScore,
            SourceItem = generator,
            AvailableAt = availableAt
        };
    }

    public UpgradeResult EvaluateResearchPurchase(
        Research research,
        List<Generator> allGenerators,
        List<Research> allResearch,
        Dictionary<string, double> resourceValues,
        Dictionary<string, double> bottleneckWeights,
        Dictionary<string, double> currentProductionByResource)
    {
        // Skip already applied research
        if (research.IsApplied)
        {
            return new UpgradeResult
            {
                ItemName = research.Name,
                Type = "Research",
                Cost = research.GetTotalCost(),
                ResourceCosts = research.ResourceCosts != null && research.ResourceCosts.Count > 0 ? new Dictionary<string, double>(research.ResourceCosts) : null,
                Gain = 0,
                TimeToAfford = double.MaxValue,
                TimeToPayback = double.MaxValue,
                CascadeScore = 0,
                TargetGenerators = string.Join(", ", research.TargetGenerators),
                SourceItem = research,
                AvailableAt = null
            };
        }
        
        double currentTotal = _productionService.GetTotalProduction(allGenerators);
        
        // Calculate new total by applying the research multiplier to affected generators' BaseProduction and Resources
        // Calculate production per resource separately
        var newProductionByResource = new Dictionary<string, double>();
        foreach (var generator in allGenerators)
        {
            Dictionary<string, double> generatorResources;
            if (research.TargetGenerators.Contains(generator.Name))
            {
                // Apply multiplier to Resources if available, otherwise to BaseProduction
                if (generator.Resources != null && generator.Resources.Count > 0)
                {
                    generatorResources = [];
                    double multiplier = research.GetMultiplier(generator.Name);
                    foreach (var resource in generator.Resources)
                    {
                        double newResourceProduction = resource.Value * multiplier;
                        generatorResources[resource.Key] = newResourceProduction * generator.Count;
                    }
                }
                else
                {
                    // No resources defined - use empty dictionary
                    generatorResources = [];
                }
            }
            else
            {
                generatorResources = generator.GetCurrentProductionByResource();
            }
            
            // Add to total production by resource
            foreach (var resource in generatorResources)
            {
                if (newProductionByResource.ContainsKey(resource.Key))
                {
                    newProductionByResource[resource.Key] += resource.Value;
                }
                else
                {
                    newProductionByResource[resource.Key] = resource.Value;
                }
            }
        }
        
        // Calculate production increase per resource
        var productionIncrease = new Dictionary<string, double>();
        
        foreach (var resource in newProductionByResource)
        {
            double currentValue = currentProductionByResource.ContainsKey(resource.Key) 
                ? currentProductionByResource[resource.Key] 
                : 0;
            productionIncrease[resource.Key] = resource.Value - currentValue;
        }
        
        // Also account for resources that were removed
        foreach (var resource in currentProductionByResource)
        {
            if (!productionIncrease.ContainsKey(resource.Key))
            {
                productionIncrease[resource.Key] = -resource.Value;
            }
        }
        
        // Calculate effective production gain using resource values and bottleneck weights
        double effectiveGain = CalculateEffectiveProductionGain(productionIncrease, resourceValues, bottleneckWeights);
        
        // For total calculation, use the same logic as GetTotalProduction
        double newTotal = newProductionByResource.Count == 1 
            ? newProductionByResource.Values.First() 
            : 0;

        // Calculate time-based efficiency metrics
        double totalCost = research.GetTotalCost();
        double timeToAfford;
        double secondsToAdd;

        if (research.ResourceCosts != null && research.ResourceCosts.Count > 0)
        {
            // Multiple resource costs - find the bottleneck (longest time to afford)
            timeToAfford = CalculateTimeToAffordWithResourceCosts(research.ResourceCosts, currentProductionByResource);
            secondsToAdd = CalculateTimeToAffordWithResourceCosts(research.ResourceCosts, currentProductionByResource);
        }
        else
        {
            timeToAfford = double.MaxValue;
            secondsToAdd = double.MaxValue;
        }

        // Calculate time to payback (using effective gain if available, otherwise simple gain)
        double timeToPayback = double.MaxValue;
        if (effectiveGain > 0)
        {
            // Use effective cost for payback calculation
            double effectiveCost = CalculateEffectiveCost(research.ResourceCosts ?? [], resourceValues);
            if (effectiveCost > 0)
            {
                timeToPayback = effectiveCost / effectiveGain;
            }
        }
        else
        {
            // Fallback to simple calculation
            double extraProduction = newTotal - currentTotal;
            timeToPayback = extraProduction > 0 
                ? totalCost / extraProduction 
                : double.MaxValue;
        }
        
        // Calculate cascade score using resource dictionaries
        var upgradeResult = new UpgradeResult 
        { 
            Cost = totalCost, 
            Gain = effectiveGain, // Use effective gain
            ResourceCosts = research.ResourceCosts != null && research.ResourceCosts.Count > 0 
                ? new Dictionary<string, double>(research.ResourceCosts) 
                : null,
            TimeToAfford = timeToAfford
        };
        double cascadeScore = CalculateCascadeScore(
            upgradeResult,
            allGenerators,
            allResearch,
            currentProductionByResource,
            newProductionByResource
        );
        
        // Clamp to a safe maximum value (approximately 100 years in seconds)
        // But don't clamp if it's actually MaxValue (can't afford)
        const double maxSafeSeconds = 100.0 * 365.25 * 24 * 60 * 60; // ~3,155,760,000 seconds
        DateTime? availableAt;

        if (double.IsInfinity(secondsToAdd) || double.IsNaN(secondsToAdd))
        {
            availableAt = null; // Can't afford
        }
        else if (secondsToAdd == double.MaxValue)
        {
            availableAt = null; // Can't afford
        }
        else if (secondsToAdd > maxSafeSeconds)
        {
            availableAt = DateTime.Now.AddSeconds(maxSafeSeconds);
        }
        else
        {
            availableAt = DateTime.Now.AddSeconds(secondsToAdd);
        }
        
        return new UpgradeResult
        {
            ItemName = research.Name,
            Type = "Research",
            Cost = totalCost, // Total cost for display
            ResourceCosts = research.ResourceCosts != null && research.ResourceCosts.Count > 0 ? new Dictionary<string, double>(research.ResourceCosts) : null,
            Gain = effectiveGain, // Use effective gain
            GainByResource = productionIncrease.Count > 0 ? new Dictionary<string, double>(productionIncrease) : null,
            TimeToAfford = timeToAfford,
            TimeToPayback = timeToPayback,
            CascadeScore = cascadeScore,
            TargetGenerators = string.Join(", ", research.TargetGenerators),
            SourceItem = research,
            AvailableAt = availableAt
        };
    }
}