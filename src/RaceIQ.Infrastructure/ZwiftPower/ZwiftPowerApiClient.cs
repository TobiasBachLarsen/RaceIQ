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

        // ZwiftPower's cache3 JSON endpoints wrap their row array in an object under a
        // "data" property - they do NOT return a bare JSON array. This was confirmed
        // (Task 6) by reading the real source of two independent community tools that
        // call this exact endpoint, since Tobias's own session cookie wasn't available
        // to curl the live endpoint during this task:
        //   - jessicah/ZwiftPower (C#): ProfileResultsAsync(int zwid) calls
        //     "/cache3/profile/{zwid}_all.json" via a helper (ParseListAsync<Result>)
        //     that deserializes into an internal `Data<T>` type holding `T[] data`,
        //     then returns `result.data`.
        //   - puckdoug/zpdatafetch (Python): ZPCyclistFetch fetches the identical URL
        //     ("https://zwiftpower.com/cache3/profile/" + id + "_all.json"), discards
        //     the parsed response unless it's a dict, and ZPCyclist.racelog raises
        //     KeyError if a "data" key is absent - i.e. it also assumes an object
        //     wrapper, not a bare array.
        // Still provisional: neither source was cross-checked against a live curl to
        // Tobias's own account, so treat this as our best evidence rather than
        // ground truth until confirmed.
        var envelope = await response.Content.ReadFromJsonAsync<ZwiftPowerResponseEnvelope>()
            ?? new ZwiftPowerResponseEnvelope(null);
        var raw = envelope.Data ?? new();

        return raw.Select(r => new ZwiftPowerRaceResult(
            r.RaceId.ToString(),
            r.EventName,
            DateTimeOffset.FromUnixTimeSeconds(r.EventDate).UtcDateTime,
            r.Category,
            r.Position,
            r.Time.HasValue ? TimeSpan.FromSeconds(r.Time.Value) : null)).ToList();
    }

    private record ZwiftPowerResponseEnvelope(
        [property: JsonPropertyName("data")] List<ZwiftPowerResultPayload>? Data);

    // Field names below (race_id, event_name, event_date, category, position, time)
    // are the brief's original assumption, sourced from zpdatafetch's
    // zpraceresult.py. NOT re-verified in Task 6: while confirming the envelope
    // shape above, we found zpraceresult.py actually models a *different* ZwiftPower
    // endpoint (a single event's full participant list), not this rider-profile
    // endpoint. The zpdatafetch code that actually calls THIS endpoint
    // (zpcyclistfetch.py -> zpcyclist.py -> zpracefinish.py) uses different field
    // names for what looks like the same per-race data: no "race_id" field at all,
    // "event_title" instead of "event_name", "pos" instead of "position", and
    // "category" derived from a numeric "div" code rather than a raw string.
    // jessicah's C# Result record for this same endpoint likewise has no explicit
    // race_id/position fields. So the wrapper fix above is well-evidenced, but these
    // inner field names remain UNVERIFIED - confirm against one of Tobias's real
    // ZwiftPower activities before trusting non-null RaceId/EventName/Position values.
    private record ZwiftPowerResultPayload(
        [property: JsonPropertyName("race_id")] long RaceId,
        [property: JsonPropertyName("event_name")] string EventName,
        [property: JsonPropertyName("event_date")] long EventDate,
        [property: JsonPropertyName("category")] string? Category,
        [property: JsonPropertyName("position")] int? Position,
        [property: JsonPropertyName("time")] double? Time);
}
