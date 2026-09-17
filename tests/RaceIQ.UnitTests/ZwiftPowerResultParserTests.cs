using RaceIQ.Infrastructure.ZwiftPower;
using Xunit;

namespace RaceIQ.UnitTests;

public class ZwiftPowerResultParserTests
{
    [Fact]
    public void Parse_ReadsRealResultShape()
    {
        // Field names and shapes mirror a real /cache3/profile/{id}_all.json row: rows
        // are wrapped in a top-level "data" object, "time" is a [seconds, flag] array
        // while "time_gun" is the scalar finish time, and event_title may have leading
        // whitespace. Personal fields (name, HR, power) are omitted - only what the
        // parser reads is kept.
        // pos 21 is the rider's place in the whole mixed-category field; position_in_cat 1
        // is their place in category B, which is what the app should show.
        var results = ZwiftPowerResultParser.Parse(
            """{"data":[{"zid":"5233969","event_title":" Crit Race","event_date":1756742400,"category":"B","pos":21,"position_in_cat":1,"display_pos":1,"time":[2705.5,0],"time_gun":2705.5,"f_t":"TYPE_RACE TYPE_RACE ","skill_gain":"28.29"}]}""");

        Assert.Single(results);
        Assert.Equal("5233969", results[0].RaceId);
        Assert.Equal("Crit Race", results[0].EventName);      // trimmed
        Assert.Equal("B", results[0].Category);
        Assert.Equal(1, results[0].Position);
        Assert.Equal(TimeSpan.FromSeconds(2705.5), results[0].Duration);
        Assert.Equal(28.29, results[0].RatingChange);         // skill_gain sent as a string
    }

    [Fact]
    public void Parse_KeepsOnlyRaces()
    {
        // The profile feed mixes races with workouts and rides; only TYPE_RACE entries
        // are real results. A disqualified race stays TYPE_RACE (category "DQ") and is
        // kept.
        var results = ZwiftPowerResultParser.Parse(
            """
            {"data":[
              {"zid":"1","event_title":"Real Race","event_date":1756742400,"category":"C","pos":10,"time_gun":1800,"f_t":"TYPE_RACE TYPE_RACE ","skill_gain":"12.5"},
              {"zid":"2","event_title":"Workout Hour","event_date":1756828800,"category":"E","pos":42,"time_gun":3300,"f_t":"TYPE_WORKOUT TYPE_WORKOUT"},
              {"zid":"3","event_title":"Just A Ride","event_date":1756915200,"category":"E","pos":20,"time_gun":2000,"f_t":"TYPE_RIDE"},
              {"zid":"4","event_title":"DQ Race","event_date":1757001600,"category":"DQ","pos":36,"time_gun":4315,"f_t":"TYPE_RACE TYPE_RACE ","skill_gain":0}
            ]}
            """);

        Assert.Equal(new[] { "1", "4" }, results.Select(r => r.RaceId));
        // Without position_in_cat the overall pos is used as a fallback.
        Assert.Equal(10, results[0].Position);
        // skill_gain parses whether sent as a string ("12.5") or a bare number (0).
        Assert.Equal(12.5, results[0].RatingChange);
        Assert.Equal(0.0, results[1].RatingChange);
    }

    [Fact]
    public void Parse_FallsBackWhenIdOrTitleMissing()
    {
        // Defensive guard: if zid or event_title were ever absent, results must still get
        // a distinct id and a readable name rather than colliding or showing blank.
        var results = ZwiftPowerResultParser.Parse(
            """
            {"data":[
              {"event_date":1756742400,"time_gun":1000,"f_t":"TYPE_RACE"},
              {"event_date":1756828800,"time_gun":2000,"f_t":"TYPE_RACE"}
            ]}
            """);

        Assert.Equal(2, results.Count);
        Assert.All(results, r => Assert.False(string.IsNullOrEmpty(r.RaceId)));
        Assert.NotEqual(results[0].RaceId, results[1].RaceId);
        Assert.All(results, r => Assert.False(string.IsNullOrWhiteSpace(r.EventName)));
    }

    [Theory]
    [InlineData("<html><body>Please log in</body></html>")]   // pasted the profile page, not the JSON
    [InlineData("")]
    [InlineData("""{"error":"not found"}""")]                  // JSON, but not the results document
    public void Parse_RejectsTextThatIsNotAResultsDocument(string pasted)
    {
        Assert.Throws<ZwiftPowerImportException>(() => ZwiftPowerResultParser.Parse(pasted));
    }

    [Fact]
    public void Parse_ToleratesNullRowsAndNumbersWrittenAsStrings()
    {
        // ZwiftPower is loose with types (skill_gain is documented as sometimes a string),
        // so the parser must accept a number written as "1" anywhere, and a null element
        // in the list must simply be skipped rather than crash the import.
        var results = ZwiftPowerResultParser.Parse(
            """{"data":[null,{"zid":"7","event_title":"Race","event_date":"1756742400","category":"B","pos":"21","position_in_cat":"1","time_gun":"2705.5","f_t":"TYPE_RACE"}]}""");

        Assert.Single(results);
        Assert.Equal(1, results[0].Position);
        Assert.Equal(TimeSpan.FromSeconds(2705.5), results[0].Duration);
        Assert.Equal(new DateTime(2025, 9, 1, 16, 0, 0, DateTimeKind.Utc), results[0].EventDate);
    }

    [Fact]
    public void Parse_RejectsImpossibleEventDateAsImportError()
    {
        // Garbage in the paste must come back as the "paste it again" error, never as a
        // crash page.
        Assert.Throws<ZwiftPowerImportException>(() => ZwiftPowerResultParser.Parse(
            """{"data":[{"zid":"1","event_date":99999999999999,"f_t":"TYPE_RACE"}]}"""));
    }
}
