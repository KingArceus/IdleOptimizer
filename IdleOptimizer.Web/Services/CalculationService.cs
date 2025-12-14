using IdleOptimizer.Models;

namespace IdleOptimizer.Services;

public class CalculationService(ILocalStorageService localStorage) : ICalculationService
{
    private readonly ILocalStorageService _localStorage = localStorage;
    private double _lastTotalProduction = 0;
    private const double CascadeScoreMultiplier = 10.0;
    private Dictionary<string, double> _previousBottleneckWeights = new();
    
    public List<Generator> Generators { get; private set; } = [];
    public List<Research> Research { get; private set; } = [];
    public List<Resource> Resources { get; private set; } = [];

    public async Task InitializeAsync()
    {
        await LoadStateAsync();
        UpdateResourceTotalProduction();
                
        // Initialize total production
        _lastTotalProduction = GetTotalProduction();
    }

    public double ApplyAbbreviation(double value, string? abbr)
    {
        return abbr switch
        {
            "K"  => value * 1e3,
            "M"  => value * 1e6,
            "B"  => value * 1e9,
            "T"  => value * 1e12,
            "Qa" => value * 1e15,
            "Qi" => value * 1e18,
            "Sx" => value * 1e21,
            "Sp" => value * 1e24,
            "Oc" => value * 1e27,
            "No" => value * 1e30,
            _    => value
        };
    }

    private double CalculateTotalProduction()
    {
        // Do not sum different resources - they are separate
        // Only return a value if all generators use the same single resource
        var productionByResource = GetTotalProductionByResource();
        
        // If there's only one resource type, return its production
        if (productionByResource.Count == 1)
        {
            return productionByResource.Values.First();
        }
        
        // If there are multiple different resources, return 0 (cannot sum different resource types)
        return 0;
    }

    public double GetTotalProduction()
    {
        double total = CalculateTotalProduction();
        
        // Update last total production if changed
        if (Math.Abs(total - _lastTotalProduction) > 0.001)
        {
            _lastTotalProduction = total;
        }
        
        return total;
    }

    public Dictionary<string, double> GetTotalProductionByResource()
    {
        var result = new Dictionary<string, double>();
        
        foreach (var generator in Generators)
        {
            var generatorResources = generator.GetCurrentProductionByResource();
            foreach (var resource in generatorResources)
            {
                if (result.ContainsKey(resource.Key))
                {
                    result[resource.Key] += resource.Value;
                }
                else
                {
                    result[resource.Key] = resource.Value;
                }
            }
        }
        
        return result;
    }

    public double GetTotalProductionByResourceName(string resourceName)
    {
        var productionByResource = GetTotalProductionByResource();
        return productionByResource.ContainsKey(resourceName) ? productionByResource[resourceName] : 0;
    }

    public void UpdateResourceTotalProduction()
    {
        var productionByResource = GetTotalProductionByResource();
        
        // Update existing resources
        foreach (var resource in Resources)
        {
            double totalProduction = productionByResource.ContainsKey(resource.Name) 
                ? productionByResource[resource.Name] 
                : 0;
            resource.UpdateTotalProduction(totalProduction);
        }
        
        // Add resources that exist in production but not in Resources list
        foreach (var resourceName in productionByResource.Keys)
        {
            if (Resources.All(r => r.Name != resourceName))
            {
                var newResource = new Resource { Name = resourceName };
                newResource.UpdateTotalProduction(productionByResource[resourceName]);
                Resources.Add(newResource);
            }
        }
    }

    public Resource? GetResource(string name)
    {
        return Resources.FirstOrDefault(r => r.Name == name);
    }

    public void AddResource(Resource resource)
    {
        if (Resources.All(r => r.Name != resource.Name))
        {
            Resources.Add(resource);
            UpdateResourceTotalProduction();
        }
    }

    public void RemoveResource(Resource resource)
    {
        Resources.Remove(resource);
        UpdateResourceTotalProduction();
    }

    /// <summary>
    /// Calculates resource valuations based on demand across all upgrades.
    /// Resource Value = Σ(Demand across all upgrades) / Current production rate
    /// </summary>
    private Dictionary<string, double> CalculateResourceValuations()
    {
        var resourceValues = new Dictionary<string, double>();
        var productionByResource = GetTotalProductionByResource();
        
        // Calculate total demand per resource across all unpurchased upgrades
        var demandByResource = new Dictionary<string, double>();
        
        // Sum costs from all generators
        foreach (var generator in Generators)
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
        foreach (var research in Research)
        {
            if (research.ResourceCosts != null && research.ResourceCosts.Count > 0)
            {
                foreach (var cost in research.ResourceCosts)
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
            else if (research.Cost > 0)
            {
                // Legacy cost - distribute across all resources proportionally
                double totalProduction = productionByResource.Values.Sum();
                foreach (var resource in productionByResource)
                {
                    if (totalProduction > 0)
                    {
                        double resourceShare = resource.Value / totalProduction;
                        double effectiveDemand = research.Cost * resourceShare;
                        if (demandByResource.ContainsKey(resource.Key))
                        {
                            demandByResource[resource.Key] += effectiveDemand;
                        }
                        else
                        {
                            demandByResource[resource.Key] = effectiveDemand;
                        }
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
    /// Identifies the bottleneck resource (the one with maximum wait time) for a given cost.
    /// </summary>
    private string? IdentifyBottleneckResource(Dictionary<string, double> resourceCosts, Dictionary<string, double> productionByResource)
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

    /// <summary>
    /// Calculates dynamic bottleneck weights based on delay impact across all upgrades.
    /// </summary>
    private Dictionary<string, double> CalculateBottleneckWeights(Dictionary<string, double>? previousWeights = null)
    {
        var productionByResource = GetTotalProductionByResource();
        var weights = new Dictionary<string, double>();
        
        // Get all unpurchased upgrades
        var allUpgrades = new List<(Dictionary<string, double>? ResourceCosts, double Cost)>();
        
        foreach (var generator in Generators)
        {
            if (generator.ResourceCosts != null && generator.ResourceCosts.Count > 0)
            {
                allUpgrades.Add((generator.ResourceCosts, generator.GetPurchaseCost()));
            }
            else if (generator.Cost > 0)
            {
                allUpgrades.Add((null, generator.Cost));
            }
        }
        
        foreach (var research in Research)
        {
            if (research.ResourceCosts != null && research.ResourceCosts.Count > 0)
            {
                allUpgrades.Add((research.ResourceCosts, research.GetTotalCost()));
            }
            else if (research.Cost > 0)
            {
                allUpgrades.Add((null, research.Cost));
            }
        }
        
        // Early game fallback: if fewer than 3 upgrades, use simplified calculation
        if (allUpgrades.Count < 3)
        {
            // Calculate immediate shortage for each resource
            double totalImmediateNeed = 0;
            var immediateShortage = new Dictionary<string, double>();
            
            foreach (var (resourceCosts, cost) in allUpgrades)
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
                else
                {
                    // Legacy cost - distribute
                    double totalProduction = productionByResource.Values.Sum();
                    foreach (var resource in productionByResource)
                    {
                        if (totalProduction > 0)
                        {
                            double resourceShare = resource.Value / totalProduction;
                            double effectiveCost = cost * resourceShare;
                            double shortage = Math.Max(0, effectiveCost - resource.Value);
                            
                            if (immediateShortage.ContainsKey(resource.Key))
                            {
                                immediateShortage[resource.Key] += shortage;
                            }
                            else
                            {
                                immediateShortage[resource.Key] = shortage;
                            }
                            totalImmediateNeed += effectiveCost;
                        }
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
        
        // Store for next calculation
        _previousBottleneckWeights = new Dictionary<string, double>(weights);
        
        return weights;
    }

    /// <summary>
    /// Calculates effective production gain using resource values and bottleneck weights.
    /// Formula: Σ(Production increase for resourceᵢ × Resource Valueᵢ × Bottleneck Weightᵢ)
    /// </summary>
    private double CalculateEffectiveProductionGain(
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
    private double CalculateEffectiveCost(
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

    /// <summary>
    /// Calculates cascade score using the new multi-resource formula:
    /// 1 + (Total Future Time Saved / Baseline) + 0.5 × (Bottleneck Shifts Caused)
    /// </summary>
    private double CalculateCascadeScore(
        UpgradeResult upgrade,
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
        
        foreach (var generator in Generators)
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
        
        foreach (var research in Research)
        {
            if (research.ResourceCosts != null && research.ResourceCosts.Count > 0)
            {
                futureUpgrades.Add((research.ResourceCosts, research.GetTotalCost(), research));
            }
            else if (research.Cost > 0)
            {
                futureUpgrades.Add((null, research.Cost, research));
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
                effectiveCosts = new Dictionary<string, double>();
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
            string? bottleneckBefore = IdentifyBottleneckResource(effectiveCosts, currentProductionByResource);
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
            string? bottleneckAfter = IdentifyBottleneckResource(effectiveCosts, newProductionByResource);
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
    
    private double CalculateTimeToAffordWithResourceCosts(Dictionary<string, double> resourceCosts, Dictionary<string, double> productionByResource)
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

    private UpgradeResult EvaluateGeneratorPurchase(
        Generator g,
        Dictionary<string, double> resourceValues,
        Dictionary<string, double> bottleneckWeights)
    {
        
        double currentTotal = CalculateTotalProduction();
        double cost = g.GetPurchaseCost();
        int purchaseAmount = 1;
        
        var tempGenerator = new Generator
        {
            Name = g.Name,
            BaseProduction = g.BaseProduction,
            Count = g.Count + purchaseAmount,
            Cost = g.Cost,
            Resources = g.Resources != null ? new Dictionary<string, double>(g.Resources) : new Dictionary<string, double>()
        };
        
        // Calculate production per resource separately (do not sum different resources)
        var newProductionByResource = new Dictionary<string, double>();
        foreach (var generator in Generators)
        {
            Dictionary<string, double> generatorResources;
            if (generator.Name == g.Name)
            {
                generatorResources = tempGenerator.GetCurrentProductionByResource();
            }
            else
            {
                generatorResources = generator.GetCurrentProductionByResource();
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
        var currentProductionByResource = GetTotalProductionByResource();
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
        
        // For total calculation, use the same logic as CalculateTotalProduction
        double newTotal = newProductionByResource.Count == 1 
            ? newProductionByResource.Values.First() 
            : 0;
        
        // Calculate time-based efficiency metrics using resource costs
        double timeToAfford = double.MaxValue;
        double secondsToAdd = double.MaxValue;
        
        if (g.ResourceCosts != null && g.ResourceCosts.Count > 0)
        {
            // Multiple resource costs - find the bottleneck (longest time to afford)
            timeToAfford = CalculateTimeToAffordWithResourceCosts(g.ResourceCosts, currentProductionByResource);
            secondsToAdd = CalculateTimeToAffordWithResourceCosts(g.ResourceCosts, currentProductionByResource);
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
            double effectiveCost = CalculateEffectiveCost(g.ResourceCosts ?? new Dictionary<string, double>(), resourceValues);
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
            ResourceCosts = g.ResourceCosts != null && g.ResourceCosts.Count > 0 
                ? new Dictionary<string, double>(g.ResourceCosts) 
                : null,
            TimeToAfford = timeToAfford
        };
        double cascadeScore = CalculateCascadeScore(
            upgradeResult,
            currentProductionByResource,
            newProductionByResource
        );
        
        // Clamp to a safe maximum value (approximately 100 years in seconds)
        // But don't clamp if it's actually MaxValue (can't afford)
        const double maxSafeSeconds = 100.0 * 365.25 * 24 * 60 * 60; // ~3,155,760,000 seconds
        DateTime? availableAt = null;
        
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
            ItemName = g.Name,
            Type = "Generator",
            Cost = cost,
            ResourceCosts = g.ResourceCosts != null && g.ResourceCosts.Count > 0 ? new Dictionary<string, double>(g.ResourceCosts) : null,
            Gain = effectiveGain, // Use effective gain
            GainByResource = productionIncrease.Count > 0 ? new Dictionary<string, double>(productionIncrease) : null,
            TimeToAfford = timeToAfford,
            TimeToPayback = timeToPayback,
            CascadeScore = cascadeScore,
            SourceItem = g,
            AvailableAt = availableAt
        };
    }

    private UpgradeResult EvaluateResearchPurchase(
        Research r,
        Dictionary<string, double> resourceValues,
        Dictionary<string, double> bottleneckWeights)
    {
        
        double currentTotal = CalculateTotalProduction();
        
        // Calculate new total by applying the research multiplier to affected generators' BaseProduction and Resources
        // Calculate production per resource separately
        var newProductionByResource = new Dictionary<string, double>();
        foreach (var generator in Generators)
        {
            Dictionary<string, double> generatorResources;
            if (r.TargetGenerators.Contains(generator.Name))
            {
                // Apply multiplier to Resources if available, otherwise to BaseProduction
                if (generator.Resources != null && generator.Resources.Count > 0)
                {
                    generatorResources = new Dictionary<string, double>();
                    foreach (var resource in generator.Resources)
                    {
                        double newResourceProduction = resource.Value * r.GetMultiplier();
                        generatorResources[resource.Key] = newResourceProduction * generator.Count;
                    }
                }
                else
                {
                    // No resources defined - use empty dictionary
                    generatorResources = new Dictionary<string, double>();
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
        var currentProductionByResource = GetTotalProductionByResource();
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
        
        // For total calculation, use the same logic as CalculateTotalProduction
        double newTotal = newProductionByResource.Count == 1 
            ? newProductionByResource.Values.First() 
            : 0;
        
        // Calculate time-based efficiency metrics
        double currentProductionPerSecond = currentTotal;
        double newProductionPerSecond = newTotal;
        
        // Handle multiple resource costs
        double totalCost = r.GetTotalCost();
        double timeToAfford = double.MaxValue;
        double secondsToAdd = double.MaxValue;
        
        if (r.ResourceCosts != null && r.ResourceCosts.Count > 0)
        {
            // Multiple resource costs - find the bottleneck (longest time to afford)
            var productionByResource = GetTotalProductionByResource();
            timeToAfford = CalculateTimeToAffordWithResourceCosts(r.ResourceCosts, productionByResource);
            secondsToAdd = CalculateTimeToAffordWithResourceCosts(r.ResourceCosts, productionByResource);
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
            double effectiveCost = CalculateEffectiveCost(r.ResourceCosts ?? new Dictionary<string, double>(), resourceValues);
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
            ResourceCosts = r.ResourceCosts != null && r.ResourceCosts.Count > 0 
                ? new Dictionary<string, double>(r.ResourceCosts) 
                : null,
            TimeToAfford = timeToAfford
        };
        double cascadeScore = CalculateCascadeScore(
            upgradeResult,
            currentProductionByResource,
            newProductionByResource
        );
        
        // Clamp to a safe maximum value (approximately 100 years in seconds)
        // But don't clamp if it's actually MaxValue (can't afford)
        const double maxSafeSeconds = 100.0 * 365.25 * 24 * 60 * 60; // ~3,155,760,000 seconds
        DateTime? availableAt = null;
        
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
            ItemName = r.Name,
            Type = "Research",
            Cost = totalCost, // Total cost for display
            ResourceCosts = r.ResourceCosts != null && r.ResourceCosts.Count > 0 ? new Dictionary<string, double>(r.ResourceCosts) : null,
            Gain = effectiveGain, // Use effective gain
            GainByResource = productionIncrease.Count > 0 ? new Dictionary<string, double>(productionIncrease) : null,
            TimeToAfford = timeToAfford,
            TimeToPayback = timeToPayback,
            CascadeScore = cascadeScore,
            TargetGenerators = string.Join(", ", r.TargetGenerators),
            SourceItem = r,
            AvailableAt = availableAt
        };
    }

    public List<UpgradeResult> GetRankedUpgrades()
    {
        // Calculate resource valuations and bottleneck weights once for current state
        var resourceValues = CalculateResourceValuations();
        var bottleneckWeights = CalculateBottleneckWeights(_previousBottleneckWeights);
        
        var results = new List<UpgradeResult>();
        
        // Evaluate all generators
        foreach (var generator in Generators)
        {
            results.Add(EvaluateGeneratorPurchase(generator, resourceValues, bottleneckWeights));
        }
        
        // Evaluate all research
        foreach (var research in Research)
        {
            results.Add(EvaluateResearchPurchase(research, resourceValues, bottleneckWeights));
        }
        
        // Sort by CascadeScore descending
        return [.. results.OrderByDescending(r => r.CascadeScore)];
    }

    public async Task AppliedPurchaseAsync(UpgradeResult upgrade)
    {
        if (upgrade.SourceItem is Generator generator)
        {
            generator.Count += 1; // Purchase 1 unit at a time
            generator.Cost *= generator.CostRatio;
            
            // Update ResourceCosts if present
            if (generator.ResourceCosts != null && generator.ResourceCosts.Count > 0)
            {
                var resourceKeys = generator.ResourceCosts.Keys.ToList();
                foreach (var resourceKey in resourceKeys)
                {
                    generator.ResourceCosts[resourceKey] *= generator.CostRatio;
                }
            }
            
            UpdateResourceTotalProduction();
        }
        else if (upgrade.SourceItem is Research research)
        {
            // Apply research multiplier directly to BaseProduction and Resources of affected generators
            foreach (var generatorName in research.TargetGenerators)
            {
                var targetGenerator = Generators.FirstOrDefault(g => g.Name == generatorName);
                if (targetGenerator != null)
                {
                    // Apply to all resources
                    if (targetGenerator.Resources != null && targetGenerator.Resources.Count > 0)
                    {
                        var resourceKeys = targetGenerator.Resources.Keys.ToList();
                        foreach (var resourceKey in resourceKeys)
                        {
                            targetGenerator.Resources[resourceKey] *= research.GetMultiplier();
                        }
                    }
                    // Also update BaseProduction for consistency
                    targetGenerator.BaseProduction *= research.GetMultiplier();
                }
            }
            
            // Remove the research from the list since it's been applied
            Research.Remove(research);
            UpdateResourceTotalProduction();
        }
        
        await SaveStateAsync();
    }

    public async Task SaveStateAsync()
    {
        // Save to local storage first
        await _localStorage.SaveGeneratorsAsync(Generators);
        await _localStorage.SaveResearchAsync(Research);
        await _localStorage.SaveResourcesAsync(Resources);
        
        // Save to cloud through API if user ID is set
        try
        {
            if (await _localStorage.HasUserIdAsync())
            {
                await _localStorage.SyncToCloudAsync(Generators, Research, Resources);
            }
        }
        catch (Exception ex)
        {
            // Log error but don't throw - local save succeeded
            Console.WriteLine($"Error saving to cloud: {ex.Message}");
        }
    }

    public async Task LoadStateAsync()
    {
        // Try to load from cloud first if user ID is set
        if (await _localStorage.HasUserIdAsync())
        {
            var cloudData = await _localStorage.SyncFromCloudAsync();
            if (cloudData != null)
            {
                // Check if we have local data to compare timestamps
                var localGenerators = await _localStorage.LoadGeneratorsAsync();
                var localResearch = await _localStorage.LoadResearchAsync();
                var localResources = await _localStorage.LoadResourcesAsync();
                
                // For now, use cloud data if available (can be enhanced with timestamp comparison)
                Generators = cloudData.Generators;
                Research = cloudData.Research;
                Resources = cloudData.Resources;
                
                // Save cloud data to local storage
                await _localStorage.SaveGeneratorsAsync(Generators);
                await _localStorage.SaveResearchAsync(Research);
                await _localStorage.SaveResourcesAsync(Resources);
                
                return;
            }
        }
        
        // Fallback to local load
        Generators = await _localStorage.LoadGeneratorsAsync();
        Research = await _localStorage.LoadResearchAsync();
        Resources = await _localStorage.LoadResourcesAsync();
    }

    public async Task ClearAllAsync()
    {
        Generators.Clear();
        Research.Clear();
        Resources.Clear();
        await _localStorage.ClearAllAsync();
    }
}