# RaceIQ

A cycling training companion: connect your Strava account, see your ride history, and
get an AI-written pacing analysis for any ride.

## Architecture

- `RaceIQ.Domain` — plain models (`Activity`, `ConnectedAccount`, `AnalysisReport`, ...)
- `RaceIQ.Application` — business logic behind repository/client interfaces,
  independent of EF Core, Strava, and Claude
- `RaceIQ.Infrastructure` — EF Core + PostgreSQL, Strava OAuth/API client, Claude API
  client
- `RaceIQ.Web` — ASP.NET Core Blazor Server with ASP.NET Core Identity

## Running locally

Prerequisites: .NET 8 SDK, a local PostgreSQL instance with a `raceiq` database and
user (see `docs/superpowers/plans/2026-09-09-raceiq-plan-a-implementation-plan.md`
Task 3 for exact setup commands), a Strava API application (register one at
https://www.strava.com/settings/api) for `Strava:ClientId`/`Strava:ClientSecret` in
`appsettings.Development.json`, and an `ANTHROPIC_API_KEY` environment variable.

```bash
export PATH="$HOME/.dotnet:$PATH"
export ANTHROPIC_API_KEY="sk-ant-..."
dotnet run --project src/RaceIQ.Web
```

## Testing

```bash
dotnet test RaceIQ.sln
```

- Unit tests (`tests/RaceIQ.UnitTests`) exercise `ActivityAnalysisService`'s prompt
  building and analysis flow against fake repositories and a fake Claude client.
- Integration tests (`tests/RaceIQ.IntegrationTests`) exercise the real Strava OAuth
  callback and a full sync-then-analyze flow through `WebApplicationFactory`, against a
  real (test) PostgreSQL database with Strava and Claude's HTTP responses faked.

## Security

Provider credentials — the Strava access/refresh tokens, the ZwiftPower session cookie,
and the ZwiftRacing API key — are encrypted at rest. The `ConnectedAccount.AccessToken`
and `RefreshToken` columns run through an EF Core value converter backed by ASP.NET
Core Data Protection (`IDataProtector`), so the database only ever stores ciphertext; the
public Zwift rider id is not a secret and stays in clear text. Data Protection keys are
persisted by the host's default key ring, so in a real deployment they must live somewhere
stable and backed up — if the keys are lost a stored credential can no longer be decrypted
and the account simply falls back to a reconnect prompt.

## Roadmap

Level 2 — tactical/draft analysis from live Zwift race telemetry — is a deliberately
separate follow-up project; see
`docs/superpowers/specs/2026-09-09-raceiq-phase1-design.md` for why, and "Plan B" for
the ZwiftPower/ZwiftRacing result-import feature layered on top of this app.
