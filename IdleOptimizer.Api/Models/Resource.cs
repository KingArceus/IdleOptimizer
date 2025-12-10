using MongoDB.Bson.Serialization.Attributes;

namespace IdleOptimizer.Api.Models;

public class Resource
{
    [BsonElement("name")]
    public string Name { get; set; } = string.Empty;
    
    // TotalProduction is calculated from generators, not stored
    // Make it public setter for MongoDB deserialization, but it's typically not set from API
    [BsonElement("totalProduction")]
    public double TotalProduction { get; set; }
    
    public void UpdateTotalProduction(double totalProduction)
    {
        TotalProduction = totalProduction;
    }
}

