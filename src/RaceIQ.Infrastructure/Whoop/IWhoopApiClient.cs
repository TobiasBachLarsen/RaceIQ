namespace RaceIQ.Infrastructure.Whoop;

public interface IWhoopApiClient
{
    Task<WhoopProfile> GetProfileAsync(string accessToken);

    // Every scored recovery from startUtc onwards, following pagination to the end.
    Task<IReadOnlyList<WhoopRecovery>> GetRecoveriesAsync(string accessToken, DateTime startUtc);
}

public record WhoopProfile(string UserId);

public record WhoopRecovery(
    string CycleId,
    DateTime CreatedAtUtc,
    int RecoveryScore,
    double HrvMs,
    int RestingHeartRate);
