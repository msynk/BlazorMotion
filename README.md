# BlazorMotion

A Blazor-native animation library inspired by [Framer Motion](https://www.framer.com/motion/). Springs, gestures, layout animations, variants, and keyframes — **zero JavaScript dependencies**. All animation math runs in C# via WebAssembly.

> Targets **.NET 8, 9, and 10**

---

## Table of Contents

- [Installation](#installation)
- [Quick Start](#quick-start)
- [Components](#components)
  - [Motion](#motion)
  - [AnimatePresence](#animatepresence)
  - [MotionConfig](#motionconfig)
- [Animation Models](#animation-models)
  - [AnimationProps](#animationprops)
  - [TransitionConfig](#transitionconfig)
  - [MotionVariants](#motionvariants)
  - [DragOptions](#dragoptions)
  - [ViewportOptions](#viewportoptions)
- [Services](#services)
  - [AnimationController](#animationcontroller)
  - [MotionAnimateService](#motionanimateservice)
  - [MotionValue](#motionvalue)
- [Examples](#examples)
  - [Basic Animation](#basic-animation)
  - [Hover & Tap Gestures](#hover--tap-gestures)
  - [Spring Physics](#spring-physics)
  - [Drag](#drag)
  - [Variants & Stagger](#variants--stagger)
  - [Keyframes](#keyframes)
  - [Enter / Exit (AnimatePresence)](#enter--exit-animatepresence)
  - [Layout Animations (FLIP)](#layout-animations-flip)
  - [Scroll / Viewport](#scroll--viewport)
  - [Programmatic Control](#programmatic-control)
- [Accessibility](#accessibility)

---

## Installation

```bash
dotnet add package BlazorMotion
```

Register the services in `Program.cs`:

```csharp
builder.Services.AddBlazorMotion();
```

Add the script to your `index.html` (Blazor WebAssembly) or `App.razor` / `_Host.cshtml` (Blazor Server):

```html
<script src="_content/BlazorMotion/blazor-motion.js"></script>
```

---

## Quick Start

```razor
<Motion Animate='new AnimationProps { Opacity = 1, Y = 0 }'
        Initial='new AnimationProps { Opacity = 0, Y = 20 }'>
    Hello, BlazorMotion!
</Motion>
```

That's it — the element fades in and slides up on first render.

---

## Components

### Motion

`<Motion>` is the core component. It replaces any HTML element and adds animation superpowers.

```razor
<Motion Tag="section"
        Class="my-card"
        Initial='new AnimationProps { Opacity = 0, Scale = 0.9 }'
        Animate='new AnimationProps { Opacity = 1, Scale = 1 }'
        Exit='new AnimationProps { Opacity = 0, Scale = 0.9 }'
        WhileHover='new AnimationProps { Scale = 1.05 }'
        WhileTap='new AnimationProps { Scale = 0.97 }'
        Transition='new TransitionConfig { Type = TransitionType.Spring, Stiffness = 200, Damping = 20 }'>
    <p>Content</p>
</Motion>
```

#### Parameters

| Parameter | Type | Description |
|---|---|---|
| `Tag` | `string` | HTML element tag (default: `"div"`) |
| `Class` | `string?` | CSS class attribute |
| `Style` | `string?` | Inline style attribute |
| `ChildContent` | `RenderFragment?` | Child content |
| `Initial` | `AnimationTarget?` | Starting state (props, variant name, or `false`) |
| `Animate` | `AnimationTarget?` | Target state |
| `Exit` | `AnimationTarget?` | State to animate to before unmounting (requires `<AnimatePresence>`) |
| `WhileHover` | `AnimationTarget?` | Overlay applied while hovered |
| `WhileTap` | `AnimationTarget?` | Overlay applied while tapped/pressed |
| `WhileFocus` | `AnimationTarget?` | Overlay applied while focused |
| `WhileDrag` | `AnimationTarget?` | Overlay applied while dragging |
| `WhileInView` | `AnimationTarget?` | Overlay applied while in viewport |
| `Transition` | `TransitionConfig?` | Controls timing/physics of all transitions |
| `Variants` | `MotionVariants?` | Named animation states |
| `Drag` | `bool` | Enable drag gesture |
| `DragOptions` | `DragOptions?` | Drag axis, constraints, elasticity |
| `Layout` | `bool` | Enable automatic FLIP layout animations |
| `LayoutId` | `string?` | Shared-element transition ID |
| `Once` | `bool` | `WhileInView` fires once and never reverses |
| `Viewport` | `ViewportOptions?` | Advanced viewport tracking options |
| `AdditionalAttributes` | `Dictionary<string, object>?` | Extra HTML attributes (passed through) |

#### Event Callbacks

```
OnHoverStart / OnHoverEnd
OnTapStart / OnTap / OnTapCancel
OnFocusStart / OnFocusEnd
OnPanStart / OnPan / OnPanEnd         (PanInfo)
OnDragStart / OnDrag / OnDragEnd      (PanInfo)
OnAnimationStart / OnAnimationComplete
OnViewportEnter / OnViewportLeave
```

---

### AnimatePresence

Wraps conditional content to enable exit animations. Children remain in the DOM while their exit animation plays, then are removed.

```razor
<AnimatePresence IsPresent="@_show">
    <Motion Initial='new AnimationProps { Opacity = 0 }'
            Animate='new AnimationProps { Opacity = 1 }'
            Exit='new AnimationProps { Opacity = 0 }'>
        I animate in and out!
    </Motion>
</AnimatePresence>

<button @onclick="() => _show = !_show">Toggle</button>

@code {
    bool _show = true;
}
```

#### Parameters

| Parameter | Type | Description |
|---|---|---|
| `IsPresent` | `bool` | Controls whether the child content is present (default: `true`) |
| `ExitBeforeEnter` | `bool` | Wait for exit animation to finish before entering new content |
| `ChildContent` | `RenderFragment?` | Content to animate |

---

### MotionConfig

Provides global animation defaults to an entire subtree via cascading values.

```razor
<MotionConfig Transition='new TransitionConfig { Duration = 0.2 }'
              TransitionSpeed="1.5">
    <!-- all Motion elements inside inherit these defaults -->
</MotionConfig>
```

#### Parameters

| Parameter | Type | Description |
|---|---|---|
| `Transition` | `TransitionConfig?` | Global default transition for all descendant `<Motion>` elements |
| `ReduceMotion` | `bool?` | Override motion reduction (`null` = auto-detect user preference) |
| `TransitionSpeed` | `double` | Scale factor for all animation durations (default: `1.0`) |

---

## Animation Models

### AnimationProps

Describes the animatable state — the *what* of an animation.

```csharp
new AnimationProps
{
    // Transform
    X = 100, Y = -20, Z = 0,
    Scale = 1.2, ScaleX = 1, ScaleY = 1,
    Rotate = 45, RotateX = 0, RotateY = 0, RotateZ = 0,
    SkewX = 10, SkewY = 0,
    Perspective = 800,

    // Visual
    Opacity = 1,
    BackgroundColor = "#ff0000",
    Color = "rgba(0,0,0,0.8)",
    BorderColor = "#ccc",
    Width = 200, Height = 200,
    BorderRadius = 8,
    BoxShadow = "0 4px 20px rgba(0,0,0,0.2)",

    // SVG
    Fill = "#blue",
    Stroke = "#red",
    PathLength = 1,        // 0–1, drives stroke-dashoffset drawing

    // CSS custom properties
    CssVars = new() { ["--accent"] = "#ff6b6b" },

    // Keyframe arrays (multi-step)
    Keyframes = new() { ["scale"] = new double[] { 1, 1.4, 0.8, 1 } }
}
```

---

### TransitionConfig

Controls *how* a value moves between states.

```csharp
// Tween (duration-based, default)
new TransitionConfig
{
    Type = TransitionType.Tween,
    Duration = 0.4,         // seconds
    Delay = 0.1,
    Ease = Easing.EaseInOut
}

// Spring (physics-based)
new TransitionConfig
{
    Type = TransitionType.Spring,
    Stiffness = 200,        // higher = snappier
    Damping = 15,           // higher = less bounce
    Mass = 1,
    Bounce = 0.4,           // 0 = critical, 1 = very bouncy
    VisualDuration = 0.5    // visual time to reach target (works with Bounce)
}

// Inertia (momentum deceleration)
new TransitionConfig
{
    Type = TransitionType.Inertia,
    InertiaVelocity = 500,
    TimeConstant = 700,
    Power = 0.8,
    InertiaMin = 0, InertiaMax = 1000
}
```

**Shorthand:**
```csharp
TransitionConfig.Spring(stiffness: 150, damping: 12)
```

**Repeat:**
```csharp
new TransitionConfig
{
    Repeat = int.MaxValue,       // loop forever
    RepeatType = RepeatType.Mirror  // ping-pong
}
```

**Variant orchestration:**
```csharp
new TransitionConfig
{
    StaggerChildren = 0.08,      // delay between each child
    DelayChildren = 0.2,         // delay before first child
    When = WhenType.BeforeChildren
}
```

**Per-property overrides:**
```csharp
new TransitionConfig
{
    Duration = 0.3,
    Properties = new()
    {
        ["opacity"] = new TransitionConfig { Duration = 0.6, Ease = Easing.EaseOut }
    }
}
```

---

### MotionVariants

Named sets of `AnimationProps` for reusable, coordinated animations.

```csharp
var variants = MotionVariants.Create(
    ("hidden",  new AnimationProps { Opacity = 0, Y = 20 }),
    ("visible", new AnimationProps { Opacity = 1, Y = 0  })
);
```

```razor
<Motion Variants="variants"
        Initial='"hidden"'
        Animate='"visible"'
        Transition='new TransitionConfig { StaggerChildren = 0.1 }'>
    <Motion>Item 1</Motion>
    <Motion>Item 2</Motion>
    <Motion>Item 3</Motion>
</Motion>
```

Children of a `<Motion>` with `Variants` automatically inherit and propagate the active variant name down the tree.

---

### DragOptions

```csharp
new DragOptions
{
    Axis = DragAxis.X,                          // Both (default), X, or Y
    Constraints = DragConstraints.Horizontal(-200, 200),  // or .Vertical / .Box
    Elastic = 0.2,                              // 0 = hard boundary, 1 = no constraint
    Momentum = true,                            // apply inertia on release
    SnapToOrigin = false,                       // spring back to start
    DirectionLock = true                        // lock to dominant axis
}
```

`DragConstraints` helpers:
```csharp
DragConstraints.Horizontal(left: -100, right: 100)
DragConstraints.Vertical(top: -100, bottom: 100)
DragConstraints.Box(left: -100, right: 100, top: -100, bottom: 100)
```

---

### ViewportOptions

Controls `WhileInView` tracking.

```csharp
new ViewportOptions
{
    Once = true,            // trigger once, never reverse
    Margin = "-100px",      // IntersectionObserver root margin
    Amount = "some"         // "some", "all", or 0–1 threshold
}
```

---

## Services

### AnimationController

Programmatic control bound to a specific element by ID.

```razor
<Motion id="my-box" ... />

@code {
    [Inject] AnimationController Controller { get; set; } = default!;

    protected override void OnAfterRender(bool firstRender)
    {
        if (firstRender) Controller.BindTo("my-box");
    }

    async Task Pulse()
    {
        await Controller.AnimateAsync(
            new AnimationProps { Scale = 1.2 },
            new TransitionConfig { Type = TransitionType.Spring, Bounce = 0.5 }
        );
    }

    void Snap() => Controller.Set(new AnimationProps { X = 0, Y = 0 });
}
```

| Method | Description |
|---|---|
| `BindTo(id)` | Bind controller to element with the given ID |
| `AnimateAsync(props, transition)` | Fire-and-forget animation |
| `AnimateAwaitAsync(props, transition)` | Awaitable animation |
| `Set(props)` | Instantly set properties without animation |
| `Stop(params string[] properties)` | Stop animations (`null` = all) |

---

### MotionAnimateService

Animate elements by CSS selector or `ElementReference` without wrapping them in `<Motion>`.

```razor
@inject MotionAnimateService Motion

<div id="target">Animate me</div>

@code {
    async Task AnimateIt()
    {
        var controls = await Motion.AnimateAsync(
            "#target",
            new AnimationProps { X = 100, Opacity = 0.5 },
            new TransitionConfig { Duration = 0.6 }
        );

        await controls.WhenCompleteAsync();
    }

    async Task StaggerList()
    {
        await Motion.AnimateAsync(
            ".list-item",
            new AnimationProps { Opacity = 1, Y = 0 },
            new TransitionConfig { StaggerChildren = 0.1 }
        );
    }
}
```

`AnimationControls` returned by `AnimateAsync`:

| Member | Description |
|---|---|
| `Stop()` | Cancel and snap to current position |
| `Complete()` | Cancel and snap to target |
| `WhenCompleteAsync()` | Await natural completion |
| `await controls` | Also awaitable directly |

---

### MotionValue

A reactive numeric value you can subscribe to and transform, similar to Framer Motion's `MotionValue<T>`.

```csharp
var mv = new MotionValue<double>(0);

// Subscribe
mv.Subscribe(v => Console.WriteLine($"value: {v}"));

// Update
await mv.SetAsync(100);

// Transform to another value
MotionValue<double> scaled = mv.Transform(v => v * 2);

// Map between ranges
MotionValue<double> mapped = mv.Transform(
    inputRange:  new[] { 0.0, 1.0 },
    outputRange: new[] { 0.0, 360.0 }
);
```

---

## Examples

### Basic Animation

```razor
<Motion Initial='new AnimationProps { Opacity = 0, Y = -20 }'
        Animate='new AnimationProps { Opacity = 1, Y = 0 }'
        Transition='new TransitionConfig { Duration = 0.5, Ease = Easing.EaseOut }'>
    I animate on mount
</Motion>
```

---

### Hover & Tap Gestures

```razor
<Motion WhileHover='new AnimationProps { Scale = 1.1, BackgroundColor = "#4f46e5" }'
        WhileTap='new AnimationProps { Scale = 0.95 }'
        Transition='TransitionConfig.Spring(stiffness: 300, damping: 20)'
        OnHoverStart="@(() => Console.WriteLine("hovered"))"
        Style="cursor: pointer; padding: 1rem; border-radius: 8px;">
    Click me
</Motion>
```

---

### Spring Physics

```razor
<Motion Animate='new AnimationProps { X = _x }'
        Transition='new TransitionConfig
        {
            Type = TransitionType.Spring,
            Stiffness = 120,
            Damping = 8,
            Mass = 0.5
        }' />

@code { double _x = 0; }
```

---

### Drag

```razor
<Motion Drag="true"
        DragOptions='new DragOptions
        {
            Axis = DragAxis.X,
            Constraints = DragConstraints.Horizontal(-150, 150),
            Elastic = 0.3,
            Momentum = true
        }'
        WhileDrag='new AnimationProps { Scale = 1.05 }'
        Style="width: 80px; height: 80px; background: #6366f1; border-radius: 50%; cursor: grab;">
</Motion>
```

---

### Variants & Stagger

```razor
@code {
    MotionVariants _variants = MotionVariants.Create(
        ("hidden",  new AnimationProps { Opacity = 0, Y = 15 }),
        ("visible", new AnimationProps { Opacity = 1, Y = 0  })
    );
}

<Motion Variants="_variants"
        Initial='"hidden"'
        Animate='"visible"'
        Transition='new TransitionConfig { StaggerChildren = 0.1, DelayChildren = 0.2 }'>
    @foreach (var item in _items)
    {
        <Motion Tag="li">@item</Motion>
    }
</Motion>
```

---

### Keyframes

```razor
<Motion Animate='new AnimationProps
        {
            Keyframes = new()
            {
                ["scale"]   = new double[] { 1, 1.3, 0.85, 1 },
                ["opacity"] = new double[] { 1, 0.5, 1 }
            }
        }'
        Transition='new TransitionConfig { Duration = 0.8, Repeat = int.MaxValue }' />
```

---

### Enter / Exit (AnimatePresence)

```razor
<AnimatePresence IsPresent="@_visible">
    <Motion Tag="div"
            Initial='new AnimationProps { Opacity = 0, Scale = 0.9 }'
            Animate='new AnimationProps { Opacity = 1, Scale = 1 }'
            Exit='new AnimationProps { Opacity = 0, Scale = 0.9 }'
            Transition='TransitionConfig.Spring(stiffness: 250, damping: 22)'>
        Modal content
    </Motion>
</AnimatePresence>

<button @onclick="() => _visible = !_visible">Toggle Modal</button>

@code { bool _visible; }
```

---

### Layout Animations (FLIP)

```razor
<Motion Layout="true"
        Transition='new TransitionConfig { Type = TransitionType.Spring, Stiffness = 200, Damping = 25 }'>
    @ChildContent
</Motion>
```

Any time this element's position or size changes (e.g., items being added/removed from a list), it will smoothly animate to its new layout position.

---

### Scroll / Viewport

```razor
<Motion WhileInView='new AnimationProps { Opacity = 1, Y = 0 }'
        Initial='new AnimationProps { Opacity = 0, Y = 40 }'
        Viewport='new ViewportOptions { Once = true, Margin = "-80px", Amount = "some" }'
        Transition='new TransitionConfig { Duration = 0.6, Ease = Easing.EaseOut }'>
    Animates in when scrolled into view
</Motion>
```

---

### Programmatic Control

```razor
<div id="box" style="width:100px; height:100px; background:#6366f1;"></div>

@inject MotionAnimateService Motion

<button @onclick="Animate">Animate</button>

@code {
    async Task Animate()
    {
        var controls = await Motion.AnimateAsync(
            "#box",
            new AnimationProps { X = 200, Rotate = 45 },
            new TransitionConfig { Type = TransitionType.Spring, Bounce = 0.4 }
        );
        await controls;
    }
}
```

---

## Accessibility

BlazorMotion respects the user's **prefers-reduced-motion** media query by default. When `ReduceMotion` is not explicitly set on `<MotionConfig>`, the library auto-detects the OS preference and reduces or disables animations accordingly.

To override:

```razor
<MotionConfig ReduceMotion="false">
    <!-- forces animations on even if the OS says reduce motion -->
</MotionConfig>
```

---

## License

[MIT](LICENSE)
