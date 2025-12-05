using IdleOptimizer.Models;
using Microsoft.JSInterop;
using System.Globalization;
using System.Text;

namespace IdleOptimizer.Services;

public class LocalStorageService(IJSRuntime jsRuntime) : ILocalStorageService
{
    private const string GeneratorsFileName = "generators.csv";
    private const string ResearchFileName = "research.csv";
    private const string ResourcesFileName = "resources.csv";
    private readonly IJSRuntime _jsRuntime = jsRuntime;
    private List<Generator> _cachedGenerators = [];
    private List<Research> _cachedResearch = [];
    private List<Resource> _cachedResources = [];

    public async Task SaveGeneratorsAsync(List<Generator> generators)
    {
        try
        {
            _cachedGenerators = generators;
            var csv = ConvertGeneratorsToCsv(generators);
            await SaveCsvFile(GeneratorsFileName, csv);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error saving generators: {ex.Message}");
        }
    }

    public async Task SaveResearchAsync(List<Research> research)
    {
        try
        {
            _cachedResearch = research;
            var csv = ConvertResearchToCsv(research);
            await SaveCsvFile(ResearchFileName, csv);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error saving research: {ex.Message}");
        }
    }

    public async Task<List<Generator>> LoadGeneratorsAsync()
    {
        try
        {
            var csv = await LoadCsvFile(GeneratorsFileName);
            if (string.IsNullOrEmpty(csv))
            {
                return _cachedGenerators;
            }
            _cachedGenerators = ParseGeneratorsFromCsv(csv);
            return _cachedGenerators;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error loading generators: {ex.Message}");
            return _cachedGenerators;
        }
    }

    public async Task<List<Research>> LoadResearchAsync()
    {
        try
        {
            var csv = await LoadCsvFile(ResearchFileName);
            if (string.IsNullOrEmpty(csv))
            {
                return _cachedResearch;
            }
            _cachedResearch = ParseResearchFromCsv(csv);
            return _cachedResearch;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error loading research: {ex.Message}");
            return _cachedResearch;
        }
    }

    public async Task SaveResourcesAsync(List<Resource> resources)
    {
        try
        {
            _cachedResources = resources;
            var csv = ConvertResourcesToCsv(resources);
            await SaveCsvFile(ResourcesFileName, csv);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error saving resources: {ex.Message}");
        }
    }

    public async Task<List<Resource>> LoadResourcesAsync()
    {
        try
        {
            var csv = await LoadCsvFile(ResourcesFileName);
            if (string.IsNullOrEmpty(csv))
            {
                return _cachedResources;
            }
            _cachedResources = ParseResourcesFromCsv(csv);
            return _cachedResources;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error loading resources: {ex.Message}");
            return _cachedResources;
        }
    }

    public async Task ClearAllAsync()
    {
        try
        {
            _cachedGenerators.Clear();
            _cachedResearch.Clear();
            _cachedResources.Clear();
            await DeleteCsvFile(GeneratorsFileName);
            await DeleteCsvFile(ResearchFileName);
            await DeleteCsvFile(ResourcesFileName);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error clearing storage: {ex.Message}");
        }
    }

    private string ConvertGeneratorsToCsv(List<Generator> generators)
    {
        var sb = new StringBuilder();
        sb.AppendLine("Name,BaseProduction,Resources,Count,Cost,ResourceCosts,CostRatio");
        
        foreach (var generator in generators)
        {
            // Convert resources to semicolon-separated "Name:Value" pairs
            string resourcesStr = string.Empty;
            if (generator.Resources != null && generator.Resources.Count > 0)
            {
                var resourcePairs = generator.Resources.Select(r => $"{EscapeCsvField(r.Key)}:{r.Value.ToString(CultureInfo.InvariantCulture)}");
                resourcesStr = string.Join(";", resourcePairs);
            }
            
            // Convert resource costs to semicolon-separated "Name:Value" pairs
            string resourceCostsStr = string.Empty;
            if (generator.ResourceCosts != null && generator.ResourceCosts.Count > 0)
            {
                var costPairs = generator.ResourceCosts.Select(r => $"{EscapeCsvField(r.Key)}:{r.Value.ToString(CultureInfo.InvariantCulture)}");
                resourceCostsStr = string.Join(";", costPairs);
            }
            
            sb.AppendLine($"{EscapeCsvField(generator.Name)},{generator.BaseProduction.ToString(CultureInfo.InvariantCulture)},{EscapeCsvField(resourcesStr)},{generator.Count},{generator.Cost.ToString(CultureInfo.InvariantCulture)},{EscapeCsvField(resourceCostsStr)},{generator.CostRatio.ToString(CultureInfo.InvariantCulture)}");
        }
        
        return sb.ToString();
    }

    private string ConvertResearchToCsv(List<Research> research)
    {
        var sb = new StringBuilder();
        sb.AppendLine("Name,MultiplierValue,Cost,ResourceCosts,TargetGenerators");
        
        foreach (var item in research)
        {
            // Convert resource costs to semicolon-separated "Name:Value" pairs
            string resourceCostsStr = string.Empty;
            if (item.ResourceCosts != null && item.ResourceCosts.Count > 0)
            {
                var costPairs = item.ResourceCosts.Select(r => $"{EscapeCsvField(r.Key)}:{r.Value.ToString(CultureInfo.InvariantCulture)}");
                resourceCostsStr = string.Join(";", costPairs);
            }
            
            var targetGenerators = string.Join(";", item.TargetGenerators);
            sb.AppendLine($"{EscapeCsvField(item.Name)},{item.MultiplierValue.ToString(CultureInfo.InvariantCulture)},{item.Cost.ToString(CultureInfo.InvariantCulture)},{EscapeCsvField(resourceCostsStr)},{EscapeCsvField(targetGenerators)}");
        }
        
        return sb.ToString();
    }

    private List<Generator> ParseGeneratorsFromCsv(string csv)
    {
        var generators = new List<Generator>();
        var lines = csv.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        
        if (lines.Length < 2) return generators; // Need at least header + 1 data line
        
        for (int i = 1; i < lines.Length; i++) // Skip header
        {
            var line = lines[i].Trim();
            if (string.IsNullOrEmpty(line)) continue;
            
            var fields = ParseCsvLine(line);
            if (fields.Count < 7) continue; // Skip invalid lines
            
            var generator = new Generator();
            
            // Format: Name,BaseProduction,Resources,Count,Cost,ResourceCosts,CostRatio
            generator.Name = UnescapeCsvField(fields[0]);
            generator.BaseProduction = double.TryParse(fields[1], NumberStyles.Float, CultureInfo.InvariantCulture, out var bp) ? bp : 0;
            
            // Parse resources
            var resourcesStr = UnescapeCsvField(fields[2]);
            if (!string.IsNullOrEmpty(resourcesStr))
            {
                generator.Resources = new Dictionary<string, double>();
                var resourcePairs = resourcesStr.Split(';', StringSplitOptions.RemoveEmptyEntries);
                foreach (var pair in resourcePairs)
                {
                    var parts = pair.Split(':', 2);
                    if (parts.Length == 2 && double.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out var value))
                    {
                        generator.Resources[parts[0]] = value;
                    }
                }
            }
            
            generator.Count = int.TryParse(fields[3], NumberStyles.Integer, CultureInfo.InvariantCulture, out var count) ? count : 0;
            generator.Cost = double.TryParse(fields[4], NumberStyles.Float, CultureInfo.InvariantCulture, out var cost) ? cost : 0;
            
            // Parse resource costs - always initialize the dictionary
            generator.ResourceCosts = new Dictionary<string, double>();
            var resourceCostsStr = UnescapeCsvField(fields[5]);
            if (!string.IsNullOrEmpty(resourceCostsStr))
            {
                var costPairs = resourceCostsStr.Split(';', StringSplitOptions.RemoveEmptyEntries);
                foreach (var pair in costPairs)
                {
                    var parts = pair.Split(':', 2);
                    if (parts.Length == 2 && double.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out var value))
                    {
                        generator.ResourceCosts[parts[0]] = value;
                    }
                }
            }
            
            generator.CostRatio = double.TryParse(fields[6], NumberStyles.Float, CultureInfo.InvariantCulture, out var ratio) ? ratio : 0;
            
            generators.Add(generator);
        }
        
        return generators;
    }

    private List<Research> ParseResearchFromCsv(string csv)
    {
        var research = new List<Research>();
        var lines = csv.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        
        if (lines.Length < 2) return research; // Need at least header + 1 data line
        
        for (int i = 1; i < lines.Length; i++) // Skip header
        {
            var line = lines[i].Trim();
            if (string.IsNullOrEmpty(line)) continue;
            
            var fields = ParseCsvLine(line);
            if (fields.Count < 5) continue; // Skip invalid lines
            
            var researchItem = new Research();
            
            // Format: Name,MultiplierValue,Cost,ResourceCosts,TargetGenerators
            researchItem.Name = UnescapeCsvField(fields[0]);
            researchItem.MultiplierValue = double.TryParse(fields[1], NumberStyles.Float, CultureInfo.InvariantCulture, out var mv) ? mv : 0;
            researchItem.Cost = double.TryParse(fields[2], NumberStyles.Float, CultureInfo.InvariantCulture, out var cost) ? cost : 0;
            
            // Parse resource costs - always initialize the dictionary
            researchItem.ResourceCosts = new Dictionary<string, double>();
            var resourceCostsStr = UnescapeCsvField(fields[3]);
            if (!string.IsNullOrEmpty(resourceCostsStr))
            {
                var costPairs = resourceCostsStr.Split(';', StringSplitOptions.RemoveEmptyEntries);
                foreach (var pair in costPairs)
                {
                    var parts = pair.Split(':', 2);
                    if (parts.Length == 2 && double.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out var value))
                    {
                        researchItem.ResourceCosts[parts[0]] = value;
                    }
                }
            }
            
            var targetGenerators = UnescapeCsvField(fields[4])
                .Split(';', StringSplitOptions.RemoveEmptyEntries)
                .ToList();
            researchItem.TargetGenerators = targetGenerators;
            
            research.Add(researchItem);
        }
        
        return research;
    }

    private string ConvertResourcesToCsv(List<Resource> resources)
    {
        var sb = new StringBuilder();
        sb.AppendLine("Name");
        
        foreach (var resource in resources)
        {
            sb.AppendLine($"{EscapeCsvField(resource.Name)}");
        }
        
        return sb.ToString();
    }

    private List<Resource> ParseResourcesFromCsv(string csv)
    {
        var resources = new List<Resource>();
        var lines = csv.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        
        if (lines.Length < 2) return resources; // Need at least header + 1 data line
        
        for (int i = 1; i < lines.Length; i++) // Skip header
        {
            var line = lines[i].Trim();
            if (string.IsNullOrEmpty(line)) continue;
            
            var fields = ParseCsvLine(line);
            if (fields.Count >= 1)
            {
                resources.Add(new Resource
                {
                    Name = UnescapeCsvField(fields[0])
                });
            }
        }
        
        return resources;
    }

    private List<string> ParseCsvLine(string line)
    {
        var fields = new List<string>();
        var currentField = new StringBuilder();
        bool inQuotes = false;
        
        for (int i = 0; i < line.Length; i++)
        {
            char c = line[i];
            
            if (c == '"')
            {
                if (inQuotes && i + 1 < line.Length && line[i + 1] == '"')
                {
                    // Escaped quote
                    currentField.Append('"');
                    i++; // Skip next quote
                }
                else
                {
                    // Toggle quote state
                    inQuotes = !inQuotes;
                }
            }
            else if (c == ',' && !inQuotes)
            {
                fields.Add(currentField.ToString());
                currentField.Clear();
            }
            else
            {
                currentField.Append(c);
            }
        }
        
        fields.Add(currentField.ToString()); // Add last field
        return fields;
    }

    private string EscapeCsvField(string field)
    {
        if (string.IsNullOrEmpty(field)) return string.Empty;
        
        // If field contains comma, quote, or newline, wrap in quotes and escape quotes
        if (field.Contains(',') || field.Contains('"') || field.Contains('\n') || field.Contains('\r'))
        {
            return $"\"{field.Replace("\"", "\"\"")}\"";
        }
        
        return field;
    }

    private string UnescapeCsvField(string field)
    {
        if (string.IsNullOrEmpty(field)) return string.Empty;
        
        // Remove surrounding quotes if present
        if (field.StartsWith('"') && field.EndsWith('"'))
        {
            field = field.Substring(1, field.Length - 2);
            field = field.Replace("\"\"", "\"");
        }
        
        return field;
    }

    private async Task SaveCsvFile(string fileName, string csvContent)
    {
        // Store in localStorage for persistence
        await _jsRuntime.InvokeVoidAsync("csvStorage.saveCsvToStorage", fileName, csvContent);
    }

    private async Task<string> LoadCsvFile(string fileName)
    {
        try
        {
            return await _jsRuntime.InvokeAsync<string>("csvStorage.loadCsvFromStorage", fileName) ?? string.Empty;
        }
        catch
        {
            return string.Empty;
        }
    }

    private async Task DeleteCsvFile(string fileName)
    {
        try
        {
            await _jsRuntime.InvokeVoidAsync("csvStorage.deleteCsvFile", fileName);
        }
        catch
        {
            // Ignore errors when deleting
        }
    }

    // Export methods for manual file download
    public async Task ExportGeneratorsToFileAsync(List<Generator> generators)
    {
        var csv = ConvertGeneratorsToCsv(generators);
        await _jsRuntime.InvokeVoidAsync("csvStorage.downloadCsvFile", GeneratorsFileName, csv);
    }

    public async Task ExportResearchToFileAsync(List<Research> research)
    {
        var csv = ConvertResearchToCsv(research);
        await _jsRuntime.InvokeVoidAsync("csvStorage.downloadCsvFile", ResearchFileName, csv);
    }

    // Import methods for file upload
    public async Task ImportGeneratorsFromFileAsync(string csvContent)
    {
        await _jsRuntime.InvokeVoidAsync("csvStorage.importCsvFile", GeneratorsFileName, csvContent);
        _cachedGenerators = ParseGeneratorsFromCsv(csvContent);
    }

    public async Task ImportResearchFromFileAsync(string csvContent)
    {
        await _jsRuntime.InvokeVoidAsync("csvStorage.importCsvFile", ResearchFileName, csvContent);
        _cachedResearch = ParseResearchFromCsv(csvContent);
    }
}

