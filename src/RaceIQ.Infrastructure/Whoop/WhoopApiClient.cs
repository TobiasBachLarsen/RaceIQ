using System.Globalization;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Serialization;

namespace RaceIQ.Infrastructure.Whoop;

public class WhoopApiClient : IWhoopApiClient
{
    public const string BaseUrl = "https://api.prod.whoop.com/developer/v2";

    // WHOOP caps a recovery page at 25 records.
    private const int PageSize = 25;

    private readonly HttpClient _httpClient;

    public WhoopApiClient(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<WhoopProfile> GetProfileAsync(string accessToken)
    {
        var payload = await GetAsync<WhoopProfilePayload>($"{BaseUrl}/user/profile/basic", accessToken);
        return new WhoopProfile(payload.UserId.ToString(CultureInfo.InvariantCulture));
    }

    public async Task<IReadOnlyList<WhoopRecovery>> GetRecoveriesAsync(string accessToken, DateTime startUtc)
    {
        var recoveries = new List<WhoopRecovery>();
        string? nextToken = null;

        do
        {
            var url = $"{BaseUrl}/recovery" +
                $"?start={Uri.EscapeDataString(startUtc.ToString("yyyy-MM-ddTHH:mm:ss.fffZ", CultureInfo.InvariantCulture))}" +
                $"&limit={PageSize}" +
                (nextToken is null ? "" : $"&nextToken={Uri.EscapeDataString(nextToken)}");

            var page = await GetAsync<WhoopRecoveryPage>(url, accessToken);

            foreach (var record in page.Records ?? new())
            {
                // Only SCORED records carry numbers; PENDING_SCORE and UNSCORABLE have a
                // null or meaningless score object and are skipped rather than stored as 0.
                if (record.ScoreState != "SCORED" || record.Score is null)
                    continue;

                recoveries.Add(new WhoopRecovery(
                    record.CycleId.ToString(CultureInfo.InvariantCulture),
                    record.CreatedAt.UtcDateTime,
                    record.Score.RecoveryScore,
                    record.Score.HrvRmssdMilli,
                    record.Score.RestingHeartRate));
            }

            nextToken = string.IsNullOrEmpty(page.NextToken) ? null : page.NextToken;
        } while (nextToken is not null);

        return recoveries;
    }

    private async Task<T> GetAsync<T>(string url, string accessToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        var response = await _httpClient.SendAsync(request);
        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync<T>()
            ?? throw new InvalidOperationException($"WHOOP response from {url} was empty.");
    }

    private record WhoopProfilePayload(
        [property: JsonPropertyName("user_id")] long UserId);

    private record WhoopRecoveryPage(
        [property: JsonPropertyName("records")] List<WhoopRecoveryPayload>? Records,
        [property: JsonPropertyName("next_token")] string? NextToken);

    private record WhoopRecoveryPayload(
        [property: JsonPropertyName("cycle_id")] long CycleId,
        [property: JsonPropertyName("created_at")] DateTimeOffset CreatedAt,
        [property: JsonPropertyName("score_state")] string? ScoreState,
        [property: JsonPropertyName("score")] WhoopRecoveryScore? Score);

    private record WhoopRecoveryScore(
        [property: JsonPropertyName("recovery_score")] int RecoveryScore,
        [property: JsonPropertyName("resting_heart_rate")] int RestingHeartRate,
        [property: JsonPropertyName("hrv_rmssd_milli")] double HrvRmssdMilli);
}
