# Mock Interviews

## Setup (MacOS)
1. Install .NET 8: `brew install --cask dotnet-sdk@8`
2. Copy the template with `cp .env.example .env`
3. Run `dotnet restore && dotnet build`, then start the app with either `dotnet run --project mock-interviews` from the repository root or `dotnet run` from `mock-interviews`.

## Resource Links

Set `mock_interview_manual` and `guest_parking_pass` to public HTTP(S) URLs in the admin Event Configuration screen. Unconfigured or invalid resource URLs are not shown to interviewers.

### Production
The root `.env` file is loaded only for local Development runs. Process environment variables override `.env` values. Set `ASPNETCORE_ENVIRONMENT=Production` and `ASPNETCORE_URLS=http://+:${PORT}` as described in Railway's [ASP.NET Core deployment guide](https://docs.railway.com/guides/aspnet-core).

## Original Team Members:

Logan Thompson - PM
Erin O'Laughlin - BA
Jaehee Kim - UI/UX Lead
Sam Riddle - Tech Lead
