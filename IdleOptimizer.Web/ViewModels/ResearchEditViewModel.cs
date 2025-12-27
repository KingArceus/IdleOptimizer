using IdleOptimizer.Models;
using IdleOptimizer.Services;

namespace IdleOptimizer.ViewModels;

public class ResearchEditViewModel
{
    public string Name { get; set; } = string.Empty;
    public List<TargetMultiplierViewModel> TargetMultipliers { get; set; } = [];
    public List<ResourceCostEntryViewModel> ResourceCosts { get; set; } = [];
    public List<string> RequiredGenerators { get; set; } = [];
    public List<string> RequiredResearch { get; set; } = [];

    public static ResearchEditViewModel FromResearch(Research research, INumberFormattingService formattingService)
    {
        var viewModel = new ResearchEditViewModel
        {
            Name = research.Name,
            RequiredGenerators = research.RequiredGenerators?.ToList() ?? [],
            RequiredResearch = research.RequiredResearch?.ToList() ?? []
        };

        // Convert ResourceCosts
        if (research.ResourceCosts != null && research.ResourceCosts.Count > 0)
        {
            foreach (var cost in research.ResourceCosts)
            {
                var (baseValue, abbr) = formattingService.ParseAbbreviation(cost.Value);
                viewModel.ResourceCosts.Add(new ResourceCostEntryViewModel
                {
                    ResourceName = cost.Key,
                    Value = baseValue,
                    Abbr = abbr
                });
            }
        }

        // Convert TargetMultipliers
        if (research.TargetMultipliers != null && research.TargetMultipliers.Count > 0)
        {
            foreach (var multiplier in research.TargetMultipliers)
            {
                viewModel.TargetMultipliers.Add(new TargetMultiplierViewModel
                {
                    GeneratorName = multiplier.Key,
                    MultiplierValue = multiplier.Value
                });
            }
        }

        return viewModel;
    }

    public Research ToResearch(ICalculationService calculationService, INumberFormattingService formattingService)
    {
        var research = new Research
        {
            Name = Name,
            RequiredGenerators = RequiredGenerators?.ToList() ?? [],
            RequiredResearch = RequiredResearch?.ToList() ?? []
        };

        // Convert ResourceCosts
        research.ResourceCosts = [];
        foreach (var cost in ResourceCosts)
        {
            if (!string.IsNullOrWhiteSpace(cost.ResourceName) && cost.Value > 0)
            {
                double costValue = formattingService.ApplyAbbreviation(cost.Value, cost.Abbr);
                research.ResourceCosts[cost.ResourceName] = costValue;
            }
        }
        research.Cost = research.ResourceCosts.Count > 0 
            ? research.ResourceCosts.Values.Sum() 
            : 0;

        // Convert TargetMultipliers
        research.TargetGenerators = [];
        research.TargetMultipliers = [];

        foreach (var target in TargetMultipliers)
        {
            if (string.IsNullOrWhiteSpace(target.GeneratorName) || target.MultiplierValue <= 0)
                continue;

            if (target.GeneratorName == "None")
            {
                if (!research.TargetGenerators.Contains("None"))
                {
                    research.TargetGenerators.Add("None");
                }
                research.TargetMultipliers["None"] = target.MultiplierValue;
            }
            else if (target.GeneratorName == "All")
            {
                foreach (var generator in calculationService.Generators)
                {
                    if (!research.TargetGenerators.Contains(generator.Name))
                    {
                        research.TargetGenerators.Add(generator.Name);
                    }
                    research.TargetMultipliers[generator.Name] = target.MultiplierValue;
                }
            }
            else
            {
                if (!research.TargetGenerators.Contains(target.GeneratorName))
                {
                    research.TargetGenerators.Add(target.GeneratorName);
                }
                research.TargetMultipliers[target.GeneratorName] = target.MultiplierValue;
            }
        }

        return research;
    }
}