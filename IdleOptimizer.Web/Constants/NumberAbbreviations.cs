namespace IdleOptimizer.Constants;

public static class NumberAbbreviations
{
    /// <summary>
    /// Dictionary mapping abbreviation strings to their multiplier values
    /// </summary>
    public static readonly Dictionary<string, double> Values = new()
    {
        ["K"] = 1e3,
        ["M"] = 1e6,
        ["B"] = 1e9,
        ["T"] = 1e12,
        ["Qa"] = 1e15,
        ["Qi"] = 1e18,
        ["Sx"] = 1e21,
        ["Sp"] = 1e24,
        ["Oc"] = 1e27,
        ["No"] = 1e30,
        ["Dc"] = 1e33,
        ["Udc"] = 1e36,
        ["Ddc"] = 1e39
    };

    /// <summary>
    /// Ordered list of abbreviations from smallest to largest, for UI dropdowns
    /// </summary>
    public static readonly List<string> OrderedList = [.. Values.Keys];

    /// <summary>
    /// Ordered list of abbreviation thresholds for parsing, from largest to smallest
    /// </summary>
    public static readonly List<(double Threshold, string Abbreviation)> ParseThresholds = 
        [.. Values.Select(x => (x.Value, x.Key)).OrderByDescending(x => x.Value)];
}