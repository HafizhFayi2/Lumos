using System.Text.Json;
using Xunit;
using Lumos.MCP.Tools;

namespace Lumos.Tests.MCP;

public class McpToolHelpersTests
{
    // ── Response builders ─────────────────────────────────────────────────

    [Fact]
    public void Ok_NoArgs_ReturnsOkTrue()
    {
        var result = McpToolHelpers.Ok();
        using var doc = JsonDocument.Parse(result);
        Assert.True(doc.RootElement.GetProperty("ok").GetBoolean());
    }

    [Fact]
    public void Ok_WithPayload_ReturnsData()
    {
        var payload = new { key = "value", count = 42 };
        var result = McpToolHelpers.Ok(payload);
        using var doc = JsonDocument.Parse(result);
        Assert.True(doc.RootElement.GetProperty("ok").GetBoolean());
        var data = doc.RootElement.GetProperty("data");
        Assert.Equal("value", data.GetProperty("key").GetString());
        Assert.Equal(42, data.GetProperty("count").GetInt32());
    }

    [Fact]
    public void Error_ReturnsErrorWithMessage()
    {
        var result = McpToolHelpers.Error("something went wrong");
        using var doc = JsonDocument.Parse(result);
        Assert.False(doc.RootElement.GetProperty("ok").GetBoolean());
        Assert.Equal("something went wrong", doc.RootElement.GetProperty("error").GetString());
    }

    [Fact]
    public void Error_EmptyMessage_ReturnsEmptyError()
    {
        var result = McpToolHelpers.Error("");
        using var doc = JsonDocument.Parse(result);
        Assert.False(doc.RootElement.GetProperty("ok").GetBoolean());
        Assert.Equal("", doc.RootElement.GetProperty("error").GetString());
    }

    // ── TryGetString ─────────────────────────────────────────────────────

    [Fact]
    public void TryGetString_PresentKey_ReturnsTrueAndValue()
    {
        var el = JsonDocument.Parse("{\"name\":\"hello\"}").RootElement;
        Assert.True(McpToolHelpers.TryGetString(el, "name", out var value));
        Assert.Equal("hello", value);
    }

    [Fact]
    public void TryGetString_MissingKey_ReturnsFalseAndNull()
    {
        var el = JsonDocument.Parse("{\"other\":1}").RootElement;
        Assert.False(McpToolHelpers.TryGetString(el, "name", out var value));
        Assert.Null(value);
    }

    [Fact]
    public void TryGetString_WrongType_ReturnsFalse()
    {
        var el = JsonDocument.Parse("{\"name\":42}").RootElement;
        Assert.False(McpToolHelpers.TryGetString(el, "name", out var value));
        Assert.Null(value);
    }

    [Fact]
    public void TryGetString_NullValue_ReturnsFalse()
    {
        // JSON null is not a string kind, so TryGetString returns false
        var el = JsonDocument.Parse("{\"name\":null}").RootElement;
        Assert.False(McpToolHelpers.TryGetString(el, "name", out var value));
        Assert.Null(value);
    }

    // ── TryGetInt ────────────────────────────────────────────────────────

    [Fact]
    public void TryGetInt_PresentKey_ReturnsTrueAndValue()
    {
        var el = JsonDocument.Parse("{\"count\":99}").RootElement;
        Assert.True(McpToolHelpers.TryGetInt(el, "count", out var value));
        Assert.Equal(99, value);
    }

    [Fact]
    public void TryGetInt_MissingKey_ReturnsFalseAndZero()
    {
        var el = JsonDocument.Parse("{}").RootElement;
        Assert.False(McpToolHelpers.TryGetInt(el, "count", out var value));
        Assert.Equal(0, value);
    }

    [Fact]
    public void TryGetInt_WrongType_ReturnsFalse()
    {
        var el = JsonDocument.Parse("{\"count\":\"notanumber\"}").RootElement;
        Assert.False(McpToolHelpers.TryGetInt(el, "count", out var value));
        Assert.Equal(0, value);
    }

    [Fact]
    public void TryGetInt_FloatValue_ReturnsFalse()
    {
        var el = JsonDocument.Parse("{\"count\":3.14}").RootElement;
        Assert.False(McpToolHelpers.TryGetInt(el, "count", out var value));
        Assert.Equal(0, value);
    }

    // ── TryGetDouble ─────────────────────────────────────────────────────

    [Fact]
    public void TryGetDouble_PresentKey_ReturnsTrueAndValue()
    {
        var el = JsonDocument.Parse("{\"threshold\":-40.5}").RootElement;
        Assert.True(McpToolHelpers.TryGetDouble(el, "threshold", out var value));
        Assert.Equal(-40.5, value);
    }

    [Fact]
    public void TryGetDouble_MissingKey_ReturnsFalseAndZero()
    {
        var el = JsonDocument.Parse("{}").RootElement;
        Assert.False(McpToolHelpers.TryGetDouble(el, "threshold", out var value));
        Assert.Equal(0, value);
    }

    [Fact]
    public void TryGetDouble_IntegerValue_ReturnsTrue()
    {
        var el = JsonDocument.Parse("{\"threshold\":30}").RootElement;
        Assert.True(McpToolHelpers.TryGetDouble(el, "threshold", out var value));
        Assert.Equal(30.0, value);
    }

    // ── TryGetStringArray ────────────────────────────────────────────────

    [Fact]
    public void TryGetStringArray_PresentArray_ReturnsTrueAndValues()
    {
        var el = JsonDocument.Parse("{\"ids\":[\"a\",\"b\",\"c\"]}").RootElement;
        Assert.True(McpToolHelpers.TryGetStringArray(el, "ids", out var values));
        Assert.NotNull(values);
        Assert.Equal(3, values.Count);
        Assert.Equal("a", values[0]);
        Assert.Equal("b", values[1]);
        Assert.Equal("c", values[2]);
    }

    [Fact]
    public void TryGetStringArray_MissingKey_ReturnsFalseAndNull()
    {
        var el = JsonDocument.Parse("{}").RootElement;
        Assert.False(McpToolHelpers.TryGetStringArray(el, "ids", out var values));
        Assert.Null(values);
    }

    [Fact]
    public void TryGetStringArray_WrongType_ReturnsFalse()
    {
        var el = JsonDocument.Parse("{\"ids\":\"notarray\"}").RootElement;
        Assert.False(McpToolHelpers.TryGetStringArray(el, "ids", out var values));
        Assert.Null(values);
    }

    [Fact]
    public void TryGetStringArray_EmptyArray_ReturnsTrueAndEmptyList()
    {
        var el = JsonDocument.Parse("{\"ids\":[]}").RootElement;
        Assert.True(McpToolHelpers.TryGetStringArray(el, "ids", out var values));
        Assert.NotNull(values);
        Assert.Empty(values);
    }

    [Fact]
    public void TryGetStringArray_MixedTypes_SkipsNonStrings()
    {
        var el = JsonDocument.Parse("{\"ids\":[\"a\",42,false,\"b\"]}").RootElement;
        Assert.True(McpToolHelpers.TryGetStringArray(el, "ids", out var values));
        Assert.NotNull(values);
        Assert.Equal(2, values.Count);
        Assert.Equal("a", values[0]);
        Assert.Equal("b", values[1]);
    }

    // ── ValidateClipId ───────────────────────────────────────────────────

    [Fact]
    public void ValidateClipId_ValidId_ReturnsNull()
    {
        var args = JsonDocument.Parse("{\"clip_id\":\"clip123\"}").RootElement;
        var err = McpToolHelpers.ValidateClipId(args, out var clipId);
        Assert.Null(err);
        Assert.Equal("clip123", clipId);
    }

    [Fact]
    public void ValidateClipId_MissingKey_ReturnsError()
    {
        var args = JsonDocument.Parse("{}").RootElement;
        var err = McpToolHelpers.ValidateClipId(args, out var clipId);
        Assert.NotNull(err);
        Assert.Contains("clip_id", err);
        Assert.Null(clipId);
    }

    [Fact]
    public void ValidateClipId_EmptyString_ReturnsError()
    {
        var args = JsonDocument.Parse("{\"clip_id\":\"\"}").RootElement;
        var err = McpToolHelpers.ValidateClipId(args, out var clipId);
        Assert.NotNull(err);
        Assert.Contains("clip_id", err);
    }

    [Fact]
    public void ValidateClipId_WhitespaceString_ReturnsError()
    {
        var args = JsonDocument.Parse("{\"clip_id\":\"   \"}").RootElement;
        var err = McpToolHelpers.ValidateClipId(args, out var clipId);
        Assert.NotNull(err);
        Assert.Contains("clip_id", err);
    }

    [Fact]
    public void ValidateClipId_WrongType_ReturnsError()
    {
        var args = JsonDocument.Parse("{\"clip_id\":123}").RootElement;
        var err = McpToolHelpers.ValidateClipId(args, out var clipId);
        Assert.NotNull(err);
        Assert.Contains("clip_id", err);
        Assert.Null(clipId);
    }

    // ── ValidateNonEmptyClipIds ──────────────────────────────────────────

    [Fact]
    public void ValidateNonEmptyClipIds_ValidArray_ReturnsNull()
    {
        var args = JsonDocument.Parse("{\"clip_ids\":[\"a\",\"b\"]}").RootElement;
        var err = McpToolHelpers.ValidateNonEmptyClipIds(args, out var ids);
        Assert.Null(err);
        Assert.NotNull(ids);
        Assert.Equal(2, ids.Count);
    }

    [Fact]
    public void ValidateNonEmptyClipIds_MissingKey_ReturnsError()
    {
        var args = JsonDocument.Parse("{}").RootElement;
        var err = McpToolHelpers.ValidateNonEmptyClipIds(args, out var ids);
        Assert.NotNull(err);
        Assert.Contains("clip_ids", err);
        Assert.Null(ids);
    }

    [Fact]
    public void ValidateNonEmptyClipIds_EmptyArray_ReturnsError()
    {
        var args = JsonDocument.Parse("{\"clip_ids\":[]}").RootElement;
        var err = McpToolHelpers.ValidateNonEmptyClipIds(args, out var ids);
        Assert.NotNull(err);
        Assert.Contains("clip_ids", err);
        Assert.NotNull(ids);
        Assert.Empty(ids);
    }

    [Fact]
    public void ValidateNonEmptyClipIds_WrongType_ReturnsError()
    {
        var args = JsonDocument.Parse("{\"clip_ids\":\"notarray\"}").RootElement;
        var err = McpToolHelpers.ValidateNonEmptyClipIds(args, out var ids);
        Assert.NotNull(err);
        Assert.Contains("clip_ids", err);
        Assert.Null(ids);
    }

    // ── ValidateFrameRange ───────────────────────────────────────────────

    [Fact]
    public void ValidateFrameRange_ValidFrame_ReturnsNull()
    {
        var err = McpToolHelpers.ValidateFrameRange(50, 0, 100);
        Assert.Null(err);
    }

    [Fact]
    public void ValidateFrameRange_BelowMin_ReturnsError()
    {
        var err = McpToolHelpers.ValidateFrameRange(-5, 0, 100);
        Assert.NotNull(err);
        Assert.Contains("frame", err);
        Assert.Contains("-5", err);
    }

    [Fact]
    public void ValidateFrameRange_AboveMax_ReturnsError()
    {
        var err = McpToolHelpers.ValidateFrameRange(200, 0, 100);
        Assert.NotNull(err);
        Assert.Contains("frame", err);
        Assert.Contains("200", err);
        Assert.Contains("100", err);
    }

    [Fact]
    public void ValidateFrameRange_AtMinBoundary_ReturnsNull()
    {
        var err = McpToolHelpers.ValidateFrameRange(0, 0, 100);
        Assert.Null(err);
    }

    [Fact]
    public void ValidateFrameRange_AtMaxBoundary_ReturnsNull()
    {
        var err = McpToolHelpers.ValidateFrameRange(100, 0, 100);
        Assert.Null(err);
    }

    [Fact]
    public void ValidateFrameRange_NoUpperBound_DoesNotCheckMax()
    {
        var err = McpToolHelpers.ValidateFrameRange(999999, 0, 0);
        Assert.Null(err);
    }

    [Fact]
    public void ValidateFrameRange_CustomParamName_UsesProvidedName()
    {
        var err = McpToolHelpers.ValidateFrameRange(-1, 0, 100, "position");
        Assert.NotNull(err);
        Assert.Contains("position", err);
        Assert.DoesNotContain("frame", err);
    }

    // ── ValidateEffectType ───────────────────────────────────────────────

    [Theory]
    [InlineData("color_grade")]
    [InlineData("chroma_key")]
    [InlineData("clarity")]
    [InlineData("glow")]
    [InlineData("grain")]
    [InlineData("grade_curves")]
    [InlineData("highlights_shadows")]
    [InlineData("lut_tetra")]
    [InlineData("levels")]
    [InlineData("color_wheels")]
    [InlineData("vignette")]
    public void ValidateEffectType_ValidTypes_ReturnsNull(string effectType)
    {
        var err = McpToolHelpers.ValidateEffectType(effectType);
        Assert.Null(err);
    }

    [Fact]
    public void ValidateEffectType_InvalidType_ReturnsError()
    {
        var err = McpToolHelpers.ValidateEffectType("unknown_effect");
        Assert.NotNull(err);
        Assert.Contains("effect_type", err);
        Assert.Contains("unknown_effect", err);
    }

    [Fact]
    public void ValidateEffectType_EmptyString_ReturnsError()
    {
        var err = McpToolHelpers.ValidateEffectType("");
        Assert.NotNull(err);
    }

    [Fact]
    public void ValidateEffectType_CaseSensitive_FailsOnWrongCase()
    {
        var err = McpToolHelpers.ValidateEffectType("Color_Grade");
        Assert.NotNull(err);
    }
}
