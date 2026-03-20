using BlazorMotion.Interop;
using BlazorMotion.Services;
using Microsoft.Extensions.DependencyInjection;

namespace BlazorMotion.Extensions;

/// <summary>
/// Extension methods to register BlazorMotion services in the DI container.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers all BlazorMotion services.
    /// Call this in <c>Program.cs</c> before <c>builder.Build()</c>:
    /// <code>builder.Services.AddBlazorMotion();</code>
    /// </summary>
    public static IServiceCollection AddBlazorMotion(this IServiceCollection services)
    {
        // Core interop – one instance per DI scope (component tree)
        services.AddScoped<MotionInterop>();

        // Higher-level services
        services.AddScoped<ScrollTracker>();
        services.AddTransient<AnimationController>();

        return services;
    }
}
