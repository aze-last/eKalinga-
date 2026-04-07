using AttendanceShiftingManagement.Models;
using AttendanceShiftingManagement.Services;
using AttendanceShiftingManagement.ViewModels;

namespace AttendanceShiftingManagement.Tests;

public sealed class BeneficiaryVerificationViewModelTests
{
    [Fact]
    public async Task LoadCurrentPageAsync_LoadsBenefitsReceivedForSelectedBeneficiary()
    {
        var queueService = new FakeBeneficiaryVerificationQueueService();
        var stagingRow = new BeneficiaryStaging
        {
            StagingID = 101,
            BeneficiaryId = "BEN-0101",
            CivilRegistryId = "CRS-0101",
            FirstName = "Elena",
            LastName = "Rivera",
            FullName = "Elena Rivera",
            VerificationStatus = VerificationStatus.Approved,
            ImportedAt = new DateTime(2026, 3, 21, 8, 0, 0)
        };

        queueService.Enqueue(new BeneficiaryVerificationQueuePageResult
        {
            Rows =
            [
                new BeneficiaryVerificationQueueRow
                {
                    Staging = stagingRow,
                    DigitalId = new BeneficiaryDigitalId
                    {
                        BeneficiaryStagingId = stagingRow.StagingID,
                        CardNumber = "BID-000101",
                        QrPayload = "ASM-BID|000101|ABC123",
                        IssuedAt = new DateTime(2026, 3, 22, 9, 30, 0),
                        IsActive = true
                    }
                }
            ],
            PageNumber = 1,
            FilteredRecordCount = 1,
            TotalCount = 1,
            ApprovedCount = 1
        });

        var requestedStagingIds = new List<int>();
        var benefitHistory = (IReadOnlyList<BeneficiaryAssistanceLedgerEntry>)
        [
            new BeneficiaryAssistanceLedgerEntry
            {
                SourceModule = BeneficiaryAssistanceSourceModule.CashForWork,
                ReleaseDate = new DateTime(2026, 3, 21),
                Amount = 800m,
                Remarks = "Cash-for-work payout"
            },
            new BeneficiaryAssistanceLedgerEntry
            {
                SourceModule = BeneficiaryAssistanceSourceModule.AssistanceCase,
                ReleaseDate = new DateTime(2026, 3, 20),
                Amount = 1200m,
                Remarks = "Food assistance"
            }
        ];

        var viewModel = new BeneficiaryVerificationViewModel(
            new User { Id = 1, Username = "admin" },
            queueService,
            autoLoad: false,
            autoRefresh: false,
            benefitHistoryLoader: stagingId =>
            {
                requestedStagingIds.Add(stagingId);
                return Task.FromResult(benefitHistory);
            });

        await viewModel.LoadCurrentPageAsync();
        await WaitForConditionAsync(() => viewModel.BenefitsReceived.Count == 2);

        Assert.Equal([stagingRow.StagingID], requestedStagingIds);
        Assert.Equal("BID-000101", viewModel.DigitalIdCardNumber);
        Assert.Equal(2, viewModel.BenefitsReceived.Count);
        Assert.Contains("2 release(s) recorded", viewModel.BenefitsReceivedSummary, StringComparison.Ordinal);
        Assert.Contains("2,000.00", viewModel.BenefitsReceivedSummary, StringComparison.Ordinal);
        Assert.Contains("March 21, 2026", viewModel.BenefitsReceivedLatestReleaseText, StringComparison.Ordinal);
        Assert.Equal("Cash For Work", viewModel.BenefitsReceived[0].SourceLabel);
        Assert.Equal("800.00", viewModel.BenefitsReceived[0].AmountText);
        Assert.Equal("Cash-for-work payout", viewModel.BenefitsReceived[0].Remarks);
    }

    private static async Task WaitForConditionAsync(Func<bool> condition)
    {
        for (var attempt = 0; attempt < 20; attempt++)
        {
            if (condition())
            {
                return;
            }

            await Task.Delay(25);
        }

        Assert.True(condition(), "Timed out waiting for the expected viewmodel state.");
    }

    private sealed class FakeBeneficiaryVerificationQueueService : IBeneficiaryVerificationQueueService
    {
        private readonly Queue<BeneficiaryVerificationQueuePageResult> _results = new();

        public void Enqueue(BeneficiaryVerificationQueuePageResult result)
        {
            _results.Enqueue(result);
        }

        public Task<BeneficiaryVerificationQueuePageResult> LoadPageAsync(
            BeneficiaryVerificationQueuePageRequest request,
            CancellationToken cancellationToken = default)
        {
            if (_results.Count == 0)
            {
                throw new InvalidOperationException("No fake beneficiary verification result was queued.");
            }

            return Task.FromResult(_results.Dequeue());
        }
    }
}
