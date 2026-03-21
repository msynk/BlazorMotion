using BlazorMotion.Engine;
using BlazorMotion.Models;

namespace BlazorMotion.Tests.Engine;

public class NumericKeyframesDriverTests
{
    // ── Two frames ────────────────────────────────────────────────────────────

    [Fact]
    public void Tick_TwoFrames_LinearEase_InterpolatesCorrectly()
    {
        var log = new List<double>();
        var driver = new NumericKeyframesDriver(
            [0, 100],
            new TransitionConfig { Duration = 0.3, Ease = Easing.Linear },
            v => log.Add(v));

        driver.Tick(0);   // t=0   → 0
        driver.Tick(150); // t=0.5 → 50
        driver.Tick(300); // t=1.0 → 100

        Assert.Equal(0.0, log[0], 5);
        Assert.Equal(50.0, log[1], 1);
        Assert.Equal(100.0, log[2], 5);
    }

    [Fact]
    public void Tick_TwoFrames_AtEnd_ReturnsTrue()
    {
        var log = new List<double>();
        var driver = new NumericKeyframesDriver(
            [0, 100],
            new TransitionConfig { Duration = 0.3, Ease = Easing.Linear },
            v => log.Add(v));

        driver.Tick(0);
        bool done = driver.Tick(300);

        Assert.True(done);
        Assert.Equal(100.0, log[^1], 5);
    }

    // ── Three frames (even distribution) ──────────────────────────────────────

    [Fact]
    public void Tick_ThreeFrames_EvenTimes_CorrectSegmentInterpolation()
    {
        var log = new List<double>();
        // times automatically: [0, 0.5, 1.0]
        var driver = new NumericKeyframesDriver(
            [0, 50, 100],
            new TransitionConfig { Duration = 0.4, Ease = Easing.Linear },
            v => log.Add(v));

        driver.Tick(0);   // t=0   → frame[0] = 0
        driver.Tick(200); // t=0.5 → boundary between segment 0 and 1 → 50
        driver.Tick(400); // t=1.0 → frame[2] = 100

        Assert.Equal(0.0, log[0], 5);
        Assert.Equal(50.0, log[1], 1);
        Assert.Equal(100.0, log[2], 5);
    }

    // ── Custom times ──────────────────────────────────────────────────────────

    [Fact]
    public void Tick_CustomTimes_RespectsKeyframePlacement()
    {
        var log = new List<double>();
        // First segment covers t=0..0.8, second 0.8..1.0
        var driver = new NumericKeyframesDriver(
            [0, 80, 100],
            new TransitionConfig { Duration = 0.4, Ease = Easing.Linear, Times = [0.0, 0.8, 1.0] },
            v => log.Add(v));

        driver.Tick(0);   // t=0   → 0
        driver.Tick(320); // t=0.8 → frame[1] = 80
        driver.Tick(400); // t=1.0 → frame[2] = 100

        Assert.Equal(0.0, log[0], 5);
        Assert.Equal(80.0, log[1], 1);
        Assert.Equal(100.0, log[2], 5);
    }

    // ── Cancel ────────────────────────────────────────────────────────────────

    [Fact]
    public void Cancel_SnapsToLastFrame()
    {
        var log = new List<double>();
        var driver = new NumericKeyframesDriver(
            [0, 50, 100],
            new TransitionConfig { Duration = 0.3 },
            v => log.Add(v));

        driver.Tick(0);
        driver.Cancel();
        bool done = driver.Tick(100);

        Assert.Equal(100.0, log[^1], 5);
        Assert.True(done);
    }

    // ── Repeat / Mirror ───────────────────────────────────────────────────────

    [Fact]
    public void Tick_RepeatOnce_PlaysAnimationTwice()
    {
        var log = new List<double>();
        var driver = new NumericKeyframesDriver(
            [0, 100],
            new TransitionConfig { Duration = 0.3, Ease = Easing.Linear, Repeat = 1 },
            v => log.Add(v));

        driver.Tick(0);
        bool done1 = driver.Tick(300); // end of first pass
        bool done2 = driver.Tick(600); // end of second pass

        Assert.False(done1);
        Assert.True(done2);
    }

    [Fact]
    public void Tick_MirrorRepeat_SecondPassIsReversed()
    {
        var log = new List<double>();
        var driver = new NumericKeyframesDriver(
            [0, 100],
            new TransitionConfig
            {
                Duration = 0.3,
                Ease = Easing.Linear,
                Repeat = 1,
                RepeatType = RepeatType.Mirror,
            },
            v => log.Add(v));

        driver.Tick(0);   // → 0
        driver.Tick(300); // → 100, mirrors
        driver.Tick(450); // midpoint of reversed pass → ≈ 50
        driver.Tick(600); // end of reversed pass → 0

        Assert.Equal(0.0, log[^1], 1);
    }
}

public class ColorKeyframesDriverTests
{
    // ── Interpolation ─────────────────────────────────────────────────────────

    [Fact]
    public void Tick_TwoColorFrames_AtMidpoint_InterpolatesCorrectly()
    {
        string? lastValue = null;
        var driver = new ColorKeyframesDriver(
            ["#000000", "#ffffff"],
            new TransitionConfig { Duration = 0.3, Ease = Easing.Linear },
            v => lastValue = v);

        driver.Tick(0);
        driver.Tick(150); // t=0.5 → rgba(128,128,128,1)

        Assert.Equal("rgba(128,128,128,1)", lastValue);
    }

    [Fact]
    public void Tick_ThreeColorFrames_AtEnd_ReturnsLastFrame()
    {
        string? lastValue = null;
        var driver = new ColorKeyframesDriver(
            ["#000000", "#ff0000", "#0000ff"],
            new TransitionConfig { Duration = 0.4, Ease = Easing.Linear },
            v => lastValue = v);

        driver.Tick(0);
        driver.Tick(400); // t=1.0 → last frame is blue

        Assert.Equal("rgba(0,0,255,1)", lastValue);
    }

    [Fact]
    public void Tick_AtEnd_ReturnsTrue()
    {
        bool done = false;
        var driver = new ColorKeyframesDriver(
            ["#000000", "#ffffff"],
            new TransitionConfig { Duration = 0.3 },
            _ => { });

        driver.Tick(0);
        done = driver.Tick(300);

        Assert.True(done);
    }

    // ── Cancel ────────────────────────────────────────────────────────────────

    [Fact]
    public void Cancel_SnapsToOriginalLastFrame()
    {
        string? lastValue = null;
        var driver = new ColorKeyframesDriver(
            ["#000000", "#ff0000"],
            new TransitionConfig { Duration = 0.3 },
            v => lastValue = v);

        driver.Tick(0);
        driver.Cancel();
        driver.Tick(50);

        Assert.Equal("#ff0000", lastValue);
    }

    // ── Mirror repeat ─────────────────────────────────────────────────────────

    [Fact]
    public void Tick_MirrorRepeat_SecondPassGoesBackToFirstFrame()
    {
        string? lastValue = null;
        var driver = new ColorKeyframesDriver(
            ["#000000", "#ffffff"],
            new TransitionConfig
            {
                Duration = 0.3,
                Ease = Easing.Linear,
                Repeat = 1,
                RepeatType = RepeatType.Mirror,
            },
            v => lastValue = v);

        driver.Tick(0);   // → rgba(0,0,0,1)
        driver.Tick(300); // → rgba(255,255,255,1), mirrors
        driver.Tick(600); // reversed pass end → rgba(0,0,0,1)

        Assert.Equal("rgba(0,0,0,1)", lastValue);
    }
}
