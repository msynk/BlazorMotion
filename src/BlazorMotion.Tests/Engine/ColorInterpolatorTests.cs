using BlazorMotion.Engine;

namespace BlazorMotion.Tests.Engine;

public class ColorInterpolatorTests
{
    // ── LooksLikeColor ────────────────────────────────────────────────────────

    [Theory]
    [InlineData("#ff0000", true)]
    [InlineData("#fff", true)]
    [InlineData("rgb(255,0,0)", true)]
    [InlineData("rgba(255,0,0,1)", true)]
    [InlineData("hsl(0,100%,50%)", true)]
    [InlineData("red", false)]
    [InlineData("transparent", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void LooksLikeColor_ReturnsExpected(string? value, bool expected)
    {
        Assert.Equal(expected, ColorInterpolator.LooksLikeColor(value));
    }

    // ── Lerp — boundary conditions ────────────────────────────────────────────

    [Fact]
    public void Lerp_AtT0_ReturnsFromColor()
    {
        Assert.Equal("rgba(0,0,0,1)", ColorInterpolator.Lerp("#000000", "#ffffff", 0.0));
    }

    [Fact]
    public void Lerp_AtT1_ReturnsToColor()
    {
        Assert.Equal("rgba(255,255,255,1)", ColorInterpolator.Lerp("#000000", "#ffffff", 1.0));
    }

    [Fact]
    public void Lerp_AtMidpoint_InterpolatesChannels()
    {
        Assert.Equal("rgba(128,128,128,1)", ColorInterpolator.Lerp("#000000", "#ffffff", 0.5));
    }

    // ── Hex format parsing ────────────────────────────────────────────────────

    [Fact]
    public void Lerp_ShorthandHex_Expands()
    {
        Assert.Equal("rgba(128,128,128,1)", ColorInterpolator.Lerp("#000", "#fff", 0.5));
    }

    [Fact]
    public void Lerp_ShorthandHexWithAlpha_ParsesAlpha()
    {
        // #000f → [0,0,0,alpha=1.0]; #fff0 → [255,255,255,alpha=0.0]
        var result = ColorInterpolator.Lerp("#000f", "#fff0", 0.5);
        Assert.Equal("rgba(128,128,128,0.5)", result);
    }

    [Fact]
    public void Lerp_FullHex_MixesChannels()
    {
        // red + blue at 0.5 → rgba(128,0,128,1)
        Assert.Equal("rgba(128,0,128,1)", ColorInterpolator.Lerp("#ff0000", "#0000ff", 0.5));
    }

    // ── rgb/rgba format parsing ───────────────────────────────────────────────

    [Fact]
    public void Lerp_RgbFormat_AtT1_ReturnsToColor()
    {
        Assert.Equal("rgba(100,200,100,1)", ColorInterpolator.Lerp("rgb(0,0,0)", "rgb(100,200,100)", 1.0));
    }

    [Fact]
    public void Lerp_RgbaFormat_InterpolatesAlpha()
    {
        Assert.Equal("rgba(0,0,0,0.5)", ColorInterpolator.Lerp("rgba(0,0,0,0)", "rgba(0,0,0,1)", 0.5));
    }

    // ── Invalid input ─────────────────────────────────────────────────────────

    [Fact]
    public void Lerp_UnparsableFrom_ReturnsFallbackToValue()
    {
        // When 'from' can't be parsed, returns the raw 'to' string unchanged
        Assert.Equal("#ff0000", ColorInterpolator.Lerp("notacolor", "#ff0000", 0.5));
    }

    [Fact]
    public void Lerp_UnparsableTo_ReturnsFallbackToValue()
    {
        Assert.Equal("notacolor", ColorInterpolator.Lerp("#ff0000", "notacolor", 0.5));
    }
}
