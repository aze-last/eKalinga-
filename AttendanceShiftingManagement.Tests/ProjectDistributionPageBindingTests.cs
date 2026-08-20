namespace AttendanceShiftingManagement.Tests;

public sealed class ProjectDistributionPageBindingTests
{
    [Fact]
    public void ProjectDistributionPage_BindsPaginatedThreeColumnManualDistributionLayout()
    {
        var pagePath = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory,
            "..",
            "..",
            "..",
            "..",
            "Views",
            "ProjectDistributionPage.xaml"));

        var xaml = File.ReadAllText(pagePath);

        Assert.Contains("Text=\"DISTRIBUTION FORM\"", xaml, StringComparison.Ordinal);
        Assert.Contains("ItemsSource=\"{Binding PendingBeneficiaries}\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Text=\"{Binding PendingPaginationText}\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Command=\"{Binding PrevPendingPageCommand}\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Command=\"{Binding NextPendingPageCommand}\"", xaml, StringComparison.Ordinal);

        Assert.Contains("Text=\"{Binding SelectedPendingBeneficiary.FullName}\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Command=\"{Binding ConfirmReleaseCommand}\"", xaml, StringComparison.Ordinal);

        Assert.Contains("Text=\"RELEASED / CLAIMED\"", xaml, StringComparison.Ordinal);
        Assert.Contains("ItemsSource=\"{Binding ReleasedClaims}\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Text=\"{Binding ReleasedPaginationText}\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Command=\"{Binding PrevReleasedPageCommand}\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Command=\"{Binding NextReleasedPageCommand}\"", xaml, StringComparison.Ordinal);

        // Third status bucket (client's reference layout): held-back / not yet claimed beneficiaries.
        Assert.Contains("Text=\"UNRELEASED / UNCLAIMED\"", xaml, StringComparison.Ordinal);
        Assert.Contains("ItemsSource=\"{Binding RejectedBeneficiaries}\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Text=\"{Binding RejectedPaginationText}\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Command=\"{Binding PrevRejectedPageCommand}\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Command=\"{Binding NextRejectedPageCommand}\"", xaml, StringComparison.Ordinal);
    }

    [Fact]
    public void ProjectDistributionPage_ScannedProfileModalShowsReferenceFormFields()
    {
        var pagePath = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory,
            "..",
            "..",
            "..",
            "..",
            "Views",
            "ProjectDistributionPage.xaml"));

        var xaml = File.ReadAllText(pagePath);

        // Beneficiaries Profile Form modal: demographics, household identity, and allocated amount.
        Assert.Contains("Text=\"BENEFICIARIES PROFILE FORM\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Text=\"{Binding ScannedBeneficiaryAddress}\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Text=\"{Binding ScannedBeneficiaryAge}\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Text=\"{Binding ScannedBeneficiaryGender}\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Text=\"{Binding ScannedHouseholdNumber}\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Text=\"{Binding ScannedHouseholdRole}\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Text=\"{Binding ScannedAllocatedAmountText}\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Text=\"HOUSEHOLD RELATIONSHIP\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Command=\"{Binding ConfirmScannedClaimCommand}\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Command=\"{Binding CancelScannedClaimCommand}\"", xaml, StringComparison.Ordinal);
    }

    [Fact]
    public void ProjectDistributionPage_HouseholdReviewModalShowsBeneficiaryPhotoWithDefaultIconFallback()
    {
        var pagePath = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory,
            "..",
            "..",
            "..",
            "..",
            "Views",
            "ProjectDistributionPage.xaml"));

        var xaml = File.ReadAllText(pagePath);

        // The Household Review modal identifies the beneficiary with their photo when one is on
        // file, and a default profile icon otherwise.
        Assert.Contains("ImageSource=\"{Binding HouseholdConfirmBeneficiaryPhoto}\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Visibility=\"{Binding HouseholdConfirmBeneficiaryPhoto, Converter={StaticResource NullToHiddenConverter}}\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Visibility=\"{Binding HouseholdConfirmBeneficiaryPhoto, Converter={StaticResource NullToHiddenConverter}, ConverterParameter=Inverse}\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Text=\"{Binding HouseholdConfirmBeneficiaryName}\"", xaml, StringComparison.Ordinal);
    }

    [Fact]
    public void ProjectDistributionPage_ReleaseModalGatesOnRequirementsChecklist()
    {
        var pagePath = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory,
            "..",
            "..",
            "..",
            "..",
            "Views",
            "ProjectDistributionPage.xaml"));

        var xaml = File.ReadAllText(pagePath);

        // New release flow: attachments (cedula, barangay certificate, ...) are reviewed inside the
        // release modal; missing items keep the beneficiary in UNRELEASED / UNCLAIMED.
        Assert.Contains("Text=\"REQUIREMENTS CHECKLIST\"", xaml, StringComparison.Ordinal);
        Assert.Contains("ItemsSource=\"{Binding ReleaseRequirementRows}\"", xaml, StringComparison.Ordinal);
        Assert.Contains("IsChecked=\"{Binding IsComplete, UpdateSourceTrigger=PropertyChanged}\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Visibility=\"{Binding HasMissingReleaseRequirements, Converter={StaticResource BooleanToVisibilityConverter}}\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Text=\"{Binding ReleaseRequirementsSummaryText}\"", xaml, StringComparison.Ordinal);
    }

    [Fact]
    public void ProjectDistributionPage_WalkthroughElementsAndBindings()
    {
        var pagePath = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory,
            "..",
            "..",
            "..",
            "..",
            "Views",
            "ProjectDistributionPage.xaml"));

        var xaml = File.ReadAllText(pagePath);

        // Header walkthrough trigger
        Assert.Contains("Command=\"{Binding OpenOnboardingCommand}\"", xaml, StringComparison.Ordinal);
        Assert.Contains("WALKTHROUGH", xaml, StringComparison.Ordinal);

        // Named targets
        Assert.Contains("x:Name=\"ProjectContextBar\"", xaml, StringComparison.Ordinal);
        Assert.Contains("x:Name=\"ScannerSearchBarSection\"", xaml, StringComparison.Ordinal);
        Assert.Contains("x:Name=\"SwipeIdCardButton\"", xaml, StringComparison.Ordinal);
        Assert.Contains("x:Name=\"DistributionColumnsGrid\"", xaml, StringComparison.Ordinal);
        Assert.Contains("x:Name=\"ReleasedClaimsCard\"", xaml, StringComparison.Ordinal);
        Assert.Contains("x:Name=\"PendingClaimsCard\"", xaml, StringComparison.Ordinal);
        Assert.Contains("x:Name=\"UnreleasedClaimsCard\"", xaml, StringComparison.Ordinal);

        // Spotlight and card controls
        Assert.Contains("x:Name=\"SpotlightOverlayGrid\"", xaml, StringComparison.Ordinal);
        Assert.Contains("x:Name=\"SpotlightMaskPath\"", xaml, StringComparison.Ordinal);
        Assert.Contains("x:Name=\"SpotlightBorder\"", xaml, StringComparison.Ordinal);
        Assert.Contains("x:Name=\"InstructionCard\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Text=\"{Binding OnboardingTitle}\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Text=\"{Binding OnboardingInstruction}\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Text=\"{Binding OnboardingActionHint}\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Command=\"{Binding NextOnboardingStepCommand}\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Command=\"{Binding PreviousOnboardingStepCommand}\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Command=\"{Binding CloseOnboardingCommand}\"", xaml, StringComparison.Ordinal);
    }

    [Fact]
    public void ProjectDistributionViewModel_OnboardingStepsAndProgression()
    {
        var vm = new AttendanceShiftingManagement.ViewModels.ProjectDistributionViewModel(new AttendanceShiftingManagement.Models.User
        {
            Id = 1,
            Username = "admin",
            Email = "admin@ekalinga.gov",
            Role = AttendanceShiftingManagement.Models.UserRole.Admin
        });

        Assert.False(vm.IsOnboardingOpen);

        vm.OpenOnboardingCommand.Execute(null);
        Assert.True(vm.IsOnboardingOpen);
        Assert.Equal(1, vm.OnboardingStep);
        Assert.Equal("Active Project & Fund Context", vm.OnboardingTitle);
        Assert.Equal("ProjectContextBar", vm.OnboardingTargetName);

        vm.NextOnboardingStepCommand.Execute(null);
        Assert.Equal(2, vm.OnboardingStep);
        Assert.Equal("Digital ID Scanner & Search", vm.OnboardingTitle);
        Assert.Equal("ScannerSearchBarSection", vm.OnboardingTargetName);

        vm.NextOnboardingStepCommand.Execute(null);
        Assert.Equal(3, vm.OnboardingStep);
        Assert.Equal("Distribution Queue & Status", vm.OnboardingTitle);
        Assert.Equal("DistributionColumnsGrid", vm.OnboardingTargetName);

        vm.NextOnboardingStepCommand.Execute(null);
        Assert.Equal(4, vm.OnboardingStep);
        Assert.Equal("Verification & Disbursement", vm.OnboardingTitle);
        Assert.Equal("PendingClaimsCard", vm.OnboardingTargetName);

        vm.PreviousOnboardingStepCommand.Execute(null);
        Assert.Equal(3, vm.OnboardingStep);

        vm.SetOnboardingStepCommand.Execute(4);
        Assert.Equal(4, vm.OnboardingStep);

        // Next from step 4 finishes/closes the onboarding
        vm.NextOnboardingStepCommand.Execute(null);
        Assert.False(vm.IsOnboardingOpen);
    }

    [Fact]
    public void ProjectDistributionPage_ScanErrorModalElementsAndBindings()
    {
        var pagePath = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "Views", "ProjectDistributionPage.xaml");
        var xaml = File.ReadAllText(pagePath);

        Assert.Contains("x:Name=\"ScanDiagnosticErrorModalGrid\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Visibility=\"{Binding IsScanErrorModalVisible, Converter={StaticResource BooleanToVisibilityConverter}}\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Text=\"{Binding ScanErrorModalTitle", xaml, StringComparison.Ordinal);
        Assert.Contains("Text=\"{Binding ScanErrorModalMessage}\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Text=\"{Binding ScanErrorModalRawPayload", xaml, StringComparison.Ordinal);
        Assert.Contains("Text=\"{Binding ScanErrorModalReason}\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Text=\"{Binding ScanErrorModalSuggestedAction}\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Command=\"{Binding CloseScanErrorModalCommand}\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Command=\"{Binding RetryScanCommand}\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Command=\"{Binding SearchManuallyFromErrorCommand}\"", xaml, StringComparison.Ordinal);
    }

    [Fact]
    public void ProjectDistributionViewModel_ScanErrorModalWorkflow()
    {
        var vm = new AttendanceShiftingManagement.ViewModels.ProjectDistributionViewModel(new AttendanceShiftingManagement.Models.User
        {
            Id = 1,
            Username = "admin",
            Email = "admin@ekalinga.gov",
            Role = AttendanceShiftingManagement.Models.UserRole.Admin
        });

        Assert.False(vm.IsScanErrorModalVisible);

        vm.ShowScanErrorModal(
            "Beneficiary ID Not Found",
            "Scanned ID 'BEN-99999' was not found.",
            "BEN-99999",
            "No record exists in database.",
            "Verify physical card.",
            "NotFound");

        Assert.True(vm.IsScanErrorModalVisible);
        Assert.True(vm.IsStandardModalOpen);
        Assert.True(vm.IsAnyOverlayOpen);
        Assert.Equal("Beneficiary ID Not Found", vm.ScanErrorModalTitle);
        Assert.Equal("BEN-99999", vm.ScanErrorModalRawPayload);

        // Search manually command copies payload to search text and closes modal
        vm.SearchManuallyFromErrorCommand.Execute(null);
        Assert.False(vm.IsScanErrorModalVisible);
        Assert.Equal("BEN-99999", vm.BeneficiarySearchText);

        // Test retry command closes modal
        vm.ShowScanErrorModal("Error", "Message", "XYZ", "Reason", "Action");
        Assert.True(vm.IsScanErrorModalVisible);
        vm.RetryScanCommand.Execute(null);
        Assert.False(vm.IsScanErrorModalVisible);
    }
}
