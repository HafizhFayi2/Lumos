namespace Lumos.AI;

/// Supported Anthropic Claude models. Mirrors upstream AnthropicModel Swift enum.
public enum AnthropicModel
{
    Sonnet46,
    Opus48,
    Haiku45,
}

public static class AnthropicModelExtensions
{
    public static string ToApiString(this AnthropicModel model) => model switch
    {
        AnthropicModel.Sonnet46 => "claude-sonnet-4-6",
        AnthropicModel.Opus48   => "claude-opus-4-8",
        AnthropicModel.Haiku45  => "claude-haiku-4-5-20251001",
        _                       => "claude-sonnet-4-6",
    };

    public static string DisplayName(this AnthropicModel model) => model switch
    {
        AnthropicModel.Sonnet46 => "Sonnet 4.6",
        AnthropicModel.Opus48   => "Opus 4.8",
        AnthropicModel.Haiku45  => "Haiku 4.5",
        _                       => "Sonnet 4.6",
    };
}
