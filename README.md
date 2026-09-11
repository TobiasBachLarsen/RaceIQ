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

## Roadmap

Level 2 — tactical/draft analysis from live Zwift race telemetry — is a deliberately
separate follow-up project; see
`docs/superpowers/specs/2026-09-09-raceiq-phase1-design.md` for why, and "Plan B" for
the ZwiftPower/ZwiftRacing result-import feature layered on top of this app.
