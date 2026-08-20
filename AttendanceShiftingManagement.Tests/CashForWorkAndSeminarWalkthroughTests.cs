namespace AttendanceShiftingManagement.Tests;

public sealed class CashForWorkAndSeminarWalkthroughTests
{
    [Fact]
    public void CashForWorkOcrPage_WalkthroughMarkupAndBindingVerification()
    {
        var pagePath = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory,
            "..",
            "..",
            "..",
            "..",
            "Views",
            "CashForWorkOcrPage.xaml"));

        var xaml = File.ReadAllText(pagePath);

        // Header Walkthrough Button
        Assert.Contains("Command=\"{Binding OpenOnboardingCommand}\"", xaml, StringComparison.Ordinal);
        Assert.Contains("WALKTHROUGH", xaml, StringComparison.Ordinal);

        // Target Named UI Elements
        Assert.Contains("x:Name=\"SidebarContainer\"", xaml, StringComparison.Ordinal);
        Assert.Contains("x:Name=\"LiveScannerDockBorder\"", xaml, StringComparison.Ordinal);
        Assert.Contains("x:Name=\"AttendanceGridBorder\"", xaml, StringComparison.Ordinal);
        Assert.Contains("x:Name=\"EventHeaderActions\"", xaml, StringComparison.Ordinal);

        // Spotlight & Instruction Card Controls
        Assert.Contains("x:Name=\"SpotlightOverlayGrid\"", xaml, StringComparison.Ordinal);
        Assert.Contains("x:Name=\"SpotlightMaskPath\"", xaml, StringComparison.Ordinal);
        Assert.Contains("x:Name=\"SpotlightBorder\"", xaml, StringComparison.Ordinal);
        Assert.Contains("x:Name=\"InstructionCard\"", xaml, StringComparison.Ordinal);

        // Title and Step Bindings
        Assert.Contains("Text=\"{Binding OnboardingTitle}\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Text=\"{Binding OnboardingInstruction}\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Text=\"{Binding OnboardingActionHint}\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Command=\"{Binding NextOnboardingStepCommand}\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Command=\"{Binding PreviousOnboardingStepCommand}\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Command=\"{Binding CloseOnboardingCommand}\"", xaml, StringComparison.Ordinal);
    }

    [Fact]
    public void SeminarAttendancePage_WalkthroughMarkupAndBindingVerification()
    {
        var pagePath = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory,
            "..",
            "..",
            "..",
            "..",
            "Views",
            "SeminarAttendancePage.xaml"));

        var xaml = File.ReadAllText(pagePath);

        // Header Walkthrough Button
        Assert.Contains("Command=\"{Binding OpenOnboardingCommand}\"", xaml, StringComparison.Ordinal);
        Assert.Contains("WALKTHROUGH", xaml, StringComparison.Ordinal);

        // Target Named UI Elements
        Assert.Contains("x:Name=\"SidebarContainer\"", xaml, StringComparison.Ordinal);
        Assert.Contains("x:Name=\"LiveScannerDockBorder\"", xaml, StringComparison.Ordinal);
        Assert.Contains("x:Name=\"AttendanceGridBorder\"", xaml, StringComparison.Ordinal);
        Assert.Contains("x:Name=\"EventHeaderActions\"", xaml, StringComparison.Ordinal);

        // Spotlight & Instruction Card Controls
        Assert.Contains("x:Name=\"SpotlightOverlayGrid\"", xaml, StringComparison.Ordinal);
        Assert.Contains("x:Name=\"SpotlightMaskPath\"", xaml, StringComparison.Ordinal);
        Assert.Contains("x:Name=\"SpotlightBorder\"", xaml, StringComparison.Ordinal);
        Assert.Contains("x:Name=\"InstructionCard\"", xaml, StringComparison.Ordinal);

        // Title and Step Bindings
        Assert.Contains("Text=\"{Binding OnboardingTitle}\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Text=\"{Binding OnboardingInstruction}\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Text=\"{Binding OnboardingActionHint}\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Command=\"{Binding NextOnboardingStepCommand}\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Command=\"{Binding PreviousOnboardingStepCommand}\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Command=\"{Binding CloseOnboardingCommand}\"", xaml, StringComparison.Ordinal);
    }

    [Fact]
    public void CashForWorkViewModel_WalkthroughWorkflowAndStepProgression()
    {
        var dummyUser = new AttendanceShiftingManagement.Models.User { Username = "admin", Role = AttendanceShiftingManagement.Models.UserRole.Admin };
        var vm = new AttendanceShiftingManagement.ViewModels.CashForWorkOcrViewModel(dummyUser);

        Assert.False(vm.IsOnboardingOpen);

        // Open Tour
        vm.OpenOnboardingCommand.Execute(null);
        Assert.True(vm.IsOnboardingOpen);
        Assert.Equal(1, vm.OnboardingStep);
        Assert.Equal("SidebarContainer", vm.OnboardingTargetName);
        Assert.Contains("Active Event", vm.OnboardingTitle);

        // Step 2
        vm.NextOnboardingStepCommand.Execute(null);
        Assert.Equal(2, vm.OnboardingStep);
        Assert.Equal("LiveScannerDockBorder", vm.OnboardingTargetName);
        Assert.Contains("Digital ID", vm.OnboardingTitle);

        // Step 3
        vm.NextOnboardingStepCommand.Execute(null);
        Assert.Equal(3, vm.OnboardingStep);
        Assert.Equal("AttendanceGridBorder", vm.OnboardingTargetName);
        Assert.Contains("Worker Roster", vm.OnboardingTitle);

        // Step 4
        vm.NextOnboardingStepCommand.Execute(null);
        Assert.Equal(4, vm.OnboardingStep);
        Assert.Equal("EventHeaderActions", vm.OnboardingTargetName);
        Assert.Contains("Wage Calculation", vm.OnboardingTitle);

        // Finish Tour on Step 4 next
        vm.NextOnboardingStepCommand.Execute(null);
        Assert.False(vm.IsOnboardingOpen);

        // Direct Step selection
        vm.OpenOnboardingCommand.Execute(null);
        vm.SetOnboardingStepCommand.Execute(3);
        Assert.Equal(3, vm.OnboardingStep);
        vm.PreviousOnboardingStepCommand.Execute(null);
        Assert.Equal(2, vm.OnboardingStep);

        // Close Tour
        vm.CloseOnboardingCommand.Execute(null);
        Assert.False(vm.IsOnboardingOpen);
    }

    [Fact]
    public void SeminarViewModel_WalkthroughWorkflowAndStepProgression()
    {
        var dummyUser = new AttendanceShiftingManagement.Models.User { Username = "admin", Role = AttendanceShiftingManagement.Models.UserRole.Admin };
        var vm = new AttendanceShiftingManagement.ViewModels.SeminarAttendanceViewModel(dummyUser);

        Assert.False(vm.IsOnboardingOpen);

        // Open Tour
        vm.OpenOnboardingCommand.Execute(null);
        Assert.True(vm.IsOnboardingOpen);
        Assert.Equal(1, vm.OnboardingStep);
        Assert.Equal("SidebarContainer", vm.OnboardingTargetName);
        Assert.Contains("Active Seminar", vm.OnboardingTitle);

        // Step 2
        vm.NextOnboardingStepCommand.Execute(null);
        Assert.Equal(2, vm.OnboardingStep);
        Assert.Equal("LiveScannerDockBorder", vm.OnboardingTargetName);
        Assert.Contains("Attendee Scanner", vm.OnboardingTitle);

        // Step 3
        vm.NextOnboardingStepCommand.Execute(null);
        Assert.Equal(3, vm.OnboardingStep);
        Assert.Equal("AttendanceGridBorder", vm.OnboardingTargetName);
        Assert.Contains("Attendee Roster", vm.OnboardingTitle);

        // Step 4
        vm.NextOnboardingStepCommand.Execute(null);
        Assert.Equal(4, vm.OnboardingStep);
        Assert.Equal("EventHeaderActions", vm.OnboardingTargetName);
        Assert.Contains("Training Allowance", vm.OnboardingTitle);

        // Finish Tour on Step 4 next
        vm.NextOnboardingStepCommand.Execute(null);
        Assert.False(vm.IsOnboardingOpen);

        // Direct Step selection
        vm.OpenOnboardingCommand.Execute(null);
        vm.SetOnboardingStepCommand.Execute(2);
        Assert.Equal(2, vm.OnboardingStep);
        vm.PreviousOnboardingStepCommand.Execute(null);
        Assert.Equal(1, vm.OnboardingStep);

        // Close Tour
        vm.CloseOnboardingCommand.Execute(null);
        Assert.False(vm.IsOnboardingOpen);
    }
}
