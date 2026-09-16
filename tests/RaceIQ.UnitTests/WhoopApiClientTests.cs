using System.Net;
using RaceIQ.Infrastructure.Whoop;
using Xunit;

namespace RaceIQ.UnitTests;

public class WhoopApiClientTests
{
    private class FakeHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, string> _respond;
        public List<HttpRequestMessage> Requests = new();

        public FakeHandler(Func<HttpRequestMessage, string> respond) => _respond = respond;

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests.Add(request);
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(_respond(request), System.Text.Encoding.UTF8, "application/json")
            });
        }
    }

    // Shape mirrors a real v2 recovery response: recovery_score and resting_heart_rate
    // arrive as decimals (72.0) even though the docs say integer. spo2/skin-temp are
    // included to prove unused fields are tolerated.
    private const string PageOne =
        """
        {"records":[
          {"cycle_id":93845,"sleep_id":"e3c3f1a2-0000-4000-8000-000000000001","user_id":10129,"created_at":"2026-09-10T05:12:00.123Z","updated_at":"2026-09-10T05:12:00.123Z","score_state":"SCORED","score":{"user_calibrating":false,"recovery_score":72.0,"resting_heart_rate":48.0,"hrv_rmssd_milli":61.4,"spo2_percentage":96.1,"skin_temp_celsius":33.2}},
          {"cycle_id":93846,"sleep_id":"e3c3f1a2-0000-4000-8000-000000000002","user_id":10129,"created_at":"2026-09-11T05:40:00Z","updated_at":"2026-09-11T05:40:00Z","score_state":"PENDING_SCORE","score":null},
          {"cycle_id":93847,"sleep_id":"e3c3f1a2-0000-4000-8000-000000000003","user_id":10129,"created_at":"2026-09-12T06:01:00Z","updated_at":"2026-09-12T06:01:00Z","score_state":"UNSCORABLE"}
        ],"next_token":"page-2"}
        """;

    private const string PageTwo =
        """
        {"records":[
          {"cycle_id":93848,"sleep_id":"e3c3f1a2-0000-4000-8000-000000000004","user_id":10129,"created_at":"2026-09-13T05:55:00Z","updated_at":"2026-09-13T05:55:00Z","score_state":"SCORED","score":{"user_calibrating":false,"recovery_score":31.0,"resting_heart_rate":55.0,"hrv_rmssd_milli":38.0,"spo2_percentage":95.0,"skin_temp_celsius":33.9}}
        ],"next_token":null}
        """;

    [Fact]
    public async Task GetRecoveriesAsync_KeepsScoredRecordsAndFollowsPagination()
    {
        var handler = new FakeHandler(req =>
            req.RequestUri!.Query.Contains("nextToken=page-2") ? PageTwo : PageOne);
        var client = new WhoopApiClient(new HttpClient(handler));

        var start = new DateTime(2026, 9, 1, 0, 0, 0, DateTimeKind.Utc);
        var recoveries = await client.GetRecoveriesAsync("token-abc", start);

        Assert.Equal(new[] { "93845", "93848" }, recoveries.Select(r => r.CycleId));
        Assert.Equal(72, recoveries[0].RecoveryScore);
        Assert.Equal(61.4, recoveries[0].HrvMs);
        Assert.Equal(48, recoveries[0].RestingHeartRate);
        Assert.Equal(new DateTime(2026, 9, 10, 5, 12, 0, 123, DateTimeKind.Utc), recoveries[0].CreatedAtUtc);
        Assert.Equal(DateTimeKind.Utc, recoveries[0].CreatedAtUtc.Kind);

        Assert.Equal(2, handler.Requests.Count);
        Assert.All(handler.Requests, r => Assert.Equal("Bearer token-abc", r.Headers.Authorization!.ToString()));
        Assert.Contains("start=2026-09-01T00%3A00%3A00.000Z", handler.Requests[0].RequestUri!.Query);
        Assert.Contains("limit=25", handler.Requests[0].RequestUri!.Query);
        Assert.DoesNotContain("nextToken", handler.Requests[0].RequestUri!.Query);
        Assert.StartsWith(WhoopApiClient.BaseUrl + "/recovery", handler.Requests[0].RequestUri!.ToString());
    }

    [Fact]
    public async Task GetProfileAsync_ReturnsUserIdAsString()
    {
        var handler = new FakeHandler(_ =>
            """{"user_id":10129,"email":"rider@example.com","first_name":"A","last_name":"B"}""");
        var client = new WhoopApiClient(new HttpClient(handler));

        var profile = await client.GetProfileAsync("token-abc");

        Assert.Equal("10129", profile.UserId);
        Assert.Equal(WhoopApiClient.BaseUrl + "/user/profile/basic", handler.Requests[0].RequestUri!.ToString());
    }
}
