using BlazorMotion.Models;
using Microsoft.JSInterop;

namespace BlazorMotion.Interop;

/// <summary>
/// Thin C# wrapper around the <c>BlazorMotion</c> JS object defined in
/// <c>blazor-motion.js</c>. All public methods map 1-to-1 to their JS equivalents.
/// </summary>
public sealed class MotionInterop : IAsyncDisposable
{
    private readonly Lazy<Task<IJSObjectReference>> _moduleTask;

    public MotionInterop(IJSRuntime js)
    {
        _moduleTask = new Lazy<Task<IJSObjectReference>>(
            () => js.InvokeAsync<IJSObjectReference>(
                "import", "./_content/BlazorMotion/blazor-motion.js").AsTask());
    }

    // ── Lifecycle ─────────────────────────────────────────────────────────────

    /// <summary>
    /// Register an element with the animation engine and optionally apply initial values.
    /// </summary>
    public async ValueTask InitAsync<T>(string elementId, DotNetObjectReference<T> dotNetRef, object config)
        where T : class
    {
        var module = await _moduleTask.Value;
        await module.InvokeVoidAsync("init", elementId, dotNetRef, config);
    }

    /// <summary>Tear down all animations and event listeners for an element.</summary>
    public async ValueTask DisposeElementAsync(string elementId)
    {
        var module = await _moduleTask.Value;
        await module.InvokeVoidAsync("dispose", elementId);
    }

    // ── Animation ─────────────────────────────────────────────────────────────

    /// <summary>Animate an element to the given values.</summary>
    public async ValueTask AnimateToAsync(string elementId, object values, object? transition)
    {
        var module = await _moduleTask.Value;
        await module.InvokeVoidAsync("animateTo", elementId, values, transition);
    }

    /// <summary>
    /// Animate to values and return a Task that completes when all properties finish.
    /// </summary>
    public async ValueTask AnimateToAwaitAsync(string elementId, object values, object? transition)
    {
        var module = await _moduleTask.Value;
        await module.InvokeVoidAsync("animateToAwait", elementId, values, transition);
    }

    /// <summary>Animate one or more properties along a keyframe sequence.</summary>
    public async ValueTask AnimateKeyframesAsync(
        string elementId, string property, double[] frames, object? transition)
    {
        var module = await _moduleTask.Value;
        await module.InvokeVoidAsync("animateKeyframes", elementId, property, frames, transition);
    }

    /// <summary>Instantly set values with no animation.</summary>
    public async ValueTask SetAsync(string elementId, object values)
    {
        var module = await _moduleTask.Value;
        await module.InvokeVoidAsync("set", elementId, values);
    }

    /// <summary>Stop running animations on specific properties (or all if none specified).</summary>
    public async ValueTask StopAsync(string elementId, string[]? properties = null)
    {
        var module = await _moduleTask.Value;
        await module.InvokeVoidAsync("stop", elementId, (object?)properties);
    }

    // ── Gestures ─────────────────────────────────────────────────────────────

    /// <summary>Attach hover / tap / focus / drag gesture handlers.</summary>
    public async ValueTask AttachGesturesAsync(string elementId, object gestureOptions)
    {
        var module = await _moduleTask.Value;
        await module.InvokeVoidAsync("attachGestures", elementId, gestureOptions);
    }

    // ── Layout (FLIP) ─────────────────────────────────────────────────────────

    /// <summary>
    /// Snapshot the element's current bounding rect (call BEFORE Blazor re-renders).
    /// </summary>
    public async ValueTask CaptureLayoutAsync(string elementId)
    {
        var module = await _moduleTask.Value;
        await module.InvokeVoidAsync("captureLayout", elementId);
    }

    /// <summary>
    /// Play a FLIP animation from the snapshot to the current layout (call AFTER re-render).
    /// </summary>
    public async ValueTask PlayLayoutAnimationAsync(string elementId, object? transition)
    {
        var module = await _moduleTask.Value;
        await module.InvokeVoidAsync("playLayoutAnimation", elementId, transition);
    }

    // ── Scroll ────────────────────────────────────────────────────────────────

    /// <summary>
    /// Start observing scroll events on a container element (or the window if null).
    /// The DotNet ref will receive <c>OnScroll(ScrollInfo)</c> callbacks.
    /// </summary>
    public async ValueTask<string> ObserveScrollAsync<T>(
        string? containerId, DotNetObjectReference<T> dotNetRef)
        where T : class
    {
        var module = await _moduleTask.Value;
        return await module.InvokeAsync<string>("observeScroll", containerId, dotNetRef);
    }

    /// <summary>Stop observing scroll events for a subscription key.</summary>
    public async ValueTask UnobserveScrollAsync(string subscriptionKey)
    {
        var module = await _moduleTask.Value;
        await module.InvokeVoidAsync("unobserveScroll", subscriptionKey);
    }

    /// <summary>
    /// Observe element scroll-progress (how far an element has scrolled through the viewport).
    /// </summary>
    public async ValueTask<string> ObserveElementScrollAsync<T>(
        string elementId, DotNetObjectReference<T> dotNetRef, string[]? offset = null)
        where T : class
    {
        var module = await _moduleTask.Value;
        return await module.InvokeAsync<string>("observeElementScroll", elementId, dotNetRef, offset);
    }

    // ── Motion Values ─────────────────────────────────────────────────────────

    public async ValueTask CreateMotionValueAsync(string valueId, double initial)
    {
        var module = await _moduleTask.Value;
        await module.InvokeVoidAsync("createMotionValue", valueId, initial);
    }

    public async ValueTask SetMotionValueAsync(string valueId, double value)
    {
        var module = await _moduleTask.Value;
        await module.InvokeVoidAsync("setMotionValue", valueId, value);
    }

    public async ValueTask<double> GetMotionValueAsync(string valueId)
    {
        var module = await _moduleTask.Value;
        return await module.InvokeAsync<double>("getMotionValue", valueId);
    }

    public async ValueTask DestroyMotionValueAsync(string valueId)
    {
        var module = await _moduleTask.Value;
        await module.InvokeVoidAsync("destroyMotionValue", valueId);
    }

    // ── Dispose ───────────────────────────────────────────────────────────────

    public async ValueTask DisposeAsync()
    {
        if (_moduleTask.IsValueCreated)
        {
            var module = await _moduleTask.Value;
            await module.DisposeAsync();
        }
    }
}
