namespace F1Telemetry.App.Formatting;

/// <summary>
/// Formats raw F1 25 tyre compound identifiers into Chinese tyre names.
/// </summary>
public static class TyreCompoundFormatter
{
    /// <summary>
    /// Formats the currently visible tyre compound, preferring visual compound over actual compound.
    /// </summary>
    /// <param name="visualCompound">The visual tyre compound identifier when known.</param>
    /// <param name="actualCompound">The actual tyre compound identifier when known.</param>
    /// <param name="hasTelemetryAccess">Whether tyre telemetry is visible for this car.</param>
    public static string Format(byte? visualCompound, byte? actualCompound, bool hasTelemetryAccess)
    {
        if (visualCompound is not null)
        {
            return FormatVisualCompound(visualCompound.Value) ?? FormatUnknownCompound(visualCompound.Value);
        }

        if (actualCompound is not null)
        {
            return FormatActualCompound(actualCompound.Value) ?? FormatUnknownCompound(actualCompound.Value);
        }

        return hasTelemetryAccess ? "未知胎" : "遥测受限";
    }

    /// <summary>
    /// Formats an existing raw tyre summary such as "V16 / A19" for display-only projections.
    /// </summary>
    /// <param name="rawCompoundText">The raw tyre summary text produced by older display paths.</param>
    public static string FormatRawCompoundText(string? rawCompoundText)
    {
        if (string.IsNullOrWhiteSpace(rawCompoundText) || rawCompoundText == "-")
        {
            return "未知胎";
        }

        byte? visualCompound = null;
        byte? actualCompound = null;
        foreach (var token in rawCompoundText.Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (token.Length < 2 || !byte.TryParse(token[1..], out var compound))
            {
                continue;
            }

            if (token[0] is 'V' or 'v')
            {
                visualCompound = compound;
            }
            else if (token[0] is 'A' or 'a')
            {
                actualCompound = compound;
            }
        }

        return visualCompound is null && actualCompound is null
            ? "未知胎"
            : Format(visualCompound, actualCompound, hasTelemetryAccess: true);
    }

    private static string? FormatVisualCompound(byte compound)
    {
        return compound switch
        {
            7 => "半雨胎",
            8 => "全雨胎",
            16 => "红胎",
            17 => "黄胎",
            18 => "白胎",
            _ => null
        };
    }

    private static string? FormatActualCompound(byte compound)
    {
        return compound switch
        {
            7 => "半雨胎",
            8 => "全雨胎",
            _ => null
        };
    }

    private static string FormatUnknownCompound(byte compound)
    {
        return $"未知胎（编码 {compound}）";
    }
}
