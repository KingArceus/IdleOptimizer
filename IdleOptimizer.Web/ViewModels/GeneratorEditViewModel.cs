using IdleOptimizer.Models;
using IdleOptimizer.Services;

namespace IdleOptimizer.ViewModels;

public class GeneratorEditViewModel
{
    public string Name { get; set; } = string.Empty;
    public int Count { get; set; }
    public double CostRatio { get; set; }
    public List<ResourceEntryViewModel> Resources { get; set; } = [];
    public List<ResourceCostEntryViewModel> ResourceCosts { get; set; } = [];
    public List<string> RequiredGenerators { get; set; } = [];
    public List<string> RequiredResearch { get; set; } = [];

    public static GeneratorEditViewModel FromGenerator(Generator generator, INumberFormattingService formattingService)
    {
        var viewModel = new GeneratorEditViewModel
        {
            Name = generator.Name,
            Count = generator.Count,
            CostRatio = generator.CostRatio > 0 ? generator.CostRatio : 1.15,
            RequiredGenerators = generator.RequiredGenerators?.ToList() ?? [],
            RequiredResearch = generator.RequiredResearch?.ToList() ?? []
        };

        // Convert Resources
        if (generator.Resources != null && generator.Resources.Count > 0)
        {
            foreach (var resource in generator.Resources)
            {
                var (baseValue, abbr) = formattingService.ParseAbbreviation(resource.Value);
                viewModel.Resources.Add(new ResourceEntryViewModel
                {
                    ResourceName = resource.Key,
                    Value = baseValue,
                    Abbr = abbr
                });
            }
        }

        // Convert ResourceCosts
        if (generator.ResourceCosts != null && generator.ResourceCosts.Count > 0)
        {
            foreach (var cost in generator.ResourceCosts)
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

        return viewModel;
    }

    public Generator ToGenerator(INumberFormattingService formattingService)
    {
        var generator = new Generator
        {
            Name = Name,
            Count = Count,
            CostRatio = CostRatio > 0 ? CostRatio : 1.15,
            RequiredGenerators = RequiredGenerators?.ToList() ?? [],
            RequiredResearch = RequiredResearch?.ToList() ?? []
        };

        // Convert ResourceCosts
        generator.ResourceCosts = [];
        foreach (var cost in ResourceCosts)
        {
            if (!string.IsNullOrWhiteSpace(cost.ResourceName) && cost.Value > 0)
            {
                double costValue = formattingService.ApplyAbbreviation(cost.Value, cost.Abbr);
                generator.ResourceCosts[cost.ResourceName] = costValue;
            }
        }
        generator.Cost = generator.ResourceCosts.Count > 0 
            ? generator.ResourceCosts.Values.Sum() 
            : 0;

        // Convert Resources
        generator.Resources = [];
        foreach (var resource in Resources)
        {
            if (!string.IsNullOrWhiteSpace(resource.ResourceName) && resource.Value > 0)
            {
                double productionValue = formattingService.ApplyAbbreviation(resource.Value, resource.Abbr);
                generator.Resources[resource.ResourceName] = productionValue;
            }
        }
        generator.BaseProduction = generator.Resources.Count > 0 
            ? generator.Resources.Values.Sum() 
            : 0;

        // Initialize BaseResources as a copy of Resources
        generator.BaseResources = generator.Resources.Count > 0
            ? new Dictionary<string, double>(generator.Resources)
            : [];

        // Initialize BaseResourceCosts as a copy of ResourceCosts
        generator.BaseResourceCosts = generator.ResourceCosts.Count > 0
            ? new Dictionary<string, double>(generator.ResourceCosts)
            : [];

        return generator;
    }
}