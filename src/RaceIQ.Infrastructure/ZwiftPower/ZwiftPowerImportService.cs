using Microsoft.Extensions.Logging;
using RaceIQ.Application;
using RaceIQ.Domain;

namespace RaceIQ.Infrastructure.ZwiftPower;

// Takes the results JSON a rider pasted from ZwiftPower, stores every race in it, and
// matches them to Strava rides. The rider id is remembered on a ConnectedAccount so the
// dashboard can link straight to the rider's JSON next time.
public class ZwiftPowerImportService
{
    private readonly RaceResultImporter _importer;
    private readonly IConnectedAccountRepository _accountRepository;
    private readonly ILogger<ZwiftPowerImportService> _logger;

    public ZwiftPowerImportService(
        RaceResultImporter importer,
        IConnectedAccountRepository accountRepository,
        ILogger<ZwiftPowerImportService> logger)
    {
        _importer = importer;
        _accountRepository = accountRepository;
        _logger = logger;
    }

    public async Task<RaceResultImportOutcome> ImportAsync(string userId, string zwiftRiderId, string resultsJson)
    {
        var results = ZwiftPowerResultParser.Parse(resultsJson);

        var incoming = results.Select(r => new RaceResult
        {
            UserId = userId,
            Provider = ConnectedAccountProvider.ZwiftPower,
            ProviderResultId = r.RaceId,
            EventName = r.EventName,
            EventDate = r.EventDate,
            Category = r.Category,
            Position = r.Position,
            Duration = r.Duration,
            RatingChange = r.RatingChange
        }).ToList();

        var outcome = await _importer.ImportAsync(userId, incoming);

        if (!string.IsNullOrWhiteSpace(zwiftRiderId))
        {
            // No credential is involved any more - the account row only remembers the
            // rider id (public) so the import card can prefill it.
            await _accountRepository.UpsertAsync(new ConnectedAccount
            {
                UserId = userId,
                Provider = ConnectedAccountProvider.ZwiftPower,
                AccessToken = string.Empty,
                ExternalAccountId = zwiftRiderId.Trim(),
                Status = ConnectedAccountStatus.Connected
            });
        }

        _logger.LogInformation(
            "ZwiftPower import for user {UserId}: {Imported} races, {Matched} matched", userId, outcome.Imported, outcome.Matched);
        return outcome;
    }
}
