using IdleOptimizer.Models;

namespace IdleOptimizer.Services;

public interface IValuationService
{
    Dictionary<string, double> CalculateResourceValuations(
        List<Generator> generators, 
        List<Research> research, 
        Dictionary<string, double> productionByResource);
    
    Dictionary<string, double> CalculateBottleneckWeights(
        List<Generator> generators, 
        List<Research> research, 
        Dictionary<string, double> productionByResource, 
        Dictionary<string, double>? previousWeights);
    
    string? IdentifyBottleneckResource(
        Dictionary<string, double> resourceCosts, 
        Dictionary<string, double> productionByResource);
}