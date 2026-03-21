using BlazorMotion.Engine;
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
        // Slim browser-API interop bridge — one instance per DI scope
        services.AddScoped<MotionInterop>();

        // C# animation engine — drives all animation math in WebAssembly
        services.AddScoped<AnimationEngine>();

        // Higher-level services
        services.AddScoped<ScrollTracker>();
        services.AddTransient<AnimationController>();

        return services;
    }
}
