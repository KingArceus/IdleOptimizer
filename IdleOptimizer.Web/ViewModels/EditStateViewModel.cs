using IdleOptimizer.Models;

namespace IdleOptimizer.ViewModels;

public class EditStateViewModel
{
    public Dictionary<object, UpgradeResult> EditItems { get; set; } = [];
    public Dictionary<Generator, List<ResourceEntryViewModel>> EditResources { get; set; } = [];
    public Dictionary<Generator, List<ResourceCostEntryViewModel>> EditCostResources { get; set; } = [];
    public Dictionary<Research, List<ResourceCostEntryViewModel>> EditResearchCostResources { get; set; } = [];
    public Dictionary<Generator, double> EditCostRatios { get; set; } = [];
    public string? EditCostAbbr { get; set; }

    public bool IsEditing(object sourceItem)
    {
        return sourceItem != null && EditItems.ContainsKey(sourceItem);
    }

    public void ClearEditState(object sourceItem)
    {
        if (sourceItem == null) return;

        EditItems.Remove(sourceItem);

        if (sourceItem is Generator generator)
        {
            EditResources.Remove(generator);
            EditCostResources.Remove(generator);
            EditCostRatios.Remove(generator);
        }
        else if (sourceItem is Research research)
        {
            EditResearchCostResources.Remove(research);
        }
    }

    public void ClearAll()
    {
        EditItems.Clear();
        EditResources.Clear();
        EditCostResources.Clear();
        EditResearchCostResources.Clear();
        EditCostRatios.Clear();
        EditCostAbbr = null;
    }

    public void RemoveStaleItems(List<object> currentSourceItems)
    {
        var keysToRemove = EditItems.Keys.Where(k => !currentSourceItems.Contains(k)).ToList();
        foreach (var key in keysToRemove)
        {
            ClearEditState(key);
        }
    }
}