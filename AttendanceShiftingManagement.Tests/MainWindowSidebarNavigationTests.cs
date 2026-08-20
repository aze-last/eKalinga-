namespace AttendanceShiftingManagement.Tests;

public sealed class MainWindowSidebarNavigationTests
{
    [Fact]
    public void MainWindow_BindsCollapsibleSideNavigationMarkup()
    {
        var pagePath = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory,
            "..",
            "..",
            "..",
            "..",
            "Views",
            "MainWindow.xaml"));

        var xaml = File.ReadAllText(pagePath);

        // Sidebar Dock & Toggle
        Assert.Contains("Width=\"{Binding SidebarWidth}\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Command=\"{Binding ToggleSidebarCommand}\"", xaml, StringComparison.Ordinal);
        Assert.Contains("ToolTip=\"{Binding SidebarToggleTooltip}\"", xaml, StringComparison.Ordinal);

        // Module Links
        Assert.Contains("Command=\"{Binding ShowDashboardCommand}\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Command=\"{Binding ShowMasterListCommand}\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Command=\"{Binding ShowBudgetCommand}\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Command=\"{Binding ShowDistributionCommand}\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Command=\"{Binding ShowCashForWorkCommand}\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Command=\"{Binding ShowSeminarAttendanceCommand}\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Command=\"{Binding ShowAssistanceCasesCommand}\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Command=\"{Binding ShowGgmsTransactionsCommand}\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Command=\"{Binding ShowReportsCommand}\"", xaml, StringComparison.Ordinal);

        // Visibility and Profile bindings
        Assert.Contains("Text=\"{Binding UserDisplayName}\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Text=\"{Binding UserRoleLabel}\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Click=\"Settings_Click\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Click=\"Logout_Click\"", xaml, StringComparison.Ordinal);
    }

    [Fact]
    public void BarangayMainViewModel_SidebarCollapseTogglesCorrectly()
    {
        Exception? threadException = null;

        var thread = new Thread(() =>
        {
            try
            {
                var dummyUser = new AttendanceShiftingManagement.Models.User
                {
                    Id = 1,
                    Username = "admin",
                    Role = AttendanceShiftingManagement.Models.UserRole.Admin
                };

                var vm = new AttendanceShiftingManagement.ViewModels.BarangayMainViewModel(dummyUser);

                // Initially expanded
                Assert.False(vm.IsSidebarCollapsed);
                Assert.Equal(240.0, vm.SidebarWidth);
                Assert.Contains("Collapse", vm.SidebarToggleTooltip);

                // Toggle to Collapsed
                vm.ToggleSidebarCommand.Execute(null);
                Assert.True(vm.IsSidebarCollapsed);
                Assert.Equal(68.0, vm.SidebarWidth);
                Assert.Contains("Expand", vm.SidebarToggleTooltip);

                // Toggle back to Expanded
                vm.ToggleSidebarCommand.Execute(null);
                Assert.False(vm.IsSidebarCollapsed);
                Assert.Equal(240.0, vm.SidebarWidth);
            }
            catch (Exception ex)
            {
                threadException = ex;
            }
        });

        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();

        if (threadException != null)
        {
            throw new Exception("STA test execution failed.", threadException);
        }
    }
}
