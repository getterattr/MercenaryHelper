using System;

namespace MercenaryHelper.Runtime;

internal sealed record MainSettingsBindingTargets(
    Action DrawOverviewPanel,
    Action DrawConfigurationHeader,
    Action DrawPricingHeader,
    Action DrawArchetypesHeader,
    Action DrawQualityFilterHeader,
    Action DrawPricingStatusPanel,
    Action OnRefreshPricesPressed,
    Action<string> DrawArchetypeTableForAttribute,
    Action DrawQualityRulesPanel);

internal static class MainSettingsBindings
{
    public static void Bind(Settings settings, MainSettingsBindingTargets targets)
    {
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(targets);

        settings.OverviewPanel.DrawDelegate = targets.DrawOverviewPanel;
        settings.ConfigurationHeader.DrawDelegate = targets.DrawConfigurationHeader;
        settings.PricingHeader.DrawDelegate = targets.DrawPricingHeader;
        settings.ArchetypesHeader.DrawDelegate = targets.DrawArchetypesHeader;
        settings.QualityFilterHeader.DrawDelegate = targets.DrawQualityFilterHeader;
        settings.QualityFilter.RulesPanel.DrawDelegate = targets.DrawQualityRulesPanel;
        settings.Pricing.StatusPanel.DrawDelegate = targets.DrawPricingStatusPanel;
        settings.Pricing.RefreshPricesAction.OnPressed = targets.OnRefreshPricesPressed;

        var arch = settings.Archetypes;
        arch.Str.Table.DrawDelegate = () => targets.DrawArchetypeTableForAttribute("Str");
        arch.Dex.Table.DrawDelegate = () => targets.DrawArchetypeTableForAttribute("Dex");
        arch.Int.Table.DrawDelegate = () => targets.DrawArchetypeTableForAttribute("Int");
        arch.StrDex.Table.DrawDelegate = () => targets.DrawArchetypeTableForAttribute("Str/Dex");
        arch.StrInt.Table.DrawDelegate = () => targets.DrawArchetypeTableForAttribute("Str/Int");
        arch.DexInt.Table.DrawDelegate = () => targets.DrawArchetypeTableForAttribute("Dex/Int");
        arch.StrDexInt.Table.DrawDelegate = () => targets.DrawArchetypeTableForAttribute("Str/Dex/Int");
    }
}
