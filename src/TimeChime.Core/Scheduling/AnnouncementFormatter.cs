namespace TimeChime.Core.Scheduling;

public static class AnnouncementFormatter
{
    private static readonly string[] EnglishHourWords =
    [
        "twelve", "one", "two", "three", "four", "five", "six", "seven", "eight", "nine", "ten", "eleven"
    ];

    private static readonly string[] HindiHourWords =
    [
        "बारह", "एक", "दो", "तीन", "चार", "पांच", "छह", "सात", "आठ", "नौ", "दस", "ग्यारह"
    ];

    private static readonly string[] NumbersToWords =
    [
        "zero", "one", "two", "three", "four", "five", "six", "seven", "eight", "nine",
        "ten", "eleven", "twelve", "thirteen", "fourteen", "fifteen", "sixteen", "seventeen", "eighteen", "nineteen",
        "twenty", "twenty-one", "twenty-two", "twenty-three", "twenty-four", "twenty-five",
        "twenty-six", "twenty-seven", "twenty-eight", "twenty-nine",
        "thirty", "thirty-one", "thirty-two", "thirty-three", "thirty-four", "thirty-five",
        "thirty-six", "thirty-seven", "thirty-eight", "thirty-nine",
        "forty", "forty-one", "forty-two", "forty-three", "forty-four", "forty-five",
        "forty-six", "forty-seven", "forty-eight", "forty-nine",
        "fifty", "fifty-one", "fifty-two", "fifty-three", "fifty-four", "fifty-five",
        "fifty-six", "fifty-seven", "fifty-eight", "fifty-nine"
    ];

    public static string Format(DateTimeOffset time, string timeFormat = "12h", string language = "en")
    {
        if (language.StartsWith("hi", StringComparison.OrdinalIgnoreCase))
        {
            return FormatHindi(time);
        }

        return FormatEnglish(time);
    }

    public static string FormatEnglish(DateTimeOffset time)
    {
        int hour = time.Hour;
        int minute = time.Minute;

        int displayHour = hour % 12;
        string hourWord = EnglishHourWords[displayHour];

        if (minute == 0)
        {
            return $"It's {hourWord} o'clock.";
        }

        string minuteWord = minute switch
        {
            15 => "fifteen",
            30 => "thirty",
            45 => "forty-five",
            _ => minute < 10 ? $"oh {NumbersToWords[minute]}" : NumbersToWords[minute]
        };

        return $"It's {hourWord} {minuteWord}.";
    }

    public static string FormatHindi(DateTimeOffset time)
    {
        int hour = time.Hour;
        int minute = time.Minute;

        // Time of day period
        string period = hour switch
        {
            >= 4 and < 12 => "सुबह के",
            >= 12 and < 16 => "दोपहर के",
            >= 16 and < 20 => "शाम के",
            _ => "रात के"
        };

        int displayHour = hour % 12;
        string hourWord = HindiHourWords[displayHour];

        if (minute == 0)
        {
            return $"अभी {period} {hourWord} बज रहे हैं।";
        }
        else if (minute == 15)
        {
            return $"अभी {period} सवा {hourWord} बजे हैं।";
        }
        else if (minute == 30)
        {
            string halfWord = displayHour switch
            {
                1 => "डेढ़",
                2 => "ढाई",
                _ => $"साढ़े {hourWord}"
            };
            return $"अभी {period} {halfWord} बजे हैं।";
        }
        else if (minute == 45)
        {
            int nextHour = (displayHour + 1) % 12;
            string nextHourWord = HindiHourWords[nextHour];
            return $"अभी {period} पौने {nextHourWord} बजे हैं।";
        }
        else
        {
            return $"अभी {period} {hourWord} बजकर {minute} मिनट हुए हैं।";
        }
    }
}
