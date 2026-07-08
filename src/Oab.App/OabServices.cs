namespace Oab.App;

/// <summary>
/// Escape hatch for places MAUI doesn't constructor-inject (pages pushed from
/// code-behind, prompts). Set once at startup by OabApp.
/// </summary>
public static class OabServices
{
    public static IServiceProvider? Provider { get; internal set; }

    public static T Get<T>() where T : notnull =>
        (Provider ?? throw new InvalidOperationException("OabServices used before OabApp started."))
        .GetRequiredService<T>();
}
