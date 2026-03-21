namespace AttendanceShiftingManagement.Services
{
    public interface IOcrService
    {
        Task<OcrHealthResult> GetHealthAsync(CancellationToken cancellationToken = default);
        Task<IReadOnlyList<string>> ExtractNamesAsync(
            string imagePath,
            IReadOnlyList<string>? hintNames = null,
            CancellationToken cancellationToken = default);
    }

    public sealed record OcrHealthResult(bool IsAvailable, string Detail);
}
