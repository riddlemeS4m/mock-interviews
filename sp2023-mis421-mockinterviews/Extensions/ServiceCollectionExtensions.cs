using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.Options;
using SendGrid;
using sp2023_mis421_mockinterviews.Options;
using sp2023_mis421_mockinterviews.Models.UserDb;
using sp2023_mis421_mockinterviews.Interfaces.IServices;
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
