using AttendanceShiftingManagement.Helpers;
using AttendanceShiftingManagement.Models;
using AttendanceShiftingManagement.Services;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;

namespace AttendanceShiftingManagement.ViewModels
{
    public class GgmsConsolidatedTransactionViewModel : ObservableObject
    {
        private readonly IGgmsConsolidatedTransactionService _ggmsService;
        private readonly User _currentUser;

        private ObservableCollection<GgmsConsolidatedTransaction> _transactions = new();
        private ObservableCollection<GgmsConsolidatedTransaction> _pagedTransactions = new();
        private string _searchText = string.Empty;
        private string? _selectedProjectFilter = "All Transactions";
        private int _currentPage = 1;
        private int _pageSize = 25;
        private int _totalPages = 1;
        private bool _isBusy;
        private bool _isFilterPanelOpen;
        private bool _isDetailPanelOpen;
        private string _statusMessage = "Ready";
        private bool _isGgmsAvailable;
        private readonly System.Threading.SynchronizationContext? _syncContext;
        
        public bool IsGgmsAvailable
        {
            get => _isGgmsAvailable;
            private set => SetProperty(ref _isGgmsAvailable, value);
        }

        public GgmsConsolidatedTransactionViewModel(User currentUser)
        {
            _currentUser = currentUser;
            _ggmsService = new GgmsConsolidatedTransactionService();
            _syncContext = System.Threading.SynchronizationContext.Current;
            _isGgmsAvailable = ConnectivityService.Instance.IsGgmsAvailable;
            
            ConnectivityService.Instance.ConnectivityChanged += OnConnectivityChanged;
            
            LoadCommand = new RelayCommand(async _ => await SyncAndLoadDataAsync());
            SearchCommand = new RelayCommand(_ => ApplyFilters());
            FilterCommand = new RelayCommand(p => 
            {
                if (p is string filter)
                {
                    SelectedProjectFilter = filter;
                    ApplyFilters();
                }
            });
            
            PreviousPageCommand = new RelayCommand(_ => 
            {
                if (CurrentPage > 1)
                {
                    CurrentPage--;
                    UpdatePagedResults();
                }
            });

            NextPageCommand = new RelayCommand(_ => 
            {
                if (CurrentPage < TotalPages)
                {
                    CurrentPage++;
                    UpdatePagedResults();
                }
            });

            Task.Run(SyncAndLoadDataAsync);
        }

        private void OnConnectivityChanged(object? sender, ConnectivityStatusChangedEventArgs e)
        {
            if (_syncContext != null)
            {
                _syncContext.Post(_ => IsGgmsAvailable = e.IsGgmsAvailable, null);
            }
            else
            {
                System.Windows.Application.Current?.Dispatcher?.Invoke(() => IsGgmsAvailable = e.IsGgmsAvailable);
            }
        }

        public void Dispose()
        {
            ConnectivityService.Instance.ConnectivityChanged -= OnConnectivityChanged;
        }

        private async Task SyncAndLoadDataAsync()
        {
            if (IsBusy) return;

            IsBusy = true;
            StatusMessage = "Checking connectivity and flushing pending transactions...";

            try
            {
                // 1. Run live check on demand
                await ConnectivityService.Instance.CheckConnectivityAsync();

                // 2. If available, trigger the queue flush
                if (ConnectivityService.Instance.IsGgmsAvailable)
                {
                    using (var localDb = new Data.LocalDbContext())
                    {
                        var pendingCount = await localDb.GgmsPendingTransactionCache.CountAsync();
                        if (pendingCount > 0)
                        {
                            StatusMessage = $"Syncing {pendingCount} offline transactions to GGMS...";
                            await _ggmsService.FlushPendingTransactionsAsync(localDb);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"GGMS Pre-Sync Error: {ex.Message}");
            }

            StatusMessage = "Loading transactions from GGMS...";
            
            try
            {
                var allTransactions = await _ggmsService.LoadTransactionsAsync();
                
                System.Windows.Application.Current.Dispatcher.Invoke(() =>
                {
                    Transactions.Clear();
                    foreach (var tx in allTransactions)
                    {
                        Transactions.Add(tx);
                    }

                    ApplyFilters();
                });

                StatusMessage = $"Loaded {Transactions.Count} transactions.";
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error: {ex.Message}";
            }
            finally
            {
                IsBusy = false;
            }
        }

        public ObservableCollection<GgmsConsolidatedTransaction> Transactions
        {
            get => _transactions;
            set => SetProperty(ref _transactions, value);
        }

        public ObservableCollection<GgmsConsolidatedTransaction> PagedTransactions
        {
            get => _pagedTransactions;
            set => SetProperty(ref _pagedTransactions, value);
        }

        public string SearchText
        {
            get => _searchText;
            set => SetProperty(ref _searchText, value);
        }

        public string? SelectedProjectFilter
        {
            get => _selectedProjectFilter;
            set => SetProperty(ref _selectedProjectFilter, value);
        }

        public int CurrentPage
        {
            get => _currentPage;
            set => SetProperty(ref _currentPage, value);
        }

        public int PageSize
        {
            get => _pageSize;
            set => SetProperty(ref _pageSize, value);
        }

        public int TotalPages
        {
            get => _totalPages;
            set => SetProperty(ref _totalPages, value);
        }

        public bool IsBusy
        {
            get => _isBusy;
            set => SetProperty(ref _isBusy, value);
        }

        public bool IsAnyOverlayOpen => _isFilterPanelOpen || _isDetailPanelOpen;

        public bool IsFilterPanelOpen
        {
            get => _isFilterPanelOpen;
            set
            {
                if (SetProperty(ref _isFilterPanelOpen, value))
                {
                    OnPropertyChanged(nameof(IsAnyOverlayOpen));
                }
            }
        }

        public bool IsDetailPanelOpen
        {
            get => _isDetailPanelOpen;
            set
            {
                if (SetProperty(ref _isDetailPanelOpen, value))
                {
                    OnPropertyChanged(nameof(IsAnyOverlayOpen));
                }
            }
        }

        public string StatusMessage
        {
            get => _statusMessage;
            set => SetProperty(ref _statusMessage, value);
        }

        public ICommand LoadCommand { get; }
        public ICommand SearchCommand { get; }
        public ICommand FilterCommand { get; }
        public ICommand PreviousPageCommand { get; }
        public ICommand NextPageCommand { get; }



        private void ApplyFilters()
        {
            var filtered = Transactions.AsEnumerable();

            if (!string.IsNullOrWhiteSpace(SearchText))
            {
                var search = SearchText.Trim().ToLowerInvariant();
                filtered = filtered.Where(t => 
                    (t.FullName?.ToLowerInvariant().Contains(search) ?? false) ||
                    (t.ProjectCode?.ToLowerInvariant().Contains(search) ?? false) ||
                    (t.BeneficiaryId?.ToLowerInvariant().Contains(search) ?? false));
            }

            if (SelectedProjectFilter != "All Transactions")
            {
                filtered = filtered.Where(t => t.ProjectName == SelectedProjectFilter);
            }

            var finalResults = filtered.ToList();
            TotalPages = (int)Math.Ceiling(finalResults.Count / (double)PageSize);
            if (TotalPages == 0) TotalPages = 1;
            
            CurrentPage = 1;
            UpdatePagedResults(finalResults);
        }

        private void UpdatePagedResults(List<GgmsConsolidatedTransaction>? filteredList = null)
        {
            var list = filteredList ?? Transactions.Where(t => 
                (string.IsNullOrWhiteSpace(SearchText) || 
                 (t.FullName?.ToLowerInvariant().Contains(SearchText.ToLower()) ?? false) ||
                 (t.ProjectCode?.ToLowerInvariant().Contains(SearchText.ToLower()) ?? false)) &&
                (SelectedProjectFilter == "All Transactions" || t.ProjectName == SelectedProjectFilter)
            ).ToList();

            System.Windows.Application.Current.Dispatcher.Invoke(() =>
            {
                PagedTransactions.Clear();
                var paged = list.Skip((CurrentPage - 1) * PageSize).Take(PageSize);
                foreach (var tx in paged)
                {
                    PagedTransactions.Add(tx);
                }
            });
        }
    }
}
