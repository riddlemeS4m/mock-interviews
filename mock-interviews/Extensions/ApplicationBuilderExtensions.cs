using Microsoft.AspNetCore.HttpOverrides;
using Serilog;
using MockInterviews.Services.SignalR;
using MockInterviews.Data;

namespace MockInterviews.Extensions;

public static class ApplicationBuilderExtensions
{
    public static WebApplication UseStandardPipeline(this WebApplication app)
    {
        app.UseForwardedHeaders(new ForwardedHeadersOptions
        {
            ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto
        });

        app.UseSerilogRequestLogging(options =>
        {
            options.MessageTemplate = "Handled {RequestPath} in {Elapsed:0.0000} ms";
            options.EnrichDiagnosticContext = (diagnosticContext, httpContext) =>
            {
                diagnosticContext.Set("RequestHost", httpContext.Request.Host.Value);
                diagnosticContext.Set("RequestScheme", httpContext.Request.Scheme);
            };
        });

        if (app.Environment.IsDevelopment())
        {
            app.UseDeveloperExceptionPage();
            app.UseMigrationsEndPoint();
            app.UseWebSockets();
        }
        else
        {
            app.UseExceptionHandler("/Home/Error");
            app.UseStatusCodePages();
            app.UseHsts();
            app.UseHttpsRedirection();
            app.UseResponseCompression();
            app.UseWebSockets();
        }

        app.UseStaticFiles();
        app.UseRouting();
        app.UseAuthentication();
        app.UseAuthorization();

        // endpoints
        app.MapAreaControllerRoute(
            name: "system",
            areaName: "System",
            pattern: "System",
            defaults: new { controller = "System", action = "Index" });
        app.MapControllerRoute(
            name: "default",
            pattern: "{controller=Home}/{action=Index}/{id?}");
        app.MapRazorPages();
        app.MapHealthChecks("/health");
        app.MapHub<AssignInterviewsHub>("/interviewhub");
        app.MapHub<AvailableInterviewersHub>("/interviewershub");

        return app;
    }

    public static async Task<WebApplication> UseStartupTasksAsync(this WebApplication app)
    {
        await StartupTasks.RunStartupTasksAsync(app);
        return app;
    }
}
