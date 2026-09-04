# Mock Interviews

## Setup (MacOS)
1. Install .NET 10.
2. Copy the template with `cp .env.example .env`
3. Run `dotnet restore`, `./scripts/tailwind.sh build`, and `dotnet build`.
4. Install the local development inbox once with `brew install mailpit`.
5. In one terminal run `mailpit`, then start the app with either `dotnet run --project mock-interviews` from the repository root or `dotnet run` from `mock-interviews`.
6. Open Mailpit at http://127.0.0.1:8025 to inspect captured messages. Mailpit is a local capture-only inbox; messages never reach the addressed recipient.
7. Trust the development certificate with `dotnet dev-certs https --trust`.

In Conductor, start both `Mailpit` and `Run Server`. The Mailpit script prints that workspace's UI URL and configures SMTP on a workspace-specific port, so multiple workspaces can run independently.

## Tailwind CSS

Tailwind uses its pinned standalone CLI, so Node.js and a JavaScript package manager are not required. The build script downloads the correct macOS or Linux executable and verifies its checksum before use.

- Run `./scripts/tailwind.sh build` for a minified one-time build.
- Run `./scripts/tailwind.sh watch` while editing Tailwind views.
- In Conductor, use the `Tailwind CSS` run script alongside `Run Server`.

The generated `mock-interviews/wwwroot/css/tailwind.css` file is gitignored and is rebuilt by workspace setup, CI, and Docker publish. Tailwind scans all Razor views, areas, UI helpers, view components, and application JavaScript configured in `mock-interviews/Styles/tailwind.css`.

## Db setup
1. `psql postgres`
2. `create user mock_interviews_user with password 'topsecretpassword';`
3. `create database mock_interviews_db owner mock_interviews_user;`
4. `\c mock_interviews_db`
5. `alter schema public owner to mock_interviews_user;`
6. `grant all on schema public to mock_interviews_user;`
7. `\q`
8. Apply migrations: `dotnet ef database update`

## Integration specs

Integration specs use a dedicated PostgreSQL database and erase its application data between tests. Create the database once:

```sh
createdb -U postgres mock_interviews_test_db
```

Set `IntegrationTests__ConnectionString` in the root `.env` file as shown in `.env.example`, then run:

```sh
dotnet test mock-interviews.sln
```

For safety, the test fixture cleans only a database named exactly `mock_interviews_test_db` on a loopback host
(`localhost`, `127.0.0.1`, or `::1`).

## Authentication

Local ASP.NET Identity accounts are always available and new accounts must confirm their email. Microsoft sign-in is optional: set both `Authentication__Microsoft__ClientId` and `Authentication__Microsoft__ClientSecret` to enable it, or leave both unset.


## Resource Links

Set `mock_interview_manual` and `guest_parking_pass` to public HTTP(S) URLs in the admin Event Configuration screen. Unconfigured or invalid resource URLs are not shown to interviewers.

### Production
The root `.env` file is loaded only for local Development runs. Process environment variables override `.env` values. Set `SuperUser__Email` to the seeded administrator and application sender address. Production requires `Email__Provider=SendGrid` (the base default) and `SendGrid__ApiKey`; it will not fall back to SMTP. Set `ASPNETCORE_ENVIRONMENT=Production` and `ASPNETCORE_URLS=http://+:${PORT}` as described in Railway's [ASP.NET Core deployment guide](https://docs.railway.com/guides/aspnet-core).

## Original Team Members:

Logan Thompson - PM
Erin O'Laughlin - BA
Jaehee Kim - UI/UX Lead
Sam Riddle - Tech Lead
