using Oab.App.Localization;
using Oab.App.Modules;

namespace Oab.App;

/// <summary>
/// The whole app chrome, built at runtime from whichever modules the customer
/// project registered. No feature is hard-coded here: the flyout is exactly
/// the sum of the modules' nav items, plus the language switcher.
/// </summary>
public class OabShell : Shell
{
    public OabShell(
        IReadOnlyList<IOabModule> modules,
        LocalizationManager localization,
        ShopConfig config,
        IServiceProvider services)
    {
        Title = config.ShopName;
        FlyoutBehavior = FlyoutBehavior.Flyout;
        SetBinding(FlowDirectionProperty,
            new Binding(nameof(LocalizationManager.FlowDirection), source: localization));

        FlyoutHeader = new Label
        {
            Text = config.ShopName,
            FontSize = 24,
            FontAttributes = FontAttributes.Bold,
            Padding = new Thickness(20, 40, 20, 20),
        };

        foreach (var module in modules)
        {
            foreach (var nav in module.GetNavItems())
            {
                var content = new ShellContent
                {
                    Route = nav.Route,
                    ContentTemplate = new DataTemplate(() => (Page)services.GetRequiredService(nav.PageType)),
                };
                content.SetBinding(TitleProperty, new Binding($"[{nav.TitleKey}]", source: localization));

                var flyoutItem = new FlyoutItem();
                flyoutItem.SetBinding(TitleProperty, new Binding($"[{nav.TitleKey}]", source: localization));
                flyoutItem.Items.Add(content);
                Items.Add(flyoutItem);
            }
        }

        var languageButton = new Button { Margin = new Thickness(12) };
        languageButton.SetBinding(Button.TextProperty, new Binding("[Common_Language]", source: localization));
        languageButton.Clicked += (_, _) => localization.CycleCulture();
        FlyoutFooter = languageButton;
    }
}
