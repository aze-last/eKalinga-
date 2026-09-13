using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using AttendanceShiftingManagement.Services;
using Xunit;

namespace AttendanceShiftingManagement.Tests
{
    public sealed class CedulaVerificationServiceTests
    {
        private sealed class FakeCedulaGateway : ICrsGateway
        {
            public CrsCedulaVerificationRow? CedulaRow { get; set; }
            public bool ThrowOnCedula { get; set; }
            public int CedulaCallCount;
            public string? LastBeneficiaryId;
            public int LastYear;

            public Task<CrsCedulaVerificationRow?> GetValidCedulaAsync(string beneficiaryId, int year, CancellationToken cancellationToken)
            {
                Interlocked.Increment(ref CedulaCallCount);
                LastBeneficiaryId = beneficiaryId;
                LastYear = year;
                if (ThrowOnCedula) throw new InvalidOperationException("CRS unreachable");
                return Task.FromResult(CedulaRow);
            }

            public Task<CrsDigitalIdRow?> GetLatestDigitalIdRowAsync(string beneficiaryId, CancellationToken cancellationToken)
                => Task.FromResult<CrsDigitalIdRow?>(null);
            public Task<long?> GetDemographicCharacteristicIdAsync(string beneficiaryId, CancellationToken cancellationToken)
                => Task.FromResult<long?>(null);
            public Task<DateTime?> GetPhotoUpdatedAtAsync(long demographicCharacteristicId, CancellationToken cancellationToken)
                => Task.FromResult<DateTime?>(null);
            public Task<CrsPhotoRow?> GetPhotoAsync(long demographicCharacteristicId, CancellationToken cancellationToken)
                => Task.FromResult<CrsPhotoRow?>(null);
            public Task InsertAccessLogAsync(CrsAccessLogEntry entry, CancellationToken cancellationToken)
                => Task.CompletedTask;
            public Task<CrsSchemaProbeResult> ProbeSchemaAsync(CancellationToken cancellationToken)
                => Task.FromResult(new CrsSchemaProbeResult(true, null));
            public Task<IReadOnlyList<CrsValBeneficiaryRow>> GetAllValidatedBeneficiariesAsync(CancellationToken cancellationToken)
                => Task.FromResult<IReadOnlyList<CrsValBeneficiaryRow>>(Array.Empty<CrsValBeneficiaryRow>());
            public Task<IReadOnlyList<CrsDigitalIdListRow>> GetAllLatestDigitalIdRowsAsync(CancellationToken cancellationToken)
                => Task.FromResult<IReadOnlyList<CrsDigitalIdListRow>>(Array.Empty<CrsDigitalIdListRow>());
            public Task<IReadOnlyList<CrsDemographicRow>> GetAllDemographicCharacteristicsAsync(CancellationToken cancellationToken)
                => Task.FromResult<IReadOnlyList<CrsDemographicRow>>(Array.Empty<CrsDemographicRow>());
        }

        [Fact]
        public async Task CheckCurrentYear_ReturnsValidCedula_WithContractDetails()
        {
            var gateway = new FakeCedulaGateway
            {
                CedulaRow = new CrsCedulaVerificationRow("CTC-2026-001", new DateTime(2026, 1, 5), 2026)
            };
            var service = new CedulaVerificationService(gateway);

            var result = await service.CheckCurrentYearAsync("BEN-2026-0001");

            Assert.True(result.HasValidCedula);
            Assert.Equal("CTC-2026-001", result.ReferenceNumber);
            Assert.Equal(new DateTime(2026, 1, 5), result.PaymentDate);
            Assert.Equal(2026, result.YearCovered);
            Assert.Equal("BEN-2026-0001", gateway.LastBeneficiaryId);
            Assert.Equal(DateTime.Now.Year, gateway.LastYear);
        }

        [Fact]
        public async Task CheckCurrentYear_NoRow_ReturnsNeutralMiss()
        {
            var gateway = new FakeCedulaGateway { CedulaRow = null };
            var service = new CedulaVerificationService(gateway);

            var result = await service.CheckCurrentYearAsync("BEN-2026-0001");

            Assert.False(result.HasValidCedula);
            Assert.Null(result.ReferenceNumber);
        }

        [Fact]
        public async Task CheckCurrentYear_GatewayFailure_NeverBlocks_ReturnsMiss()
        {
            var gateway = new FakeCedulaGateway { ThrowOnCedula = true };
            var service = new CedulaVerificationService(gateway);

            // Must not throw: a slow/failed check is never a failed transaction.
            var result = await service.CheckCurrentYearAsync("BEN-2026-0001");

            Assert.False(result.HasValidCedula);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public async Task CheckCurrentYear_BlankId_SkipsGatewayQuery(string? beneficiaryId)
        {
            var gateway = new FakeCedulaGateway();
            var service = new CedulaVerificationService(gateway);

            var result = await service.CheckCurrentYearAsync(beneficiaryId);

            Assert.False(result.HasValidCedula);
            Assert.Equal(0, gateway.CedulaCallCount);
        }
    }
}
