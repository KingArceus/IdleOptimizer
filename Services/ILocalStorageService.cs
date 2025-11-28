using IdleOptimizer.Models;

namespace IdleOptimizer.Services;

public interface ILocalStorageService
{
    Task SaveGeneratorsAsync(List<Generator> generators);
    Task SaveResearchAsync(List<Research> research);
    Task<List<Generator>> LoadGeneratorsAsync();
    Task<List<Research>> LoadResearchAsync();
    Task ClearAllAsync();
}

