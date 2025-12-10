namespace IdleOptimizer.Api.Models;

public class SyncData
{
    public string UserId { get; set; } = string.Empty;
    public List<Generator> Generators { get; set; } = new();
    public List<Research> Research { get; set; } = new();
    public List<Resource> Resources { get; set; } = new();
    public DateTime LastModified { get; set; }
}

