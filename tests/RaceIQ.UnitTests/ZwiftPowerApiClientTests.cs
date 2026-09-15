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
        // Field names and shapes mirror a real /cache3/profile/{id}_all.json row: rows
        // are wrapped in a top-level "data" object, "time" is a [seconds, flag] array
        // while "time_gun" is the scalar finish time, and event_title may have leading
        // whitespace. Personal fields (name, HR, power) are omitted - only what the
        // parser reads is kept.
        var handler = new FakeHandler(
            """{"data":[{"zid":"5233969","event_title":" Crit Race","event_date":1756742400,"category":"B","pos":4,"time":[2705.5,0],"time_gun":2705.5,"f_t":"TYPE_RACE TYPE_RACE "}]}""");
        var client = new ZwiftPowerApiClient(new HttpClient(handler));

        var results = await client.GetRecentResultsAsync("session=abc123", "12345");

        Assert.Single(results);
        Assert.Equal("5233969", results[0].RaceId);
        Assert.Equal("Crit Race", results[0].EventName);      // trimmed
        Assert.Equal("B", results[0].Category);
        Assert.Equal(4, results[0].Position);
        Assert.Equal(TimeSpan.FromSeconds(2705.5), results[0].Duration);
        Assert.Contains("session=abc123", handler.LastRequest!.Headers.GetValues("Cookie"));
    }

    [Fact]
    public async Task GetRecentResultsAsync_KeepsOnlyRaces()
    {
        // The profile feed mixes races with workouts and rides; only TYPE_RACE entries
        // are real results. A disqualified race stays TYPE_RACE (category "DQ") and is
        // kept.
        var handler = new FakeHandler(
            """
            {"data":[
              {"zid":"1","event_title":"Real Race","event_date":1756742400,"category":"C","pos":10,"time_gun":1800,"f_t":"TYPE_RACE TYPE_RACE "},
              {"zid":"2","event_title":"Workout Hour","event_date":1756828800,"category":"E","pos":42,"time_gun":3300,"f_t":"TYPE_WORKOUT TYPE_WORKOUT"},
              {"zid":"3","event_title":"Just A Ride","event_date":1756915200,"category":"E","pos":20,"time_gun":2000,"f_t":"TYPE_RIDE"},
              {"zid":"4","event_title":"DQ Race","event_date":1757001600,"category":"DQ","pos":36,"time_gun":4315,"f_t":"TYPE_RACE TYPE_RACE "}
            ]}
            """);
        var client = new ZwiftPowerApiClient(new HttpClient(handler));

        var results = await client.GetRecentResultsAsync("session=abc123", "12345");

        Assert.Equal(new[] { "1", "4" }, results.Select(r => r.RaceId));
    }

    [Fact]
    public async Task GetRecentResultsAsync_FallsBackWhenIdOrTitleMissing()
    {
        // Defensive guard: if zid or event_title were ever absent, results must still get
        // a distinct id and a readable name rather than colliding or showing blank.
        var handler = new FakeHandler(
            """
            {"data":[
              {"event_date":1756742400,"time_gun":1000,"f_t":"TYPE_RACE"},
              {"event_date":1756828800,"time_gun":2000,"f_t":"TYPE_RACE"}
            ]}
            """);
        var client = new ZwiftPowerApiClient(new HttpClient(handler));

        var results = await client.GetRecentResultsAsync("session=abc123", "12345");

        Assert.Equal(2, results.Count);
        Assert.All(results, r => Assert.False(string.IsNullOrEmpty(r.RaceId)));
        Assert.NotEqual(results[0].RaceId, results[1].RaceId);
        Assert.All(results, r => Assert.False(string.IsNullOrWhiteSpace(r.EventName)));
    }
}
