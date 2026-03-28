using AttendanceShiftingManagement.Services;

namespace AttendanceShiftingManagement.Tests;

public sealed class BeneficiaryImportDeduplicationTests
{
    [Fact]
    public void Evaluate_WhenCivilRegistryIdAlreadyExists_ReturnsSkip()
    {
        var snapshot = new BeneficiaryImportDeduplicationSnapshot(
            CivilRegistryIds: ["CRS-1"],
            BeneficiaryIds: [],
            ResidentsIds: [],
            PersonFingerprints: []);

        var decision = BeneficiaryImportDeduplication.Evaluate(
            residentsId: null,
            beneficiaryId: "BEN-2",
            civilRegistryId: "CRS-1",
            fullName: "Elena Rivera",
            dateOfBirth: "1980-01-02",
            snapshot);

        Assert.True(decision.ShouldSkip);
    }

    [Fact]
    public void Evaluate_WhenNameAndBirthDateFingerprintAlreadyExists_ReturnsSkip()
    {
        var snapshot = new BeneficiaryImportDeduplicationSnapshot(
            CivilRegistryIds: [],
            BeneficiaryIds: [],
            ResidentsIds: [],
            PersonFingerprints: [BeneficiaryImportDeduplication.BuildFingerprint("Elena Rivera", "1980-01-02")]);

        var decision = BeneficiaryImportDeduplication.Evaluate(
            residentsId: 55,
            beneficiaryId: "BEN-55",
            civilRegistryId: "CRS-55",
            fullName: "Elena Rivera",
            dateOfBirth: "1980-01-02",
            snapshot);

        Assert.True(decision.ShouldSkip);
    }

    [Fact]
    public void Evaluate_WhenIdentityIsNew_DoesNotSkip()
    {
        var snapshot = new BeneficiaryImportDeduplicationSnapshot(
            CivilRegistryIds: ["CRS-1"],
            BeneficiaryIds: ["BEN-1"],
            ResidentsIds: [1],
            PersonFingerprints: [BeneficiaryImportDeduplication.BuildFingerprint("Elena Rivera", "1980-01-02")]);

        var decision = BeneficiaryImportDeduplication.Evaluate(
            residentsId: 77,
            beneficiaryId: "BEN-77",
            civilRegistryId: "CRS-77",
            fullName: "Pedro Santos",
            dateOfBirth: "1985-05-04",
            snapshot);

        Assert.False(decision.ShouldSkip);
    }
}
