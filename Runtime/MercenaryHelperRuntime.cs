using System;
using MercenaryHelper.Runtime.Lifecycle;
using MercenaryHelper.Runtime.State;

namespace MercenaryHelper.Runtime;

internal sealed class MercenaryHelperRuntime
{
    private readonly Main _plugin;

    public MercenaryHelperRuntime(Main plugin)
    {
        _plugin = plugin ?? throw new ArgumentNullException(nameof(plugin));
        State = new MercenaryHelperRuntimeState();
        AreaTransitions = new AreaTransitionCoordinator(State);
    }

    public MercenaryHelperRuntimeState State { get; }

    public AreaTransitionCoordinator AreaTransitions { get; }

    public void Initialize(DateTime now, MainSettingsBindingTargets bindingTargets)
    {
        MainSettingsBindings.Bind(_plugin.Settings, bindingTargets);
    }

    public void Shutdown()
    {
    }
}
