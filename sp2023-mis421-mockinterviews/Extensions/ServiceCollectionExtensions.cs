using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Options;
using Microsoft.AspNetCore.HttpOverrides;
using Google.Apis.Drive.v3;
using Google.Apis.Auth.OAuth2;
using Google.Apis.Services;
using SendGrid;
using sp2023_mis421_mockinterviews.Options;
using sp2023_mis421_mockinterviews.Models.UserDb;
using sp2023_mis421_mockinterviews.Interfaces.IServices;
using sp2023_mis421_mockinterviews.Services.GoogleDrive;
using sp2023_mis421_mockinterviews.Services.Controllers;
using sp2023_mis421_mockinterviews.Services.SignalR;
using sp2023_mis421_mockinterviews.Services.UserDb;
using sp2023_mis421_mockinterviews.Services.SignupDb;
using sp2023_mis421_mockinterviews.Data.Contexts;

namespace sp2023_mis421_mockinterviews.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddForwardedHeaders(this IServiceCollection services)
    {
        services.Configure<ForwardedHeadersOptions>(options =>
        {
            options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
        });

        return services;
    }

    public static IServiceCollection AddSendGrid(this IServiceCollection services, IConfiguration config)
    {
        services.AddOptions<SendGridOptions>()
            .Bind(config.GetSection("SendGrid"))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddSingleton<ISendGridClient>(provider =>
        {
            var options = provider.GetRequiredService<IOptions<SendGridOptions>>().Value;
            return new SendGridClient(options.ApiKey);
        });

        return services;
    }

    public static IServiceCollection AddGoogleDrive(this IServiceCollection services, IConfiguration config)
    {
        services.AddOptions<GoogleDriveOptions>()
            .Configure(options =>
            {
                options.SiteContentFolderId = config["GoogleDriveFolders:SiteContent"] ?? "";
                options.ResumesFolderId = config["GoogleDriveFolders:Resumes"] ?? "";
                options.PfpsFolderId = config["GoogleDriveFolders:PFPs"] ?? "";
            })
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddOptions<GoogleCredentialOptions>()
            .Bind(config.GetSection("GoogleCredential"))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddSingleton<DriveService>(provider =>
        {
            var credentialOptions = provider.GetRequiredService<IOptions<GoogleCredentialOptions>>().Value;
            var driveOptions = provider.GetRequiredService<IOptions<GoogleDriveOptions>>().Value;
            
            string json = GoogleDriveUtility.SerializeCredentials(credentialOptions);

            GoogleCredential credential;
            using (var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(json)))
            {
                credential = GoogleCredential.FromStream(stream).CreateScoped(new[]
                {
                    DriveService.Scope.DriveFile
                });
            }

            return new DriveService(new BaseClientService.Initializer()
            {
                HttpClientInitializer = credential,
                ApplicationName = driveOptions.ApplicationName
            });
        });

        services.AddScoped<GoogleDriveSiteContentService>(serviceProvider =>
        {
            var driveService = serviceProvider.GetRequiredService<DriveService>();
            var logger = serviceProvider.GetRequiredService<ILogger<IGoogleDrive>>();
            var options = serviceProvider.GetRequiredService<IOptions<GoogleDriveOptions>>().Value;
            return new GoogleDriveSiteContentService(options.SiteContentFolderId, driveService, logger);
        });

        services.AddScoped<GoogleDriveResumeService>(serviceProvider =>
        {
            var logger = serviceProvider.GetRequiredService<ILogger<IGoogleDrive>>();
            var driveService = serviceProvider.GetRequiredService<DriveService>();
            var options = serviceProvider.GetRequiredService<IOptions<GoogleDriveOptions>>().Value;
            return new GoogleDriveResumeService(options.ResumesFolderId, driveService, logger);
        });

        services.AddScoped<GoogleDrivePfpService>(serviceProvider =>
        {
            var logger = serviceProvider.GetRequiredService<ILogger<IGoogleDrive>>();
            var driveService = serviceProvider.GetRequiredService<DriveService>();
            var cacheService = serviceProvider.GetRequiredService<Microsoft.Extensions.Caching.Memory.IMemoryCache>();
            var options = serviceProvider.GetRequiredService<IOptions<GoogleDriveOptions>>().Value;
            return new GoogleDrivePfpService(options.PfpsFolderId, driveService, cacheService, logger);
        });

        return services;
    }

    public static IServiceCollection AddDatabases(this IServiceCollection services, IConfiguration config)
    {

        var usersConnectionString = config["ConnectionStrings:Users"]!;
        var signupsConnectionString = config["ConnectionStrings:Signups"]!;

        services.AddDbContextPool<UsersDbContext>(options =>
            options.UseNpgsql(usersConnectionString));
        services.AddDbContextPool<MockInterviewsDbContext>(options =>
            options.UseNpgsql(signupsConnectionString));

        services.AddDatabaseDeveloperPageExceptionFilter();

        return services;
    }

    public static IServiceCollection AddIdentityAndAuth(this IServiceCollection services, IConfiguration config)
    {
        services.AddIdentity<ApplicationUser, IdentityRole>()
            .AddEntityFrameworkStores<UsersDbContext>()
            .AddDefaultUI()
            .AddDefaultTokenProviders();

        services.AddScoped<RoleManager<IdentityRole>>();
        services.AddScoped<UserManager<ApplicationUser>>();

        services.AddAuthentication()
            .AddMicrosoftAccount(microsoftOptions =>
            {
                microsoftOptions.ClientId = config["Authentication:Microsoft:ClientId"]!;
                microsoftOptions.ClientSecret = config["Authentication:Microsoft:ClientSecret"]!;
            });

        return services;
    }

    public static IServiceCollection AddExternalIntegrations(this IServiceCollection services, IConfiguration config)
    {
        services.AddHttpClient();
        services.AddSignalR();
        services.AddResponseCompression(opts => { opts.EnableForHttps = true; });
        services.AddMemoryCache();
        services.AddHealthChecks();
        services.AddControllersWithViews();
        services.AddRazorPages();

        return services;
    }

    public static IServiceCollection AddApplicationServices(this IServiceCollection services)
    {
        services.AddScoped<InterviewService>();
        services.AddScoped<SettingsService>();
        services.AddScoped<TimeslotService>();
        services.AddScoped<EventService>();
        services.AddScoped<InterviewerSignupService>();
        services.AddScoped<InterviewerLocationService>();
        services.AddScoped<InterviewerTimeslotService>();
        services.AddScoped<UserService>();

        services.AddScoped<ISignupDbServiceFactory, SignupDbServiceFactory>();

        services.AddTransient<IManageInterviews, ManageInterviewsService>(serviceProvider => {
            var factory = serviceProvider.GetRequiredService<ISignupDbServiceFactory>();
            var users = serviceProvider.GetRequiredService<UserService>();
            var sendGrid = serviceProvider.GetRequiredService<ISendGridClient>();
            var interviews = serviceProvider.GetRequiredService<IHubContext<AssignInterviewsHub>>();
            var interviewers = serviceProvider.GetRequiredService<IHubContext<AvailableInterviewersHub>>();
            var logger = serviceProvider.GetRequiredService<ILogger<ManageInterviewsService>>();
            return new ManageInterviewsService(factory, users, sendGrid, interviews, interviewers, logger);
        });

        return services;
    }

    public static IServiceCollection AddProblemDetails(this IServiceCollection services, IHostEnvironment env)
    {
        services.AddProblemDetails(options =>
        {
            options.CustomizeProblemDetails = context =>
            {
                if (env.IsDevelopment() && context.Exception != null)
                {
                    context.ProblemDetails.Detail = context.Exception.ToString();
                }
            };
        });

        return services;
    }
}
