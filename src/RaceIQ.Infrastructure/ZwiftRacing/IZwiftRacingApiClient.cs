namespace RaceIQ.Infrastructure.ZwiftRacing;

public interface IZwiftRacingApiClient
{
    Task<IReadOnlyList<ZwiftRacingRaceResult>> GetRecentResultsAsync(string apiKey, string zwiftRiderId);
}

public record ZwiftRacingRaceResult(
    string RaceId,
    string EventTitle,
    DateTime EventTime,
    string? Category,
    int? Position,
    int? FieldSize,
    TimeSpan? Duration,
    double? RatingDelta);
