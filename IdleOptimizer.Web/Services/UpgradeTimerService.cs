using IdleOptimizer.Models;

namespace IdleOptimizer.Services;

public class UpgradeTimerService(ICalculationService calculationService) : IUpgradeTimerService
{
    private readonly ICalculationService _calculationService = calculationService;
    private readonly Dictionary<string, DateTime> _timerStartTimes = [];
    private Timer? _timer;
    private double _lastTotalProduction = 0;
    private const double ProductionPerSecond = 1.0; // Production rate per second

    public event EventHandler? TimerTick;

    public void StartTimer()
    {
        if (_timer == null)
        {
            _lastTotalProduction = _calculationService.GetTotalProduction();
            _timer = new Timer(TimerCallback, null, TimeSpan.Zero, TimeSpan.FromSeconds(1));
        }
    }

    public void StopTimer()
    {
        _timer?.Dispose();
        _timer = null;
    }

    public void ClearCache()
    {
        _timerStartTimes.Clear();
    }

    private void TimerCallback(object? state)
    {
        // Check if total production changed
        var currentProduction = _calculationService.GetTotalProduction();
        if (Math.Abs(currentProduction - _lastTotalProduction) > 0.001)
        {
            _lastTotalProduction = currentProduction;
            // Clear timer cache to force recalculation
            _timerStartTimes.Clear();
        }
        
        // Raise event for components to handle UI updates
        TimerTick?.Invoke(this, EventArgs.Empty);
    }

    public int GetTimeToAfford(UpgradeResult item)
    {
        double timeNeeded;

        // Check if item has resource costs
        if (item.ResourceCosts != null && item.ResourceCosts.Count > 0)
        {
            // Multiple resource costs - find the bottleneck (longest time to afford)
            var productionByResource = _calculationService.GetTotalProductionByResource();
            double maxTime = 0;
            
            foreach (var cost in item.ResourceCosts)
            {
                if (productionByResource.ContainsKey(cost.Key))
                {
                    double resourceProduction = productionByResource[cost.Key];
                    if (resourceProduction > 0)
                    {
                        double resourceTime = cost.Value / resourceProduction;
                        if (resourceTime > maxTime)
                        {
                            maxTime = resourceTime;
                        }
                    }
                    else
                    {
                        // Resource not being produced - can't afford
                        return int.MaxValue;
                    }
                }
                else
                {
                    // Resource not being produced - can't afford
                    return int.MaxValue;
                }
            }
            
            timeNeeded = maxTime;
        }
        else
        {
            var totalProduction = _calculationService.GetTotalProduction();
            
            // If no production, return a large number
            if (totalProduction <= 0)
            {
                return int.MaxValue;
            }
            
            // Calculate production per second (assuming total production is per second)
            var productionPerSecond = totalProduction * ProductionPerSecond;
            
            // Calculate time needed to afford this item (in seconds)
            timeNeeded = item.Cost / productionPerSecond;
        }
        
        // Get the timer key for this item
        var timerKey = $"{item.Type}_{item.ItemName}";
        
        // Check if we need to recalculate (production changed or timer expired)
        var needsRecalculation = !_timerStartTimes.ContainsKey(timerKey);
        
        if (!needsRecalculation)
        {
            var endTime = _timerStartTimes[timerKey];
            _ = (DateTime.Now - endTime.AddSeconds(-timeNeeded)).TotalSeconds;

            // Recalculate time needed with current production
            double currentTimeNeeded = double.MaxValue;
            if (item.ResourceCosts != null && item.ResourceCosts.Count > 0)
            {
                var productionByResource = _calculationService.GetTotalProductionByResource();
                double maxTime = 0;
                
                foreach (var cost in item.ResourceCosts)
                {
                    if (productionByResource.ContainsKey(cost.Key))
                    {
                        double resourceProduction = productionByResource[cost.Key];
                        if (resourceProduction > 0)
                        {
                            double resourceTime = cost.Value / resourceProduction;
                            if (resourceTime > maxTime)
                            {
                                maxTime = resourceTime;
                            }
                        }
                        else
                        {
                            currentTimeNeeded = double.MaxValue;
                            break;
                        }
                    }
                    else
                    {
                        currentTimeNeeded = double.MaxValue;
                        break;
                    }
                }
                
                if (currentTimeNeeded != double.MaxValue)
                {
                    currentTimeNeeded = maxTime;
                }
            }
            else
            {
                var totalProduction = _calculationService.GetTotalProduction();
                if (totalProduction > 0)
                {
                    var productionPerSecond = totalProduction * ProductionPerSecond;
                    currentTimeNeeded = item.Cost / productionPerSecond;
                }
            }
            
            // Recalculate if production changed significantly (more than 1% difference)
            if (currentTimeNeeded != double.MaxValue && timeNeeded != double.MaxValue &&
                Math.Abs(currentTimeNeeded - timeNeeded) / Math.Max(currentTimeNeeded, 1) > 0.01)
            {
                needsRecalculation = true;
            }
        }
        
        if (needsRecalculation)
        {
            // Recalculate based on current production
            if (item.AvailableAt.HasValue)
            {
                _timerStartTimes[timerKey] = item.AvailableAt.Value;
            }
        }
        
        if (!item.AvailableAt.HasValue)
        {
            return int.MaxValue; // Can't afford
        }
        
        var remaining = (int)(item.AvailableAt.Value - DateTime.Now).TotalSeconds;
        return Math.Max(0, remaining);
    }

    public string FormatTime(int seconds)
    {
        if (seconds < 0) return "0s";
        int years = seconds / (365 * 24 * 60 * 60);
        int remaining = seconds % (365 * 24 * 60 * 60);

        int weeks = remaining / (7 * 24 * 60 * 60);
        remaining %= 7 * 24 * 60 * 60;

        int days = remaining / (24 * 60 * 60);
        remaining %= 24 * 60 * 60;

        int hours = remaining / (60 * 60);
        remaining %= 60 * 60;

        int minutes = remaining / 60;
        int secs = remaining % 60;

        var parts = new List<string>();
        if (years > 0) parts.Add($"{years}y");
        if (weeks > 0) parts.Add($"{weeks}w");
        if (days > 0) parts.Add($"{days}d");
        if (hours > 0) parts.Add($"{hours}h");
        if (minutes > 0) parts.Add($"{minutes}m");
        if (secs > 0 || parts.Count == 0) parts.Add($"{secs}s");
        
        return string.Join(" ", parts);
    }

    public void Dispose()
    {
        StopTimer();
    }
}