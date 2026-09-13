namespace RaceIQ.Infrastructure.ZwiftPower;

public interface IZwiftPowerApiClient
{
    Task<IReadOnlyList<ZwiftPowerRaceResult>> GetRecentResultsAsync(string sessionCookie, string zwiftRiderId);
}

public record ZwiftPowerRaceResult(
    string RaceId,
    string EventName,
    DateTime EventDate,
    string? Category,
    int? Position,
    TimeSpan? Duration);
