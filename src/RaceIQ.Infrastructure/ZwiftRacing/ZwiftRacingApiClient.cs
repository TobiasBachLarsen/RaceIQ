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

    // KNOWN LIMITATION: the public ZwiftRacing API has no endpoint that lists a rider's
    // recent races. `/public/riders/{id}` is a real endpoint, but it returns a rider's
    // rating/vELO profile (rating, category, phenotype, power curve) with no race or
    // event ids. The documented public API exposes only three shapes - rider ratings,
    // a single race result by id, and team rosters - none of which is a race history.
    // Until the real rider-race-history endpoint is obtained from ZwiftRacing (their API
    // access is gated behind their Discord), this method calls the rider endpoint, finds
    // no recent_race_ids property, and returns an empty list, so GetRecentResultsAsync
    // yields no results. When that endpoint is known, replace the path and payload below.
    private async Task<IReadOnlyList<string>> DiscoverRecentRaceIdsAsync(string apiKey, string zwiftRiderId)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, $"{BaseUrl}/public/riders/{Uri.EscapeDataString(zwiftRiderId)}");
        // TryAddWithoutValidation rather than AuthenticationHeaderValue.Parse: Parse throws
        // FormatException on a pasted key containing characters that aren't valid in an HTTP
        // token, which would surface as an unhandled exception instead of a clean reconnect.
        request.Headers.TryAddWithoutValidation("Authorization", apiKey);

        var response = await _httpClient.SendAsync(request);
        response.EnsureSuccessStatusCode();

        var rider = await response.Content.ReadFromJsonAsync<ZwiftRacingRiderPayload>();
        return rider?.RecentRaceIds?.Select(id => id.ToString()).ToList() ?? new List<string>();
    }

    private async Task<ZwiftRacingRaceDetailPayload> GetRaceResultAsync(string apiKey, string raceId)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, $"{BaseUrl}/public/results/{Uri.EscapeDataString(raceId)}");
        request.Headers.TryAddWithoutValidation("Authorization", apiKey);

        var response = await _httpClient.SendAsync(request);
        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync<ZwiftRacingRaceDetailPayload>()
            ?? throw new InvalidOperationException($"ZwiftRacing returned no body for race {raceId}.");
    }

    private record ZwiftRacingRiderPayload(
        [property: JsonPropertyName("recent_race_ids")] List<long>? RecentRaceIds);

    // These are the raw JSON keys the ZwiftRacing results API sends (race level:
    // eventId, title, time, results; rider level: riderId, position, category, time,
    // ratingDelta). Still to be verified against a live account once API access is
    // granted; fields not needed by ZwiftRacingRaceResult are left unmapped.
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
