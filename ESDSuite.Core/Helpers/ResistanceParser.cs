using System.Globalization;
using System.Text.RegularExpressions;

namespace ESDSuite.Core.Helpers;

public static class ResistanceParser
{
    public static double? ParseResistance(string? input)
    {
        if (string.IsNullOrWhiteSpace(input))
            return null;

        string clean = input.Trim().Replace(",", ".");
        
        // Handle scientific notation like 3.5x10^7 or 3.5e7
        var match = Regex.Match(clean, @"^([0-9.]+)\s*(?:x\s*10\^|e|\*10\^)\s*([0-9-]+)$", RegexOptions.IgnoreCase);
        if (match.Success)
        {
            if (double.TryParse(match.Groups[1].Value, NumberStyles.Any, CultureInfo.InvariantCulture, out double mantissa) &&
                int.TryParse(match.Groups[2].Value, out int exponent))
            {
                return mantissa * Math.Pow(10, exponent);
            }
        }

        if (double.TryParse(clean, NumberStyles.Any, CultureInfo.InvariantCulture, out double val))
        {
            return val;
        }

        return null;
    }

    public static string EvaluateStatus(string category, double val)
    {
        if (!Constants.EsdConstants.InfoElementosEsd.TryGetValue(category, out var info))
        {
            return "PASSED";
        }

        if (info.RefNum <= 0) return "PASSED";

        if (val <= info.RefNum)
        {
            return "PASSED";
        }
        else
        {
            return "FAILED";
        }
    }
}
