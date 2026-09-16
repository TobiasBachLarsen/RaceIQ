using System.Text.Json;
using System.Text.Json.Serialization;

namespace RaceIQ.Infrastructure.ZwiftPower;

public record ZwiftPowerRaceResult(
    string RaceId,
    string EventName,
    DateTime EventDate,
    string? Category,
    int? Position,
    TimeSpan? Duration,
    double? RatingChange);

// Parses the JSON that ZwiftPower serves at /cache3/profile/{riderId}_all.json. The rider
// pastes that document into RaceIQ themselves: the endpoint sits behind CloudFront signed
// cookies that only their logged-in browser has, so the app cannot fetch it directly.
public static class ZwiftPowerResultParser
{
    public static IReadOnlyList<ZwiftPowerRaceResult> Parse(string json)
    {
        ZwiftPowerResponseEnvelope? envelope;
        try
        {
            envelope = JsonSerializer.Deserialize<ZwiftPowerResponseEnvelope>(json);
        }
        catch (JsonException ex)
        {
            throw new ZwiftPowerImportException("The pasted text is not ZwiftPower's results JSON.", ex);
        }

        // The cache3 profile endpoint wraps its row array in an object under a "data"
        // property (it does not return a bare array). A document without it is some other
        // page - most likely the HTML of the profile itself.
        if (envelope?.Data is null)
            throw new ZwiftPowerImportException("The pasted JSON has no \"data\" list of results.");

        return envelope.Data
            // f_t marks the entry type ("TYPE_RACE", "TYPE_WORKOUT", "TYPE_RIDE"); only
            // real races belong in the race-result list. A disqualified race is still a
            // race (f_t stays TYPE_RACE, category becomes "DQ"), so filter on f_t alone.
            .Where(r => r.RaceType is not null && r.RaceType.Contains("TYPE_RACE"))
            .Select(r =>
            {
                var eventDate = DateTimeOffset.FromUnixTimeSeconds(r.EventDate).UtcDateTime;

                // zid is the per-event result id and is always present in practice;
                // fall back to an EventDate-derived id only if it were ever missing, so
                // results can never collide on the (UserId, Provider, ProviderResultId)
                // de-dup key.
                var raceId = string.IsNullOrEmpty(r.Zid)
                    ? eventDate.ToString("yyyyMMddHHmmss")
                    : r.Zid;

                // event_title occasionally has leading whitespace; trim it, and generate
                // a readable placeholder if it were ever empty.
                var eventName = string.IsNullOrWhiteSpace(r.EventTitle)
                    ? $"ZwiftPower race {eventDate:d}"
                    : r.EventTitle.Trim();

                return new ZwiftPowerRaceResult(
                    raceId,
                    eventName,
                    eventDate,
                    r.Category,
                    // pos is the finishing position across the whole event, which for a
                    // mixed-category race counts every rider in A-E. position_in_cat is the
                    // rider's placing within their own category, which is the one a rider
                    // means by "I won" - prefer it, fall back to the overall position.
                    r.PositionInCategory ?? r.Position,
                    r.GunTime.HasValue ? TimeSpan.FromSeconds(r.GunTime.Value) : null,
                    r.SkillGain);
            }).ToList();
    }

    private record ZwiftPowerResponseEnvelope(
        [property: JsonPropertyName("data")] List<ZwiftPowerResultPayload>? Data);

    // Field names confirmed against a real ZwiftPower account's
    // /cache3/profile/{id}_all.json response: zid (per-event result id, string),
    // event_title (race name), event_date (unix seconds), category (A-E/DQ letter),
    // pos (finishing position across the whole event), position_in_cat (placing within
    // the rider's category), time_gun (finish time in seconds, a scalar -
    // the sibling "time" field is a [seconds, flag] array, not used here), f_t (entry
    // type, used above to keep only races), and skill_gain (ZwiftPower ranking points
    // gained/lost in the race - sometimes a string like "28.29", sometimes a bare 0).
    private record ZwiftPowerResultPayload(
        [property: JsonPropertyName("zid")] string? Zid,
        [property: JsonPropertyName("event_title")] string? EventTitle,
        [property: JsonPropertyName("event_date")] long EventDate,
        [property: JsonPropertyName("category")] string? Category,
        [property: JsonPropertyName("pos")] int? Position,
        [property: JsonPropertyName("position_in_cat")] int? PositionInCategory,
        [property: JsonPropertyName("time_gun")] double? GunTime,
        [property: JsonPropertyName("f_t")] string? RaceType,
        [property: JsonPropertyName("skill_gain")]
        [property: JsonConverter(typeof(Json.FlexibleDoubleConverter))] double? SkillGain);
}
