using BlazorMotion.Models;

namespace BlazorMotion.Tests.Models;

public class TransitionConfigTests
{
    // ── Default values ────────────────────────────────────────────────────────

    [Fact]
    public void DefaultValues_MatchExpected()
    {
        var config = new TransitionConfig();

        Assert.Equal(TransitionType.Tween, config.Type);
        Assert.Equal(0.3, config.Duration);
        Assert.Equal(0.0, config.Delay);
        Assert.Equal(Easing.EaseOut, config.Ease);
        Assert.Null(config.EaseCubicBezier);
        Assert.Equal(0, config.Repeat);
        Assert.Equal(RepeatType.Loop, config.RepeatType);
        Assert.Equal(0.0, config.RepeatDelay);
        Assert.Null(config.Times);

        // Spring defaults
        Assert.Equal(100, config.Stiffness);
        Assert.Equal(10, config.Damping);
        Assert.Equal(1, config.Mass);
        Assert.Equal(0.0, config.Velocity);
        Assert.Equal(0.01, config.RestSpeed);
        Assert.Equal(0.01, config.RestDelta);

        // Inertia defaults
        Assert.Equal(0.0, config.InertiaVelocity);
        Assert.Equal(700, config.TimeConstant);
        Assert.Equal(0.8, config.Power);
        Assert.Equal(0.5, config.InertiaRestDelta);
        Assert.Null(config.InertiaMin);
        Assert.Null(config.InertiaMax);

        // Orchestration defaults
        Assert.Null(config.StaggerChildren);
        Assert.Null(config.DelayChildren);
        Assert.Equal(WhenType.Default, config.When);
        Assert.Null(config.Properties);
    }

    // ── Factory helpers ───────────────────────────────────────────────────────

    [Fact]
    public void Tween_DefaultFactory_UsesDefaults()
    {
        var config = TransitionConfig.Tween();

        Assert.Equal(TransitionType.Tween, config.Type);
        Assert.Equal(0.3, config.Duration);
        Assert.Equal(Easing.EaseOut, config.Ease);
    }

    [Fact]
    public void Tween_CustomFactory_SetsValues()
    {
        var config = TransitionConfig.Tween(0.5, Easing.EaseIn);

        Assert.Equal(TransitionType.Tween, config.Type);
        Assert.Equal(0.5, config.Duration);
        Assert.Equal(Easing.EaseIn, config.Ease);
    }

    [Fact]
    public void Spring_DefaultFactory_UsesDefaults()
    {
        var config = TransitionConfig.Spring();

        Assert.Equal(TransitionType.Spring, config.Type);
        Assert.Equal(100, config.Stiffness);
        Assert.Equal(10, config.Damping);
        Assert.Equal(1, config.Mass);
    }

    [Fact]
    public void Spring_CustomFactory_SetsValues()
    {
        var config = TransitionConfig.Spring(stiffness: 200, damping: 25, mass: 2);

        Assert.Equal(TransitionType.Spring, config.Type);
        Assert.Equal(200, config.Stiffness);
        Assert.Equal(25, config.Damping);
        Assert.Equal(2, config.Mass);
    }

    [Fact]
    public void Inertia_DefaultFactory_UsesDefaults()
    {
        var config = TransitionConfig.Inertia();

        Assert.Equal(TransitionType.Inertia, config.Type);
        Assert.Equal(0.0, config.InertiaVelocity);
        Assert.Equal(700, config.TimeConstant);
    }

    [Fact]
    public void Inertia_CustomFactory_SetsValues()
    {
        var config = TransitionConfig.Inertia(velocity: 500, timeConstant: 1000);

        Assert.Equal(TransitionType.Inertia, config.Type);
        Assert.Equal(500, config.InertiaVelocity);
        Assert.Equal(1000, config.TimeConstant);
    }

    // ── Repeat / Infinite sentinel ────────────────────────────────────────────

    [Fact]
    public void InfiniteRepeat_UsesIntMaxValue()
    {
        var config = new TransitionConfig { Repeat = int.MaxValue };
        Assert.Equal(int.MaxValue, config.Repeat);
    }

    // ── Per-property overrides ────────────────────────────────────────────────

    [Fact]
    public void PerPropertyOverrides_CanBeSetAndRetrieved()
    {
        var config = new TransitionConfig
        {
            Duration = 0.5,
            Properties = new Dictionary<string, TransitionConfig>
            {
                ["opacity"] = new TransitionConfig { Duration = 0.1 },
                ["transform"] = TransitionConfig.Spring(stiffness: 300),
            },
        };

        Assert.NotNull(config.Properties);
        Assert.Equal(2, config.Properties.Count);
        Assert.Equal(0.1, config.Properties["opacity"].Duration);
        Assert.Equal(TransitionType.Spring, config.Properties["transform"].Type);
        Assert.Equal(300, config.Properties["transform"].Stiffness);
    }

    // ── Orchestration ─────────────────────────────────────────────────────────

    [Fact]
    public void Orchestration_Properties_CanBeSet()
    {
        var config = new TransitionConfig
        {
            StaggerChildren = 0.05,
            DelayChildren = 0.1,
            When = WhenType.BeforeChildren,
        };

        Assert.Equal(0.05, config.StaggerChildren);
        Assert.Equal(0.1, config.DelayChildren);
        Assert.Equal(WhenType.BeforeChildren, config.When);
    }

    // ── Custom cubic-bezier ───────────────────────────────────────────────────

    [Fact]
    public void EaseCubicBezier_CanBeSet()
    {
        var config = new TransitionConfig { EaseCubicBezier = [0.25, 0.1, 0.25, 1.0] };

        Assert.NotNull(config.EaseCubicBezier);
        Assert.Equal(4, config.EaseCubicBezier.Length);
        Assert.Equal(0.25, config.EaseCubicBezier[0]);
    }
}
