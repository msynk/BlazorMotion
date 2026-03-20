using BlazorMotion.Context;
using BlazorMotion.Interop;
using BlazorMotion.Models;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.JSInterop;

namespace BlazorMotion.Components;

/// <summary>
/// The primary animation component — a drop-in replacement for any HTML element.
/// Wraps the target tag and drives animations, gestures, layout transitions and
/// scroll-linked effects through the JS animation engine.
///
/// <example>
/// <code>
/// &lt;Motion Tag="div"
///         Initial="@(new AnimationProps { Opacity = 0, Y = 20 })"
///         Animate="@(new AnimationProps { Opacity = 1, Y = 0 })"
///         Exit="@(new AnimationProps { Opacity = 0, Y = -20 })"
///         Transition="@TransitionConfig.Spring()"
///         WhileHover="@(new AnimationProps { Scale = 1.05 })"
///         WhileTap="@(new AnimationProps { Scale = 0.95 })" /&gt;
/// </code>
/// </example>
/// </summary>
public class Motion : ComponentBase, IAsyncDisposable
{
    // ── Injected services ─────────────────────────────────────────────────────
    [Inject] private IJSRuntime JS { get; set; } = null!;

    // ── Cascaded contexts ─────────────────────────────────────────────────────
    [CascadingParameter] private PresenceContext?     PresenceCtx { get; set; }
    [CascadingParameter] private VariantContext?      VariantCtx  { get; set; }
    [CascadingParameter] private MotionConfigContext? ConfigCtx   { get; set; }

    // ── Core rendering parameters ─────────────────────────────────────────────
    /// <summary>HTML tag to render. Default: <c>"div"</c>.</summary>
    [Parameter] public string Tag { get; set; } = "div";

    /// <summary>CSS class(es) to add to the element.</summary>
    [Parameter] public string? Class { get; set; }

    /// <summary>Inline styles merged with motion-generated ones.</summary>
    [Parameter] public string? Style { get; set; }

    [Parameter] public RenderFragment? ChildContent { get; set; }

    /// <summary>Any additional HTML attributes are forwarded to the underlying element.</summary>
    [Parameter(CaptureUnmatchedValues = true)]
    public Dictionary<string, object>? AdditionalAttributes { get; set; }

    // ── Animation targets ─────────────────────────────────────────────────────
    /// <summary>Starting state shown before the component enters. Analogous to Framer Motion's <c>initial</c>.</summary>
    [Parameter] public AnimationTarget? Initial { get; set; }

    /// <summary>Target animated state. Analogous to Framer Motion's <c>animate</c>.</summary>
    [Parameter] public AnimationTarget? Animate { get; set; }

    /// <summary>State to animate to before the element is removed from the DOM.</summary>
    [Parameter] public AnimationTarget? Exit { get; set; }

    // ── Gesture states ────────────────────────────────────────────────────────
    [Parameter] public AnimationTarget? WhileHover { get; set; }
    [Parameter] public AnimationTarget? WhileTap   { get; set; }
    [Parameter] public AnimationTarget? WhileFocus { get; set; }
    [Parameter] public AnimationTarget? WhileDrag  { get; set; }

    /// <summary>Animate while the element is visible in the viewport.</summary>
    [Parameter] public AnimationTarget? WhileInView { get; set; }

    /// <summary>If true, <see cref="WhileInView"/> only fires once on first entry.</summary>
    [Parameter] public bool Once { get; set; }

    // ── Transition ────────────────────────────────────────────────────────────
    /// <summary>Controls speed, type and timing of animations.</summary>
    [Parameter] public TransitionConfig? Transition { get; set; }

    // ── Variants ──────────────────────────────────────────────────────────────
    /// <summary>Named animation states. Children inherit the active variant name automatically.</summary>
    [Parameter] public MotionVariants? Variants { get; set; }

    // ── Drag ──────────────────────────────────────────────────────────────────
    /// <summary>Enable drag on any axis. Set to <c>true</c> and optionally pass <see cref="DragConstraints"/> via <see cref="Drag"/>Options.</summary>
    [Parameter] public bool Drag { get; set; }
    [Parameter] public DragOptions? DragOptions { get; set; }

    // ── Layout ────────────────────────────────────────────────────────────────
    /// <summary>Enable automatic FLIP layout animations.</summary>
    [Parameter] public bool Layout { get; set; }

    /// <summary>
    /// Identifier for shared-element transitions. Two Motion components with the same
    /// <c>LayoutId</c> will animate between each other when one mounts / unmounts.
    /// </summary>
    [Parameter] public string? LayoutId { get; set; }

    // ── Events ────────────────────────────────────────────────────────────────
    [Parameter] public EventCallback OnHoverStart      { get; set; }
    [Parameter] public EventCallback OnHoverEnd        { get; set; }
    [Parameter] public EventCallback OnTapStart        { get; set; }
    [Parameter] public EventCallback OnTap             { get; set; }
    [Parameter] public EventCallback OnTapCancel       { get; set; }
    [Parameter] public EventCallback OnFocusStart      { get; set; }
    [Parameter] public EventCallback OnFocusEnd        { get; set; }
    [Parameter] public EventCallback OnDragStart       { get; set; }
    [Parameter] public EventCallback OnDrag            { get; set; }
    [Parameter] public EventCallback OnDragEnd         { get; set; }
    [Parameter] public EventCallback OnAnimationStart  { get; set; }
    [Parameter] public EventCallback OnAnimationComplete { get; set; }
    [Parameter] public EventCallback OnViewportEnter   { get; set; }
    [Parameter] public EventCallback OnViewportLeave   { get; set; }

    // ── Internal state ────────────────────────────────────────────────────────
    private readonly string _id = $"bm-{Guid.NewGuid():N}";
    private ElementReference _ref;
    private MotionInterop? _interop;
    private DotNetObjectReference<Motion>? _dotnet;
    private bool _initialized;
    private bool _isExiting;
    private AnimationTarget? _prevAnimate;
    private VariantContext? _ownVariantCtx;       // context cascaded to our children
    private string? _prevInheritedVariant;         // last variant received from a parent
    private int _variantChildIndex = -1;           // this child's stagger index in the parent
    private bool _needsLayoutPlay;                 // true when a FLIP snapshot was captured this cycle
    // ═══════════════════════════════════════════════════════════════════════════
    // Rendering
    // ═══════════════════════════════════════════════════════════════════════════

    protected override void BuildRenderTree(RenderTreeBuilder builder)
    {
        int seq = 0;
        builder.OpenElement(seq++, Tag);
        builder.AddAttribute(seq++, "id", _id);

        // Cascade additional attributes first (lowest priority)
        if (AdditionalAttributes != null)
            builder.AddMultipleAttributes(seq++, AdditionalAttributes);

        if (!string.IsNullOrEmpty(Class))
            builder.AddAttribute(seq++, "class", Class);

        // Merge initial inline style to avoid first-render flash
        var motionStyle = BuildInitialStyle();
        var combinedStyle = string.IsNullOrEmpty(Style)
            ? motionStyle
            : motionStyle + Style;
        if (!string.IsNullOrEmpty(combinedStyle))
            builder.AddAttribute(seq++, "style", combinedStyle);

        builder.AddElementReferenceCapture(seq++, r => _ref = r);

        // When this component owns Variants, cascade a VariantContext to children
        // so they can auto-respond to variant changes and receive stagger delays.
        if (Variants != null)
        {
            _ownVariantCtx ??= new VariantContext();
            _ownVariantCtx.ActiveVariant   = Animate?.IsVariant == true ? Animate.Variant : null;
            _ownVariantCtx.InitialVariant  = Initial?.IsVariant == true ? Initial.Variant : null;
            _ownVariantCtx.Variants        = Variants;
            _ownVariantCtx.StaggerChildren = Transition?.StaggerChildren ?? 0;
            _ownVariantCtx.DelayChildren   = Transition?.DelayChildren   ?? 0;

            builder.OpenComponent<CascadingValue<VariantContext>>(seq++);
            builder.AddComponentParameter(seq++, "Value", _ownVariantCtx);
            builder.AddComponentParameter(seq++, "ChildContent", ChildContent);
            builder.CloseComponent();
        }
        else
        {
            builder.AddContent(seq++, ChildContent);
        }

        builder.CloseElement();
    }

    /// <summary>Compute an inline style string from the Initial target to prevent a FOUC.</summary>
    private string BuildInitialStyle()
    {
        var props = ResolveProps(Initial);
        // When this is a variant-driven child with no explicit Initial, inherit the
        // parent's initial variant to avoid a first-render flash.
        if (props == null && Animate == null && VariantCtx?.InitialVariant is string initVariant)
            props = Variants?.Get(initVariant) ?? VariantCtx.Variants?.Get(initVariant);
        return props?.ToCssStyleString() ?? string.Empty;
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // Lifecycle
    // ═══════════════════════════════════════════════════════════════════════════

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender)
        {
            _interop = new MotionInterop(JS);
            _dotnet  = DotNetObjectReference.Create(this);
            await InitialiseAsync();
            _initialized = true;
        }
        else if (_initialized)
        {
            await HandleParameterUpdateAsync();

            // FLIP: play layout animation AFTER the DOM has settled into its new position.
            if (_needsLayoutPlay)
            {
                _needsLayoutPlay = false;
                var transition = BuildEffectiveTransition();
                await _interop!.PlayLayoutAnimationAsync(_id, transition?.ToJsObject());
            }
        }
    }

    protected override async Task OnParametersSetAsync()
    {
        // Detect presence exit from parent AnimatePresence
        if (PresenceCtx is { IsExiting: true } && !_isExiting)
        {
            _isExiting = true;
            if (_initialized) await PlayExitAsync();
        }

        // FLIP: snapshot the current position BEFORE Blazor re-renders the DOM.
        if (_initialized && Layout && !_isExiting)
        {
            await _interop!.CaptureLayoutAsync(_id);
            _needsLayoutPlay = true;
        }
    }

    private async Task InitialiseAsync()
    {
        var initConfig = BuildInitConfig();
        await _interop!.InitAsync(_id, _dotnet!, initConfig);

        PresenceCtx?.Register(this);

        if (Animate != null)
        {
            // Explicit animate target (direct props or variant reference)
            var animateProps = ResolveProps(Animate);
            if (animateProps != null)
            {
                var transition = BuildEffectiveTransition();
                await _interop!.AnimateToAsync(_id, animateProps.ToJsDictionary(), transition?.ToJsObject());
            }
        }
        else if (VariantCtx?.ActiveVariant is string inheritedVariant && Variants != null)
        {
            // Variant-driven child: register for stagger and animate to the inherited active variant
            _variantChildIndex    = VariantCtx.RegisterChild();
            _prevInheritedVariant = inheritedVariant;
            var props = Variants.Get(inheritedVariant) ?? VariantCtx.Variants?.Get(inheritedVariant);
            if (props != null)
            {
                var transition = BuildEffectiveTransitionWithDelay(VariantCtx.GetChildDelay(_variantChildIndex));
                await _interop!.AnimateToAsync(_id, props.ToJsDictionary(), transition?.ToJsObject());
            }
        }

        _prevAnimate = Animate;
    }

    private async Task HandleParameterUpdateAsync()
    {
        if (_isExiting) return;

        if (!ReferenceEquals(_prevAnimate, Animate))
        {
            var animateProps = ResolveProps(Animate);
            if (animateProps != null)
            {
                var transition = BuildEffectiveTransition();
                await _interop!.AnimateToAsync(_id, animateProps.ToJsDictionary(), transition?.ToJsObject());
            }
            _prevAnimate = Animate;
        }
        else if (Animate == null && Variants != null)
        {
            // Detect an inherited variant change propagated from an ancestor with Variants
            var newVariant = VariantCtx?.ActiveVariant;
            if (newVariant != _prevInheritedVariant)
            {
                _prevInheritedVariant = newVariant;
                if (newVariant != null)
                {
                    var props = Variants.Get(newVariant) ?? VariantCtx?.Variants?.Get(newVariant);
                    if (props != null)
                    {
                        var delay = _variantChildIndex >= 0 ? VariantCtx!.GetChildDelay(_variantChildIndex) : 0;
                        var transition = BuildEffectiveTransitionWithDelay(delay);
                        await _interop!.AnimateToAsync(_id, props.ToJsDictionary(), transition?.ToJsObject());
                    }
                }
            }
        }
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // Exit animation (called by PresenceContext)
    // ═══════════════════════════════════════════════════════════════════════════

    internal async Task PlayExitAsync()
    {
        var exitProps = ResolveProps(Exit);
        if (exitProps != null && _interop != null)
        {
            var transition = BuildEffectiveTransition();
            await _interop.AnimateToAwaitAsync(_id, exitProps.ToJsDictionary(), transition?.ToJsObject());
        }
        PresenceCtx?.NotifyExitComplete(this);
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // Programmatic control (public API)
    // ═══════════════════════════════════════════════════════════════════════════

    /// <summary>Animate to a new set of values programmatically.</summary>
    public async ValueTask AnimateAsync(AnimationProps props, TransitionConfig? transition = null)
    {
        if (_interop == null) return;
        transition ??= BuildEffectiveTransition();
        await _interop.AnimateToAsync(_id, props.ToJsDictionary(), transition?.ToJsObject());
    }

    /// <summary>Instantly set values without any animation.</summary>
    public async ValueTask SetAsync(AnimationProps props)
    {
        if (_interop == null) return;
        await _interop.SetAsync(_id, props.ToJsDictionary());
    }

    /// <summary>Stop running animations on the specified properties (or all if empty).</summary>
    public async ValueTask StopAsync(params string[] properties)
    {
        if (_interop == null) return;
        await _interop.StopAsync(_id, properties.Length > 0 ? properties : null);
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // JS → Blazor callbacks
    // ═══════════════════════════════════════════════════════════════════════════

    [JSInvokable("OnHoverStart")]        public Task OnHoverStartJs()         => OnHoverStart.InvokeAsync();
    [JSInvokable("OnHoverEnd")]          public Task OnHoverEndJs()           => OnHoverEnd.InvokeAsync();
    [JSInvokable("OnTapStart")]          public Task OnTapStartJs()           => OnTapStart.InvokeAsync();
    [JSInvokable("OnTap")]               public Task OnTapJs()                => OnTap.InvokeAsync();
    [JSInvokable("OnTapCancel")]         public Task OnTapCancelJs()          => OnTapCancel.InvokeAsync();
    [JSInvokable("OnFocusStart")]        public Task OnFocusStartJs()         => OnFocusStart.InvokeAsync();
    [JSInvokable("OnFocusEnd")]          public Task OnFocusEndJs()           => OnFocusEnd.InvokeAsync();
    [JSInvokable("OnDragStart")]         public Task OnDragStartJs()          => OnDragStart.InvokeAsync();
    [JSInvokable("OnDrag")]              public Task OnDragJs()               => OnDrag.InvokeAsync();
    [JSInvokable("OnDragEnd")]           public Task OnDragEndJs()            => OnDragEnd.InvokeAsync();
    [JSInvokable("OnAnimationStart")]    public Task OnAnimationStartJs()     => OnAnimationStart.InvokeAsync();
    [JSInvokable("OnAnimationComplete")] public Task OnAnimationCompleteJs()  => OnAnimationComplete.InvokeAsync();
    [JSInvokable("OnViewportEnter")]     public Task OnViewportEnterJs()      => OnViewportEnter.InvokeAsync();
    [JSInvokable("OnViewportLeave")]     public Task OnViewportLeaveJs()      => OnViewportLeave.InvokeAsync();

    // ═══════════════════════════════════════════════════════════════════════════
    // Helpers
    // ═══════════════════════════════════════════════════════════════════════════

    private AnimationProps? ResolveProps(AnimationTarget? target)
    {
        if (target == null || target.IsDisabled) return null;
        if (target.HasProps) return target.Props;

        // Variant resolution: check own Variants, then inherited VariantContext
        if (target.IsVariant)
        {
            var variantName = target.Variant!;
            return Variants?.Get(variantName)
                ?? VariantCtx?.Variants?.Get(variantName);
        }
        return null;
    }

    private TransitionConfig? BuildEffectiveTransition()
    {
        var t = Transition ?? ConfigCtx?.DefaultTransition;
        if (t == null) return null;

        // Apply global speed multiplier
        if (ConfigCtx?.TransitionSpeed is double speed && speed != 1.0)
        {
            t = new TransitionConfig
            {
                Type      = t.Type,
                Duration  = t.Duration * speed,
                Delay     = t.Delay,
                Ease      = t.Ease,
                Stiffness = t.Stiffness,
                Damping   = t.Damping,
                Mass      = t.Mass,
            };
        }
        return t;
    }

    private TransitionConfig BuildEffectiveTransitionWithDelay(double extraDelay)
    {
        var t = BuildEffectiveTransition() ?? new TransitionConfig();
        if (extraDelay <= 0) return t;
        return new TransitionConfig
        {
            Type            = t.Type,
            Duration        = t.Duration,
            Delay           = t.Delay + extraDelay,
            Ease            = t.Ease,
            EaseCubicBezier = t.EaseCubicBezier,
            Repeat          = t.Repeat,
            RepeatType      = t.RepeatType,
            RepeatDelay     = t.RepeatDelay,
            Stiffness       = t.Stiffness,
            Damping         = t.Damping,
            Mass            = t.Mass,
            Velocity        = t.Velocity,
            RestSpeed       = t.RestSpeed,
            RestDelta       = t.RestDelta,
        };
    }

    private object BuildInitConfig()
    {
        var config = new Dictionary<string, object?>();

        var initProps = ResolveProps(Initial);
        if (initProps != null)
            config["initial"] = initProps.ToJsDictionary();

        // whileInView
        var inViewProps = ResolveProps(WhileInView);
        if (inViewProps != null)
        {
            config["whileInView"]           = inViewProps.ToJsDictionary();
            config["whileInViewTransition"] = BuildEffectiveTransition()?.ToJsObject();
            config["viewportOnce"]          = Once;
            config["observeViewport"]       = true;
        }

        // Gestures
        var gestureOpts = BuildGestureOptions();
        if (gestureOpts.Count > 0)
            config["gestures"] = gestureOpts;

        return config;
    }

    private Dictionary<string, object?> BuildGestureOptions()
    {
        var d = new Dictionary<string, object?>();

        if (WhileHover != null) { d["hover"] = true; d["whileHover"] = ResolveProps(WhileHover)?.ToJsDictionary(); }
        if (WhileTap   != null) { d["tap"]   = true; d["whileTap"]   = ResolveProps(WhileTap)?.ToJsDictionary(); }
        if (WhileFocus != null) { d["focus"] = true; d["whileFocus"] = ResolveProps(WhileFocus)?.ToJsDictionary(); }
        if (WhileDrag  != null) { d["whileDrag"] = ResolveProps(WhileDrag)?.ToJsDictionary(); }

        if (Drag)
        {
            var dragOpt = DragOptions ?? new DragOptions();
            d["drag"]        = true;
            d["dragAxis"]    = dragOpt.Axis == DragAxis.Both ? null : dragOpt.Axis.ToString().ToLowerInvariant();
            d["dragElastic"] = dragOpt.Elastic;
            d["dragMomentum"]= dragOpt.Momentum;
            if (dragOpt.Constraints != null)
                d["dragConstraints"] = dragOpt.Constraints.ToJsObject();
        }

        if (d.Count > 0)
        {
            var transition = BuildEffectiveTransition();
            if (transition != null) d["gestureTransition"] = transition.ToJsObject();
        }

        return d;
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // Dispose
    // ═══════════════════════════════════════════════════════════════════════════

    public async ValueTask DisposeAsync()
    {
        PresenceCtx?.Unregister(this);
        if (_interop != null)
        {
            try { await _interop.DisposeElementAsync(_id); } catch { /* ignore during teardown */ }
            await _interop.DisposeAsync();
        }
        _dotnet?.Dispose();
    }
}
