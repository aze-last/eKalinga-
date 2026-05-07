using AttendanceShiftingManagement.Data;
using AttendanceShiftingManagement.Helpers;
using AttendanceShiftingManagement.Models;
using AttendanceShiftingManagement.Services;
using Microsoft.EntityFrameworkCore;
using System.Collections.ObjectModel;
using System.Windows.Input;
using System.Windows.Media;

namespace AttendanceShiftingManagement.ViewModels
{
    public sealed class BorrowingViewModel : ObservableObject
    {
        private readonly User _currentUser;
        private readonly ObservableCollection<EquipmentBorrowing> _transactions = new();
        private readonly ObservableCollection<BarangayAsset> _cartItems = new();
        
        private string _sidebarSearchQuery = string.Empty;
        private string _dialogSearchQuery = string.Empty;
        private string _statusMessage = "Ready";
        private Brush _statusBrush = CreateBrush("#6B7280");
        private bool _isBusy;
        private bool _isDialogOpen;
        private string _dialogTitle = "Issue Equipment";
        private string _dialogMode = "ISSUE"; // ISSUE, RETURN, ADD_ASSET
        
        private string _targetBeneficiaryId = string.Empty;
        private string _targetBeneficiaryName = string.Empty;
        private DateTime _selectedDueDate = DateTime.Today.AddDays(3);
        
        private string _newAssetTag = string.Empty;
        private string _newAssetCategory = string.Empty;
        private string _newAssetDescription = string.Empty;

        private int _overdueCount;
        private string _currentFilterDescription = "Showing all active borrowings.";
        private string _filterMode = "ACTIVE";

        public BorrowingViewModel(User currentUser)
        {
            _currentUser = currentUser;

            // Sidebar Commands
            RefreshCommand = new RelayCommand(async _ => await LoadDataAsync());
            ProcessSidebarScanCommand = new RelayCommand(async _ => await HandleSidebarScanAsync());
            OpenIssueDialogCommand = new RelayCommand(_ => PrepareDialog("ISSUE"));
            OpenReturnDialogCommand = new RelayCommand(_ => PrepareDialog("RETURN"));
            OpenAddAssetDialogCommand = new RelayCommand(_ => PrepareDialog("ADD_ASSET"));
            ShowAssetRegistryCommand = new RelayCommand(_ => { /* Logic to show full asset registry if needed */ });
            
            // Dialog Commands
            ProcessDialogScanCommand = new RelayCommand(async _ => await HandleDialogScanAsync());
            RemoveFromCartCommand = new RelayCommand(item => { if (item is BarangayAsset asset) _cartItems.Remove(asset); });
            SubmitTransactionCommand = new RelayCommand(async _ => await SubmitBatchAsync(), _ => CanSubmitBatch());
            SubmitAddAssetCommand = new RelayCommand(async _ => await SubmitAddAssetAsync(), _ => CanSubmitAddAsset());
            CloseDialogCommand = new RelayCommand(_ => IsDialogOpen = false);

            _ = LoadDataAsync();
        }

        #region Properties

        public ObservableCollection<EquipmentBorrowing> Transactions => _transactions;
        public ObservableCollection<BarangayAsset> CartItems => _cartItems;

        public string SidebarSearchQuery
        {
            get => _sidebarSearchQuery;
            set => SetProperty(ref _sidebarSearchQuery, value);
        }

        public string DialogSearchQuery
        {
            get => _dialogSearchQuery;
            set => SetProperty(ref _dialogSearchQuery, value);
        }

        public string StatusMessage
        {
            get => _statusMessage;
            set => SetProperty(ref _statusMessage, value);
        }

        public Brush StatusBrush
        {
            get => _statusBrush;
            set => SetProperty(ref _statusBrush, value);
        }

        public bool IsBusy
        {
            get => _isBusy;
            set => SetProperty(ref _isBusy, value);
        }

        public bool IsDialogOpen
        {
            get => _isDialogOpen;
            set => SetProperty(ref _isDialogOpen, value);
        }

        public string DialogTitle
        {
            get => _dialogTitle;
            set => SetProperty(ref _dialogTitle, value);
        }

        public string TargetBeneficiaryId
        {
            get => _targetBeneficiaryId;
            set => SetProperty(ref _targetBeneficiaryId, value);
        }

        public string TargetBeneficiaryName
        {
            get => _targetBeneficiaryName;
            set => SetProperty(ref _targetBeneficiaryName, value);
        }

        public DateTime SelectedDueDate
        {
            get => _selectedDueDate;
            set => SetProperty(ref _selectedDueDate, value);
        }

        public string NewAssetTag
        {
            get => _newAssetTag;
            set => SetProperty(ref _newAssetTag, value);
        }

        public string NewAssetCategory
        {
            get => _newAssetCategory;
            set => SetProperty(ref _newAssetCategory, value);
        }

        public string NewAssetDescription
        {
            get => _newAssetDescription;
            set => SetProperty(ref _newAssetDescription, value);
        }

        public int OverdueCount
        {
            get => _overdueCount;
            set
            {
                if (SetProperty(ref _overdueCount, value))
                    OnPropertyChanged(nameof(HasOverdue));
            }
        }

        public bool HasOverdue => OverdueCount > 0;

        public string CurrentFilterDescription
        {
            get => _currentFilterDescription;
            set => SetProperty(ref _currentFilterDescription, value);
        }

        public string FilterMode
        {
            get => _filterMode;
            set => SetProperty(ref _filterMode, value);
        }

        public bool IsBatchMode => _dialogMode == "ISSUE" || _dialogMode == "RETURN";
        public bool IsAddAssetMode => _dialogMode == "ADD_ASSET";

        #endregion

        #region Commands

        public ICommand RefreshCommand { get; }
        public ICommand ProcessSidebarScanCommand { get; }
        public ICommand OpenIssueDialogCommand { get; }
        public ICommand OpenReturnDialogCommand { get; }
        public ICommand ProcessDialogScanCommand { get; }
        public ICommand RemoveFromCartCommand { get; }
        public ICommand SubmitTransactionCommand { get; }
        public ICommand CloseDialogCommand { get; }
        public ICommand ShowAssetRegistryCommand { get; }
        public ICommand OpenAddAssetDialogCommand { get; }
        public ICommand SubmitAddAssetCommand { get; }

        #endregion

        #region Logic

        private async Task LoadDataAsync()
        {
            IsBusy = true;
            try
            {
                await using var context = new AppDbContext();
                var service = new EquipmentBorrowingService(context);

                var filter = FilterMode switch
                {
                    "OVERDUE" => EquipmentBorrowingListFilter.Overdue,
                    "HISTORY" => EquipmentBorrowingListFilter.History,
                    _ => EquipmentBorrowingListFilter.Active
                };

                var results = await service.GetBorrowingHistoryAsync(0, 50, SidebarSearchQuery, filter);
                OverdueCount = await service.GetOverdueCountAsync();

                _transactions.Clear();
                foreach (var t in results) _transactions.Add(t);

                SetStatus("Data refreshed.", "#1A7A4A");
            }
            catch (Exception ex)
            {
                SetStatus($"Load error: {ex.Message}", "#991B1B");
            }
            finally
            {
                IsBusy = false;
            }
        }

        private async Task HandleSidebarScanAsync()
        {
            if (string.IsNullOrWhiteSpace(SidebarSearchQuery)) return;
            await LoadDataAsync();
        }

        private void PrepareDialog(string mode)
        {
            _dialogMode = mode;
            OnPropertyChanged(nameof(IsBatchMode));
            OnPropertyChanged(nameof(IsAddAssetMode));

            if (mode == "ADD_ASSET")
            {
                DialogTitle = "Add New Asset";
                NewAssetTag = string.Empty;
                NewAssetCategory = string.Empty;
                NewAssetDescription = string.Empty;
            }
            else
            {
                DialogTitle = mode == "ISSUE" ? "Issue Equipment" : "Process Return";
                TargetBeneficiaryId = string.Empty;
                TargetBeneficiaryName = string.Empty;
                DialogSearchQuery = string.Empty;
                _cartItems.Clear();
                SelectedDueDate = DateTime.Today.AddDays(3);
            }
            
            IsDialogOpen = true;
        }

        private async Task HandleDialogScanAsync()
        {
            var query = DialogSearchQuery?.Trim();
            if (string.IsNullOrWhiteSpace(query)) return;

            IsBusy = true;
            try
            {
                await using var context = new AppDbContext();
                
                // 1. Try Asset Lookup
                var asset = await context.BarangayAssets
                    .FirstOrDefaultAsync(a => a.AssetTag == query);

                if (asset != null)
                {
                    if (_dialogMode == "ISSUE" && asset.Status == AssetStatus.Available)
                    {
                        if (!_cartItems.Any(c => c.Id == asset.Id))
                            _cartItems.Add(asset);
                        DialogSearchQuery = string.Empty;
                        SetStatus($"Added {asset.AssetTag} to cart.", "#1A7A4A");
                        return;
                    }
                    else if (_dialogMode == "RETURN" && asset.Status == AssetStatus.Borrowed)
                    {
                        if (!_cartItems.Any(c => c.Id == asset.Id))
                        {
                            _cartItems.Add(asset);
                            if (string.IsNullOrEmpty(TargetBeneficiaryId))
                            {
                                var active = await context.EquipmentBorrowings
                                    .Include(b => b.Asset)
                                    .OrderByDescending(b => b.BorrowDate)
                                    .FirstOrDefaultAsync(b => b.AssetId == asset.Id && b.ReturnDate == null);
                                
                                if (active != null)
                                {
                                    TargetBeneficiaryId = active.BeneficiaryId ?? "N/A";
                                    TargetBeneficiaryName = active.BeneficiaryName ?? "Unknown";
                                }
                            }
                        }
                        DialogSearchQuery = string.Empty;
                        SetStatus($"Marked {asset.AssetTag} for return.", "#1A7A4A");
                        return;
                    }
                }

                // 2. Try Beneficiary Name/ID Search Fallback
                var masterService = new MasterListService();
                var search = await masterService.LoadPageAsync(new MasterListPageRequest
                {
                    SearchText = query,
                    PageSize = 1,
                    QuickFilters = new[] { MasterListQuickFilters.Approved }
                });

                if (search.Beneficiaries.Any())
                {
                    var b = search.Beneficiaries.First();
                    TargetBeneficiaryId = b.BeneficiaryId;
                    TargetBeneficiaryName = b.DisplayName;
                    DialogSearchQuery = string.Empty;
                    SetStatus($"Beneficiary resolved: {b.DisplayName}", "#1A7A4A");
                }
                else
                {
                    SetStatus("Input not recognized as Asset Tag or Beneficiary.", "#991B1B");
                }
            }
            catch (Exception ex)
            {
                SetStatus($"Search error: {ex.Message}", "#991B1B");
            }
            finally
            {
                IsBusy = false;
            }
        }

        private bool CanSubmitBatch()
        {
            return !string.IsNullOrEmpty(TargetBeneficiaryId) && _cartItems.Any();
        }

        private async Task SubmitBatchAsync()
        {
            IsBusy = true;
            try
            {
                await using var context = new AppDbContext();
                var service = new EquipmentBorrowingService(context);

                if (_dialogMode == "ISSUE")
                {
                    foreach (var asset in _cartItems)
                    {
                        await service.IssueEquipmentAsync(
                            asset.Id, 
                            TargetBeneficiaryId, 
                            TargetBeneficiaryName, 
                            SelectedDueDate, 
                            "Good", 
                            _currentUser.Id);
                    }
                }
                else
                {
                    foreach (var asset in _cartItems)
                    {
                        var active = await context.EquipmentBorrowings
                            .FirstOrDefaultAsync(b => b.AssetId == asset.Id && b.ReturnDate == null);
                        if (active != null)
                        {
                            await service.ReturnEquipmentAsync(active.Id, "Returned in Good Condition", _currentUser.Id);
                        }
                    }
                }

                IsDialogOpen = false;
                await LoadDataAsync();
                SetStatus("Batch operation completed successfully.", "#1A7A4A");
            }
            catch (Exception ex)
            {
                SetStatus($"Submission error: {ex.Message}", "#991B1B");
            }
            finally
            {
                IsBusy = false;
            }
        }

        private bool CanSubmitAddAsset()
        {
            return !string.IsNullOrWhiteSpace(NewAssetTag) && !string.IsNullOrWhiteSpace(NewAssetCategory);
        }

        private async Task SubmitAddAssetAsync()
        {
            IsBusy = true;
            try
            {
                await using var context = new AppDbContext();
                var service = new EquipmentBorrowingService(context);

                var result = await service.AddAssetAsync(NewAssetTag, NewAssetCategory, NewAssetDescription);
                if (result.IsSuccess)
                {
                    IsDialogOpen = false;
                    await LoadDataAsync();
                    SetStatus(result.Message, "#1A7A4A");
                }
                else
                {
                    SetStatus(result.Message, "#991B1B");
                }
            }
            catch (Exception ex)
            {
                SetStatus($"Registration error: {ex.Message}", "#991B1B");
            }
            finally
            {
                IsBusy = false;
            }
        }

        private void SetStatus(string msg, string color)
        {
            StatusMessage = msg;
            StatusBrush = CreateBrush(color);
        }

        private static Brush CreateBrush(string hex) => new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex));

        #endregion
    }
}
