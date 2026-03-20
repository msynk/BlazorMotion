using BlazorMotion.Components;
using BlazorMotion.Interop;
using BlazorMotion.Models;
using BlazorMotion.Services;
using Microsoft.JSInterop;

namespace BlazorMotion.Services;

/// <summary>
/// Programmatic animation controller.
/// Analogous to Framer Motion's <c>useAnimate()</c>.
/// Obtain via DI (<c>@inject AnimationController</c>) and bind to an element
/// using <c>&lt;Motion @ref="_ctrl.Element" /&gt;</c> or target by element ID.
/// </summary>
public sealed class AnimationController : IAsyncDisposable
{
    private readonly MotionInterop _interop;
    private string? _elementId;

    public AnimationController(MotionInterop interop) => _interop = interop;

    /// <summary>Bind by element ID.</summary>
    public void BindTo(string elementId) => _elementId = elementId;

    /// <summary>Animate the bound element to the given props.</summary>
    public async ValueTask AnimateAsync(AnimationProps props, TransitionConfig? transition = null)
    {
        if (_elementId == null) return;
        await _interop.AnimateToAsync(_elementId, props.ToJsDictionary(), transition?.ToJsObject());
    }

    /// <summary>Animate and await completion.</summary>
    public async ValueTask AnimateAwaitAsync(AnimationProps props, TransitionConfig? transition = null)
    {
        if (_elementId == null) return;
        await _interop.AnimateToAwaitAsync(_elementId, props.ToJsDictionary(), transition?.ToJsObject());
    }

    /// <summary>Instantly set props without animation.</summary>
    public async ValueTask SetAsync(AnimationProps props)
    {
        if (_elementId == null) return;
        await _interop.SetAsync(_elementId, props.ToJsDictionary());
    }

    /// <summary>Stop animations on the bound element.</summary>
    public async ValueTask StopAsync(params string[] properties)
    {
        if (_elementId == null) return;
        await _interop.StopAsync(_elementId, properties.Length > 0 ? properties : null);
    }

    public async ValueTask DisposeAsync()
    {
        if (_elementId != null)
        {
            try { await _interop.DisposeElementAsync(_elementId); } catch { /* ignore */ }
        }
        await _interop.DisposeAsync();
    }
}
