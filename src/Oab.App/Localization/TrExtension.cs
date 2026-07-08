namespace Oab.App.Localization;

/// <summary>
/// XAML markup extension for localized, shop-overridable text:
///   &lt;Label Text="{oab:Tr Purchases_Title}" /&gt;
/// Produces a live binding into LocalizationManager, so text updates in place
/// when the user switches language.
/// </summary>
[ContentProperty(nameof(Key))]
public class TrExtension : IMarkupExtension<BindingBase>
{
    public string Key { get; set; } = "";

    public BindingBase ProvideValue(IServiceProvider serviceProvider) =>
        new Binding($"[{Key}]", BindingMode.OneWay, source: LocalizationManager.Current);

    object IMarkupExtension.ProvideValue(IServiceProvider serviceProvider) =>
        ProvideValue(serviceProvider);
}
