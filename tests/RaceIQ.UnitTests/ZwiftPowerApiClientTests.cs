using System.Net;
using RaceIQ.Infrastructure.ZwiftPower;
using Xunit;

namespace RaceIQ.UnitTests;

public class ZwiftPowerApiClientTests
{
    private class FakeHandler : HttpMessageHandler
    {
        private readonly string _json;
        public HttpRequestMessage? LastRequest;

        public FakeHandler(string json) => _json = json;

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            LastRequest = request;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(_json, System.Text.Encoding.UTF8, "application/json")
            });
        }
    }

    [Fact]
    public async Task GetRecentResultsAsync_ParsesResultsAndSendsCookie()
    {
        // Wrapped in a top-level "data" object, not a bare array - see the
        // envelope-shape comment in ZwiftPowerApiClient for why (Task 6 research).
        var handler = new FakeHandler(
            """{"data":[{"race_id":999,"event_name":"Crit Race","event_date":1756742400,"category":"B","position":4,"time":2705.5}]}""");
        var client = new ZwiftPowerApiClient(new HttpClient(handler));

        var results = await client.GetRecentResultsAsync("session=abc123", "12345");

        Assert.Single(results);
        Assert.Equal("999", results[0].RaceId);
        Assert.Equal("Crit Race", results[0].EventName);
        Assert.Equal("B", results[0].Category);
        Assert.Equal(4, results[0].Position);
        Assert.Equal(TimeSpan.FromSeconds(2705.5), results[0].Duration);
        Assert.Contains("session=abc123", handler.LastRequest!.Headers.GetValues("Cookie"));
    }

    [Fact]
    public async Task GetRecentResultsAsync_FallsBackToEventDateWhenRaceIdAndEventNameAreMissing()
    {
        // Simulates the real disagreement over this endpoint's field names (see the
        // doc comment on ZwiftPowerResultPayload): race_id and event_name may not
        // exist in the live JSON at all. Two entries, both missing those keys, with
        // different event_date values so we can assert the fallback stays distinct
        // per race rather than collapsing every result onto the same id.
        var handler = new FakeHandler(
            """{"data":[{"event_date":1756742400,"time":1000},{"event_date":1756828800,"time":2000}]}""");
        var client = new ZwiftPowerApiClient(new HttpClient(handler));

        var results = await client.GetRecentResultsAsync("session=abc123", "12345");

        Assert.Equal(2, results.Count);
        Assert.All(results, r => Assert.False(string.IsNullOrEmpty(r.RaceId)));
        Assert.All(results, r => Assert.NotEqual("0", r.RaceId));
        Assert.NotEqual(results[0].RaceId, results[1].RaceId);
        Assert.All(results, r => Assert.False(string.IsNullOrEmpty(r.EventName)));
    }
}
