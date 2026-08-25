using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.Options;
using SendGrid;
using MockInterviews.Options;
using MockInterviews.Models.Identity;
using MockInterviews.Interfaces.IServices;
using MockInterviews.Services;
using MockInterviews.Data.Contexts;

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

        services.AddTransient<IManageInterviews, ManageInterviewsService>();

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
