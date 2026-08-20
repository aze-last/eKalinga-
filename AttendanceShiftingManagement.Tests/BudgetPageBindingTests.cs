namespace AttendanceShiftingManagement.Tests;

public sealed class BudgetPageBindingTests
{
    private static string GetProjectFilePath(params string[] relativeSegments)
    {
        var directory = AppContext.BaseDirectory;

        while (!string.IsNullOrWhiteSpace(directory))
        {
            if (File.Exists(Path.Combine(directory, "AttendanceShiftingManagement.csproj")))
            {
                return Path.Combine(new[] { directory }.Concat(relativeSegments).ToArray());
            }

            directory = Directory.GetParent(directory)?.FullName ?? string.Empty;
        }

        throw new DirectoryNotFoundException("Could not locate the AttendanceShiftingManagement project root.");
    }

    [Fact]
    public void BudgetPage_GlobalCapsUiIsRemoved()
    {
        var pagePath = GetProjectFilePath("Views", "BudgetPage.xaml");

        var xaml = File.ReadAllText(pagePath);

        Assert.DoesNotContain("GLOBAL AID CAPS", xaml, StringComparison.Ordinal);
        Assert.DoesNotContain("CASH-FOR-WORK CAPS", xaml, StringComparison.Ordinal);
        Assert.DoesNotContain("ALLOCATIONS (BUCKETS)", xaml, StringComparison.Ordinal);
        Assert.DoesNotContain("SYSTEM LIMIT CONFIGURATION", xaml, StringComparison.Ordinal);
        Assert.DoesNotContain("DISTRIBUTION &amp; CAP SETUP", xaml, StringComparison.Ordinal);
        Assert.DoesNotContain("OTP VERIFICATION", xaml, StringComparison.Ordinal);
        Assert.DoesNotContain("OpenAssistanceCaseBudgetsPanelCommand", xaml, StringComparison.Ordinal);
        Assert.DoesNotContain("OpenCashForWorkBudgetsPanelCommand", xaml, StringComparison.Ordinal);
        Assert.DoesNotContain("VerifyOtpCommand", xaml, StringComparison.Ordinal);
    }

    [Fact]
    public void BudgetPage_UsesCombinedCreateProjectModalBindings()
    {
        var pagePath = GetProjectFilePath("Views", "BudgetPage.xaml");

        var xaml = File.ReadAllText(pagePath);

        // Sidebar: RECORD DONATION replaced by CREATE PROJECT (gold), opens combined modal
        Assert.DoesNotContain("Content=\"RECORD DONATION\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Content=\"CREATE PROJECT\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Command=\"{Binding OpenNewDonationProjectCommand}\"", xaml, StringComparison.Ordinal);

        // Grid-row flow is retained
        Assert.Contains("OpenProjectCreationPanelCommand", xaml, StringComparison.Ordinal);
        Assert.Contains("Visibility=\"{Binding ProjectCreationPanelVisibility}\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Command=\"{Binding ConfirmCreateProjectCommand}\"", xaml, StringComparison.Ordinal);

        // Dual-mode funding source column driven by IsNewDonationMode
        Assert.Contains("Binding IsNewDonationMode, Converter={StaticResource BooleanToVisibilityConverter}", xaml, StringComparison.Ordinal);
        Assert.Contains("Binding IsNewDonationMode, Converter={StaticResource InverseBooleanToVisibilityConverter}", xaml, StringComparison.Ordinal);
        Assert.Contains("Text=\"{Binding NewProjectSourceDescription}\"", xaml, StringComparison.Ordinal);

        // Donation entry fields live inside the combined modal
        Assert.Contains("Text=\"{Binding DonorName, UpdateSourceTrigger=PropertyChanged}\"", xaml, StringComparison.Ordinal);
        Assert.Contains("SelectedDate=\"{Binding DonationDateReceived}\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Command=\"{Binding BrowseProofCommand}\"", xaml, StringComparison.Ordinal);

        // Widened modal
        Assert.Contains("Width=\"1100\"", xaml, StringComparison.Ordinal);
        Assert.DoesNotContain("Width=\"750\"", xaml, StringComparison.Ordinal);
    }

    [Fact]
    public void BudgetPage_HasBeneficiaryEnrollmentColumnBindings()
    {
        var pagePath = GetProjectFilePath("Views", "BudgetPage.xaml");

        var xaml = File.ReadAllText(pagePath);

        // Right column: cleared inline list, replaced by ADD BENEFICIARIES button + roster preview
        Assert.Contains("ENROLL BENEFICIARIES (OPTIONAL)", xaml, StringComparison.Ordinal);
        Assert.Contains("Content=\"ADD BENEFICIARIES\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Command=\"{Binding OpenBeneficiaryPickerCommand}\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Text=\"{Binding SelectedEnrollmentCount}\"", xaml, StringComparison.Ordinal);

        // Beneficiaries Form modal: client's dual-list design
        Assert.Contains("Text=\"BENEFICIARIES FORM\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Visibility=\"{Binding IsBeneficiaryPickerOpen, Converter={StaticResource BooleanToVisibilityConverter}}\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Text=\"Validated Residence\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Text=\"Beneficiaries for the Project\"", xaml, StringComparison.Ordinal);
        Assert.Contains("ItemsSource=\"{Binding FilteredEnrollmentBeneficiaries}\"", xaml, StringComparison.Ordinal);
        Assert.Contains("ItemsSource=\"{Binding SelectedEnrollmentBeneficiaries}\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Text=\"{Binding EnrollmentSearchText, UpdateSourceTrigger=PropertyChanged}\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Command=\"{Binding DataContext.RequestAddBeneficiaryCommand, RelativeSource={RelativeSource AncestorType=UserControl}}\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Command=\"{Binding DataContext.RemoveSelectedBeneficiaryCommand, RelativeSource={RelativeSource AncestorType=UserControl}}\"", xaml, StringComparison.Ordinal);

        // Household records confirmation modal shown before every add
        Assert.Contains("Text=\"HOUSEHOLD RECORDS REVIEW\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Visibility=\"{Binding IsHouseholdRecordsOpen, Converter={StaticResource BooleanToVisibilityConverter}}\"", xaml, StringComparison.Ordinal);
        Assert.Contains("ItemsSource=\"{Binding HouseholdRecordsMembers}\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Text=\"{Binding BenefitsReceivedText}\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Command=\"{Binding ConfirmAddBeneficiaryCommand}\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Command=\"{Binding CancelHouseholdRecordsCommand}\"", xaml, StringComparison.Ordinal);
    }

    [Fact]
    public void BudgetViewModel_EnrollmentUsesDistributionServiceAndApprovedFilter()
    {
        var vmPath = GetProjectFilePath("ViewModels", "BudgetViewModel.cs");

        var source = File.ReadAllText(vmPath);

        // Enrollment reuses the exact Distribution module service call
        Assert.Contains("BulkAddBeneficiariesAsync(", source, StringComparison.Ordinal);
        Assert.Contains("new ProjectDistributionService(", source, StringComparison.Ordinal);

        // Only Approved masterlist beneficiaries are offered
        Assert.Contains("VerificationStatus.Approved", source, StringComparison.Ordinal);

        // Donation + project stay linked 1:1
        Assert.Contains("NewProjectSourceDonationId", source, StringComparison.Ordinal);

        // Global caps / OTP plumbing is gone
        Assert.DoesNotContain("OtpChallengeSession", source, StringComparison.Ordinal);
        Assert.DoesNotContain("CreateAssistanceCaseBudgetCommand", source, StringComparison.Ordinal);
        Assert.DoesNotContain("CreateCashForWorkBudgetCommand", source, StringComparison.Ordinal);
    }

    [Fact]
    public void BudgetPage_HasWalkthroughOverlayAndButtonBindings()
    {
        var pagePath = GetProjectFilePath("Views", "BudgetPage.xaml");

        var xaml = File.ReadAllText(pagePath);

        // Header Walkthrough Launch Button
        Assert.Contains("Command=\"{Binding OpenOnboardingCommand}\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Text=\"WALKTHROUGH\"", xaml, StringComparison.Ordinal);

        // Workspace Blur separation: Bound to IsStandardModalOpen, NOT blurred by walkthrough
        Assert.Contains("BlurEffect Radius=\"{Binding IsStandardModalOpen", xaml, StringComparison.Ordinal);

        // Interactive Spotlight Layer
        Assert.Contains("x:Name=\"SpotlightOverlayGrid\"", xaml, StringComparison.Ordinal);
        Assert.Contains("x:Name=\"SpotlightMaskPath\"", xaml, StringComparison.Ordinal);
        Assert.Contains("x:Name=\"SpotlightBorder\"", xaml, StringComparison.Ordinal);
        Assert.Contains("x:Name=\"SpotlightCanvas\"", xaml, StringComparison.Ordinal);
        Assert.Contains("x:Name=\"InstructionCard\"", xaml, StringComparison.Ordinal);
        Assert.Contains("IsHitTestVisible=\"True\"", xaml, StringComparison.Ordinal);

        // Target UI Elements named for spotlight hit-testing & bounds
        Assert.Contains("x:Name=\"FinancialSummaryGrid\"", xaml, StringComparison.Ordinal);
        Assert.Contains("x:Name=\"SyncGgmsButton\"", xaml, StringComparison.Ordinal);
        Assert.Contains("x:Name=\"BudgetBrowserCard\"", xaml, StringComparison.Ordinal);
        Assert.Contains("x:Name=\"BudgetDataGrid\"", xaml, StringComparison.Ordinal);
        Assert.Contains("x:Name=\"CreateProjectButton\"", xaml, StringComparison.Ordinal);

        // Create Project Modal Guide and targets
        Assert.Contains("Command=\"{Binding OpenCreateProjectTourCommand}\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Text=\"FORM GUIDE\"", xaml, StringComparison.Ordinal);
        Assert.Contains("x:Name=\"ProjectBasicInfoSection\"", xaml, StringComparison.Ordinal);
        Assert.Contains("x:Name=\"ReleaseSettingsSection\"", xaml, StringComparison.Ordinal);
        Assert.Contains("x:Name=\"FundingSourceSection\"", xaml, StringComparison.Ordinal);
        Assert.Contains("x:Name=\"BeneficiariesEnrollmentSection\"", xaml, StringComparison.Ordinal);
        Assert.Contains("x:Name=\"ConfirmCreateProjectButton\"", xaml, StringComparison.Ordinal);
    }

    [Fact]
    public void BudgetViewModel_WalkthroughStepsAndProgression()
    {
        var vmPath = GetProjectFilePath("ViewModels", "BudgetViewModel.cs");

        var source = File.ReadAllText(vmPath);

        // Verify Tour Commands & Step Properties are implemented
        Assert.Contains("OpenOnboardingCommand", source, StringComparison.Ordinal);
        Assert.Contains("CloseOnboardingCommand", source, StringComparison.Ordinal);
        Assert.Contains("NextOnboardingStepCommand", source, StringComparison.Ordinal);
        Assert.Contains("PreviousOnboardingStepCommand", source, StringComparison.Ordinal);
        Assert.Contains("SetOnboardingStepCommand", source, StringComparison.Ordinal);
        Assert.Contains("OnboardingStep", source, StringComparison.Ordinal);
        Assert.Contains("OnboardingTitle", source, StringComparison.Ordinal);
        Assert.Contains("OnboardingInstruction", source, StringComparison.Ordinal);
        Assert.Contains("OnboardingActionHint", source, StringComparison.Ordinal);
        Assert.Contains("OnboardingTargetName", source, StringComparison.Ordinal);
        Assert.Contains("IsStandardModalOpen", source, StringComparison.Ordinal);

        // Step Targets
        Assert.Contains("FinancialSummaryGrid", source, StringComparison.Ordinal);
        Assert.Contains("SyncGgmsButton", source, StringComparison.Ordinal);
        Assert.Contains("BudgetBrowserCard", source, StringComparison.Ordinal);
        Assert.Contains("CreateProjectButton", source, StringComparison.Ordinal);
    }

    [Fact]
    public void BudgetViewModel_CreateProjectTourStepsAndProgression()
    {
        var vmPath = GetProjectFilePath("ViewModels", "BudgetViewModel.cs");

        var source = File.ReadAllText(vmPath);

        // Verify Create Project Tour Commands & Step Properties
        Assert.Contains("OpenCreateProjectTourCommand", source, StringComparison.Ordinal);
        Assert.Contains("CloseCreateProjectTourCommand", source, StringComparison.Ordinal);
        Assert.Contains("NextCreateProjectTourStepCommand", source, StringComparison.Ordinal);
        Assert.Contains("PreviousCreateProjectTourStepCommand", source, StringComparison.Ordinal);
        Assert.Contains("SetCreateProjectTourStepCommand", source, StringComparison.Ordinal);
        Assert.Contains("IsCreateProjectTourOpen", source, StringComparison.Ordinal);
        Assert.Contains("CreateProjectTourStep", source, StringComparison.Ordinal);
        Assert.Contains("IsAnyTourOpen", source, StringComparison.Ordinal);

        // Step Targets for Create Project
        Assert.Contains("ProjectBasicInfoSection", source, StringComparison.Ordinal);
        Assert.Contains("ReleaseSettingsSection", source, StringComparison.Ordinal);
        Assert.Contains("FundingSourceSection", source, StringComparison.Ordinal);
        Assert.Contains("BeneficiariesEnrollmentSection", source, StringComparison.Ordinal);
        Assert.Contains("ConfirmCreateProjectButton", source, StringComparison.Ordinal);
    }

    [Fact]
    public void DashboardPage_ContainsBudgetModuleTile()
    {
        var pagePath = GetProjectFilePath("Views", "BarangayDashboardPage.xaml");

        var xaml = File.ReadAllText(pagePath);

        Assert.Contains("Text=\"BUDGET\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Command=\"{Binding DataContext.ShowBudgetCommand, RelativeSource={RelativeSource AncestorType={x:Type Window}}}\"", xaml, StringComparison.Ordinal);
    }
}
