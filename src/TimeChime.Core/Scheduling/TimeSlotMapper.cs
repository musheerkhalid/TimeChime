namespace TimeChime.Core.Scheduling;

public static class TimeSlotMapper
{
    private static readonly Dictionary<string, string> PrecomputedSlotMap = BuildPrecomputedSlotMap();

    private static Dictionary<string, string> BuildPrecomputedSlotMap()
    {
        var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        // Precompute all 96 slots for English and Hindi
        for (int h = 0; h < 24; h++)
        {
            foreach (int m in new[] { 0, 15, 30, 45 })
            {
                string key = $"{h:D2}{m:D2}";
                var dto = new DateTimeOffset(2026, 1, 1, h, m, 0, TimeSpan.Zero);

                string eng = AnnouncementFormatter.FormatEnglish(dto);
                map.TryAdd(eng, key);

                string hi = AnnouncementFormatter.FormatHindi(dto);
                map[hi] = key;
            }
        }

        return map;
    }

    public static string ToSlotKey(DateTimeOffset time) => time.ToString("HHmm");

    public static string ToSlotKey(int hour, int minute) => $"{hour:D2}{minute:D2}";

    public static bool TryExtractSlotKey(string text, out string slotKey)
    {
        slotKey = string.Empty;
        if (string.IsNullOrWhiteSpace(text)) return false;

        text = text.Trim();

        // 1. Direct 4-digit key check (e.g. "0900", "2345")
        if (text.Length == 4 && int.TryParse(text, out int num) && num >= 0 && num <= 2359)
        {
            slotKey = text;
            return true;
        }

        // 2. Exact match in precomputed dictionary
        if (PrecomputedSlotMap.TryGetValue(text, out var mappedKey))
        {
            slotKey = mappedKey;
            return true;
        }

        // 3. Fallback heuristic for English "It's ..."
        string clean = text;
        if (clean.StartsWith("It's ", StringComparison.OrdinalIgnoreCase))
        {
            clean = clean.Substring(5).Trim();
        }
        else if (clean.StartsWith("The time is ", StringComparison.OrdinalIgnoreCase))
        {
            clean = clean.Substring(12).Trim();
        }

        var parts = clean.Split([' ', '.', '\''], StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length > 0)
        {
            string hourToken = parts[0];
            int hour = hourToken.ToLowerInvariant() switch
            {
                "twelve" => 12,
                "one" => 1,
                "two" => 2,
                "three" => 3,
                "four" => 4,
                "five" => 5,
                "six" => 6,
                "seven" => 7,
                "eight" => 8,
                "nine" => 9,
                "ten" => 10,
                "eleven" => 11,
                _ => -1
            };

            if (hour != -1)
            {
                int minute = 0;
                if (clean.Contains("forty-five", StringComparison.OrdinalIgnoreCase)) minute = 45;
                else if (clean.Contains("thirty", StringComparison.OrdinalIgnoreCase)) minute = 30;
                else if (clean.Contains("fifteen", StringComparison.OrdinalIgnoreCase)) minute = 15;

                int h24 = hour == 12 ? 0 : hour;
                slotKey = $"{h24:D2}{minute:D2}";
                return true;
            }
        }

        return false;
    }
}
