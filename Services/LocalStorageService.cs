using Blazored.LocalStorage;
using IdleOptimizer.Models;

namespace IdleOptimizer.Services;

public class LocalStorageService : ILocalStorageService
{
    private const string GeneratorsKey = "generators";
    private const string ResearchKey = "research";
    private readonly Blazored.LocalStorage.ILocalStorageService _localStorage;

    public LocalStorageService(Blazored.LocalStorage.ILocalStorageService localStorage)
    {
        _localStorage = localStorage;
    }

    public async Task SaveGeneratorsAsync(List<Generator> generators)
    {
        try
        {
            await _localStorage.SetItemAsync(GeneratorsKey, generators);
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
            await _localStorage.SetItemAsync(ResearchKey, research);
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
            return await _localStorage.GetItemAsync<List<Generator>>(GeneratorsKey) ?? new List<Generator>();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error loading generators: {ex.Message}");
            return new List<Generator>();
        }
    }

    public async Task<List<Research>> LoadResearchAsync()
    {
        try
        {
            return await _localStorage.GetItemAsync<List<Research>>(ResearchKey) ?? new List<Research>();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error loading research: {ex.Message}");
            return new List<Research>();
        }
    }

    public async Task ClearAllAsync()
    {
        try
        {
            await _localStorage.RemoveItemAsync(GeneratorsKey);
            await _localStorage.RemoveItemAsync(ResearchKey);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error clearing storage: {ex.Message}");
        }
    }
}

