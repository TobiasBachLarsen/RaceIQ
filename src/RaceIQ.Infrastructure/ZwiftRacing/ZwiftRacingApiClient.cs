using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Serialization;

namespace RaceIQ.Infrastructure.ZwiftRacing;

public class ZwiftRacingApiClient : IZwiftRacingApiClient
{
    private const string BaseUrl = "https://api.zwiftracing.app/api";
    private readonly HttpClient _httpClient;

    public ZwiftRacingApiClient(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<IReadOnlyList<ZwiftRacingRaceResult>> GetRecentResultsAsync(
        string apiKey, string zwiftRiderId)
    {
        var raceIds = await DiscoverRecentRaceIdsAsync(apiKey, zwiftRiderId);
        var results = new List<ZwiftRacingRaceResult>();

        foreach (var raceId in raceIds)
        {
            var race = await GetRaceResultAsync(apiKey, raceId);
            var rider = race.Results.FirstOrDefault(r => r.ZwiftId.ToString() == zwiftRiderId);
            if (rider is null) continue;

            results.Add(new ZwiftRacingRaceResult(
                race.RaceId.ToString(),
                race.EventTitle,
                DateTimeOffset.FromUnixTimeSeconds(race.EventTime).UtcDateTime,
                rider.Category,
                rider.Position,
                race.Results.Count,
                rider.Time.HasValue ? TimeSpan.FromSeconds(rider.Time.Value) : null,
                rider.RatingDelta));
        }

        return results;
    }

    // VERIFY (Task 9): still an unresolved open question, researched directly rather than
    // left as a guess. `/public/riders/{zwiftRiderId}` is a REAL, confirmed endpoint - not
    // a guess anymore - but it does NOT return a rider's race history or a list of race
    // ids. Confirmed by reading the actual source of puckdoug/zpdatafetch's zrdatafetch
    // module (github.com/puckdoug/zpdatafetch, src/zrdatafetch/), which is the same
    // community tool the base URL and /public/results/{raceId} endpoint were confirmed
    // against in the original research:
    //   - zrriderfetch.py's ZRRiderFetch hits exactly this path
    //     (f'/public/riders/{zwift_id}') and parses the response with ZRRider.from_dict
    //     (zrrider.py). Its `known_fields` are: name, gender, race, power, riderId,
    //     zwiftId, handicaps, phenotype, seed, velo - i.e. a rider's rating/vELO profile
    //     (current/max30/max90 rating, category, handicaps, phenotype scores, power
    //     curve). There is no field anywhere in that shape for a list of race/event ids.
    //   - docs/DATA_DICTIONARY.md's "Zwift Racing Data" section documents exactly three
    //     ZR endpoints/shapes: Rider (rating profile, as above), Results (single race by
    //     id - what GetRaceResultAsync below uses), and Team (roster). No fourth
    //     "rider's races" or "race history" shape is documented for ZwiftRacing anywhere
    //     in this library (Race History/Racelog fields DO exist in the same doc, but only
    //     under the separate "Zwift Power Data" section for ZwiftPower's Cyclist object -
    //     a different provider's endpoint, not this one).
    //   - The repo's own README table of zrdata's supported data is explicit: "Rider
    //     Ratings, Race Results, Team Rosters" - three things, not four. The `zrdata` CLI
    //     itself only exposes `rider`, `result`, and `team` subcommands.
    // So this task's brief's original guess (this same URL, assumed to return
    // `recent_race_ids`) is now known to be wrong for what DiscoverRecentRaceIdsAsync
    // needs - not "unverified", but actually falsified by real source. No alternative
    // endpoint for "this rider's recent race ids" was found anywhere in zrdatafetch's
    // source, its CLI, its README, or its data dictionary; a genuine, bounded search (not
    // an unbounded one) turned up nothing better. Per this task's instructions, the guess
    // below is kept exactly as the brief specified it (same path, same assumed
    // `recent_race_ids` response shape) rather than invented further, since no better
    // candidate exists to replace it with. In its current form this method will call a
    // real endpoint that returns HTTP 200 with a rider rating profile, which has no
    // `recent_race_ids` property - so ReadFromJsonAsync will simply fail to populate that
    // property and this method will return an empty list, not throw. GetRecentResultsAsync
    // will therefore return no results until Tobias gets ZwiftRacing API access (via their
    // Discord) and finds the actual rider-race-history endpoint from ZwiftRacing's own
    // docs/community, at which point this method's path and payload shape need to be
    // replaced with the real ones.
    private async Task<IReadOnlyList<string>> DiscoverRecentRaceIdsAsync(string apiKey, string zwiftRiderId)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, $"{BaseUrl}/public/riders/{Uri.EscapeDataString(zwiftRiderId)}");
        request.Headers.Authorization = AuthenticationHeaderValue.Parse(apiKey);

        var response = await _httpClient.SendAsync(request);
        response.EnsureSuccessStatusCode();

        var rider = await response.Content.ReadFromJsonAsync<ZwiftRacingRiderPayload>();
        return rider?.RecentRaceIds?.Select(id => id.ToString()).ToList() ?? new List<string>();
    }

    private async Task<ZwiftRacingRaceDetailPayload> GetRaceResultAsync(string apiKey, string raceId)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, $"{BaseUrl}/public/results/{Uri.EscapeDataString(raceId)}");
        request.Headers.Authorization = AuthenticationHeaderValue.Parse(apiKey);

        var response = await _httpClient.SendAsync(request);
        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync<ZwiftRacingRaceDetailPayload>()
            ?? throw new InvalidOperationException($"ZwiftRacing returned no body for race {raceId}.");
    }

    private record ZwiftRacingRiderPayload(
        [property: JsonPropertyName("recent_race_ids")] List<long>? RecentRaceIds);

    // Field names below were corrected during Task 9 research from the brief's original
    // snake_case guess (race_id, event_title, event_time, zwift_id, rating_delta, etc.)
    // to the real raw JSON keys the ZwiftRacing API actually sends. Confirmed directly
    // from puckdoug/zpdatafetch's zrdatafetch source and its docs/DATA_DICTIONARY.md
    // ("Zwift Racing Data" > "Results" section), which documents the raw-API-field ->
    // python-attribute mapping explicitly and matches the field names used to build the
    // request/parse the response in zrresultfetch.py and zrraceresult.py:
    //   race level:  eventId, title, time, routeId, distance, type, subType, results
    //   rider level: riderId, position, positionInCategory, category, time, gap,
    //                ratingBefore, rating, ratingDelta
    // (routeId, distance, type, subType, positionInCategory, gap, ratingBefore, rating
    // aren't needed by ZwiftRacingRaceResult and are left unmapped.) This came from the
    // same source used to confirm the base URL and /public/results/{raceId} endpoint, but
    // is still provisional pending live verification against a real ZwiftRacing account -
    // confirm all of this against an actual API response once Tobias has API access.
    private record ZwiftRacingRaceDetailPayload(
        [property: JsonPropertyName("eventId")] long RaceId,
        [property: JsonPropertyName("title")] string EventTitle,
        [property: JsonPropertyName("time")] long EventTime,
        [property: JsonPropertyName("results")] List<ZwiftRacingRiderResultPayload> Results);

    private record ZwiftRacingRiderResultPayload(
        [property: JsonPropertyName("riderId")] long ZwiftId,
        [property: JsonPropertyName("position")] int? Position,
        [property: JsonPropertyName("category")] string? Category,
        [property: JsonPropertyName("time")] double? Time,
        [property: JsonPropertyName("ratingDelta")] double? RatingDelta);
}
