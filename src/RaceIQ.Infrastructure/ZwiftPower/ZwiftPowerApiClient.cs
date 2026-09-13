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

        return raw.Select(r =>
        {
            var eventDate = DateTimeOffset.FromUnixTimeSeconds(r.EventDate).UtcDateTime;

            // Defensive fallback (added after Task 6 review): three independent real
            // sources disagree on this endpoint's actual field names (see the doc
            // comment on ZwiftPowerResultPayload below), so `race_id`/`event_name`
            // may simply not exist in the live JSON. If they don't, System.Text.Json
            // silently defaults RaceId to 0 for every result - which would make every
            // race share ProviderResultId "0" and collide on the
            // (UserId, Provider, ProviderResultId) de-dup key from Task 3, silently
            // overwriting one race's stored result with the next sync's. EventDate is
            // the one field all three disagreeing sources agree exists and is
            // genuinely distinct per race for a single rider, so it's the fallback:
            // worst case (wrong field-name guess) results still land in separate,
            // provisionally-labeled rows instead of overwriting each other.
            var raceId = r.RaceId != 0
                ? r.RaceId.ToString()
                : eventDate.ToString("yyyyMMddHHmmss");

            var eventName = string.IsNullOrEmpty(r.EventName)
                ? $"ZwiftPower race {eventDate:d}"
                : r.EventName;

            return new ZwiftPowerRaceResult(
                raceId,
                eventName,
                eventDate,
                r.Category,
                r.Position,
                r.Time.HasValue ? TimeSpan.FromSeconds(r.Time.Value) : null);
        }).ToList();
    }

    private record ZwiftPowerResponseEnvelope(
        [property: JsonPropertyName("data")] List<ZwiftPowerResultPayload>? Data);

    // Field names below (race_id, event_name, event_date, category, position, time)
    // are still the brief's original, unverified guess. Three independent real
    // sources now disagree on what this endpoint's actual field names are:
    //   - The brief's own cited source (zpdatafetch's zpraceresult.py) turned out to
    //     model a different endpoint entirely - a single event's full participant
    //     list, not this rider-profile endpoint.
    //   - jessicah/ZwiftPower's C# `Result` record for this exact endpoint
    //     (ProfileResultsAsync) has no race_id, event_name, or position field at
    //     all - only zwid (the rider's own id, not per-race), zid (string, possibly
    //     the per-race id), div/divw (numeric division, not a letter category),
    //     event_date, time_gun, distance.
    //   - zpdatafetch's zpracefinish.py (which does call this exact endpoint via
    //     zpcyclistfetch.py) uses yet another set of names: no race_id, event_title
    //     instead of event_name, pos instead of position.
    // With three mutually-disagreeing guesses and no live data to settle it,
    // GetRecentResultsAsync falls back to an EventDate-derived RaceId and a
    // generated EventName placeholder whenever the parsed race_id/event_name comes
    // back missing/zero/empty (see the fallback logic above), so a wrong
    // field-name guess can never collapse multiple races onto the same
    // ProviderResultId - it can only produce a provisionally-labeled row instead of
    // a lost one. Position and Category are left nullable and un-fallback'd on
    // purpose: a permanently-null value there is a real possibility (no source
    // confirms either field exists on this endpoint), not something to guess
    // around. Confirm all of this against one of Tobias's real ZwiftPower
    // activities before trusting non-placeholder RaceId/EventName values.
    private record ZwiftPowerResultPayload(
        [property: JsonPropertyName("race_id")] long RaceId,
        [property: JsonPropertyName("event_name")] string? EventName,
        [property: JsonPropertyName("event_date")] long EventDate,
        [property: JsonPropertyName("category")] string? Category,
        [property: JsonPropertyName("position")] int? Position,
        [property: JsonPropertyName("time")] double? Time);
}
