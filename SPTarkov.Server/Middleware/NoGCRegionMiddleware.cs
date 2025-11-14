using System.Runtime;
using SPTarkov.Server.Core.Models.Spt.Config;

namespace SPTarkov.Server.Middleware;

// ReSharper disable once InconsistentNaming
public class NoGCRegionMiddleware(CoreConfig coreConfig, RequestDelegate next)
{
    private static long _activeRequests;

    private static bool OtherRequestsActive
    {
        get { return Interlocked.Read(ref _activeRequests) > 1; }
    }

    public async Task InvokeAsync(HttpContext context)
    {
        Interlocked.Increment(ref _activeRequests);

        // if no other requests are running, start the no GC region, otherwise dont start it
        if (!OtherRequestsActive)
        {
            if (coreConfig.EnableNoGCRegions && GCSettings.LatencyMode != GCLatencyMode.NoGCRegion)
            {
                try
                {
                    GC.TryStartNoGCRegion(
                        1024L * 1024L * 1024L * coreConfig.NoGCRegionMaxMemoryGB,
                        1024L * 1024L * 1024L * coreConfig.NoGCRegionMaxLOHMemoryGB,
                        true
                    );
                }
                catch (Exception)
                {
                    // ignored, we keep going
                }
            }
        }
        try
        {
            await next(context);
        }
        finally
        {
            Interlocked.Decrement(ref _activeRequests);
        }

        // if no other requests are running, end the no GC region, otherwise dont stop it as other requests need it still
        if (!OtherRequestsActive)
        {
            if (coreConfig.EnableNoGCRegions && GCSettings.LatencyMode == GCLatencyMode.NoGCRegion)
            {
                try
                {
                    GC.EndNoGCRegion();
                }
                catch (Exception)
                {
                    // ignored, we dont care about handling this
                }
            }
        }
    }
}

// ReSharper disable once InconsistentNaming
public static class NoGCRegionMiddlewareExtensions
{
    // ReSharper disable once InconsistentNaming
    public static IApplicationBuilder UseNoGCRegions(this IApplicationBuilder builder)
    {
        return builder.UseMiddleware<NoGCRegionMiddleware>();
    }
}
