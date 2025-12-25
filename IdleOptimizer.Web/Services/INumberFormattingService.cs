namespace IdleOptimizer.Services;

public interface INumberFormattingService
{
    /// <summary>
    /// Applies an abbreviation multiplier to a value
    /// </summary>
    /// <param name="value">The base value</param>
    /// <param name="abbr">The abbreviation (K, M, B, etc.) or null for no multiplier</param>
    /// <returns>The value multiplied by the abbreviation multiplier</returns>
    double ApplyAbbreviation(double value, string? abbr);

    /// <summary>
    /// Formats a number with appropriate abbreviation
    /// </summary>
    /// <param name="value">The value to format</param>
    /// <param name="decimals">Number of decimal places (default: 2)</param>
    /// <returns>Formatted string with abbreviation (e.g., "1.5M", "2.3K")</returns>
    string FormatNumber(double value, int decimals = 2);

    /// <summary>
    /// Parses a value to determine its base value and abbreviation
    /// </summary>
    /// <param name="value">The value to parse</param>
    /// <returns>Tuple containing the base value and abbreviation (or null if no abbreviation applies)</returns>
    (double baseValue, string? abbr) ParseAbbreviation(double value);

    /// <summary>
    /// Gets the ordered list of abbreviations for UI dropdowns
    /// </summary>
    /// <returns>Ordered list of abbreviation strings</returns>
    IEnumerable<string> GetAbbreviations();
}

