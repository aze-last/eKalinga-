using AttendanceShiftingManagement.Data;
using AttendanceShiftingManagement.Helpers;
using AttendanceShiftingManagement.Models;
using AttendanceShiftingManagement.Services;
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Windows.Data;

namespace AttendanceShiftingManagement.ViewModels
{
    public class AttendanceLogsViewModel : ObservableObject
    {
        private readonly AttendanceStatusService _statusService;
        private string _filter = "All";
        private string _title = "Attendance Logs";

        public ObservableCollection<AttendanceStatusRow> Rows { get; } = new();
        public ICollectionView RowsView { get; private set; }

        public string Title
        {
            get => _title;
            set => SetProperty(ref _title, value);
        }

        public AttendanceLogsViewModel(string filter)
        {
            _statusService = new AttendanceStatusService(new AppDbContext());
            RowsView = CollectionViewSource.GetDefaultView(Rows);
            ApplyFilter(filter);
            LoadRows();
        }

        private void ApplyFilter(string filter)
        {
            _filter = string.IsNullOrWhiteSpace(filter) ? "All" : filter;
            Title = _filter == "All" ? "Attendance Logs" : $"{_filter} - Attendance Logs";

            RowsView.Filter = item =>
            {
                if (item is not AttendanceStatusRow row)
                {
                    return false;
                }

                if (_filter == "All")
                {
                    return true;
                }

                return string.Equals(row.Status, _filter, StringComparison.OrdinalIgnoreCase);
            };
        }

        private void LoadRows()
        {
            Rows.Clear();
            var items = _statusService.GetTodayStatuses(DateTime.Now);
            foreach (var row in items.OrderBy(r => r.EmployeeName))
            {
                Rows.Add(row);
            }

            RowsView.Refresh();
        }
    }
}
