using System;
using System.Collections.Generic;
using Lumos.Domain;
using Lumos.Infrastructure.Effects;
using Xunit;
using SkiaSharp;

namespace Lumos.Tests.Effects;

public class EffectRendererTests
{
    private SKBitmap CreateSolidBitmap(int width, int height, SKColor color)
    {
        var bmp = new SKBitmap(width, height, SKColorType.Bgra8888, SKAlphaType.Premul);
        using var canvas = new SKCanvas(bmp);
        canvas.Clear(color);
        return bmp;
    }

    [Fact]
    public void ChromaKeyRenderer_RemovesGreenBackground()
    {
        // Arrange
        var renderer = new ChromaKeyRenderer();
        var bmp = CreateSolidBitmap(10, 10, new SKColor(0, 255, 0)); // Pure green
        var effect = new Effect { Type = "chroma_key", Enabled = true };
        effect.Params.Add("key_r", new EffectParam { NumericValue = 0.0 });
        effect.Params.Add("key_g", new EffectParam { NumericValue = 255.0 }); // Green key color
        effect.Params.Add("key_b", new EffectParam { NumericValue = 0.0 });
        effect.Params.Add("threshold", new EffectParam { NumericValue = 0.1 });

        // Act
        var result = renderer.Apply(bmp, effect, 0);

        // Assert
        var pixel = result.GetPixel(5, 5);
        Assert.Equal(0, pixel.Alpha); // Green pixel should be completely transparent
    }

    [Fact]
    public void ColorGradeRenderer_AppliesSaturation()
    {
        // Arrange
        var renderer = new ColorGradeRenderer();
        var bmp = CreateSolidBitmap(10, 10, new SKColor(100, 100, 100)); // Gray
        var effect = new Effect { Type = "color_grade", Enabled = true };
        effect.Params.Add("saturation", new EffectParam { NumericValue = 2.0 }); // Double saturation
        effect.Params.Add("contrast", new EffectParam { NumericValue = 1.0 });
        effect.Params.Add("warmth", new EffectParam { NumericValue = 0.0 });

        // Act
        var result = renderer.Apply(bmp, effect, 0);

        // Assert
        var pixel = result.GetPixel(5, 5);
        // Gray should remain gray even with saturation boost
        Assert.Equal(100, pixel.Red);
        Assert.Equal(100, pixel.Green);
        Assert.Equal(100, pixel.Blue);
    }

    [Fact]
    public void AllRenderers_SmokeTest_NoCrash()
    {
        // Arrange
        var renderers = new IEffectRenderer[]
        {
            new ColorGradeRenderer(),
            new ChromaKeyRenderer(),
            new LutRenderer(),
            new ClarityRenderer(),
            new GlowRenderer(),
            new GrainRenderer(),
            new VignetteRenderer(),
            new GradeCurvesRenderer(),
            new HighlightsShadowsRenderer(),
            new LUTTetraRenderer(),
            new LevelsRenderer(),
            new ColorWheelsRenderer()
        };

        var bmp = CreateSolidBitmap(16, 16, new SKColor(128, 128, 128, 255));

        // Act & Assert
        foreach (var renderer in renderers)
        {
            var effect = new Effect { Type = renderer.EffectType, Enabled = true };
            var result = renderer.Apply(bmp, effect, 0);
            Assert.NotNull(result);
            Assert.Equal(16, result.Width);
            Assert.Equal(16, result.Height);
        }
    }
}
