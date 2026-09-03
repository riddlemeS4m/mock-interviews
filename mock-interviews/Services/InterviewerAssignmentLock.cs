using Microsoft.EntityFrameworkCore;
using MockInterviews.Data.Contexts;

namespace MockInterviews.Services;

/// <summary>
/// Serializes assignment resources without persisting additional state.
/// PostgreSQL releases transaction-scoped advisory locks automatically on
/// commit or rollback. Callers provide every resource they will mutate and this
/// helper acquires them in a stable order to avoid deadlocks.
/// </summary>
internal static class InterviewerAssignmentLock
{
    public static Task AcquireAsync(MockInterviewsDbContext context, params string[] resourceKeys)
        => AcquireAsync(context, resourceKeys.AsEnumerable());

    public static async Task AcquireAsync(MockInterviewsDbContext context, IEnumerable<string> resourceKeys)
    {
        foreach (var resourceKey in resourceKeys
                     .Distinct(StringComparer.Ordinal)
                     .OrderBy(resourceKey => resourceKey, StringComparer.Ordinal))
        {
            await context.Database.ExecuteSqlInterpolatedAsync(
                $"SELECT pg_advisory_xact_lock(hashtext({$"assignment-resource:{resourceKey}"}))");
        }
    }

    public static string Interview(int interviewId) => $"interview:{interviewId}";

    public static string Interviewer(string interviewerId) => $"interviewer:{interviewerId}";
}
