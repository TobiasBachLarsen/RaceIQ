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
}
