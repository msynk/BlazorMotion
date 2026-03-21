using BlazorMotion.Engine;
using BlazorMotion.Models;

namespace BlazorMotion.Tests.Engine;

public class EasingFunctionsTests
{
    [Fact]
    public void Get_Linear_ReturnsLinearFunction()
    {
        var fn = EasingFunctions.Get(new TransitionConfig { Ease = Easing.Linear });

        Assert.Equal(0.0, fn(0.0), 5);
        Assert.Equal(0.5, fn(0.5), 5);
        Assert.Equal(1.0, fn(1.0), 5);
    }

    [Theory]
    [InlineData((int)Easing.EaseIn)]
    [InlineData((int)Easing.EaseOut)]
    [InlineData((int)Easing.EaseInOut)]
    [InlineData((int)Easing.CircIn)]
    [InlineData((int)Easing.CircOut)]
    [InlineData((int)Easing.CircInOut)]
    [InlineData((int)Easing.BackIn)]
    [InlineData((int)Easing.BackOut)]
    [InlineData((int)Easing.BackInOut)]
    [InlineData((int)Easing.Anticipate)]
    public void Get_AllEasings_BoundaryConditions(int easing)
    {
        var fn = EasingFunctions.Get(new TransitionConfig { Ease = (Easing)easing });

        Assert.Equal(0.0, fn(0.0), 3);
        Assert.Equal(1.0, fn(1.0), 3);
    }

    [Fact]
    public void Get_EaseOut_FasterAtStart()
    {
        // ease-out is faster early: at 25% of time, more than 25% of progress
        var fn = EasingFunctions.Get(new TransitionConfig { Ease = Easing.EaseOut });
        Assert.True(fn(0.25) > 0.25);
    }

    [Fact]
    public void Get_EaseIn_SlowerAtStart()
    {
        // ease-in is slower early: at 25% of time, less than 25% of progress
        var fn = EasingFunctions.Get(new TransitionConfig { Ease = Easing.EaseIn });
        Assert.True(fn(0.25) < 0.25);
    }

    [Fact]
    public void Get_EaseInOut_SymmetricAtMidpoint()
    {
        var fn = EasingFunctions.Get(new TransitionConfig { Ease = Easing.EaseInOut });
        Assert.Equal(0.5, fn(0.5), 2);
    }

    [Fact]
    public void Get_CircIn_CorrectValueAtMidpoint()
    {
        var fn = EasingFunctions.Get(new TransitionConfig { Ease = Easing.CircIn });
        double expected = 1 - Math.Sqrt(1 - 0.5 * 0.5);
        Assert.Equal(expected, fn(0.5), 5);
    }

    [Fact]
    public void Get_CircOut_CorrectValueAtMidpoint()
    {
        var fn = EasingFunctions.Get(new TransitionConfig { Ease = Easing.CircOut });
        double expected = Math.Sqrt(1 - (0.5 - 1) * (0.5 - 1));
        Assert.Equal(expected, fn(0.5), 5);
    }

    [Fact]
    public void Get_CustomCubicBezier_OverridesNamedEase()
    {
        // A (0,0,1,1) cubic-bezier approximates linear
        var config = new TransitionConfig { EaseCubicBezier = [0, 0, 1, 1] };
        var fn = EasingFunctions.Get(config);

        Assert.Equal(0.0, fn(0.0), 5);
        Assert.Equal(1.0, fn(1.0), 5);
        // Mid-point should be close to 0.5
        Assert.Equal(0.5, fn(0.5), 1);
    }

    // ── ToCssString ──────────────────────────────────────────────────────────

    [Fact]
    public void ToCssString_Null_ReturnsEase()
    {
        Assert.Equal("ease", EasingFunctions.ToCssString(null));
    }

    [Theory]
    [InlineData((int)Easing.Linear, "linear")]
    [InlineData((int)Easing.EaseIn, "ease-in")]
    [InlineData((int)Easing.EaseOut, "ease-out")]
    [InlineData((int)Easing.EaseInOut, "ease-in-out")]
    [InlineData((int)Easing.CircIn, "ease")]
    [InlineData((int)Easing.BackOut, "ease")]
    [InlineData((int)Easing.Anticipate, "ease")]
    public void ToCssString_NamedEasing_ReturnsCorrectString(int easing, string expected)
    {
        var config = new TransitionConfig { Ease = (Easing)easing };
        Assert.Equal(expected, EasingFunctions.ToCssString(config));
    }

    [Fact]
    public void ToCssString_CubicBezier_ReturnsCubicBezierString()
    {
        var config = new TransitionConfig { EaseCubicBezier = [0.1, 0.2, 0.3, 0.4] };
        Assert.Equal("cubic-bezier(0.1,0.2,0.3,0.4)", EasingFunctions.ToCssString(config));
    }

    // ── CubicBezier factory ───────────────────────────────────────────────────

    [Fact]
    public void CubicBezier_AtZero_ReturnsZero()
    {
        var fn = EasingFunctions.CubicBezier(0.42, 0, 0.58, 1);
        Assert.Equal(0.0, fn(0.0), 5);
    }

    [Fact]
    public void CubicBezier_AtOne_ReturnsOne()
    {
        var fn = EasingFunctions.CubicBezier(0.42, 0, 0.58, 1);
        Assert.Equal(1.0, fn(1.0), 5);
    }

    [Fact]
    public void CubicBezier_Linear_ApproximatesT()
    {
        // (0,0,1,1) is the identity cubic-bezier — should approximate t at all points
        var fn = EasingFunctions.CubicBezier(0.0, 0.0, 1.0, 1.0);
        Assert.Equal(0.5, fn(0.5), 1);
    }
}
