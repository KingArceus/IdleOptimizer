using IdleOptimizer.Models;
using Microsoft.JSInterop;
using System.Globalization;
using System.Text;

namespace IdleOptimizer.Services;

public class LocalStorageService(IJSRuntime jsRuntime) : ILocalStorageService
{
    private const string GeneratorsFileName = "generators.csv";
    private const string ResearchFileName = "research.csv";
    private readonly IJSRuntime _jsRuntime = jsRuntime;
    private List<Generator> _cachedGenerators = [];
    private List<Research> _cachedResearch = [];

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

    public async Task ClearAllAsync()
    {
        try
        {
            _cachedGenerators.Clear();
            _cachedResearch.Clear();
            await DeleteCsvFile(GeneratorsFileName);
            await DeleteCsvFile(ResearchFileName);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error clearing storage: {ex.Message}");
        }
    }

    private string ConvertGeneratorsToCsv(List<Generator> generators)
    {
        var sb = new StringBuilder();
        sb.AppendLine("Name,BaseProduction,Count,Cost,CostRatio");
        
        foreach (var generator in generators)
        {
            sb.AppendLine($"{EscapeCsvField(generator.Name)},{generator.BaseProduction.ToString(CultureInfo.InvariantCulture)},{generator.Count},{generator.Cost.ToString(CultureInfo.InvariantCulture)},{generator.CostRatio.ToString(CultureInfo.InvariantCulture)}");
        }
        
        return sb.ToString();
    }

    private string ConvertResearchToCsv(List<Research> research)
    {
        var sb = new StringBuilder();
        sb.AppendLine("Name,MultiplierValue,Cost,TargetGenerators");
        
        foreach (var item in research)
        {
            var targetGenerators = string.Join(";", item.TargetGenerators);
            sb.AppendLine($"{EscapeCsvField(item.Name)},{item.MultiplierValue.ToString(CultureInfo.InvariantCulture)},{item.Cost.ToString(CultureInfo.InvariantCulture)},{EscapeCsvField(targetGenerators)}");
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
            if (fields.Count >= 5)
            {
                generators.Add(new Generator
                {
                    Name = UnescapeCsvField(fields[0]),
                    BaseProduction = double.TryParse(fields[1], NumberStyles.Float, CultureInfo.InvariantCulture, out var bp) ? bp : 0,
                    Count = int.TryParse(fields[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out var count) ? count : 0,
                    Cost = double.TryParse(fields[3], NumberStyles.Float, CultureInfo.InvariantCulture, out var cost) ? cost : 0,
                    CostRatio = double.TryParse(fields[4], NumberStyles.Float, CultureInfo.InvariantCulture, out var ratio) ? ratio : 0
                });
            }
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
            if (fields.Count >= 4)
            {
                var targetGenerators = UnescapeCsvField(fields[3])
                    .Split(';', StringSplitOptions.RemoveEmptyEntries)
                    .ToList();
                
                research.Add(new Research
                {
                    Name = UnescapeCsvField(fields[0]),
                    MultiplierValue = double.TryParse(fields[1], NumberStyles.Float, CultureInfo.InvariantCulture, out var mv) ? mv : 0,
                    Cost = double.TryParse(fields[2], NumberStyles.Float, CultureInfo.InvariantCulture, out var cost) ? cost : 0,
                    TargetGenerators = targetGenerators
                });
            }
        }
        
        return research;
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

