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

        Assert.Contains("Text=\"DESTRIBUTION FORM\"", xaml, StringComparison.Ordinal);
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
}
