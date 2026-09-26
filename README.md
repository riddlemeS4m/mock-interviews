# Mock Interviews

## Local setup (macOS and Linux)

Install these prerequisites:

- The [.NET 10 SDK](https://learn.microsoft.com/dotnet/core/install/). The required SDK feature band is recorded in `global.json`.
- [PostgreSQL](https://www.postgresql.org/download/) server and command-line tools.
- `curl` and either `shasum` (macOS) or `sha256sum` (Linux), used by the Tailwind build script.
- [Mailpit](https://mailpit.axllent.org/docs/install/) for viewing email sent during local development. On macOS with Homebrew, run `brew install mailpit`; on Linux, follow Mailpit's package or binary instructions for your distribution.

Then, from the repository root:

1. Copy the environment template: `cp .env.example .env`.
2. Edit `.env`, at minimum replacing the database password, super-user email, and seeded administrator password placeholders. See [Environment configuration](#environment-configuration) below.
3. Create the development databases. On macOS, run `psql postgres`; Linux installations using peer authentication can run `sudo -u postgres psql` instead. At the prompt, run:

   ```sql
   SELECT current_user;
   \password
   CREATE DATABASE mockinterviews;
   CREATE DATABASE mock_interviews_test_db;
   \q
   ```

   Use the reported role name and the password you set here in both connection strings in `.env`. If your PostgreSQL host, port, or database names differ, update the connection strings to match as well.
4. Restore tools and dependencies, build the generated CSS and application, and apply database migrations:

   ```sh
   dotnet tool restore
   dotnet restore mock-interviews.sln
   ./scripts/tailwind.sh build
   dotnet build mock-interviews.sln --no-restore
   dotnet ef database update --project mock-interviews
   ```

5. Start Mailpit in one terminal with `mailpit`. In another terminal, start the application over HTTP with `dotnet run --project mock-interviews --launch-profile http`.
6. Open the application at http://localhost:5157 and Mailpit at http://127.0.0.1:8025. Mailpit is a local capture-only inbox; messages never reach the addressed recipient.

To use the HTTPS launch profile on macOS, first run `dotnet dev-certs https --trust`. Certificate trust on Linux is distribution- and browser-specific, so the HTTP profile is the simplest local default.

In Conductor, start both `Mailpit` and `Run Server`. The Mailpit script prints that workspace's UI URL and configures SMTP on a workspace-specific port, so multiple workspaces can run independently.

## Environment configuration

Local Development runs load the nearest `.env` file at or above the working directory. Copy `.env.example` rather than creating it from scratch, never commit `.env`, and use double underscores to represent nested .NET configuration keys. Values already exported by the process take precedence over values in `.env`.

| Variable | Requirement | Purpose |
| --- | --- | --- |
| `ConnectionString__DefaultConnection` | Required | PostgreSQL connection used by the application and Entity Framework migrations. |
| `IntegrationTests__ConnectionString` | Required for integration specs | Connection to the dedicated `mock_interviews_test_db` database. The test safety check permits only that database name on a loopback host. |
| `SuperUser__Email` | Required | Email address for the seeded administrator and application sender. |
| `SeededAdminPwd` | Required | Initial password for the seeded administrator. Use a development-only value locally and a secret in production. |
| `Email__Provider` | Optional override | `Smtp` or `SendGrid`. Development defaults to `Smtp`; production defaults to `SendGrid`. |
| `Email__Smtp__Host`, `Email__Smtp__Port`, `Email__Smtp__UseTls` | Optional development overrides | SMTP endpoint settings. Development defaults target Mailpit at `127.0.0.1:1025` without TLS; an SMTP provider always requires a valid host and port. |
| `Email__Smtp__Username`, `Email__Smtp__Password` | Optional pair | SMTP credentials; if one is set, both must be set. Mailpit does not require them. |
| `SendGrid__ApiKey` | Required with `Email__Provider=SendGrid` | SendGrid API credential. |
| `Authentication__Microsoft__ClientId`, `Authentication__Microsoft__ClientSecret` | Optional pair | Enables Microsoft sign-in; if one is set, both must be set. Local ASP.NET Identity remains available when they are unset. |
| `ASPNETCORE_ENVIRONMENT` | Set by launch profile locally; required in production | Selects environment-specific settings. Use `Production` when deployed. |
| `ASPNETCORE_URLS` | Required by the Railway deployment | Sets the listening URL, normally `http://+:${PORT}` on Railway. |

Restart the application after changing configuration. Do not put production secrets in `.env`; configure them through the deployment environment.

## Tailwind CSS

Tailwind uses its pinned standalone CLI, so Node.js and a JavaScript package manager are not required. The build script downloads the correct macOS or Linux executable and verifies its checksum before use.

- Run `./scripts/tailwind.sh build` for a minified one-time build.
- Run `./scripts/tailwind.sh watch` while editing Tailwind views.
- In Conductor, use the `Tailwind CSS` run script alongside `Run Server`.

The generated `mock-interviews/wwwroot/css/tailwind.css` file is gitignored and is rebuilt by workspace setup, CI, and Docker publish. Tailwind scans all Razor views, areas, UI helpers, view components, and application JavaScript configured in `mock-interviews/Styles/tailwind.css`.

## Integration specs

Integration specs use the dedicated PostgreSQL database created during local setup and erase its application data between tests. Set `IntegrationTests__ConnectionString` in the root `.env` file as shown in `.env.example`, then run:

```sh
dotnet test mock-interviews.sln
```

For safety, the test fixture cleans only a database named exactly `mock_interviews_test_db` on a loopback host
(`localhost`, `127.0.0.1`, or `::1`).

## Authentication

Local ASP.NET Identity accounts are always available and new accounts must confirm their email. Microsoft sign-in is optional: set both `Authentication__Microsoft__ClientId` and `Authentication__Microsoft__ClientSecret` to enable it, or leave both unset.


## Resource Links

Set `mock_interview_manual` and `guest_parking_pass` to public HTTP(S) URLs in the admin Event Configuration screen. Unconfigured or invalid resource URLs are not shown to interviewers.

## Production

The root `.env` file is loaded only for Development runs. Set all required values as process environment variables or deployment secrets. Production requires `Email__Provider=SendGrid` (the base default) and `SendGrid__ApiKey`; it will not fall back to SMTP. Set `ASPNETCORE_ENVIRONMENT=Production` and `ASPNETCORE_URLS=http://+:${PORT}` as described in Railway's [ASP.NET Core deployment guide](https://docs.railway.com/guides/aspnet-core).

## Original Team Members:

Logan Thompson - PM
Erin O'Laughlin - BA
Jaehee Kim - UI/UX Lead
Sam Riddle - Tech Lead
