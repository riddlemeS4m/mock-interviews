# Mock Interviews

## Setup (MacOS)
1. Install .NET 8: `brew install --cask dotnet-sdk@8`
2. Before removing the old User Secrets entry, transfer its values to a local `.env` file. Copy the template with `cp .env.example .env`, inspect the existing values with `dotnet user-secrets list --id aspnet-sp2023_mis421_mockinterviews-6d184366-7fbc-4612-b0c8-5e40c252810f`, then fill in every value. The application reports all missing key names at startup without printing values.
3. `__` in an environment-variable name maps to a nested .NET configuration key. For example, `ConnectionStrings__Users` maps to `ConnectionStrings:Users`.
4. Run `dotnet restore && dotnet build`, then start the app with either `dotnet run --project sp2023-mis421-mockinterviews` from the repository root or `dotnet run` from `sp2023-mis421-mockinterviews`.

The root `.env` file is loaded only for local Development runs and is intentionally ignored by Git and Docker. Process environment variables override `.env` values. Staging and production must inject the same variables directly; Railway can import them through its [Variables UI](https://docs.railway.com/variables) or Raw Editor, including its multiline-value support for `GoogleCredential__private_key`. Set `ASPNETCORE_ENVIRONMENT=Production` and `ASPNETCORE_URLS=http://+:${PORT}` as described in Railway's [ASP.NET Core deployment guide](https://docs.railway.com/guides/aspnet-core).

After confirming the local `.env` works, the former User Secrets store can be cleared with:

```bash
dotnet user-secrets clear --id aspnet-sp2023_mis421_mockinterviews-6d184366-7fbc-4612-b0c8-5e40c252810f
```

Provision all variables on staging before deploying this change. Verify startup, both databases, Microsoft login, email delivery, Google Drive access, seeded-admin behavior, and `/health`; deploy production only after staging passes. Existing GitHub Actions secrets for registry login, Tailscale, and migration bundles are unrelated to application configuration and remain in place.

## Original Team Members:

Logan Thompson - PM
Erin O'Laughlin - BA
Jaehee Kim - UI/UX Lead
Sam Riddle - Tech Lead
