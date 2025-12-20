using System.Text.Json;
using IdleOptimizer.Api.Models;
using MongoDB.Driver;

namespace IdleOptimizer.Api.Services;

public class MongoService : IMongoService
{
    private readonly IMongoCollection<SyncData> _collection;
    private readonly ILogger<MongoService> _logger;
    private const string DatabaseName = "idleoptimizer";
    private const string CollectionName = "syncdata";

    public MongoService(IConfiguration configuration, ILogger<MongoService> logger)
    {
        // Configuration system automatically reads from appsettings.json and environment variables
        // Environment variables with double underscores (MongoDB__ConnectionString) are automatically mapped to nested config
        var connectionString = configuration.GetValue<string>("MongoDB:ConnectionString") 
            ?? "mongodb://localhost:27017";

        var client = new MongoClient(connectionString);
        var database = client.GetDatabase(DatabaseName);
        _collection = database.GetCollection<SyncData>(CollectionName);
        _logger = logger;
        // Note: Since UserId is now the _id field, we don't need a separate index
        // MongoDB automatically indexes the _id field
    }

    public async Task SaveSyncDataAsync(SyncData data)
    {
        if (string.IsNullOrWhiteSpace(data.UserId))
        {
            throw new ArgumentException("UserId cannot be null or empty", nameof(data));
        }

        data.LastModified = DateTime.UtcNow;

        // Use ReplaceOne with upsert to either update existing or insert new
        var filter = Builders<SyncData>.Filter.Eq(x => x.UserId, data.UserId);
        await _collection.ReplaceOneAsync(filter, data, new ReplaceOptions { IsUpsert = true });
    }

    public async Task<SyncData?> LoadSyncDataAsync(string userId)
    {
        if (string.IsNullOrWhiteSpace(userId))
        {
            throw new ArgumentException("UserId cannot be null or empty", nameof(userId));
        }

        var filter = Builders<SyncData>.Filter.Eq(x => x.UserId, userId);
        return await _collection.Find(filter).FirstOrDefaultAsync();
    }

    public async Task<List<string>> GetAllUserIdsAsync()
    {
        try
        {
            // Since UserId is the _id field, we can use Distinct to get all unique user IDs
            // This is more efficient than projecting all documents
            var userIds = await _collection.DistinctAsync<string>("_id", FilterDefinition<SyncData>.Empty);
            return userIds.ToList().OrderBy(id => id).ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving all user IDs");
            return [];
        }
    }
}

