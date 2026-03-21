using BlazorMotion.Engine;
using BlazorMotion.Models;

namespace BlazorMotion.Tests.Engine;

public class TweenDriverTests
{
    private static TweenDriver Create(double from, double to, TransitionConfig config, List<double> log)
        => new(from, to, config, v => log.Add(v));

    // ── Basic interpolation ───────────────────────────────────────────────────

    [Fact]
    public void Tick_FirstTick_AppliesFromValue()
    {
        var log = new List<double>();
        var driver = Create(0, 100, new TransitionConfig { Duration = 0.3, Ease = Easing.Linear }, log);

        driver.Tick(0);

        Assert.Equal(0.0, log[0], 5);
    }

    [Fact]
    public void Tick_MidAnimation_AppliesInterpolatedValue()
    {
        var log = new List<double>();
        var driver = Create(0, 100, new TransitionConfig { Duration = 0.3, Ease = Easing.Linear }, log);

        driver.Tick(0);   // seeds startTime = 0
        driver.Tick(150); // elapsed = 150ms, t = 0.5 → value = 50

        Assert.Equal(50.0, log[1], 1);
    }

    [Fact]
    public void Tick_AtDurationEnd_AppliesTargetAndReturnsTrue()
    {
        var log = new List<double>();
        var driver = Create(0, 100, new TransitionConfig { Duration = 0.3, Ease = Easing.Linear }, log);

        driver.Tick(0);
        bool done = driver.Tick(300); // t = 1.0

        Assert.Equal(100.0, log[^1], 5);
        Assert.True(done);
    }

    [Fact]
    public void Tick_ZeroDuration_CompletesImmediately()
    {
        var log = new List<double>();
        var driver = Create(0, 100, new TransitionConfig { Duration = 0 }, log);

        bool done = driver.Tick(0);

        Assert.Equal(100.0, log[0], 5);
        Assert.True(done);
    }

    [Fact]
    public void Tick_BeyondDuration_StillReturnsDone()
    {
        var log = new List<double>();
        var driver = Create(0, 100, new TransitionConfig { Duration = 0.3 }, log);

        driver.Tick(0);
        bool done = driver.Tick(1000); // well past end

        Assert.True(done);
        Assert.Equal(100.0, log[^1], 5);
    }

    // ── Delay ─────────────────────────────────────────────────────────────────

    [Fact]
    public void Tick_DuringDelay_AppliesFromValue()
    {
        var log = new List<double>();
        var driver = Create(0, 100, new TransitionConfig { Duration = 0.3, Delay = 0.2 }, log);

        driver.Tick(0);   // seeds startTime = 200
        driver.Tick(100); // timestamp 100 < startTime 200 → still in delay

        Assert.Equal(0.0, log[0], 5);
        Assert.Equal(0.0, log[1], 5);
    }

    [Fact]
    public void Tick_AfterDelay_CompletesAtExpectedTime()
    {
        var log = new List<double>();
        var driver = Create(0, 100, new TransitionConfig { Duration = 0.3, Delay = 0.2, Ease = Easing.Linear }, log);

        driver.Tick(0);   // startTime = 200
        driver.Tick(200); // elapsed = 0 → value ≈ 0
        bool done = driver.Tick(500); // elapsed = 300ms, t = 1.0

        Assert.True(done);
        Assert.Equal(100.0, log[^1], 5);
    }

    // ── Cancel ────────────────────────────────────────────────────────────────

    [Fact]
    public void Cancel_SnapsToTarget()
    {
        var log = new List<double>();
        var driver = Create(0, 100, new TransitionConfig { Duration = 0.3 }, log);

        driver.Tick(0);
        driver.Cancel();
        bool done = driver.Tick(150);

        Assert.Equal(100.0, log[^1], 5);
        Assert.True(done);
    }

    // ── Repeat ────────────────────────────────────────────────────────────────

    [Fact]
    public void Tick_RepeatOnce_PlaysAnimationTwiceBeforeFinishing()
    {
        var log = new List<double>();
        var driver = Create(0, 100, new TransitionConfig { Duration = 0.3, Ease = Easing.Linear, Repeat = 1 }, log);

        driver.Tick(0);
        bool done1 = driver.Tick(300); // end of first pass → repeat, returns false
        bool done2 = driver.Tick(600); // end of second pass → done, returns true

        Assert.False(done1);
        Assert.True(done2);
    }

    [Fact]
    public void Tick_MirrorRepeat_SecondPassIsReversed()
    {
        var log = new List<double>();
        var driver = Create(0, 100, new TransitionConfig
        {
            Duration = 0.3,
            Ease = Easing.Linear,
            Repeat = 1,
            RepeatType = RepeatType.Mirror,
        }, log);

        driver.Tick(0);   // value = 0
        driver.Tick(300); // value = 100, mirrors (from↔to swapped)
        driver.Tick(450); // midpoint of reversed pass: value ≈ 50
        driver.Tick(600); // end of reversed pass: value = 0

        Assert.Equal(0.0, log[^1], 1);
    }

    [Fact]
    public void Tick_InfiniteRepeat_NeverReturnsDone()
    {
        var log = new List<double>();
        var driver = Create(0, 100, new TransitionConfig
        {
            Duration = 0.3,
            Repeat = int.MaxValue,
        }, log);

        driver.Tick(0);
        for (int i = 1; i <= 10; i++)
        {
            bool done = driver.Tick(i * 300.0);
            Assert.False(done, $"Unexpected completion after iteration {i}");
        }
    }
}
