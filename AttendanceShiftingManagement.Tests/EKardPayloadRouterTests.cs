using AttendanceShiftingManagement.Services;
using Xunit;

namespace AttendanceShiftingManagement.Tests
{
    public class EKardPayloadRouterTests
    {
        [Theory]
        [InlineData("BEN-000123")]
        [InlineData("ben-000123")]
        [InlineData("  BEN-000123  ")]
        [InlineData("|BEN-000123|")]
        [InlineData("?BEN-000123?")]
        public void IsEKardPayload_ReturnsTrue_ForBenPrefixedPayloads(string payload)
        {
            Assert.True(EKardPayloadRouter.IsEKardPayload(payload));
        }

        [Theory]
        [InlineData("ASMBID000123ABCDEF")]
        [InlineData("BID-000123")]
        [InlineData("")]
        [InlineData("   ")]
        [InlineData(null)]
        [InlineData("XBEN-000123")]
        public void IsEKardPayload_ReturnsFalse_ForNonEKardPayloads(string? payload)
        {
            Assert.False(EKardPayloadRouter.IsEKardPayload(payload));
        }

        [Theory]
        [InlineData("BEN-000123", "BEN-000123")]
        [InlineData("  BEN-000123  ", "BEN-000123")]
        [InlineData("|BEN-000123|", "BEN-000123")]
        [InlineData("?BEN-000123", "BEN-000123")]
        public void ExtractBeneficiaryId_StripsWrappers_PreservesBenCore(string payload, string expected)
        {
            Assert.Equal(expected, EKardPayloadRouter.ExtractBeneficiaryId(payload));
        }

        [Fact]
        public void ExtractBeneficiaryId_ReturnsEmpty_ForNullOrWhitespace()
        {
            Assert.Equal(string.Empty, EKardPayloadRouter.ExtractBeneficiaryId(null));
            Assert.Equal(string.Empty, EKardPayloadRouter.ExtractBeneficiaryId("   "));
        }
    }
}
