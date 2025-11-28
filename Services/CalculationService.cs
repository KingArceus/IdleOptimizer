using IdleOptimizer.Models;

namespace IdleOptimizer.Services;

public class CalculationService : ICalculationService
{
    private readonly ILocalStorageService _localStorage;
    private double _valueScoreMultiplier = 1.0;
    private double _lastTotalProduction = 0;
    
    public List<Generator> Generators { get; private set; } = new();
    public List<Research> Research { get; private set; } = new();

    public CalculationService(ILocalStorageService localStorage)
    {
        _localStorage = localStorage;
    }

    public async Task InitializeAsync()
    {
        await LoadStateAsync();
                
        // Initialize total production and calculate multiplier
        _lastTotalProduction = GetTotalProduction();
        RecalculateValueScoreMultiplier();
    }

    private double CalculateTotalProduction()
    {
        double total = 0;
        
        foreach (var generator in Generators)
        {
            double multiplier = 1.0;
            
            // Apply all research multipliers that target this generator
            foreach (var research in Research.Where(r => r.IsPurchased && r.TargetGenerators.Contains(generator.Name)))
            {
                multiplier *= research.GetMultiplier();
            }
            
            total += generator.GetCurrentProduction() * multiplier;
        }
        
        return total;
    }

    public double GetTotalProduction()
    {
        double total = CalculateTotalProduction();
        
        // Recalculate value score multiplier if total production changed
        if (Math.Abs(total - _lastTotalProduction) > 0.001)
        {
            _lastTotalProduction = total;
            RecalculateValueScoreMultiplier();
        }
        
        return total;
    }

    private void RecalculateValueScoreMultiplier()
    {
        // Get all upgrade results without multiplier applied
        var results = new List<UpgradeResult>();
        
        foreach (var generator in Generators)
        {
            var result = EvaluateGeneratorPurchaseInternal(generator);
            results.Add(result);
        }
        
        foreach (var research in Research)
        {
            var result = EvaluateResearchPurchaseInternal(research);
            results.Add(result);
        }
        
        // Filter out purchased research
        var validResults = results
            .Where(r => r.Type != "Research" || !((Research)r.SourceItem!).IsPurchased)
            .Where(r => r.ValueScore > 0)
            .ToList();
        
        if (validResults.Count == 0)
        {
            _valueScoreMultiplier = 1.0;
            return;
        }
        
        // Find the highest value score
        double maxValueScore = validResults.Max(r => r.ValueScore);
        
        // If max value score is less than 1, calculate multiplier to scale it to at least 1
        if (maxValueScore > 0 && maxValueScore < 1.0)
        {
            _valueScoreMultiplier = 1.0 / maxValueScore;
        }
        else
        {
            _valueScoreMultiplier = 1.0;
        }
    }

    private UpgradeResult EvaluateGeneratorPurchaseInternal(Generator g)
    {
        double currentTotal = CalculateTotalProduction();
        double cost = g.GetPurchaseCost();
        int purchaseAmount = 10;
        
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
            double multiplier = 1.0;
            foreach (var research in Research.Where(r => r.IsPurchased && r.TargetGenerators.Contains(generator.Name)))
            {
                multiplier *= research.GetMultiplier();
            }
            
            if (generator.Name == g.Name)
            {
                newTotal += tempGenerator.GetCurrentProduction() * multiplier;
            }
            else
            {
                newTotal += generator.GetCurrentProduction() * multiplier;
            }
        }
        
        double gain = newTotal - currentTotal;
        double valueScore = cost > 0 ? gain / cost : 0;
        
        return new UpgradeResult
        {
            ItemName = g.Name,
            Type = "Generator",
            Cost = cost,
            Gain = gain,
            ValueScore = valueScore,
            SourceItem = g
        };
    }

    private UpgradeResult EvaluateResearchPurchaseInternal(Research r)
    {
        if (r.IsPurchased)
        {
            return new UpgradeResult
            {
                ItemName = r.Name,
                Type = "Research",
                Cost = r.Cost,
                Gain = 0,
                ValueScore = 0,
                TargetGenerators = string.Join(", ", r.TargetGenerators),
                SourceItem = r
            };
        }
        
        double currentTotal = CalculateTotalProduction();
        
        double newTotal = 0;
        foreach (var generator in Generators)
        {
            double multiplier = 1.0;
            
            foreach (var existingResearch in Research.Where(res => res.IsPurchased && res.TargetGenerators.Contains(generator.Name)))
            {
                multiplier *= existingResearch.GetMultiplier();
            }
            
            if (r.TargetGenerators.Contains(generator.Name))
            {
                multiplier *= r.GetMultiplier();
            }
            
            newTotal += generator.GetCurrentProduction() * multiplier;
        }
        
        double gain = newTotal - currentTotal;
        double valueScore = r.Cost > 0 ? gain / r.Cost : 0;
        
        return new UpgradeResult
        {
            ItemName = r.Name,
            Type = "Research",
            Cost = r.Cost,
            Gain = gain,
            ValueScore = valueScore,
            TargetGenerators = string.Join(", ", r.TargetGenerators),
            SourceItem = r
        };
    }

    public UpgradeResult EvaluateGeneratorPurchase(Generator g)
    {
        var result = EvaluateGeneratorPurchaseInternal(g);
        // Apply the value score multiplier
        result.ValueScore *= _valueScoreMultiplier;
        return result;
    }

    public UpgradeResult EvaluateResearchPurchase(Research r)
    {
        var result = EvaluateResearchPurchaseInternal(r);
        // Apply the value score multiplier
        result.ValueScore *= _valueScoreMultiplier;
        return result;
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
        
        // Filter out purchased research and sort by ValueScore descending
        return results
            .Where(r => r.Type != "Research" || !((Research)r.SourceItem!).IsPurchased)
            .OrderByDescending(r => r.ValueScore)
            .ToList();
    }

    public async Task AppliedPurchaseAsync(UpgradeResult upgrade)
    {
        if (upgrade.SourceItem is Generator generator)
        {
            generator.Count += 10; // Purchase 10 units at a time
        }
        else if (upgrade.SourceItem is Research research)
        {
            research.IsPurchased = true;
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
}

