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
    public void DashboardPage_ContainsBudgetModuleTile()
    {
        var pagePath = GetProjectFilePath("Views", "BarangayDashboardPage.xaml");

        var xaml = File.ReadAllText(pagePath);

        Assert.Contains("Text=\"BUDGET\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Command=\"{Binding DataContext.ShowBudgetCommand, RelativeSource={RelativeSource AncestorType={x:Type Window}}}\"", xaml, StringComparison.Ordinal);
    }
}
