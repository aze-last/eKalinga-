using AttendanceShiftingManagement.Data;
using AttendanceShiftingManagement.Models;
using Microsoft.EntityFrameworkCore;

namespace AttendanceShiftingManagement.Services
{
    public sealed record EquipmentOperationResult(bool IsSuccess, string Message);
    public enum EquipmentBorrowingListFilter
    {
        Active,
        Overdue,
        History
    }

    public sealed class EquipmentBorrowingService
    {
        private readonly AppDbContext _context;
        private readonly AuditService _auditService;

        public EquipmentBorrowingService(AppDbContext context, AuditService? auditService = null)
        {
            _context = context;
            _auditService = auditService ?? new AuditService(context);
        }

        // --- Asset Management ---

        public async Task<List<BarangayAsset>> GetAssetsAsync(string? category = null, AssetStatus? status = null)
        {
            var query = _context.BarangayAssets.AsNoTracking();

            if (!string.IsNullOrWhiteSpace(category))
                query = query.Where(a => a.Category == category);

            if (status.HasValue)
                query = query.Where(a => a.Status == status.Value);

            return await query.OrderBy(a => a.AssetTag).ToListAsync();
        }

        public async Task<EquipmentOperationResult> AddAssetAsync(string assetTag, string category, string? description)
        {
            var normalizedAssetTag = assetTag?.Trim() ?? string.Empty;
            var normalizedCategory = category?.Trim() ?? string.Empty;
            var normalizedDescription = string.IsNullOrWhiteSpace(description) ? null : description.Trim();

            if (string.IsNullOrWhiteSpace(normalizedAssetTag))
                return new EquipmentOperationResult(false, "Asset tag is required.");

            if (string.IsNullOrWhiteSpace(normalizedCategory))
                return new EquipmentOperationResult(false, "Asset category is required.");

            if (await _context.BarangayAssets.AnyAsync(a => a.AssetTag == normalizedAssetTag))
                return new EquipmentOperationResult(false, "Asset tag already exists.");

            var asset = new BarangayAsset
            {
                AssetTag = normalizedAssetTag,
                Category = normalizedCategory,
                Description = normalizedDescription,
                Status = AssetStatus.Available
            };

            _context.BarangayAssets.Add(asset);
            await _context.SaveChangesAsync();
            return new EquipmentOperationResult(true, "Asset added successfully.");
        }

        // --- Borrowing Logic ---

        public async Task<int> GetOverdueCountAsync()
        {
            return await _context.EquipmentBorrowings
                .CountAsync(b => b.ReturnDate == null && b.DueDate < DateTime.Now);
        }

        public async Task<List<EquipmentBorrowing>> GetBorrowingHistoryAsync(
            int pageIndex,
            int pageSize,
            string? searchTerm = null,
            EquipmentBorrowingListFilter filter = EquipmentBorrowingListFilter.Active)
        {
            var query = _context.EquipmentBorrowings
                .Include(b => b.Asset)
                .AsNoTracking();

            query = filter switch
            {
                EquipmentBorrowingListFilter.Overdue => query.Where(b => b.ReturnDate == null && b.DueDate < DateTime.Now),
                EquipmentBorrowingListFilter.History => query.Where(b => b.ReturnDate != null),
                _ => query.Where(b => b.ReturnDate == null && b.DueDate >= DateTime.Now)
            };

            if (!string.IsNullOrWhiteSpace(searchTerm))
            {
                query = query.Where(b => 
                    b.BeneficiaryName!.Contains(searchTerm) || 
                    b.BeneficiaryId!.Contains(searchTerm) || 
                    b.Asset.AssetTag.Contains(searchTerm));
            }

            return await query
                .OrderByDescending(b => b.BorrowDate)
                .Skip(pageIndex * pageSize)
                .Take(pageSize)
                .ToListAsync();
        }

        public async Task<EquipmentOperationResult> IssueEquipmentAsync(
            int assetId, 
            string beneficiaryId, 
            string beneficiaryName, 
            DateTime dueDate, 
            string? conditionOut,
            int actedByUserId)
        {
            var normalizedBeneficiaryId = beneficiaryId?.Trim() ?? string.Empty;
            var normalizedBeneficiaryName = beneficiaryName?.Trim() ?? string.Empty;
            var normalizedConditionOut = string.IsNullOrWhiteSpace(conditionOut) ? null : conditionOut.Trim();

            if (string.IsNullOrWhiteSpace(normalizedBeneficiaryId))
                return new EquipmentOperationResult(false, "Approved beneficiary ID is required.");

            if (string.IsNullOrWhiteSpace(normalizedBeneficiaryName))
                return new EquipmentOperationResult(false, "Approved beneficiary details must be loaded before issuing equipment.");

            if (dueDate.Date < DateTime.Today)
                return new EquipmentOperationResult(false, "Due date cannot be earlier than today.");

            var asset = await _context.BarangayAssets.FindAsync(assetId);
            if (asset == null) return new EquipmentOperationResult(false, "Asset not found.");
            if (asset.Status != AssetStatus.Available) return new EquipmentOperationResult(false, "Asset is not available.");

            var borrowing = new EquipmentBorrowing
            {
                AssetId = assetId,
                BeneficiaryId = normalizedBeneficiaryId,
                BeneficiaryName = normalizedBeneficiaryName,
                BorrowDate = DateTime.Now,
                DueDate = dueDate,
                ConditionOut = normalizedConditionOut
            };

            asset.Status = AssetStatus.Borrowed;
            _context.EquipmentBorrowings.Add(borrowing);
            
            await _context.SaveChangesAsync();
            await _auditService.LogActivityAsync(actedByUserId, "Issue", "EquipmentBorrowing", borrowing.Id, $"Asset {asset.AssetTag} issued to {beneficiaryName} ({beneficiaryId})");
            
            return new EquipmentOperationResult(true, "Equipment issued successfully.");
        }

        public async Task<EquipmentOperationResult> ReturnEquipmentAsync(int borrowingId, string? conditionIn, int actedByUserId)
        {
            var borrowing = await _context.EquipmentBorrowings
                .Include(b => b.Asset)
                .FirstOrDefaultAsync(b => b.Id == borrowingId);

            if (borrowing == null) return new EquipmentOperationResult(false, "Borrowing record not found.");
            if (borrowing.ReturnDate != null) return new EquipmentOperationResult(false, "Equipment already returned.");

            borrowing.ReturnDate = DateTime.Now;
            borrowing.ConditionIn = conditionIn;
            borrowing.Asset.Status = AssetStatus.Available;

            await _context.SaveChangesAsync();
            await _auditService.LogActivityAsync(actedByUserId, "Return", "EquipmentBorrowing", borrowing.Id, $"Asset {borrowing.Asset.AssetTag} returned by {borrowing.BeneficiaryName}");

            return new EquipmentOperationResult(true, "Equipment returned successfully.");
        }

        public async Task<List<EquipmentBorrowing>> GetBorrowingsByBeneficiaryAsync(string beneficiaryId)
        {
            return await _context.EquipmentBorrowings
                .Include(b => b.Asset)
                .Where(b => b.BeneficiaryId == beneficiaryId)
                .OrderByDescending(b => b.BorrowDate)
                .ToListAsync();
        }

        public async Task<List<EquipmentBorrowing>> GetActiveBorrowingsByBeneficiaryAsync(string beneficiaryId)
        {
            return await _context.EquipmentBorrowings
                .Include(b => b.Asset)
                .Where(b => b.BeneficiaryId == beneficiaryId && b.ReturnDate == null)
                .ToListAsync();
        }
    }
}
