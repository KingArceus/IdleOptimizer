using IdleOptimizer.Models;

namespace IdleOptimizer.Services;

public class ProductionService : IProductionService
{
    public double GetTotalProduction(List<Generator> generators)
    {
        // Do not sum different resources - they are separate
        // Only return a value if all generators use the same single resource
        var productionByResource = GetTotalProductionByResource(generators);
        
        // If there's only one resource type, return its production
        if (productionByResource.Count == 1)
        {
            return productionByResource.Values.First();
        }
        
        // If there are multiple different resources, return 0 (cannot sum different resource types)
        return 0;
    }

    public Dictionary<string, double> GetTotalProductionByResource(List<Generator> generators)
    {
        var result = new Dictionary<string, double>();
        
        foreach (var generator in generators)
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

    public double GetTotalProductionByResourceName(List<Generator> generators, string resourceName)
    {
        var productionByResource = GetTotalProductionByResource(generators);
        return productionByResource.ContainsKey(resourceName) ? productionByResource[resourceName] : 0;
    }
}