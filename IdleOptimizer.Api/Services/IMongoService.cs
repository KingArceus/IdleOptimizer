using IdleOptimizer.Api.Models;

namespace IdleOptimizer.Api.Services;

public interface IMongoService
{
    Task SaveSyncDataAsync(SyncData data);
    Task<SyncData?> LoadSyncDataAsync(string userId);
}

