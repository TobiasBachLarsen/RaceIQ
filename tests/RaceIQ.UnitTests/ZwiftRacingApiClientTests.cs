using System.Net;
using RaceIQ.Infrastructure.ZwiftRacing;
using Xunit;

namespace RaceIQ.UnitTests;

public class ZwiftRacingApiClientTests
{
    private class FakeHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, HttpResponseMessage> _responder;
        public FakeHandler(Func<HttpRequestMessage, HttpResponseMessage> responder) => _responder = responder;

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(_responder(request));
    }

    private static HttpResponseMessage Json(string body) => new(HttpStatusCode.OK)
    {
        Content = new StringContent(body, System.Text.Encoding.UTF8, "application/json")
    };

    [Fact]
    public async Task GetRecentResultsAsync_DiscoversRaceThenFetchesAndFiltersToThisRider()
    {
        var handler = new FakeHandler(req =>
        {
            if (req.RequestUri!.AbsolutePath.Contains("/riders/"))
                return Json("""{"recent_race_ids":[777]}""");

            // Field names here (eventId, title, time, riderId, ratingDelta) are the real
            // raw ZwiftRacing API keys, matching ZwiftRacingRaceDetailPayload.
            return Json("""
                {"eventId":777,"title":"Club Crit","time":1756742400,
                 "results":[
                   {"riderId":12345,"position":4,"category":"B","time":2700.0,"ratingDelta":18.5},
                   {"riderId":999,"position":1,"category":"B","time":2600.0,"ratingDelta":25.0}
                 ]}
                """);
        });
        var client = new ZwiftRacingApiClient(new HttpClient(handler));

        var results = await client.GetRecentResultsAsync("test-key", "12345");

        Assert.Single(results);
        Assert.Equal("777", results[0].RaceId);
        Assert.Equal("Club Crit", results[0].EventTitle);
        Assert.Equal(4, results[0].Position);
        Assert.Equal(2, results[0].FieldSize);
        Assert.Equal(18.5, results[0].RatingDelta);
    }

    [Fact]
    public async Task GetRecentResultsAsync_DiscoveryPayloadWithoutRecentRaceIds_ReturnsEmpty()
    {
        // Documents the real, confirmed behaviour of DiscoverRecentRaceIdsAsync's guessed
        // endpoint: /public/riders/{id} is a real ZwiftRacing endpoint, but it returns a
        // rider rating profile (name, gender, rating, handicaps, phenotype, power curve),
        // never a "recent_race_ids" property. Until the real rider-race-history endpoint
        // is confirmed, calling this against a live rider returns 200 OK with a payload
        // shaped like this, and GetRecentResultsAsync must degrade to an empty list rather
        // than throwing.
        var handler = new FakeHandler(_ => Json("""
            {"name":"Jane Rider","gender":"F","riderId":12345,
             "race":{"current":{"rating":1500.0,"mixed":{"category":"B"}}}}
            """));
        var client = new ZwiftRacingApiClient(new HttpClient(handler));

        var results = await client.GetRecentResultsAsync("test-key", "12345");

        Assert.Empty(results);
    }
}
