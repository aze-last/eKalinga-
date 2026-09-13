using System;
using System.Collections.Generic;
using System.IO;

namespace AttendanceShiftingManagement.Tests
{
    /// <summary>
    /// Text-scan tests enforcing the Cedula Verification Contract (Sept 2026):
    /// eTaxCollect-owned tables stay read-only, the check is informational and
    /// can never gate a transaction, and the legacy local table is untouched.
    /// </summary>
    public sealed class CedulaVerificationSourceTests
    {
        private static string ReadSource(params string[] relativeParts)
        {
            var parts = new List<string> { AppContext.BaseDirectory, "..", "..", "..", ".." };
            parts.AddRange(relativeParts);
            return File.ReadAllText(Path.GetFullPath(Path.Combine(parts.ToArray())));
        }

        [Fact]
        public void CedulaGatewayQuery_MatchesContract_AndStaysReadOnly()
        {
            var source = ReadSource("Services", "CRS", "CrsGateway.cs");

            Assert.Contains("GetValidCedulaAsync", source, StringComparison.Ordinal);
            Assert.Contains("tax_transactions", source, StringComparison.Ordinal);
            Assert.Contains("cedula_details", source, StringComparison.Ordinal);
            Assert.Contains("year_covered", source, StringComparison.Ordinal);
            Assert.Contains("PAID", source, StringComparison.Ordinal);
            Assert.Contains("is_deleted", source, StringComparison.Ordinal);
            Assert.Contains("type_code = 'CEDULA'", source, StringComparison.Ordinal);

            Assert.DoesNotContain("INSERT INTO tax_transactions", source, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("UPDATE tax_transactions", source, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("DELETE FROM tax_transactions", source, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("INSERT INTO cedula_details", source, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("UPDATE cedula_details", source, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("DELETE FROM cedula_details", source, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public void CedulaGatewayContract_ExposedOnInterface()
        {
            var source = ReadSource("Services", "CRS", "ICrsGateway.cs");

            Assert.Contains("CrsCedulaVerificationRow", source, StringComparison.Ordinal);
            Assert.Contains("GetValidCedulaAsync", source, StringComparison.Ordinal);
        }

        [Fact]
        public void CedulaVerificationService_IsFailSoft_AndNeverThrows()
        {
            var source = ReadSource("Services", "CRS", "CedulaVerificationService.cs");

            Assert.Contains("CheckCurrentYearAsync", source, StringComparison.Ordinal);
            Assert.Contains("catch", source, StringComparison.Ordinal);
            Assert.DoesNotContain("throw", source, StringComparison.Ordinal);
        }

        [Fact]
        public void CedulaBadge_WiredToValidationPanel_AsInformationOnly()
        {
            var viewModel = ReadSource("ViewModels", "ProjectDistributionViewModel.cs");

            Assert.Contains("CedulaBadgeVisibility", viewModel, StringComparison.Ordinal);
            Assert.Contains("CedulaBadgeText", viewModel, StringComparison.Ordinal);
            Assert.Contains("CedulaVerificationService", viewModel, StringComparison.Ordinal);
            Assert.Contains("ConfirmAddRecipient", viewModel, StringComparison.Ordinal);

            var xaml = ReadSource("Views", "ProjectDistributionCreateProjectPanel.xaml");

            Assert.Contains("Visibility=\"{Binding CedulaBadgeVisibility}\"", xaml, StringComparison.Ordinal);
            Assert.Contains("Text=\"{Binding CedulaBadgeText}\"", xaml, StringComparison.Ordinal);
        }

        [Fact]
        public void LegacyLocalCedulaTable_LeftDormant_NotRemoved()
        {
            var viewModel = ReadSource("ViewModels", "ProjectDistributionViewModel.cs");

            Assert.Contains("CommunityTaxRows", viewModel, StringComparison.Ordinal);
            Assert.Contains("BeneficiaryCommunityTaxPayments", viewModel, StringComparison.Ordinal);
        }
    }
}
