using BlazorMotion.Engine;
using BlazorMotion.Models;

namespace BlazorMotion.Tests.Engine;

public class SpringDriverTests
{
    // ── Settling ──────────────────────────────────────────────────────────────

    [Fact]
    public void Tick_EventuallySettlesAtTarget()
    {
        double lastValue = double.NaN;
        var config = new TransitionConfig
        {
            Stiffness = 100,
            Damping = 20,
            Mass = 1,
            RestSpeed = 0.01,
            RestDelta = 0.01,
        };
        var driver = new SpringDriver(0, 100, config, v => lastValue = v);

        bool done = false;
        double ts = 0;
        while (!done && ts < 10_000)
        {
            ts += 16.67; // ~60 fps
            done = driver.Tick(ts);
        }

        Assert.True(done, "Spring did not settle within 10 s");
        Assert.Equal(100.0, lastValue, 2);
    }

    [Fact]
    public void Tick_OverdampedSpring_DoesNotOvershoot()
    {
        // With damping >> critical damping the position should be monotonically increasing
        double prevValue = -1;
        var config = new TransitionConfig
        {
            Stiffness = 100,
            Damping = 100,
            Mass = 1,
        };
        var driver = new SpringDriver(0, 100, config, v =>
        {
            // Allow a tiny floating-point tolerance
            Assert.True(v >= prevValue - 1e-6, $"Overshoot detected: {v} < {prevValue}");
            prevValue = v;
        });

        double ts = 0;
        while (ts < 5_000)
        {
            ts += 16.67;
            if (driver.Tick(ts)) break;
        }

        Assert.True(prevValue > 0, "Spring never moved");
    }

    [Fact]
    public void Tick_UnderdampedSpring_OscillatesAndSettles()
    {
        double lastValue = 0;
        var config = new TransitionConfig
        {
            Stiffness = 200,
            Damping = 5, // very low damping → will oscillate
            Mass = 1,
            RestSpeed = 0.01,
            RestDelta = 0.01,
        };
        var driver = new SpringDriver(0, 100, config, v => lastValue = v);

        bool done = false;
        double ts = 0;
        while (!done && ts < 30_000)
        {
            ts += 16.67;
            done = driver.Tick(ts);
        }

        Assert.True(done, "Under-damped spring did not settle");
        Assert.Equal(100.0, lastValue, 2);
    }

    // ── Delay ─────────────────────────────────────────────────────────────────

    [Fact]
    public void Tick_DuringDelay_HoldsAtFromValue()
    {
        var values = new List<double>();
        var config = new TransitionConfig
        {
            Delay = 0.2,  // 200 ms
            Stiffness = 100,
            Damping = 20,
            Mass = 1,
        };
        var driver = new SpringDriver(0, 100, config, v => values.Add(v));

        driver.Tick(0);   // startTs = 0; elapsed=0 < 200 ms delay
        driver.Tick(100); // still in delay

        Assert.Equal(0.0, values[0], 5);
        Assert.Equal(0.0, values[1], 5);
    }

    // ── Cancel ────────────────────────────────────────────────────────────────

    [Fact]
    public void Cancel_SnapsToTarget()
    {
        double lastValue = 0;
        var driver = new SpringDriver(0, 100, new TransitionConfig
        {
            Stiffness = 100,
            Damping = 10,
            Mass = 1,
        }, v => lastValue = v);

        driver.Tick(0);
        driver.Tick(16);
        driver.Cancel();
        bool done = driver.Tick(32);

        Assert.Equal(100.0, lastValue, 5);
        Assert.True(done);
    }

    // ── Initial velocity ──────────────────────────────────────────────────────

    [Fact]
    public void Tick_PositiveInitialVelocity_MovesImmediatelyTowardTarget()
    {
        var values = new List<double>();
        var config = new TransitionConfig
        {
            Stiffness = 100,
            Damping = 10,
            Mass = 1,
            Velocity = 500, // toward target (0 → 100)
        };
        var driver = new SpringDriver(0, 100, config, v => values.Add(v));

        driver.Tick(0);
        driver.Tick(16);

        Assert.True(values[1] > values[0], "Spring with positive velocity should move toward target immediately");
    }
}
