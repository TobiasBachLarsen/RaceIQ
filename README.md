# RaceIQ

A cycling training companion: connect Strava to sync your rides, import your Zwift race
results from ZwiftPower and ZwiftRacing, pull your daily recovery from WHOOP, see an
overview of your training and racing at a glance, and get an AI-written pacing analysis for
any ride.

## Features

- **Strava sync** — connect via OAuth and pull your recent rides with power/HR streams.
- **AI pacing analysis** — a Danish, ride-specific write-up grounded in the real
  power/HR data, aware of whether the ride was a race (so it doesn't flag normal
  pack-riding surges as pacing mistakes).
- **Race-result import** — paste your results JSON from ZwiftPower (it has no API and its
  data sits behind signed CloudFront cookies, so you copy it from your own logged-in
  browser) or connect ZwiftRacing (API key); results are matched to the Strava ride by
  date and duration and shown as category, placement, and rating change on the ride.
- **WHOOP recovery** — connect WHOOP via OAuth and sync your daily recovery score, HRV,
  and resting heart rate. The latest reading sits on the overview, each ride shows the
  recovery from the morning it was ridden, and the AI analysis takes it into account (a
  fade on a 25 % recovery day reads differently from one on an 80 % day).
- **Overview dashboard** — all-time and last-30-days totals (rides, distance, time,
  elevation, races, best placement, rating) at the top of the home page.
- **Credentials encrypted at rest** — see [Security](#security).

## Architecture

- `RaceIQ.Domain` — plain models (`Activity`, `ConnectedAccount`, `RaceResult`,
  `RecoveryDay`, `AnalysisReport`, ...)
- `RaceIQ.Application` — business logic behind repository/client interfaces,
  independent of EF Core, Strava, Zwift, and Claude
- `RaceIQ.Infrastructure` — EF Core + PostgreSQL, Strava and WHOOP OAuth/API clients,
  the ZwiftPower results parser, the ZwiftRacing client, Claude API client
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
`Strava:ClientId` and `Strava:ClientSecret`. For WHOOP, create an app in the
[WHOOP developer dashboard](https://developer.whoop.com) with the redirect URL
`http://localhost:5120/whoop/callback` and the `offline`, `read:recovery`, and
`read:profile` scopes, and add `Whoop:ClientId` and `Whoop:ClientSecret` the same way. Then
run. The app applies EF Core migrations
automatically on startup, so the tables are created on first run.

```bash
export ANTHROPIC_API_KEY="sk-ant-..."
dotnet run --project src/RaceIQ.Web
```

The app starts without an Anthropic key or Strava/WHOOP credentials — you just can't run
the AI analysis or connect those providers until they are set.

## Testing

```bash
dotnet test RaceIQ.sln
```

- Unit tests (`tests/RaceIQ.UnitTests`) cover the analysis prompt building, the overview
  stats, the race-result matching and import, the Strava and WHOOP token refresh, and the
  ZwiftPower, ZwiftRacing, and WHOOP response parsing, against fakes.
- Integration tests (`tests/RaceIQ.IntegrationTests`) exercise the real Strava and WHOOP
  OAuth callbacks, the connect and sync flows, credential encryption at rest, and a full
  sync-then-analyze flow through `WebApplicationFactory`, against a real (test) PostgreSQL
  database with the external HTTP responses faked.

## Security

Provider credentials — the Strava and WHOOP access/refresh tokens and the ZwiftRacing API
key — are encrypted at rest. The `ConnectedAccount.AccessToken`
and `RefreshToken` columns run through an EF Core value converter backed by ASP.NET
Core Data Protection (`IDataProtector`), so the database only ever stores ciphertext; the
public Zwift rider id (also all ZwiftPower stores) is not a secret and stays in clear text. Data Protection keys are
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
