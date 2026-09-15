using System.Net.Http.Json;
using System.Text.Json.Serialization;

namespace RaceIQ.Infrastructure.ZwiftPower;

public class ZwiftPowerApiClient : IZwiftPowerApiClient
{
    private readonly HttpClient _httpClient;

    public ZwiftPowerApiClient(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<IReadOnlyList<ZwiftPowerRaceResult>> GetRecentResultsAsync(
        string sessionCookie, string zwiftRiderId)
    {
        using var request = new HttpRequestMessage(
            HttpMethod.Get,
            $"https://zwiftpower.com/cache3/profile/{Uri.EscapeDataString(zwiftRiderId)}_all.json");
        request.Headers.Add("Cookie", sessionCookie);

        var response = await _httpClient.SendAsync(request);
        response.EnsureSuccessStatusCode();

        // The cache3 profile endpoint wraps its row array in an object under a "data"
        // property (it does not return a bare array), so parse the envelope first.
        var envelope = await response.Content.ReadFromJsonAsync<ZwiftPowerResponseEnvelope>()
            ?? new ZwiftPowerResponseEnvelope(null);
        var raw = envelope.Data ?? new();

        return raw
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
                    r.Position,
                    r.GunTime.HasValue ? TimeSpan.FromSeconds(r.GunTime.Value) : null,
                    r.SkillGain);
            }).ToList();
    }

    private record ZwiftPowerResponseEnvelope(
        [property: JsonPropertyName("data")] List<ZwiftPowerResultPayload>? Data);

    // Field names confirmed against a real ZwiftPower account's
    // /cache3/profile/{id}_all.json response: zid (per-event result id, string),
    // event_title (race name), event_date (unix seconds), category (A-E/DQ letter),
    // pos (overall finishing position), time_gun (finish time in seconds, a scalar -
    // the sibling "time" field is a [seconds, flag] array, not used here), f_t (entry
    // type, used above to keep only races), and skill_gain (ZwiftPower ranking points
    // gained/lost in the race - sometimes a string like "28.29", sometimes a bare 0).
    private record ZwiftPowerResultPayload(
        [property: JsonPropertyName("zid")] string? Zid,
        [property: JsonPropertyName("event_title")] string? EventTitle,
        [property: JsonPropertyName("event_date")] long EventDate,
        [property: JsonPropertyName("category")] string? Category,
        [property: JsonPropertyName("pos")] int? Position,
        [property: JsonPropertyName("time_gun")] double? GunTime,
        [property: JsonPropertyName("f_t")] string? RaceType,
        [property: JsonPropertyName("skill_gain")]
        [property: JsonConverter(typeof(Json.FlexibleDoubleConverter))] double? SkillGain);
}
