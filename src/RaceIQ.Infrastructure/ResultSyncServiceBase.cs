using Microsoft.Extensions.Logging;
using RaceIQ.Application;
using RaceIQ.Application.Exceptions;
using RaceIQ.Domain;

namespace RaceIQ.Infrastructure;

// Shared flow for providers that sync race results from an external API into
// RaceResultImporter: load the account, skip if it already needs reconnecting, fetch,
// flip to NeedsReconnect on any failure, project each result, import. A concrete provider
// supplies only its Provider, FetchAsync, and Project.
public abstract class ResultSyncServiceBase<TResult> : IProviderSyncService
{
    private readonly IConnectedAccountRepository _accountRepository;
    private readonly RaceResultImporter _importer;
    private readonly ILogger _logger;

    protected ResultSyncServiceBase(
        IConnectedAccountRepository accountRepository,
        RaceResultImporter importer,
        ILogger logger)
    {
        _accountRepository = accountRepository;
        _importer = importer;
        _logger = logger;
    }

    public abstract ConnectedAccountProvider Provider { get; }

    protected abstract Task<IReadOnlyList<TResult>> FetchAsync(ConnectedAccount account);

    protected abstract RaceResult Project(TResult result, string userId);

    public async Task SyncAsync(string userId)
    {
        var account = await _accountRepository.GetAsync(userId, Provider)
            ?? throw new ConnectedAccountNotFoundException(userId);

        if (account.Status == ConnectedAccountStatus.NeedsReconnect)
            return;

        IReadOnlyList<TResult> results;
        try
        {
            results = await FetchAsync(account);
        }
        catch (Exception ex)
        {
            // Covers both a real network failure and an expired credential that HttpClient's
            // default redirect-following turns into an HTTP 200 login page: EnsureSuccess
            // passes on that, and the downstream ReadFromJsonAsync is what actually throws.
            // Either shape means the credential can no longer be used, so treat them alike.
            _logger.LogWarning(ex,
                "{Provider} sync failed for user {UserId}; flipping account to NeedsReconnect", Provider, userId);
            account.Status = ConnectedAccountStatus.NeedsReconnect;
            await _accountRepository.UpsertAsync(account);
            return;
        }

        var incoming = results.Select(result => Project(result, userId)).ToList();
        await _importer.ImportAsync(userId, incoming);
    }
}
