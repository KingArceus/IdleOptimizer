using IdleOptimizer.Models;

namespace IdleOptimizer.Models;

public class SyncData
{
    public string UserId { get; set; } = string.Empty;
    public List<Generator> Generators { get; set; } = [];
    public List<Research> Research { get; set; } = [];
    public List<Resource> Resources { get; set; } = [];
    public DateTime LastModified { get; set; }
}

