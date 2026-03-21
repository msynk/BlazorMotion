using BlazorMotion.Engine;
using BlazorMotion.Models;

namespace BlazorMotion.Tests.Engine;

public class InertiaDriverTests
{
    // ── Motion ────────────────────────────────────────────────────────────────

    [Fact]
    public void Tick_MovesTowardProjectedTarget()
    {
        var values = new List<double>();
        var config = new TransitionConfig
        {
            InertiaVelocity = 1000, // px/s
            Power = 0.8,
            TimeConstant = 700,
            InertiaRestDelta = 0.5,
        };
        var driver = new InertiaDriver(0, config, v => values.Add(v));

        driver.Tick(0);   // pos = 0 (no elapsed time yet)
        driver.Tick(100); // ~64ms capped → pos > 0

        Assert.True(values.Count >= 2);
        Assert.True(values[1] > values[0], "Inertia should move toward projected target");
    }

    [Fact]
    public void Tick_EventuallySettlesAtProjectedTarget()
    {
        double lastValue = 0;
        var config = new TransitionConfig
        {
            InertiaVelocity = 500,
            Power = 0.8,
            TimeConstant = 700,
            InertiaRestDelta = 0.5,
        };
        var driver = new InertiaDriver(0, config, v => lastValue = v);

        bool done = false;
        double ts = 0;
        while (!done && ts < 10_000)
        {
            ts += 16.67;
            done = driver.Tick(ts);
        }

        Assert.True(done, "Inertia should settle");
        double projected = 0 + 0.8 * 500;
        Assert.Equal(projected, lastValue, 5); // snaps to _projected exactly on settle
    }

    // ── Bounds clamping ───────────────────────────────────────────────────────

    [Fact]
    public void Tick_ClampsToMaxBound()
    {
        double lastValue = 0;
        var config = new TransitionConfig
        {
            InertiaVelocity = 100_000, // projected would be huge
            Power = 0.8,
            TimeConstant = 700,
            InertiaRestDelta = 0.5,
            InertiaMax = 100.0,
        };
        var driver = new InertiaDriver(0, config, v => lastValue = v);

        bool done = false;
        double ts = 0;
        while (!done && ts < 10_000)
        {
            ts += 16.67;
            done = driver.Tick(ts);
        }

        Assert.True(done);
        Assert.Equal(100.0, lastValue, 5);
    }

    [Fact]
    public void Tick_ClampsToMinBound()
    {
        double lastValue = 0;
        var config = new TransitionConfig
        {
            InertiaVelocity = -100_000, // large negative velocity
            Power = 0.8,
            TimeConstant = 700,
            InertiaRestDelta = 0.5,
            InertiaMin = -100.0,
        };
        var driver = new InertiaDriver(0, config, v => lastValue = v);

        bool done = false;
        double ts = 0;
        while (!done && ts < 10_000)
        {
            ts += 16.67;
            done = driver.Tick(ts);
        }

        Assert.True(done);
        Assert.Equal(-100.0, lastValue, 5);
    }

    // ── Delay ─────────────────────────────────────────────────────────────────

    [Fact]
    public void Tick_DuringDelay_HoldsAtStart()
    {
        var values = new List<double>();
        var config = new TransitionConfig
        {
            Delay = 0.3, // 300 ms
            InertiaVelocity = 1000,
            Power = 0.8,
            TimeConstant = 700,
            InertiaRestDelta = 0.5,
        };
        var driver = new InertiaDriver(0, config, v => values.Add(v));

        driver.Tick(0);
        driver.Tick(100); // still within 300 ms delay

        Assert.Equal(0.0, values[0], 5);
        Assert.Equal(0.0, values[1], 5);
    }

    // ── Cancel ────────────────────────────────────────────────────────────────

    [Fact]
    public void Cancel_SnapsToProjectedTarget()
    {
        double lastValue = 0;
        var config = new TransitionConfig
        {
            InertiaVelocity = 1000,
            Power = 0.8,
            TimeConstant = 700,
            InertiaRestDelta = 0.5,
        };
        var driver = new InertiaDriver(0, config, v => lastValue = v);

        double projected = 0 + 0.8 * 1000; // 800

        driver.Tick(0);
        driver.Cancel();
        bool done = driver.Tick(16);

        Assert.Equal(projected, lastValue, 5);
        Assert.True(done);
    }

    // ── Zero velocity ─────────────────────────────────────────────────────────

    [Fact]
    public void Tick_ZeroVelocity_CompletesImmediately()
    {
        var values = new List<double>();
        var config = new TransitionConfig
        {
            InertiaVelocity = 0,
            Power = 0.8,
            TimeConstant = 700,
            InertiaRestDelta = 0.5,
        };
        var driver = new InertiaDriver(0, config, v => values.Add(v));

        // projected = 0 + 0.8*0 = 0; |projected - pos| = 0 < 0.5 → done on first non-zero elapsed tick
        driver.Tick(0);
        bool done = driver.Tick(16);

        Assert.True(done);
        Assert.Equal(0.0, values[^1], 5);
    }
}
