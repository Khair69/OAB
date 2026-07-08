using Microsoft.Extensions.DependencyInjection;

namespace Oab.App.Modules;

/// <summary>
/// A self-contained feature a shop can have or not have. The shell knows
/// nothing about concrete features; it hosts whatever modules the customer
/// head project passes to UseOab. Custom per-shop features implement this in
/// the customer folder and get promoted to shared modules if a second shop
/// wants them.
/// </summary>
public interface IOabModule
{
    /// <summary>Stable identifier, used in logs and diagnostics.</summary>
    string Name { get; }

    /// <summary>Register the module's pages, view models and services.</summary>
    void ConfigureServices(IServiceCollection services);

    /// <summary>Top-level entries this module adds to the shell's flyout menu.</summary>
    IEnumerable<OabNavItem> GetNavItems();

    /// <summary>Register non-flyout navigation routes (detail/editor pages).</summary>
    void RegisterRoutes() { }
}

/// <summary>One flyout menu entry. Title is a Strings.resx key, not display text.</summary>
public record OabNavItem(string TitleKey, string Route, Type PageType);
