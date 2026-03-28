namespace AttendanceShiftingManagement.Tests;

public sealed class BeneficiaryApprovalFlowSourceTests
{
    [Fact]
    public void BeneficiaryVerificationViewModel_UsesPagedLoadingAndRefreshOnlySnapshotSync()
    {
        var viewModelPath = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory,
            "..",
            "..",
            "..",
            "..",
            "ViewModels",
            "BeneficiaryVerificationViewModel.cs"));

        var source = File.ReadAllText(viewModelPath);

        Assert.Contains("SyncPendingFromValidatedSnapshotAsync", source, StringComparison.Ordinal);
        Assert.Contains("CrsBeneficiaryImportService.ImportPendingAsync", source, StringComparison.Ordinal);
        Assert.Contains("new BeneficiaryVerificationQueueService()", source, StringComparison.Ordinal);
        Assert.Contains("_ = LoadPageAsync(1, null, syncValidatedSnapshot: false, CancellationToken.None);", source, StringComparison.Ordinal);
        Assert.Contains("return LoadPageAsync(CurrentPage, SelectedBeneficiary?.StagingId, syncValidatedSnapshot: true, cancellationToken);", source, StringComparison.Ordinal);
        Assert.Contains("return LoadPageAsync(CurrentPage + 1, null, syncValidatedSnapshot: false, cancellationToken);", source, StringComparison.Ordinal);
    }

    [Fact]
    public void AssistanceCaseManagementViewModel_LoadsApprovedBeneficiariesFromLocalApprovalState()
    {
        var viewModelPath = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory,
            "..",
            "..",
            "..",
            "..",
            "ViewModels",
            "AssistanceCaseManagementViewModel.cs"));

        var source = File.ReadAllText(viewModelPath);

        Assert.Contains("context.BeneficiaryStaging", source, StringComparison.Ordinal);
        Assert.Contains("VerificationStatus == VerificationStatus.Approved", source, StringComparison.Ordinal);
        Assert.Contains("FromApprovedStaging", source, StringComparison.Ordinal);
        Assert.DoesNotContain("new MasterListService()", source, StringComparison.Ordinal);
    }

    [Fact]
    public void BeneficiaryApprovalFlow_PassesEditableCorrectionsIntoApproveAction()
    {
        var viewModelPath = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory,
            "..",
            "..",
            "..",
            "..",
            "ViewModels",
            "BeneficiaryVerificationViewModel.cs"));
        var servicePath = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory,
            "..",
            "..",
            "..",
            "..",
            "Services",
            "BeneficiaryVerificationService.cs"));

        var viewModelSource = File.ReadAllText(viewModelPath);
        var serviceSource = File.ReadAllText(servicePath);

        Assert.Contains("new BeneficiaryCorrectionRequest(", viewModelSource, StringComparison.Ordinal);
        Assert.Contains("Corrections:", viewModelSource, StringComparison.Ordinal);
        Assert.Contains("request.Corrections", serviceSource, StringComparison.Ordinal);
    }
}
