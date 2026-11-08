using TimeChime.Core.Models;
using TimeChime.Core.Scheduling;
using TimeChime.Core.Voices;
using Xunit;

namespace TimeChime.Core.Tests;

public class SchedulingAndFormattingTests
{
    [Fact]
    public void AnnouncementFormatter_English_FollowsUpdatedSpecification()
    {
        // 09:00 -> "It's nine o'clock."
        Assert.Equal("It's nine o'clock.", AnnouncementFormatter.Format(new DateTimeOffset(2026, 1, 1, 9, 0, 0, TimeSpan.Zero), "12h", "en"));

        // 09:15 -> "It's nine fifteen."
        Assert.Equal("It's nine fifteen.", AnnouncementFormatter.Format(new DateTimeOffset(2026, 1, 1, 9, 15, 0, TimeSpan.Zero), "12h", "en"));

        // 09:30 -> "It's nine thirty."
        Assert.Equal("It's nine thirty.", AnnouncementFormatter.Format(new DateTimeOffset(2026, 1, 1, 9, 30, 0, TimeSpan.Zero), "12h", "en"));

        // 09:45 -> "It's nine forty-five."
        Assert.Equal("It's nine forty-five.", AnnouncementFormatter.Format(new DateTimeOffset(2026, 1, 1, 9, 45, 0, TimeSpan.Zero), "12h", "en"));

        // 12:00 -> "It's twelve o'clock."
        Assert.Equal("It's twelve o'clock.", AnnouncementFormatter.Format(new DateTimeOffset(2026, 1, 1, 12, 0, 0, TimeSpan.Zero), "12h", "en"));

        // 15:00 -> "It's three o'clock."
        Assert.Equal("It's three o'clock.", AnnouncementFormatter.Format(new DateTimeOffset(2026, 1, 1, 15, 0, 0, TimeSpan.Zero), "12h", "en"));

        // 23:45 -> "It's eleven forty-five."
        Assert.Equal("It's eleven forty-five.", AnnouncementFormatter.Format(new DateTimeOffset(2026, 1, 1, 23, 45, 0, TimeSpan.Zero), "12h", "en"));

        // 00:00 -> "It's twelve o'clock."
        Assert.Equal("It's twelve o'clock.", AnnouncementFormatter.Format(new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero), "12h", "en"));
    }

    [Fact]
    public void AnnouncementFormatter_English_RulesEnforced()
    {
        for (int h = 0; h < 24; h++)
        {
            foreach (int m in new[] { 0, 15, 30, 45 })
            {
                var dto = new DateTimeOffset(2026, 1, 1, h, m, 0, TimeSpan.Zero);
                string text = AnnouncementFormatter.FormatEnglish(dto);

                // Rule 1: Always begin with "It's"
                Assert.StartsWith("It's ", text);

                // Rule 2: Never say AM or PM
                Assert.DoesNotContain("AM", text);
                Assert.DoesNotContain("PM", text);

                // Rule 3: Never say morning, afternoon, evening, noon, midnight
                Assert.DoesNotContain("morning", text);
                Assert.DoesNotContain("afternoon", text);
                Assert.DoesNotContain("evening", text);
                Assert.DoesNotContain("noon", text);
                Assert.DoesNotContain("midnight", text);

                // Rule 4: Use "o'clock" for exact hours only
                if (m == 0)
                {
                    Assert.Contains("o'clock", text);
                }
                else
                {
                    Assert.DoesNotContain("o'clock", text);
                }
            }
        }
    }

    [Fact]
    public void AnnouncementFormatter_Hindi_FollowsSpecification()
    {
        // 09:00 -> "अभी सुबह के नौ बज रहे हैं।"
        Assert.Equal("अभी सुबह के नौ बज रहे हैं।", AnnouncementFormatter.Format(new DateTimeOffset(2026, 1, 1, 9, 0, 0, TimeSpan.Zero), "12h", "hi"));

        // 09:15 -> "अभी सुबह के सवा नौ बजे हैं।"
        Assert.Equal("अभी सुबह के सवा नौ बजे हैं।", AnnouncementFormatter.Format(new DateTimeOffset(2026, 1, 1, 9, 15, 0, TimeSpan.Zero), "12h", "hi"));

        // 09:30 -> "अभी सुबह के साढ़े नौ बजे हैं।"
        Assert.Equal("अभी सुबह के साढ़े नौ बजे हैं।", AnnouncementFormatter.Format(new DateTimeOffset(2026, 1, 1, 9, 30, 0, TimeSpan.Zero), "12h", "hi"));

        // 09:45 -> "अभी सुबह के पौने दस बजे हैं।"
        Assert.Equal("अभी सुबह के पौने दस बजे हैं।", AnnouncementFormatter.Format(new DateTimeOffset(2026, 1, 1, 9, 45, 0, TimeSpan.Zero), "12h", "hi"));

        // 12:30 -> "अभी दोपहर के साढ़े बारह बजे हैं।"
        Assert.Equal("अभी दोपहर के साढ़े बारह बजे हैं।", AnnouncementFormatter.Format(new DateTimeOffset(2026, 1, 1, 12, 30, 0, TimeSpan.Zero), "12h", "hi"));

        // 18:15 -> "अभी शाम के सवा छह बजे हैं।"
        Assert.Equal("अभी शाम के सवा छह बजे हैं।", AnnouncementFormatter.Format(new DateTimeOffset(2026, 1, 1, 18, 15, 0, TimeSpan.Zero), "12h", "hi"));

        // 23:45 -> "अभी रात के पौने बारह बजे हैं।"
        Assert.Equal("अभी रात के पौने बारह बजे हैं।", AnnouncementFormatter.Format(new DateTimeOffset(2026, 1, 1, 23, 45, 0, TimeSpan.Zero), "12h", "hi"));
    }

    [Fact]
    public void TimeSlotMapper_ExtractsEnglishAndHindiSlots()
    {
        // English
        Assert.True(TimeSlotMapper.TryExtractSlotKey("It's nine o'clock.", out string keyEng9));
        Assert.Equal("0900", keyEng9);

        Assert.True(TimeSlotMapper.TryExtractSlotKey("It's ten fifteen.", out string keyEng1015));
        Assert.Equal("1015", keyEng1015);

        Assert.True(TimeSlotMapper.TryExtractSlotKey("It's twelve thirty.", out string keyEng1230));
        Assert.Equal("0030", keyEng1230); // 12h representation for 12:30

        Assert.True(TimeSlotMapper.TryExtractSlotKey("It's eleven forty-five.", out string keyEng2345));
        Assert.Equal("1145", keyEng2345);

        // Hindi
        Assert.True(TimeSlotMapper.TryExtractSlotKey("अभी सुबह के नौ बज रहे हैं।", out string keyHi9));
        Assert.Equal("0900", keyHi9);

        Assert.True(TimeSlotMapper.TryExtractSlotKey("अभी सुबह के सवा दस बजे हैं।", out string keyHi1015));
        Assert.Equal("1015", keyHi1015);

        Assert.True(TimeSlotMapper.TryExtractSlotKey("अभी दोपहर के साढ़े बारह बजे हैं।", out string keyHi1230));
        Assert.Equal("1230", keyHi1230);

        Assert.True(TimeSlotMapper.TryExtractSlotKey("अभी रात के पौने बारह बजे हैं।", out string keyHi2345));
        Assert.Equal("2345", keyHi2345);
    }

    [Fact]
    public void ActiveWindowRule_DaytimeWindow_InclusiveStart_ExclusiveEnd()
    {
        var window = new ActiveWindowRule
        {
            Start = new TimeOnly(9, 0),
            End = new TimeOnly(18, 0),
            AllDay = false
        };

        Assert.True(window.Contains(new TimeOnly(9, 0)));
        Assert.True(window.Contains(new TimeOnly(12, 30)));
        Assert.True(window.Contains(new TimeOnly(17, 59)));

        Assert.False(window.Contains(new TimeOnly(18, 0)));
        Assert.False(window.Contains(new TimeOnly(8, 59)));
        Assert.False(window.Contains(new TimeOnly(20, 0)));
    }

    [Fact]
    public void ActiveWindowRule_OvernightWindow_SpansMidnightCorrectly()
    {
        var overnight = new ActiveWindowRule
        {
            Start = new TimeOnly(22, 0),
            End = new TimeOnly(6, 0),
            AllDay = false
        };

        Assert.True(overnight.Contains(new TimeOnly(22, 0)));
        Assert.True(overnight.Contains(new TimeOnly(23, 45)));
        Assert.True(overnight.Contains(new TimeOnly(0, 0)));
        Assert.True(overnight.Contains(new TimeOnly(3, 15)));
        Assert.True(overnight.Contains(new TimeOnly(5, 59)));

        Assert.False(overnight.Contains(new TimeOnly(6, 0)));
        Assert.False(overnight.Contains(new TimeOnly(12, 0)));
        Assert.False(overnight.Contains(new TimeOnly(21, 59)));
    }

    [Fact]
    public void ActiveWindowRule_AllDay_AllowsAllTimes()
    {
        var allDay = new ActiveWindowRule { AllDay = true };
        Assert.True(allDay.Contains(new TimeOnly(0, 0)));
        Assert.True(allDay.Contains(new TimeOnly(12, 0)));
        Assert.True(allDay.Contains(new TimeOnly(23, 59)));
    }

    [Fact]
    public void VoiceProfile_MockTagging_AppendsMockLabel()
    {
        var mockVoice = new VoiceProfile
        {
            Id = "mock:test",
            DisplayName = "Test Voice",
            Gender = "Female",
            Locale = "en-US",
            Engine = "Mock",
            LicenseRecord = "Test License",
            IsMock = true
        };

        Assert.Equal("Test Voice [MOCK]", mockVoice.TaggedDisplayName);

        var realVoice = mockVoice with { IsMock = false };
        Assert.Equal("Test Voice", realVoice.TaggedDisplayName);
    }
}
