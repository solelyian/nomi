using Microsoft.UI.Xaml.Automation.Peers;
using Microsoft.UI.Xaml.Controls;

namespace Nomi;

public sealed class Region : Grid
{
    protected override AutomationPeer OnCreateAutomationPeer() => new RegionAutomationPeer(this);

    private sealed class RegionAutomationPeer(Region owner) : FrameworkElementAutomationPeer(owner)
    {
        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Group;

        protected override string GetClassNameCore() => nameof(Region);

        protected override bool IsControlElementCore() => true;

        protected override bool IsContentElementCore() => true;
    }
}
