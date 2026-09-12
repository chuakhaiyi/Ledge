namespace Ledge.Core.Tests;

using Xunit;
using FluentAssertions;
using Ledge.Core.Models;

public class SettingsTests
{
    [Fact]
    public void DockEdge_Supports_Left_Right_Top()
    {
        var values = Enum.GetValues<DockEdge>();
        values.Should().Contain([DockEdge.Left, DockEdge.Right, DockEdge.Top]);
    }

    [Fact]
    public void DockMonitorId_is_optional_so_primary_fallback_can_be_runtime_only()
    {
        var settings = Settings.Default;
        settings.DockMonitorId.Should().BeNull();
    }

    [Fact]
    public void DefaultSettings_Has_RightEdge()
    {
        var settings = Settings.Default;
        settings.DockEdge.Should().Be(DockEdge.Right);
    }
}
