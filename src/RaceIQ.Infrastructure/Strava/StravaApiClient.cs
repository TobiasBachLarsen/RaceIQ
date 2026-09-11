using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using RaceIQ.Domain;

namespace RaceIQ.Infrastructure.Strava;

public class StravaApiClient : IStravaApiClient
{
    private readonly HttpClient _httpClient;
    private readonly StravaRequestThrottle _throttle;

    public StravaApiClient(HttpClient httpClient, StravaRequestThrottle throttle)
    {
        _httpClient = httpClient;
        _throttle = throttle;
    }

    public async Task<IReadOnlyList<StravaActivitySummary>> ListRecentActivitiesAsync(string accessToken)
    {
        await _throttle.WaitForSlotAsync();

        using var request = new HttpRequestMessage(
            HttpMethod.Get, "https://www.strava.com/api/v3/athlete/activities?per_page=30");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        var response = await _httpClient.SendAsync(request);
        response.EnsureSuccessStatusCode();

        var raw = await response.Content.ReadFromJsonAsync<List<StravaActivityPayload>>() ?? new();

        return raw.Select(a => new StravaActivitySummary(
            a.Id.ToString(),
            a.Name,
            a.StartDate,
            a.Distance,
            TimeSpan.FromSeconds(a.MovingTime),
            a.TotalElevationGain,
            a.AverageWatts,
            a.WeightedAverageWatts,
            a.AverageHeartrate,
            a.AverageCadence)).ToList();
    }

    public async Task<IReadOnlyList<StreamPoint>> GetActivityStreamAsync(string accessToken, string stravaActivityId)
    {
        await _throttle.WaitForSlotAsync();

        using var request = new HttpRequestMessage(HttpMethod.Get,
            $"https://www.strava.com/api/v3/activities/{stravaActivityId}/streams" +
            "?keys=time,watts,heartrate,cadence,altitude,distance&key_by_type=true");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        var response = await _httpClient.SendAsync(request);
        response.EnsureSuccessStatusCode();

        var raw = await response.Content.ReadFromJsonAsync<StravaStreamsPayload>() ?? new();

        var times = raw.Time?.Data ?? new();
        var points = new List<StreamPoint>();
        for (var i = 0; i < times.Count; i++)
        {
            points.Add(new StreamPoint(
                TimeSeconds: times[i],
                Watts: GetAt(raw.Watts?.Data, i),
                HeartRateBpm: GetAt(raw.Heartrate?.Data, i),
                CadenceRpm: GetAt(raw.Cadence?.Data, i),
                AltitudeMeters: GetAt(raw.Altitude?.Data, i),
                DistanceMeters: GetAt(raw.Distance?.Data, i) ?? 0));
        }

        return points;
    }

    private static double? GetAt(List<double>? data, int index) =>
        data is not null && index < data.Count ? data[index] : null;

    private record StravaActivityPayload(
        long Id,
        string Name,
        [property: JsonPropertyName("start_date")] DateTime StartDate,
        double Distance,
        [property: JsonPropertyName("moving_time")] int MovingTime,
        [property: JsonPropertyName("total_elevation_gain")] double TotalElevationGain,
        [property: JsonPropertyName("average_watts")] double? AverageWatts,
        [property: JsonPropertyName("weighted_average_watts")] double? WeightedAverageWatts,
        [property: JsonPropertyName("average_heartrate")] double? AverageHeartrate,
        [property: JsonPropertyName("average_cadence")] double? AverageCadence);

    private record StravaStreamsPayload(
        [property: JsonPropertyName("time")] StravaStream<int>? Time = null,
        [property: JsonPropertyName("watts")] StravaStream<double>? Watts = null,
        [property: JsonPropertyName("heartrate")] StravaStream<double>? Heartrate = null,
        [property: JsonPropertyName("cadence")] StravaStream<double>? Cadence = null,
        [property: JsonPropertyName("altitude")] StravaStream<double>? Altitude = null,
        [property: JsonPropertyName("distance")] StravaStream<double>? Distance = null);

    private record StravaStream<T>([property: JsonPropertyName("data")] List<T>? Data);
}
