using IdleOptimizer.Constants;

namespace IdleOptimizer.Services;

public class NumberFormattingService : INumberFormattingService
{
    public double ApplyAbbreviation(double value, string? abbr)
    {
        if (string.IsNullOrEmpty(abbr))
            return value;

        return NumberAbbreviations.Values.TryGetValue(abbr, out double multiplier)
            ? value * multiplier
            : value;
    }

    public string FormatNumber(double value, int decimals = 2)
    {
        if (double.IsNaN(value) || double.IsInfinity(value))
            return "0";

        double absValue = Math.Abs(value);
        string sign = value < 0 ? "-" : "";
        string format = $"F{decimals}";

        // Use the parse thresholds in reverse order (largest to smallest) to find the appropriate abbreviation
        foreach (var (threshold, abbreviation) in NumberAbbreviations.ParseThresholds)
        {
            if (absValue >= threshold)
            {
                return $"{sign}{(absValue / threshold).ToString(format)}{abbreviation}";
            }
        }

        // No abbreviation needed
        return $"{sign}{absValue.ToString(format)}";
    }

    public (double baseValue, string? abbr) ParseAbbreviation(double value)
    {
        if (value == 0)
            return (0, null);

        double absValue = Math.Abs(value);

        // Find the largest threshold that the value is greater than or equal to
        // Iterate from largest to smallest to find the first match
        foreach (var (threshold, abbreviation) in NumberAbbreviations.ParseThresholds)
        {
            if (absValue >= threshold)
            {
                // Calculate the next threshold (1000x larger)
                double nextThreshold = threshold * 1000;
                
                // Check if value is in the valid range for this abbreviation
                // The value should be >= threshold and < nextThreshold
                // Also check remainder to ensure it's a reasonable multiple
                double remainder = absValue % threshold;
                double maxRemainder = threshold; // Allow remainder up to the threshold itself
                
                if (absValue < nextThreshold && remainder < maxRemainder)
                {
                    return (absValue / threshold, abbreviation);
                }
            }
        }

        // No abbreviation found - value is less than the smallest threshold (1e3)
        return (value, null);
    }

    public IEnumerable<string> GetAbbreviations()
    {
        return NumberAbbreviations.OrderedList;
    }
}