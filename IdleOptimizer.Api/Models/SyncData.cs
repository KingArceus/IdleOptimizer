using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace IdleOptimizer.Api.Models;

public class SyncData
{
    [BsonId]
    [BsonRepresentation(BsonType.String)]
    public string UserId { get; set; } = string.Empty;
    
    [BsonElement("generators")]
    public List<Generator> Generators { get; set; } = [];
    
    [BsonElement("research")]
    public List<Research> Research { get; set; } = [];
    
    [BsonElement("resources")]
    public List<Resource> Resources { get; set; } = [];
    
    [BsonElement("lastModified")]
    public DateTime LastModified { get; set; }
}

