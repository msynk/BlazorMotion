using BlazorMotion.Engine;

namespace BlazorMotion.Tests.Engine;

public class TransformComposerTests
{
    // ── IsTransformProp ───────────────────────────────────────────────────────

    [Theory]
    [InlineData("x", true)]
    [InlineData("y", true)]
    [InlineData("z", true)]
    [InlineData("rotate", true)]
    [InlineData("rotateX", true)]
    [InlineData("rotateY", true)]
    [InlineData("rotateZ", true)]
    [InlineData("scaleX", true)]
    [InlineData("scaleY", true)]
    [InlineData("scale", true)]
    [InlineData("skewX", true)]
    [InlineData("skewY", true)]
    [InlineData("perspective", true)]
    [InlineData("X", true)]      // case-insensitive
    [InlineData("SCALE", true)]
    [InlineData("opacity", false)]
    [InlineData("width", false)]
    [InlineData("color", false)]
    [InlineData("backgroundColor", false)]
    public void IsTransformProp_ReturnsExpected(string key, bool expected)
    {
        Assert.Equal(expected, TransformComposer.IsTransformProp(key));
    }

    // ── Build — empty/identity ────────────────────────────────────────────────

    [Fact]
    public void Build_EmptyDict_ReturnsEmpty()
    {
        Assert.Equal(string.Empty, TransformComposer.Build([]));
    }

    [Fact]
    public void Build_AllIdentityValues_ReturnsEmpty()
    {
        var t = new Dictionary<string, double> { ["x"] = 0, ["y"] = 0, ["rotate"] = 0 };
        Assert.Equal(string.Empty, TransformComposer.Build(t));
    }

    // ── Translate ─────────────────────────────────────────────────────────────

    [Fact]
    public void Build_Translation2D_ReturnsTranslate()
    {
        var t = new Dictionary<string, double> { ["x"] = 10, ["y"] = 20 };
        Assert.Equal("translate(10px,20px)", TransformComposer.Build(t));
    }

    [Fact]
    public void Build_Translation3D_ReturnsTranslate3d()
    {
        var t = new Dictionary<string, double> { ["x"] = 10, ["y"] = 20, ["z"] = 30 };
        Assert.Equal("translate3d(10px,20px,30px)", TransformComposer.Build(t));
    }

    [Fact]
    public void Build_OnlyX_ReturnsTranslateWithZeroY()
    {
        var t = new Dictionary<string, double> { ["x"] = 50 };
        Assert.Equal("translate(50px,0px)", TransformComposer.Build(t));
    }

    // ── Scale ─────────────────────────────────────────────────────────────────

    [Fact]
    public void Build_UniformScale_ReturnsScale()
    {
        var t = new Dictionary<string, double> { ["scale"] = 2.0 };
        Assert.Equal("scale(2)", TransformComposer.Build(t));
    }

    [Fact]
    public void Build_ScaleXOnly_ReturnsScaleX()
    {
        var t = new Dictionary<string, double> { ["scaleX"] = 1.5 };
        Assert.Equal("scaleX(1.5)", TransformComposer.Build(t));
    }

    [Fact]
    public void Build_ScaleYOnly_ReturnsScaleY()
    {
        var t = new Dictionary<string, double> { ["scaleY"] = 0.5 };
        Assert.Equal("scaleY(0.5)", TransformComposer.Build(t));
    }

    [Fact]
    public void Build_ScaleXAtIdentity_OmitsScaleX()
    {
        // scaleX=1 is identity → omitted; y=10 is non-zero → translate present
        var t = new Dictionary<string, double> { ["scaleX"] = 1.0, ["y"] = 10 };
        Assert.Equal("translate(0px,10px)", TransformComposer.Build(t));
    }

    // ── Rotate ────────────────────────────────────────────────────────────────

    [Fact]
    public void Build_Rotate_ReturnsRotateDeg()
    {
        var t = new Dictionary<string, double> { ["rotate"] = 45 };
        Assert.Equal("rotate(45deg)", TransformComposer.Build(t));
    }

    [Fact]
    public void Build_RotateZAlias_ReturnsRotateDeg()
    {
        var t = new Dictionary<string, double> { ["rotateZ"] = 90 };
        Assert.Equal("rotate(90deg)", TransformComposer.Build(t));
    }

    [Fact]
    public void Build_RotateX_ReturnsRotateXDeg()
    {
        var t = new Dictionary<string, double> { ["rotateX"] = 30 };
        Assert.Equal("rotateX(30deg)", TransformComposer.Build(t));
    }

    [Fact]
    public void Build_RotateY_ReturnsRotateYDeg()
    {
        var t = new Dictionary<string, double> { ["rotateY"] = 60 };
        Assert.Equal("rotateY(60deg)", TransformComposer.Build(t));
    }

    // ── Skew ──────────────────────────────────────────────────────────────────

    [Fact]
    public void Build_SkewX_ReturnsSkewXDeg()
    {
        var t = new Dictionary<string, double> { ["skewX"] = 15 };
        Assert.Equal("skewX(15deg)", TransformComposer.Build(t));
    }

    [Fact]
    public void Build_SkewY_ReturnsSkewYDeg()
    {
        var t = new Dictionary<string, double> { ["skewY"] = 10 };
        Assert.Equal("skewY(10deg)", TransformComposer.Build(t));
    }

    // ── Perspective ───────────────────────────────────────────────────────────

    [Fact]
    public void Build_Perspective_AppearsFirst()
    {
        var t = new Dictionary<string, double> { ["perspective"] = 500, ["x"] = 10 };
        var result = TransformComposer.Build(t);
        Assert.StartsWith("perspective(500px)", result);
        Assert.Contains("translate(10px,0px)", result);
    }

    // ── Combined / ordering ───────────────────────────────────────────────────

    [Fact]
    public void Build_Combined_PreservesOrder()
    {
        var t = new Dictionary<string, double>
        {
            ["x"] = 100,
            ["y"] = 50,
            ["scale"] = 1.5,
            ["rotate"] = 45,
        };
        var result = TransformComposer.Build(t);

        Assert.Contains("translate(100px,50px)", result);
        Assert.Contains("scale(1.5)", result);
        Assert.Contains("rotate(45deg)", result);
        // Order: translate → scale → rotate
        Assert.True(result.IndexOf("translate") < result.IndexOf("scale"));
        Assert.True(result.IndexOf("scale") < result.IndexOf("rotate"));
    }
}
