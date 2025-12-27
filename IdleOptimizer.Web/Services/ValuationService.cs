using IdleOptimizer.Models;

namespace IdleOptimizer.Services;

public class ValuationService : IValuationService
{
    /// <summary>
    /// Calculates resource valuations based on demand across all upgrades.
    /// Resource Value = Σ(Demand across all upgrades) / Current production rate
    /// </summary>
    public Dictionary<string, double> CalculateResourceValuations(
        List<Generator> generators,
        List<Research> research,
        Dictionary<string, double> productionByResource)
    {
        var resourceValues = new Dictionary<string, double>();
        
        // Calculate total demand per resource across all unpurchased upgrades
        var demandByResource = new Dictionary<string, double>();
        
        // Sum costs from all generators
        foreach (var generator in generators)
        {
            if (generator.ResourceCosts != null && generator.ResourceCosts.Count > 0)
            {
                foreach (var cost in generator.ResourceCosts)
                {
                    if (demandByResource.ContainsKey(cost.Key))
                    {
                        demandByResource[cost.Key] += cost.Value;
                    }
                    else
                    {
                        demandByResource[cost.Key] = cost.Value;
                    }
                }
            }
        }
        
        // Sum costs from all research
        foreach (var researchItem in research)
        {
            if (researchItem.ResourceCosts != null && researchItem.ResourceCosts.Count > 0)
            {
                foreach (var cost in researchItem.ResourceCosts)
                {
                    if (demandByResource.ContainsKey(cost.Key))
                    {
                        demandByResource[cost.Key] += cost.Value;
                    }
                    else
                    {
                        demandByResource[cost.Key] = cost.Value;
                    }
                }
            }
        }
        
        // Calculate resource values: demand / production rate
        foreach (var resource in productionByResource.Keys)
        {
            double production = productionByResource[resource];
            double demand = demandByResource.ContainsKey(resource) ? demandByResource[resource] : 0;
            
            if (production > 0)
            {
                resourceValues[resource] = demand / production;
            }
            else
            {
                // If no production, set to a very high value (or skip)
                resourceValues[resource] = double.MaxValue;
            }
        }
        
        // Also include resources that are in demand but not yet produced (for future planning)
        foreach (var resource in demandByResource.Keys)
        {
            if (!resourceValues.ContainsKey(resource))
            {
                resourceValues[resource] = double.MaxValue;
            }
        }
        
        return resourceValues;
    }

    /// <summary>
    /// Calculates dynamic bottleneck weights based on delay impact across all upgrades.
    /// </summary>
    public Dictionary<string, double> CalculateBottleneckWeights(
        List<Generator> generators,
        List<Research> research,
        Dictionary<string, double> productionByResource,
        Dictionary<string, double>? previousWeights)
    {
        var weights = new Dictionary<string, double>();
        
        // Get all unpurchased upgrades
        var allUpgrades = new List<(Dictionary<string, double>? ResourceCosts, double Cost)>();
        
        foreach (var generator in generators)
        {
            if (generator.ResourceCosts != null && generator.ResourceCosts.Count > 0)
            {
                allUpgrades.Add((generator.ResourceCosts, generator.GetPurchaseCost()));
            }
        }
        
        foreach (var researchItem in research)
        {
            if (researchItem.ResourceCosts != null && researchItem.ResourceCosts.Count > 0)
            {
                allUpgrades.Add((researchItem.ResourceCosts, researchItem.GetTotalCost()));
            }
        }
        
        // Early game fallback: if fewer than 3 upgrades, use simplified calculation
        if (allUpgrades.Count < 3)
        {
            // Calculate immediate shortage for each resource
            double totalImmediateNeed = 0;
            var immediateShortage = new Dictionary<string, double>();
            
            foreach (var (resourceCosts, _) in allUpgrades)
            {
                if (resourceCosts != null && resourceCosts.Count > 0)
                {
                    foreach (var resourceCost in resourceCosts)
                    {
                        double production = productionByResource.ContainsKey(resourceCost.Key) 
                            ? productionByResource[resourceCost.Key] 
                            : 0;
                        double shortage = Math.Max(0, resourceCost.Value - production);
                        
                        if (immediateShortage.ContainsKey(resourceCost.Key))
                        {
                            immediateShortage[resourceCost.Key] += shortage;
                        }
                        else
                        {
                            immediateShortage[resourceCost.Key] = shortage;
                        }
                        totalImmediateNeed += resourceCost.Value;
                    }
                }
            }
            
            // Calculate weights
            foreach (var resource in productionByResource.Keys)
            {
                double shortage = immediateShortage.ContainsKey(resource) ? immediateShortage[resource] : 0;
                double weight = 1.0 + Math.Max(0, shortage / Math.Max(1, totalImmediateNeed));
                weights[resource] = weight;
            }
        }
        else
        {
            // Analyze all upgrades to find bottleneck delays
            var delayByResource = new Dictionary<string, double>();
            double totalPathTime = 0;
            
            foreach (var (resourceCosts, cost) in allUpgrades)
            {
                if (resourceCosts == null || resourceCosts.Count == 0) continue;
                
                // Calculate wait times per resource and find bottleneck
                string? bottleneck = null;
                double bottleneckWaitTime = 0;
                
                foreach (var resourceCost in resourceCosts)
                {
                    if (productionByResource.ContainsKey(resourceCost.Key))
                    {
                        double production = productionByResource[resourceCost.Key];
                        if (production > 0)
                        {
                            double waitTime = resourceCost.Value / production;
                            if (waitTime > bottleneckWaitTime)
                            {
                                bottleneckWaitTime = waitTime;
                                bottleneck = resourceCost.Key;
                            }
                        }
                    }
                }
                
                if (bottleneck != null && bottleneckWaitTime > 0)
                {
                    // This resource is the bottleneck for this upgrade
                    if (delayByResource.ContainsKey(bottleneck))
                    {
                        delayByResource[bottleneck] += bottleneckWaitTime;
                    }
                    else
                    {
                        delayByResource[bottleneck] = bottleneckWaitTime;
                    }
                    totalPathTime += bottleneckWaitTime;
                }
            }
            
            // Calculate weights: 1 + (Total Delay Caused / Total Path Time)
            foreach (var resource in productionByResource.Keys)
            {
                double delay = delayByResource.ContainsKey(resource) ? delayByResource[resource] : 0;
                double weight = 1.0;
                if (totalPathTime > 0)
                {
                    weight = 1.0 + (delay / totalPathTime);
                }
                weights[resource] = weight;
            }
        }
        
        // Apply exponential smoothing if previous weights exist
        if (previousWeights != null && previousWeights.Count > 0)
        {
            var smoothedWeights = new Dictionary<string, double>();
            foreach (var resource in productionByResource.Keys)
            {
                double newWeight = weights.ContainsKey(resource) ? weights[resource] : 1.0;
                double previousWeight = previousWeights.ContainsKey(resource) ? previousWeights[resource] : 1.0;
                double smoothedWeight = 0.4 * newWeight + 0.6 * previousWeight;
                smoothedWeights[resource] = smoothedWeight;
            }
            weights = smoothedWeights;
        }
        
        return weights;
    }

    /// <summary>
    /// Identifies the bottleneck resource (the one with maximum wait time) for a given cost.
    /// </summary>
    public string? IdentifyBottleneckResource(
        Dictionary<string, double> resourceCosts,
        Dictionary<string, double> productionByResource)
    {
        if (resourceCosts == null || resourceCosts.Count == 0)
            return null;
        
        string? bottleneckResource = null;
        double maxWaitTime = 0;
        
        foreach (var cost in resourceCosts)
        {
            if (productionByResource.ContainsKey(cost.Key))
            {
                double resourceProduction = productionByResource[cost.Key];
                if (resourceProduction > 0)
                {
                    double waitTime = cost.Value / resourceProduction;
                    if (waitTime > maxWaitTime)
                    {
                        maxWaitTime = waitTime;
                        bottleneckResource = cost.Key;
                    }
                }
            }
        }
        
        return bottleneckResource;
    }
}