using IdleOptimizer.Models;

namespace IdleOptimizer.Services;

public class CalculationService(
    ILocalStorageService localStorage,
    INumberFormattingService numberFormatting,
    IProductionService productionService,
    IValuationService valuationService,
    IUpgradeEvaluationService upgradeEvaluationService) : ICalculationService
{
    private readonly ILocalStorageService _localStorage = localStorage;
    private readonly INumberFormattingService _numberFormatting = numberFormatting;
    private readonly IProductionService _productionService = productionService;
    private readonly IValuationService _valuationService = valuationService;
    private readonly IUpgradeEvaluationService _upgradeEvaluationService = upgradeEvaluationService;
    private double _lastTotalProduction = 0;
    private Dictionary<string, double> _previousBottleneckWeights = [];
    
    public List<Generator> Generators { get; private set; } = [];
    public List<Research> Research { get; private set; } = [];
    public List<Resource> Resources { get; private set; } = [];

    public async Task InitializeAsync()
    {
        await LoadStateAsync();
        UpdateResourceTotalProduction();
                
        // Initialize total production
        _lastTotalProduction = GetTotalProduction();
    }

    public double ApplyAbbreviation(double value, string? abbr)
    {
        return _numberFormatting.ApplyAbbreviation(value, abbr);
    }

    public double GetTotalProduction()
    {
        double total = _productionService.GetTotalProduction(Generators);
        
        // Update last total production if changed
        if (Math.Abs(total - _lastTotalProduction) > 0.001)
        {
            _lastTotalProduction = total;
        }
        
        return total;
    }

    public Dictionary<string, double> GetTotalProductionByResource()
    {
        return _productionService.GetTotalProductionByResource(Generators);
    }

    public double GetTotalProductionByResourceName(string resourceName)
    {
        return _productionService.GetTotalProductionByResourceName(Generators, resourceName);
    }

    public void UpdateResourceTotalProduction()
    {
        var productionByResource = GetTotalProductionByResource();
        
        // Update existing resources
        foreach (var resource in Resources)
        {
            double totalProduction = productionByResource.TryGetValue(resource.Name, out double production) 
                ? production : 0;
            resource.UpdateTotalProduction(totalProduction);
        }
        
        // Add resources that exist in production but not in Resources list
        foreach (var resourceName in productionByResource.Keys)
        {
            if (Resources.All(r => r.Name != resourceName))
            {
                var newResource = new Resource { Name = resourceName };
                newResource.UpdateTotalProduction(productionByResource[resourceName]);
                Resources.Add(newResource);
            }
        }
    }

    public Resource? GetResource(string name)
    {
        return Resources.FirstOrDefault(r => r.Name == name);
    }

    public void AddResource(Resource resource)
    {
        if (Resources.All(r => r.Name != resource.Name))
        {
            Resources.Add(resource);
            UpdateResourceTotalProduction();
        }
    }

    public void RemoveResource(Resource resource)
    {
        Resources.Remove(resource);
        UpdateResourceTotalProduction();
    }

    public List<UpgradeResult> GetRankedUpgrades()
    {
        // Evaluate unlock status first
        EvaluateUnlockStatus();
        
        // Calculate resource valuations and bottleneck weights once for current state
        var productionByResource = GetTotalProductionByResource();
        var resourceValues = _valuationService.CalculateResourceValuations(Generators, Research, productionByResource);
        var bottleneckWeights = _valuationService.CalculateBottleneckWeights(Generators, Research, productionByResource, _previousBottleneckWeights);
        
        // Store for next calculation
        _previousBottleneckWeights = new Dictionary<string, double>(bottleneckWeights);
        
        var results = new List<UpgradeResult>();
        
        // Evaluate all generators (only unlocked ones)
        foreach (var generator in Generators)
        {
            if (generator.IsUnlocked)
            {
                results.Add(_upgradeEvaluationService.EvaluateGeneratorPurchase(
                    generator, Generators, resourceValues, bottleneckWeights, productionByResource));
            }
        }
        
        // Evaluate all research (filter out already applied and locked)
        foreach (var research in Research)
        {
            if (!research.IsApplied && research.IsUnlocked)
            {
                results.Add(_upgradeEvaluationService.EvaluateResearchPurchase(
                    research, Generators, Research, resourceValues, bottleneckWeights, productionByResource));
            }
        }
        
        // Sort by CascadeScore descending
        return [.. results.OrderByDescending(r => r.CascadeScore)];
    }

    private void EvaluateUnlockStatus()
    {
        // Evaluate Generator unlocks
        foreach (var generator in Generators)
        {
            bool isUnlocked = true;
            
            // Check required generators
            if (generator.RequiredGenerators != null && generator.RequiredGenerators.Count > 0)
            {
                foreach (var requiredGenName in generator.RequiredGenerators)
                {
                    var requiredGen = Generators.FirstOrDefault(g => g.Name == requiredGenName);
                    if (requiredGen == null || requiredGen.Count == 0)
                    {
                        isUnlocked = false;
                        break;
                    }
                }
            }
            
            // Check required research
            if (isUnlocked && generator.RequiredResearch != null && generator.RequiredResearch.Count > 0)
            {
                foreach (var requiredResName in generator.RequiredResearch)
                {
                    var requiredRes = Research.FirstOrDefault(r => r.Name == requiredResName);
                    if (requiredRes == null || !requiredRes.IsApplied)
                    {
                        isUnlocked = false;
                        break;
                    }
                }
            }
            
            generator.IsUnlocked = isUnlocked;
        }
        
        // Evaluate Research unlocks
        foreach (var research in Research)
        {
            bool isUnlocked = true;
            
            // Check required generators
            if (research.RequiredGenerators != null && research.RequiredGenerators.Count > 0)
            {
                foreach (var requiredGenName in research.RequiredGenerators)
                {
                    var requiredGen = Generators.FirstOrDefault(g => g.Name == requiredGenName);
                    if (requiredGen == null || requiredGen.Count == 0)
                    {
                        isUnlocked = false;
                        break;
                    }
                }
            }
            
            // Check required research
            if (isUnlocked && research.RequiredResearch != null && research.RequiredResearch.Count > 0)
            {
                foreach (var requiredResName in research.RequiredResearch)
                {
                    var requiredRes = Research.FirstOrDefault(r => r.Name == requiredResName);
                    if (requiredRes == null || !requiredRes.IsApplied)
                    {
                        isUnlocked = false;
                        break;
                    }
                }
            }
            
            research.IsUnlocked = isUnlocked;
        }
    }

    public void AppliedPurchase(UpgradeResult upgrade)
    {
        if (upgrade.SourceItem is Generator generator)
        {
            generator.Count += 1; // Purchase 1 unit at a time
            generator.Cost *= generator.CostRatio;
            
            // Update ResourceCosts if present
            if (generator.ResourceCosts != null && generator.ResourceCosts.Count > 0)
            {
                var resourceKeys = generator.ResourceCosts.Keys.ToList();
                foreach (var resourceKey in resourceKeys)
                {
                    generator.ResourceCosts[resourceKey] *= generator.CostRatio;
                }
            }
            
            UpdateResourceTotalProduction();
        }
        else if (upgrade.SourceItem is Research research)
        {
            // Apply research multiplier to Resources (not BaseResources) of affected generators
            foreach (var generatorName in research.TargetGenerators)
            {
                var targetGenerator = Generators.FirstOrDefault(g => g.Name == generatorName);
                if (targetGenerator != null)
                {
                    // Get the multiplier for this specific generator
                    double multiplier = research.GetMultiplier(generatorName);
                    
                    // Apply to all resources
                    if (targetGenerator.Resources != null && targetGenerator.Resources.Count > 0)
                    {
                        var resourceKeys = targetGenerator.Resources.Keys.ToList();
                        foreach (var resourceKey in resourceKeys)
                        {
                            targetGenerator.Resources[resourceKey] *= multiplier;
                        }
                    }
                    // BaseProduction remains unchanged (it's calculated from BaseResources, not Resources)
                }
            }
            
            // Mark research as applied instead of removing it
            research.IsApplied = true;
            UpdateResourceTotalProduction();
        }
        
        // Re-evaluate unlock status after purchases
        EvaluateUnlockStatus();

        return;
    }

    public async Task SaveStateAsync()
    {
        // Save to local storage first
        await _localStorage.SaveGeneratorsAsync(Generators);
        await _localStorage.SaveResearchAsync(Research);
        await _localStorage.SaveResourcesAsync(Resources);
        
        // Save to cloud through API if user ID is set
        try
        {
            if (await _localStorage.HasUserIdAsync())
            {
                await _localStorage.SyncToCloudAsync(Generators, Research, Resources);
            }
        }
        catch (Exception ex)
        {
            // Log error but don't throw - local save succeeded
            Console.WriteLine($"Error saving to cloud: {ex.Message}");
        }
    }

    public async Task LoadStateAsync()
    {
        // Try to load from cloud first if user ID is set
        if (await _localStorage.HasUserIdAsync())
        {
            var cloudData = await _localStorage.SyncFromCloudAsync();
            if (cloudData != null)
            {
                // Check if we have local data to compare timestamps
                _ = await _localStorage.LoadGeneratorsAsync();
                _ = await _localStorage.LoadResearchAsync();
                _ = await _localStorage.LoadResourcesAsync();
                
                // For now, use cloud data if available (can be enhanced with timestamp comparison)

                Generators = cloudData.Generators;
                Research = cloudData.Research;
                Resources = cloudData.Resources;
                
                // Save cloud data to local storage
                await _localStorage.SaveGeneratorsAsync(Generators);
                await _localStorage.SaveResearchAsync(Research);
                await _localStorage.SaveResourcesAsync(Resources);
                
                // Migrate data for existing generators and research
                MigrateData();
                return;
            }
        }
        
        // Fallback to local load
        Generators = await _localStorage.LoadGeneratorsAsync();
        Research = await _localStorage.LoadResearchAsync();
        Resources = await _localStorage.LoadResourcesAsync();
        
        // Migrate data for existing generators and research
        MigrateData();
    }
    
    private void MigrateData()
    {
        // Initialize BaseResources and BaseResourceCosts for existing generators
        foreach (var generator in Generators)
        {
            // Initialize BaseResources if not set
            if (generator.BaseResources == null || generator.BaseResources.Count == 0)
            {
                if (generator.Resources != null && generator.Resources.Count > 0)
                {
                    generator.BaseResources = new Dictionary<string, double>(generator.Resources);
                }
                else
                {
                    generator.BaseResources = [];
                }
            }
            
            // Initialize BaseResourceCosts if not set
            if (generator.BaseResourceCosts == null || generator.BaseResourceCosts.Count == 0)
            {
                if (generator.ResourceCosts != null && generator.ResourceCosts.Count > 0)
                {
                    generator.BaseResourceCosts = new Dictionary<string, double>(generator.ResourceCosts);
                }
                else
                {
                    generator.BaseResourceCosts = [];
                }
            }
        }
        
        // Initialize IsApplied for existing research
        foreach (var research in Research)
        {
            if (!research.IsApplied)
            {
                research.IsApplied = false; // Explicitly set default
            }
            
            // Initialize RequiredGenerators and RequiredResearch if null
            if (research.RequiredGenerators == null)
            {
                research.RequiredGenerators = [];
            }
            if (research.RequiredResearch == null)
            {
                research.RequiredResearch = [];
            }
            // IsUnlocked defaults to true, will be evaluated when GetRankedUpgrades() is called
        }
        
        // Initialize RequiredGenerators and RequiredResearch for existing generators
        foreach (var generator in Generators)
        {
            generator.RequiredGenerators ??= [];
            generator.RequiredResearch ??= [];
            // IsUnlocked defaults to true, will be evaluated when GetRankedUpgrades() is called
        }
    }
    
    public async Task PerformPrestigeAsync(double productionMultiplier, double costMultiplier)
    {
        // Reset all Research IsApplied to false
        foreach (var research in Research)
        {
            research.IsApplied = false;
            
            // Multiply Research costs by cost multiplier
            if (research.ResourceCosts != null && research.ResourceCosts.Count > 0)
            {
                var resourceKeys = research.ResourceCosts.Keys.ToList();
                foreach (var resourceKey in resourceKeys)
                {
                    research.ResourceCosts[resourceKey] *= costMultiplier;
                }
            }
        }
        
        // Process each Generator
        foreach (var generator in Generators)
        {
            // Reset Count to 0
            generator.Count = 0;
            
            // Multiply BaseResources by production multiplier
            if (generator.BaseResources != null && generator.BaseResources.Count > 0)
            {
                var resourceKeys = generator.BaseResources.Keys.ToList();
                foreach (var resourceKey in resourceKeys)
                {
                    generator.BaseResources[resourceKey] *= productionMultiplier;
                }
            }
            
            // Multiply BaseResourceCosts by cost multiplier
            if (generator.BaseResourceCosts != null && generator.BaseResourceCosts.Count > 0)
            {
                var costKeys = generator.BaseResourceCosts.Keys.ToList();
                foreach (var costKey in costKeys)
                {
                    generator.BaseResourceCosts[costKey] *= costMultiplier;
                }
            }
            
            // Recalculate BaseProduction from BaseResources
            if (generator.BaseResources != null && generator.BaseResources.Count > 0)
            {
                generator.BaseProduction = generator.BaseResources.Values.Sum();
            }
            
            // Recalculate Cost from BaseResourceCosts
            if (generator.BaseResourceCosts != null && generator.BaseResourceCosts.Count > 0)
            {
                generator.Cost = generator.BaseResourceCosts.Values.Sum();
            }
            
            // Reset Resources to a copy of the new BaseResources
            if (generator.BaseResources != null && generator.BaseResources.Count > 0)
            {
                generator.Resources = new Dictionary<string, double>(generator.BaseResources);
            }
            else
            {
                generator.Resources = [];
            }
            
            // Reset ResourceCosts to a copy of the new BaseResourceCosts
            if (generator.BaseResourceCosts != null && generator.BaseResourceCosts.Count > 0)
            {
                generator.ResourceCosts = new Dictionary<string, double>(generator.BaseResourceCosts);
            }
            else
            {
                generator.ResourceCosts = [];
            }
        }
        
        UpdateResourceTotalProduction();
        await SaveStateAsync();
    }

    public async Task ClearAllAsync()
    {
        Generators.Clear();
        Research.Clear();
        Resources.Clear();
        await _localStorage.ClearAllAsync();
    }
}