using AttendanceShiftingManagement.Helpers;
using AttendanceShiftingManagement.Services;
using System.Collections.ObjectModel;
using System.Windows.Input;

namespace AttendanceShiftingManagement.ViewModels
{
    public sealed class SessionAnnouncementFilterOption
    {
        public string Key { get; init; } = string.Empty;
        public string Label { get; init; } = string.Empty;
    }

    public sealed class SessionAnnouncementGroupViewModel
    {
        public string CategoryKey { get; init; } = string.Empty;
        public string CategoryLabel { get; init; } = string.Empty;
        public string SummaryText { get; init; } = string.Empty;
        public string LatestTimestampLabel { get; init; } = string.Empty;
        public IReadOnlyList<SessionAnnouncementItem> Items { get; init; } = Array.Empty<SessionAnnouncementItem>();
    }

    public sealed class SessionAnnouncementViewModel : ObservableObject
    {
        private readonly List<SessionAnnouncementItem> _allItems;
        private SessionAnnouncementFilterOption? _selectedFilter;
        private SessionAnnouncementItem? _selectedItem;

        public SessionAnnouncementViewModel(SessionAnnouncementSnapshot snapshot)
        {
            Snapshot = snapshot;
            _allItems = snapshot.Items.ToList();
            Items = new ObservableCollection<SessionAnnouncementItem>();
            ActivityGroups = new ObservableCollection<SessionAnnouncementGroupViewModel>();
            Filters = new ObservableCollection<SessionAnnouncementFilterOption>
            {
                new() { Key = "All", Label = "All" },
                new() { Key = "Approvals", Label = "Approvals" },
                new() { Key = "Budget", Label = "Budget" },
                new() { Key = "Distribution", Label = "Distribution" },
                new() { Key = "CashForWork", Label = "Cash-for-Work" },
                new() { Key = "Reports", Label = "Reports" }
            };

            SelectFilterCommand = new RelayCommand(ExecuteSelectFilter);
            SelectedFilter = Filters[0];
        }

        public SessionAnnouncementSnapshot Snapshot { get; }

        public ObservableCollection<SessionAnnouncementItem> Items { get; }

        public ObservableCollection<SessionAnnouncementGroupViewModel> ActivityGroups { get; }

        public ObservableCollection<SessionAnnouncementFilterOption> Filters { get; }

        public ICommand SelectFilterCommand { get; }

        public string Title => "What happened since your last session";

        public string Subtitle => "Recent activity recorded in eKalinga+";

        public string RangeLabel => Snapshot.PreviousLogoutAt.HasValue && Snapshot.LastLogoutAt.HasValue
            ? $"From {Snapshot.PreviousLogoutAt:MMM dd, yyyy hh:mm tt} to {Snapshot.LastLogoutAt:MMM dd, yyyy hh:mm tt}"
            : "No prior session handoff marker was found for this station.";

        public int ActivityCount => Snapshot.Items.Count;

        public int ApprovalCount => Snapshot.ApprovalCount;

        public int BudgetCount => Snapshot.BudgetCount;

        public int DistributionCount => Snapshot.DistributionCount;

        public int CashForWorkCount => Snapshot.CashForWorkCount;

        public bool HasUpdates => Snapshot.HasUpdates;

        public bool HasActivityGroups => ActivityGroups.Count > 0;

        public int VisibleGroupCount => ActivityGroups.Count;

        public SessionAnnouncementItem? SelectedItem
        {
            get => _selectedItem;
            set
            {
                if (SetProperty(ref _selectedItem, value))
                {
                    OnPropertyChanged(nameof(HasSelectedItem));
                }
            }
        }

        public bool HasSelectedItem => SelectedItem != null;

        public SessionAnnouncementFilterOption? SelectedFilter
        {
            get => _selectedFilter;
            set
            {
                if (SetProperty(ref _selectedFilter, value))
                {
                    RefreshItems();
                }
            }
        }

        private void ExecuteSelectFilter(object? parameter)
        {
            if (parameter is SessionAnnouncementFilterOption option)
            {
                SelectedFilter = option;
                return;
            }

            if (parameter is string key)
            {
                SelectedFilter = Filters.FirstOrDefault(item => string.Equals(item.Key, key, StringComparison.OrdinalIgnoreCase));
            }
        }

        private void RefreshItems()
        {
            Items.Clear();
            ActivityGroups.Clear();

            var selectedKey = SelectedFilter?.Key ?? "All";
            var filtered = string.Equals(selectedKey, "All", StringComparison.OrdinalIgnoreCase)
                ? _allItems
                : _allItems.Where(item => string.Equals(item.CategoryKey, selectedKey, StringComparison.OrdinalIgnoreCase)).ToList();

            foreach (var item in filtered)
            {
                Items.Add(item);
            }

            foreach (var group in filtered
                         .GroupBy(item => new { item.CategoryKey, item.CategoryLabel })
                         .Select(group => new SessionAnnouncementGroupViewModel
                         {
                             CategoryKey = group.Key.CategoryKey,
                             CategoryLabel = group.Key.CategoryLabel,
                             SummaryText = BuildSummaryText(group.Key.CategoryLabel, group.Count()),
                             LatestTimestampLabel = group.Max(item => item.Timestamp).ToString("MMM dd, yyyy hh:mm tt"),
                             Items = group
                                 .OrderByDescending(item => item.Timestamp)
                                 .Take(5)
                                 .ToList()
                         })
                         .OrderByDescending(group => group.Items.FirstOrDefault()?.Timestamp ?? DateTime.MinValue))
            {
                ActivityGroups.Add(group);
            }

            SelectedItem = Items.FirstOrDefault();
            OnPropertyChanged(nameof(HasActivityGroups));
            OnPropertyChanged(nameof(VisibleGroupCount));
        }

        private static string BuildSummaryText(string categoryLabel, int count)
        {
            var noun = categoryLabel switch
            {
                "Aid Requests" => "aid request update",
                "Approvals" => "record",
                "Budget" => "budget update",
                "Distribution" => "distribution update",
                "Cash-for-Work" => "cash-for-work update",
                "Reports" => "report update",
                _ => "activity item"
            };

            return count == 1
                ? $"1 {noun} was recorded."
                : $"{count} {noun}{(noun.EndsWith("update", StringComparison.OrdinalIgnoreCase) ? "s" : "s")} were recorded.";
        }
    }
}
