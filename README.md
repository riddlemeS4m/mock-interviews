# Mock Interviews

## Setup (MacOS)
1. Install .NET 8: `brew install --cask dotnet-sdk@8`
2. Copy the template with `cp .env.example .env`
3. Run `dotnet restore && dotnet build`, then start the app with either `dotnet run --project mock-interviews` from the repository root or `dotnet run` from `mock-interviews`.
4. Trust dev cert: `dotnet dev-certs https --trust`

## Db setup
1. `psql postgres`
2. `create user mock_interviews_user with password 'topsecretpassword';`
3. `create database mock_interviews_db owner mock_interviews_user;`
4. `\c mock_interviews_db`
5. `alter schema public owner to mock_interviews_user;`
6. `grant all on schema public to mock_interviews_user;`
7. `\q`
8. Apply migrations: `dotnet ef database update`


## Resource Links

Set `mock_interview_manual` and `guest_parking_pass` to public HTTP(S) URLs in the admin Event Configuration screen. Unconfigured or invalid resource URLs are not shown to interviewers.

### Production
The root `.env` file is loaded only for local Development runs. Process environment variables override `.env` values. Set `SuperUser__Email` to the seeded administrator and application sender address. Set `ASPNETCORE_ENVIRONMENT=Production` and `ASPNETCORE_URLS=http://+:${PORT}` as described in Railway's [ASP.NET Core deployment guide](https://docs.railway.com/guides/aspnet-core).

## Original Team Members:

Logan Thompson - PM
Erin O'Laughlin - BA
Jaehee Kim - UI/UX Lead
Sam Riddle - Tech Lead
