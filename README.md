# RaceIQ

A cycling training companion: connect Strava to sync your rides, import your Zwift race
results from ZwiftPower and ZwiftRacing, see an overview of your training and racing at a
glance, and get an AI-written pacing analysis for any ride.

## Features

- **Strava sync** — connect via OAuth and pull your recent rides with power/HR streams.
- **AI pacing analysis** — a Danish, ride-specific write-up grounded in the real
  power/HR data, aware of whether the ride was a race (so it doesn't flag normal
  pack-riding surges as pacing mistakes).
- **Race-result import** — pull results from ZwiftPower (session cookie) and ZwiftRacing
  (API key), match them to the matching Strava ride by date and duration, and show
  category, placement, and rating change on the ride.
- **Overview dashboard** — all-time and last-30-days totals (rides, distance, time,
  elevation, races, best placement, rating) at the top of the home page.
- **Credentials encrypted at rest** — see [Security](#security).

## Architecture

- `RaceIQ.Domain` — plain models (`Activity`, `ConnectedAccount`, `RaceResult`,
  `AnalysisReport`, ...)
- `RaceIQ.Application` — business logic behind repository/client interfaces,
  independent of EF Core, Strava, Zwift, and Claude
- `RaceIQ.Infrastructure` — EF Core + PostgreSQL, Strava OAuth/API client, ZwiftPower and
  ZwiftRacing clients, Claude API client
- `RaceIQ.Web` — ASP.NET Core Blazor Server with ASP.NET Core Identity

## Running locally

Prerequisites: the .NET 8 SDK, a local PostgreSQL instance, a Strava API application
(register one at https://www.strava.com/settings/api), and an Anthropic API key.

Create the database and role the app expects (matching the default connection string in
`appsettings.json`):

```bash
psql -c "CREATE ROLE raceiq WITH LOGIN PASSWORD 'raceiq_dev';"
psql -c "CREATE DATABASE raceiq OWNER raceiq;"
```

Put your Strava credentials in `appsettings.Development.json` (gitignored) as
`Strava:ClientId` and `Strava:ClientSecret`, then run. The app applies EF Core migrations
automatically on startup, so the tables are created on first run.

```bash
export ANTHROPIC_API_KEY="sk-ant-..."
dotnet run --project src/RaceIQ.Web
```

The app starts without an Anthropic key or Strava credentials — you just can't run the AI
analysis or connect Strava until they are set.

## Testing

```bash
dotnet test RaceIQ.sln
```

- Unit tests (`tests/RaceIQ.UnitTests`) cover the analysis prompt building, the overview
  stats, the race-result matching and import, the Strava token refresh, and the ZwiftPower
  and ZwiftRacing response parsing, against fakes.
- Integration tests (`tests/RaceIQ.IntegrationTests`) exercise the real Strava OAuth
  callback, the connect and sync flows, credential encryption at rest, and a full
  sync-then-analyze flow through `WebApplicationFactory`, against a real (test) PostgreSQL
  database with the external HTTP responses faked.

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

**Level 2 — tactical/draft analysis from live Zwift race telemetry.** This is a
deliberately separate follow-up project. Draft percentage and rider positions are not kept
in Strava, ZwiftPower, or ZwiftRacing after a race ends; that data exists only live, inside
Zwift's game protocol while the ride is happening. Capturing it needs a live listener
during the race, which is a different shape of system than this request/response app, so
it is scoped as its own project rather than an extension of this one.
