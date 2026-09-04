using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using MockInterviews.Data.Contexts;
using MockInterviews.Email;
using MockInterviews.Interfaces.IServices;
using MockInterviews.Models.Identity;
using MockInterviews.Options;
using MockInterviews.Services;
using SendGrid;

namespace MockInterviews.Extensions;

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

    public static IServiceCollection AddEmailTransport(this IServiceCollection services, IConfiguration config)
    {
        var provider = config[$"{EmailOptions.SectionName}:Provider"];
        if (string.Equals(provider, EmailOptions.SendGridProvider, StringComparison.OrdinalIgnoreCase))
        {
            services.AddOptions<SendGridOptions>()
                .Bind(config.GetSection(SendGridOptions.SectionName))
                .ValidateDataAnnotations()
                .ValidateOnStart();

            services.AddSingleton<ISendGridClient>(serviceProvider =>
            {
                var options = serviceProvider.GetRequiredService<IOptions<SendGridOptions>>().Value;
                return new SendGridClient(options.ApiKey);
            });
            services.AddSingleton<IEmailTransport, SendGridEmailTransport>();
            return services;
        }

        if (string.Equals(provider, EmailOptions.SmtpProvider, StringComparison.OrdinalIgnoreCase))
        {
            services.AddOptions<SmtpEmailOptions>()
                .Bind(config.GetSection(SmtpEmailOptions.SectionName))
                .ValidateDataAnnotations()
                .ValidateOnStart();
            services.AddSingleton<IEmailTransport, SmtpEmailTransport>();
            return services;
        }

        throw new InvalidOperationException(
            $"Unsupported Email:Provider '{provider}'. Supported values are {EmailOptions.SendGridProvider} and {EmailOptions.SmtpProvider}.");
    }

    public static IServiceCollection AddEmailOptions(this IServiceCollection services, IConfiguration config)
    {
        services.AddOptions<EmailOptions>()
            .Bind(config.GetSection(EmailOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();
        return services;
    }

    public static IServiceCollection AddSuperUserOptions(this IServiceCollection services, IConfiguration config)
    {
        services.AddOptions<SuperUserOptions>()
            .Bind(config.GetSection(SuperUserOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        return services;
    }

    public static IServiceCollection AddDatabases(this IServiceCollection services, IConfiguration config)
    {

        var connectionString = config["ConnectionString:DefaultConnection"]!;

        services.AddDbContextPool<MockInterviewsDbContext>(options =>
            options.UseNpgsql(connectionString));

        services.AddDatabaseDeveloperPageExceptionFilter();

        return services;
    }

    public static IServiceCollection AddIdentityAndAuth(this IServiceCollection services, IHostEnvironment? environment = null)
    {
        services.AddIdentity<ApplicationUser, IdentityRole>(options =>
            {
                options.SignIn.RequireConfirmedAccount = true;
                options.SignIn.RequireConfirmedEmail = true;

                if (environment?.IsDevelopment() == true)
                {
                    options.Password.RequireDigit = false;
                    options.Password.RequireLowercase = false;
                    options.Password.RequireUppercase = false;
                    options.Password.RequireNonAlphanumeric = false;
                    options.Password.RequiredLength = 6;
                }
            })
            .AddEntityFrameworkStores<MockInterviewsDbContext>()
            .AddDefaultUI()
            .AddDefaultTokenProviders();

        services.AddScoped<RoleManager<IdentityRole>>();
        services.AddScoped<UserManager<ApplicationUser>>();
        services.AddScoped<AccountRoleProvisioner>();
        services.AddScoped<AccountInvitationService>();
        services.AddScoped<UserProfileCompletionService>();
        services.AddSingleton<UserLandingPageResolver>();

        return services;
    }

    public static IServiceCollection AddExternalIntegrations(this IServiceCollection services, IConfiguration config)
    {
        services.AddHttpClient();
        services.AddSignalR();
        services.AddResponseCompression(opts => { opts.EnableForHttps = true; });
        services.AddHealthChecks();
        services.AddControllersWithViews();
        services.AddRazorPages(options =>
            options.Conventions.AuthorizeAreaFolder("Identity", "/Account/Manage"));

        return services;
    }

    public static IServiceCollection AddOptionalMicrosoftAuthentication(
        this IServiceCollection services,
        IConfiguration config)
    {
        var clientId = config["Authentication:Microsoft:ClientId"];
        var clientSecret = config["Authentication:Microsoft:ClientSecret"];

        if (string.IsNullOrWhiteSpace(clientId) && string.IsNullOrWhiteSpace(clientSecret))
        {
            return services;
        }

        if (string.IsNullOrWhiteSpace(clientId) || string.IsNullOrWhiteSpace(clientSecret))
        {
            throw new InvalidOperationException(
                "Microsoft authentication requires both Authentication:Microsoft:ClientId and Authentication:Microsoft:ClientSecret.");
        }

        services.AddAuthentication()
            .AddMicrosoftAccount(options =>
            {
                options.ClientId = clientId;
                options.ClientSecret = clientSecret;
            });

        return services;
    }

    public static IServiceCollection AddApplicationServices(this IServiceCollection services)
    {
        services.AddScoped<InterviewService>();
        services.AddScoped<ParticipantSchedulingService>();
        services.AddScoped<AssignmentLifecycleService>();
        services.AddScoped<AssignmentBoardQueryService>();
        services.AddScoped<PreAssignmentService>();
        services.AddScoped<DashboardService>();
        services.AddScoped<SettingsService>();
        services.AddScoped<TimeslotService>();
        services.AddScoped<EventService>();
        services.AddScoped<InterviewerSignupService>();
        services.AddScoped<InterviewerLocationService>();
        services.AddScoped<InterviewerTimeslotService>();
        services.AddScoped<UserService>();
        services.AddTransient<IEmailSender, IdentityEmailSender>();

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
