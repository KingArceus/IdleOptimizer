using IdleOptimizer.Models;
using Microsoft.JSInterop;
using System.Globalization;
using System.Text;
using System.Net.Http.Json;

namespace IdleOptimizer.Services;

public class LocalStorageService(IJSRuntime jsRuntime, HttpClient httpClient) : ILocalStorageService
{
    private const string GeneratorsFileName = "generators.csv";
    private const string ResearchFileName = "research.csv";
    private const string ResourcesFileName = "resources.csv";
    private const int DebounceDelayMs = 2000; // 2 seconds
    
    private readonly IJSRuntime _jsRuntime = jsRuntime;
    private readonly HttpClient _httpClient = httpClient;
    private CancellationTokenSource? _saveDebounceToken;
    
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

    private static string ConvertGeneratorsToCsv(List<Generator> generators)
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

    private static string ConvertResearchToCsv(List<Research> research)
    {
        var sb = new StringBuilder();
        sb.AppendLine("Name,Cost,ResourceCosts,TargetGenerators,TargetMultipliers");
        
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
            
            // Convert target multipliers to semicolon-separated "GeneratorName:Multiplier" pairs
            string targetMultipliersStr = string.Empty;
            if (item.TargetMultipliers != null && item.TargetMultipliers.Count > 0)
            {
                var multiplierPairs = item.TargetMultipliers.Select(m => $"{EscapeCsvField(m.Key)}:{m.Value.ToString(CultureInfo.InvariantCulture)}");
                targetMultipliersStr = string.Join(";", multiplierPairs);
            }
            
            sb.AppendLine($"{EscapeCsvField(item.Name)},{item.Cost.ToString(CultureInfo.InvariantCulture)},{EscapeCsvField(resourceCostsStr)},{EscapeCsvField(targetGenerators)},{EscapeCsvField(targetMultipliersStr)}");
        }
        
        return sb.ToString();
    }

    private static List<Generator> ParseGeneratorsFromCsv(string csv)
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
                generator.Resources = [];
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
            generator.ResourceCosts = [];
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

    private static List<Research> ParseResearchFromCsv(string csv)
    {
        var research = new List<Research>();
        var lines = csv.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        
        if (lines.Length < 2) return research; // Need at least header + 1 data line
        
        // Check header to determine format version
        var header = lines[0].Trim();
        bool hasTargetMultipliers = header.Contains("TargetMultipliers");
        bool hasLegacyMultiplierValue = header.Contains("MultiplierValue");
        
        for (int i = 1; i < lines.Length; i++) // Skip header
        {
            var line = lines[i].Trim();
            if (string.IsNullOrEmpty(line)) continue;
            
            var fields = ParseCsvLine(line);
            if (fields.Count < 4) continue; // Skip invalid lines
            
            var researchItem = new Research();
            
            // Format (new): Name,Cost,ResourceCosts,TargetGenerators,TargetMultipliers
            // Format (legacy): Name,MultiplierValue,Cost,ResourceCosts,TargetGenerators,TargetMultipliers
            int fieldIndex = 0;
            researchItem.Name = UnescapeCsvField(fields[fieldIndex++]);
            
            // Skip MultiplierValue if present (legacy format)
            if (hasLegacyMultiplierValue)
            {
                fieldIndex++;
            }
            
            researchItem.Cost = double.TryParse(fields[fieldIndex++], NumberStyles.Float, CultureInfo.InvariantCulture, out var cost) ? cost : 0;
            
            // Parse resource costs - always initialize the dictionary
            researchItem.ResourceCosts = [];
            var resourceCostsStr = UnescapeCsvField(fields[fieldIndex++]);
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
            
            var targetGenerators = UnescapeCsvField(fields[fieldIndex++])
                .Split(';', StringSplitOptions.RemoveEmptyEntries)
                .ToList();
            researchItem.TargetGenerators = targetGenerators;
            
            // Parse target multipliers
            researchItem.TargetMultipliers = [];
            if (hasTargetMultipliers && fields.Count > fieldIndex)
            {
                var targetMultipliersStr = UnescapeCsvField(fields[fieldIndex]);
                if (!string.IsNullOrEmpty(targetMultipliersStr))
                {
                    var multiplierPairs = targetMultipliersStr.Split(';', StringSplitOptions.RemoveEmptyEntries);
                    foreach (var pair in multiplierPairs)
                    {
                        var parts = pair.Split(':', 2);
                        if (parts.Length == 2 && double.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out var multiplier))
                        {
                            researchItem.TargetMultipliers[parts[0]] = multiplier;
                        }
                    }
                }
            }
            
            research.Add(researchItem);
        }
        
        return research;
    }

    private static string ConvertResourcesToCsv(List<Resource> resources)
    {
        var sb = new StringBuilder();
        sb.AppendLine("Name");
        
        foreach (var resource in resources)
        {
            sb.AppendLine($"{EscapeCsvField(resource.Name)}");
        }
        
        return sb.ToString();
    }

    private static List<Resource> ParseResourcesFromCsv(string csv)
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

    private static List<string> ParseCsvLine(string line)
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

    private static string EscapeCsvField(string field)
    {
        if (string.IsNullOrEmpty(field)) return string.Empty;
        
        // If field contains comma, quote, or newline, wrap in quotes and escape quotes
        if (field.Contains(',') || field.Contains('"') || field.Contains('\n') || field.Contains('\r'))
        {
            return $"\"{field.Replace("\"", "\"\"")}\"";
        }
        
        return field;
    }

    private static string UnescapeCsvField(string field)
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

    // User ID management
    public async Task<string?> GetUserIdAsync()
    {
        try
        {
            return await _jsRuntime.InvokeAsync<string>("csvStorage.getUserId");
        }
        catch
        {
            return null;
        }
    }

    public async Task SetUserIdAsync(string userId)
    {
        try
        {
            await _jsRuntime.InvokeVoidAsync("csvStorage.setUserId", userId);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error setting user ID: {ex.Message}");
        }
    }

    public async Task<bool> HasUserIdAsync()
    {
        try
        {
            return await _jsRuntime.InvokeAsync<bool>("csvStorage.hasUserId");
        }
        catch
        {
            return false;
        }
    }

    public async Task<bool> CheckUserExistsAsync(string userId)
    {
        try
        {
            if (string.IsNullOrEmpty(userId))
            {
                return false;
            }

            var response = await _httpClient.GetAsync($"/api/sync/load?userId={Uri.EscapeDataString(userId)}");
            
            // If user exists, we get OK (200), if not, we get NotFound (404)
            return response.StatusCode == System.Net.HttpStatusCode.OK;
        }
        catch
        {
            // On error, assume user doesn't exist to be safe
            return false;
        }
    }

    public async Task<List<string>> GetAllUserIdsAsync()
    {
        try
        {
            var response = await _httpClient.GetAsync("/api/sync/users");
            response.EnsureSuccessStatusCode();
            var userIds = await response.Content.ReadFromJsonAsync<List<string>>();
            return userIds ?? [];
        }
        catch (Exception ex)
        {
            // Log error, return empty list on failure
            Console.WriteLine($"Error retrieving all user IDs: {ex.Message}");
            return [];
        }
    }

    // Cloud sync methods
    public async Task SyncToCloudAsync(List<Generator> generators, List<Research> research, List<Resource> resources)
    {
        try
        {
            var userId = await GetUserIdAsync();
            if (string.IsNullOrEmpty(userId))
            {
                // No user ID set, skip sync
                return;
            }

            var syncData = new SyncData
            {
                UserId = userId,
                Generators = generators,
                Research = research,
                Resources = resources,
                LastModified = DateTime.UtcNow
            };
            var response = await _httpClient.PostAsJsonAsync("/api/sync/save", syncData);
            response.EnsureSuccessStatusCode();
        }
        catch (Exception ex)
        {
            // Log error, don't throw
            Console.WriteLine($"Sync error: {ex.Message}");
        }
    }

    public async Task<SyncData?> SyncFromCloudAsync()
    {
        try
        {
            var userId = await GetUserIdAsync();
            if (string.IsNullOrEmpty(userId))
            {
                // No user ID set, return null
                return null;
            }

            var response = await _httpClient.GetAsync($"/api/sync/load?userId={Uri.EscapeDataString(userId)}");

            if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                return null; // No data exists for this user
            }

            response.EnsureSuccessStatusCode();
            var syncData = await response.Content.ReadFromJsonAsync<SyncData>();
            return syncData;
        }
        catch (Exception ex)
        {
            // Log error, don't throw
            Console.WriteLine($"Sync load error: {ex.Message}");
            return null;
        }
    }

    // Debounced auto-save with cloud sync
    public async Task SaveStateWithAutoSaveAsync(List<Generator> generators, List<Research> research, List<Resource> resources)
    {
        // Cancel previous debounce
        _saveDebounceToken?.Cancel();
        _saveDebounceToken = new CancellationTokenSource();

        // Save locally immediately
        await SaveGeneratorsAsync(generators);
        await SaveResearchAsync(research);
        await SaveResourcesAsync(resources);

        // Debounce cloud sync
        try
        {
            await Task.Delay(DebounceDelayMs, _saveDebounceToken.Token);
            await SyncToCloudAsync(generators, research, resources);
        }
        catch (OperationCanceledException)
        {
            // Debounce was cancelled, ignore
        }
    }
}