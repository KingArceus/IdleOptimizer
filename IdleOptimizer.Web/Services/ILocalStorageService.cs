using IdleOptimizer.Models;

namespace IdleOptimizer.Services;

public interface ILocalStorageService
{
    Task SaveGeneratorsAsync(List<Generator> generators);
    Task SaveResearchAsync(List<Research> research);
    Task SaveResourcesAsync(List<Resource> resources);
    Task<List<Generator>> LoadGeneratorsAsync();
    Task<List<Research>> LoadResearchAsync();
    Task<List<Resource>> LoadResourcesAsync();
    Task ClearAllAsync();
    Task ExportGeneratorsToFileAsync(List<Generator> generators);
    Task ExportResearchToFileAsync(List<Research> research);
    Task ImportGeneratorsFromFileAsync(string csvContent);
    Task ImportResearchFromFileAsync(string csvContent);
    
    // User ID management
    Task<string?> GetUserIdAsync();
    Task SetUserIdAsync(string userId);
    Task<bool> HasUserIdAsync();
    Task<bool> CheckUserExistsAsync(string userId);
    
    // Cloud sync methods
    Task SyncToCloudAsync(List<Generator> generators, List<Research> research, List<Resource> resources);
    Task<SyncData?> SyncFromCloudAsync();
}

