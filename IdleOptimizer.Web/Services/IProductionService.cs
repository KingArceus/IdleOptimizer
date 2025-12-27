using IdleOptimizer.Models;

namespace IdleOptimizer.Services;

public interface IProductionService
{
    double GetTotalProduction(List<Generator> generators);
    Dictionary<string, double> GetTotalProductionByResource(List<Generator> generators);
    double GetTotalProductionByResourceName(List<Generator> generators, string resourceName);
}