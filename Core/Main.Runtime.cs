using MercenaryHelper.Runtime;
using MercenaryHelper.Runtime.Features;

namespace MercenaryHelper;

public partial class Main
{
    private MercenaryHelperRuntime _runtime;
    private MercenaryWorldLabelService _worldLabelService;
    private MercenaryPanelInspectorService _panelInspectorService;
    private ArchetypeCatalog _archetypeCatalog;
    private UniquePriceCatalog _uniquePriceCatalog;

    private MercenaryHelperRuntime Runtime => _runtime ??= new MercenaryHelperRuntime(this);

    private MercenaryWorldLabelService WorldLabels => _worldLabelService ??= new MercenaryWorldLabelService(this);

    private MercenaryPanelInspectorService PanelInspector => _panelInspectorService ??= new MercenaryPanelInspectorService(this);

    internal ArchetypeCatalog Archetypes => _archetypeCatalog ??= new ArchetypeCatalog();

    internal UniquePriceCatalog UniquePrices => _uniquePriceCatalog ??= new UniquePriceCatalog();

    private bool _isCurrentAreaTrackable
    {
        get => Runtime.State.Map.IsCurrentAreaTrackable;
        set => Runtime.State.Map.IsCurrentAreaTrackable = value;
    }

    private string _activeMapAreaHash
    {
        get => Runtime.State.Map.ActiveMapAreaHash;
        set => Runtime.State.Map.ActiveMapAreaHash = value ?? string.Empty;
    }

    private string _activeMapAreaName
    {
        get => Runtime.State.Map.ActiveMapAreaName;
        set => Runtime.State.Map.ActiveMapAreaName = value ?? string.Empty;
    }

    private int _activeMapInstanceId
    {
        get => Runtime.State.Map.ActiveMapInstanceId;
        set => Runtime.State.Map.ActiveMapInstanceId = value;
    }

    private MainSettingsBindingTargets CreateSettingsBindingTargets()
    {
        return new MainSettingsBindingTargets(
            DrawOverviewPanel,
            () => DrawSectionHeader("Configuration"),
            () => DrawSectionHeader("Pricing"),
            () => DrawSectionHeader("Archetypes"),
            DrawPricingStatusPanel,
            OnRefreshPricesPressed,
            DrawArchetypeTableForAttribute);
    }
}
