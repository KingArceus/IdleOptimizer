using IdleOptimizer.Models;

namespace IdleOptimizer.Services;

public class CalculationService(ILocalStorageService localStorage) : ICalculationService
{
    private readonly ILocalStorageService _localStorage = localStorage;
    private double _lastTotalProduction = 0;
    private const double CascadeScoreMultiplier = 10.0;
    
    public List<Generator> Generators { get; private set; } = [];
    public List<Research> Research { get; private set; } = [];

    public async Task InitializeAsync()
    {
        await LoadStateAsync();
                
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
        double total = 0;
        
        foreach (var generator in Generators)
        {
            // BaseProduction already includes all applied research multipliers
            total += generator.GetCurrentProduction();
        }
        
        return total;
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

    private double CalculateCascadeScore(UpgradeResult upgrade, double currentProductionRate, double newProductionRate)
    {
        // Get all unpurchased upgrades
        var unpurchasedUpgrades = new List<(double Cost, double WaitTime)>();
        
        // Add all generators (they're always available)
        foreach (var generator in Generators)
        {
            double cost = generator.GetPurchaseCost();
            double waitTime = currentProductionRate > 0 ? cost / currentProductionRate : double.MaxValue;
            unpurchasedUpgrades.Add((cost, waitTime));
        }
        
        // Add all research (they're available until purchased and deleted)
        foreach (var research in Research)
        {
            double cost = research.Cost;
            double waitTime = currentProductionRate > 0 ? cost / currentProductionRate : double.MaxValue;
            unpurchasedUpgrades.Add((cost, waitTime));
        }
        
        // Calculate Current Total Path Time (sum of wait times for all unpurchased upgrades)
        double currentTotalPathTime = unpurchasedUpgrades.Sum(u => u.WaitTime > 0 && !double.IsInfinity(u.WaitTime) ? u.WaitTime : 0);
        
        // Calculate Total Future Time Saved
        double totalFutureTimeSaved = 0;
        foreach (var (cost, oldWaitTime) in unpurchasedUpgrades)
        {
            if (oldWaitTime > 0 && !double.IsInfinity(oldWaitTime))
            {
                double newWaitTime = newProductionRate > 0 ? cost / newProductionRate : double.MaxValue;
                if (newWaitTime > 0 && !double.IsInfinity(newWaitTime))
                {
                    double timeSaved = oldWaitTime - newWaitTime;
                    if (timeSaved > 0)
                    {
                        totalFutureTimeSaved += timeSaved;
                    }
                }
            }
        }
        
        // Calculate WaitTime for this upgrade
        double upgradeWaitTime = currentProductionRate > 0 ? upgrade.Cost / currentProductionRate : double.MaxValue;
        
        // Calculate Cascade Score and apply multiplier to keep values in a readable range
        double cascadeScore = 0;
        if (upgradeWaitTime > 0 && !double.IsInfinity(upgradeWaitTime))
        {
            cascadeScore = totalFutureTimeSaved / upgradeWaitTime * CascadeScoreMultiplier;
        }
        
        return cascadeScore;
    }

    private UpgradeResult EvaluateGeneratorPurchase(Generator g)
    {
        double currentTotal = CalculateTotalProduction();
        double cost = g.GetPurchaseCost();
        int purchaseAmount = 1;
        
        var tempGenerator = new Generator
        {
            Name = g.Name,
            BaseProduction = g.BaseProduction,
            Count = g.Count + purchaseAmount,
            Cost = g.Cost
        };
        
        double newTotal = 0;
        foreach (var generator in Generators)
        {
            if (generator.Name == g.Name)
            {
                newTotal += tempGenerator.GetCurrentProduction();
            }
            else
            {
                newTotal += generator.GetCurrentProduction();
            }
        }
        
        double gain = newTotal - currentTotal;
        
        // Calculate time-based efficiency metrics
        double currentProductionPerSecond = currentTotal;
        double newProductionPerSecond = newTotal;
        
        double timeToAfford = currentProductionPerSecond > 0 
            ? cost / currentProductionPerSecond 
            : double.MaxValue;
        
        double extraProduction = newProductionPerSecond - currentProductionPerSecond;
        double timeToPayback = extraProduction > 0 
            ? cost / extraProduction 
            : double.MaxValue;
        
        // Calculate cascade score
        double cascadeScore = CalculateCascadeScore(
            new UpgradeResult { Cost = cost, Gain = gain },
            currentProductionPerSecond,
            newProductionPerSecond
        );
        
        // Safely calculate AvailableAt, handling cases where production is zero or very small
        double totalProduction = GetTotalProduction();
        double secondsToAdd = totalProduction > 0 ? cost / totalProduction : double.MaxValue;
        
        // Clamp to a safe maximum value (approximately 100 years in seconds)
        const double maxSafeSeconds = 100.0 * 365.25 * 24 * 60 * 60; // ~3,155,760,000 seconds
        if (double.IsInfinity(secondsToAdd) || double.IsNaN(secondsToAdd) || secondsToAdd > maxSafeSeconds)
        {
            secondsToAdd = maxSafeSeconds;
        }
        
        return new UpgradeResult
        {
            ItemName = g.Name,
            Type = "Generator",
            Cost = cost,
            Gain = gain,
            TimeToAfford = timeToAfford,
            TimeToPayback = timeToPayback,
            CascadeScore = cascadeScore,
            SourceItem = g,
            AvailableAt = DateTime.Now.AddSeconds(secondsToAdd)
        };
    }

    private UpgradeResult EvaluateResearchPurchase(Research r)
    {
        double currentTotal = CalculateTotalProduction();
        
        // Calculate new total by applying the research multiplier to affected generators' BaseProduction
        double newTotal = 0;
        foreach (var generator in Generators)
        {
            if (r.TargetGenerators.Contains(generator.Name))
            {
                // Apply multiplier to BaseProduction for affected generators
                double newBaseProduction = generator.BaseProduction * r.GetMultiplier();
                newTotal += newBaseProduction * generator.Count;
            }
            else
            {
                newTotal += generator.GetCurrentProduction();
            }
        }
        
        double gain = newTotal - currentTotal;
        
        // Calculate time-based efficiency metrics
        double currentProductionPerSecond = currentTotal;
        double newProductionPerSecond = newTotal;
        double timeToAfford = currentProductionPerSecond > 0 
            ? r.Cost / currentProductionPerSecond 
            : double.MaxValue;
        
        double extraProduction = newProductionPerSecond - currentProductionPerSecond;
        double timeToPayback = extraProduction > 0 
            ? r.Cost / extraProduction 
            : double.MaxValue;
        
        // Calculate cascade score
        double cascadeScore = CalculateCascadeScore(
            new UpgradeResult { Cost = r.Cost, Gain = gain },
            currentProductionPerSecond,
            newProductionPerSecond
        );
        
        // Safely calculate AvailableAt, handling cases where production is zero or very small
        double totalProduction = GetTotalProduction();
        double secondsToAdd = totalProduction > 0 ? r.Cost / totalProduction : double.MaxValue;
        
        // Clamp to a safe maximum value (approximately 100 years in seconds)
        const double maxSafeSeconds = 100.0 * 365.25 * 24 * 60 * 60; // ~3,155,760,000 seconds
        if (double.IsInfinity(secondsToAdd) || double.IsNaN(secondsToAdd) || secondsToAdd > maxSafeSeconds)
        {
            secondsToAdd = maxSafeSeconds;
        }
        
        return new UpgradeResult
        {
            ItemName = r.Name,
            Type = "Research",
            Cost = r.Cost,
            Gain = gain,
            TimeToAfford = timeToAfford,
            TimeToPayback = timeToPayback,
            CascadeScore = cascadeScore,
            TargetGenerators = string.Join(", ", r.TargetGenerators),
            SourceItem = r,
            AvailableAt = DateTime.Now.AddSeconds(secondsToAdd)
        };
    }

    public List<UpgradeResult> GetRankedUpgrades()
    {
        var results = new List<UpgradeResult>();
        
        // Evaluate all generators
        foreach (var generator in Generators)
        {
            results.Add(EvaluateGeneratorPurchase(generator));
        }
        
        // Evaluate all research
        foreach (var research in Research)
        {
            results.Add(EvaluateResearchPurchase(research));
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
        }
        else if (upgrade.SourceItem is Research research)
        {
            // Apply research multiplier directly to BaseProduction of affected generators
            foreach (var generatorName in research.TargetGenerators)
            {
                var targetGenerator = Generators.FirstOrDefault(g => g.Name == generatorName);
                if (targetGenerator != null)
                {
                    targetGenerator.BaseProduction *= research.GetMultiplier();
                }
            }
            
            // Remove the research from the list since it's been applied
            Research.Remove(research);
        }
        
        await SaveStateAsync();
    }

    public async Task SaveStateAsync()
    {
        await _localStorage.SaveGeneratorsAsync(Generators);
        await _localStorage.SaveResearchAsync(Research);
    }

    public async Task LoadStateAsync()
    {
        Generators = await _localStorage.LoadGeneratorsAsync();
        Research = await _localStorage.LoadResearchAsync();
    }

    public async Task ClearAllAsync()
    {
        Generators.Clear();
        Research.Clear();
        await _localStorage.ClearAllAsync();
    }
}