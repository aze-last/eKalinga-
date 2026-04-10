using AttendanceShiftingManagement.Data;
using AttendanceShiftingManagement.Helpers;
using AttendanceShiftingManagement.Models;
using AttendanceShiftingManagement.Services;
using AttendanceShiftingManagement.Views;
using Microsoft.Win32;
using Microsoft.EntityFrameworkCore;
using System.Collections.ObjectModel;
using System.Data;
using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;

namespace AttendanceShiftingManagement.ViewModels
{
    public sealed class ReportsViewModel : ObservableObject
    {
        private static readonly Brush NeutralBrush = CreateBrush("#6B7280");
        private static readonly Brush SuccessBrush = CreateBrush("#1A7A4A");
        private static readonly Brush ErrorBrush = CreateBrush("#991B1B");

        private readonly User _currentUser;
        private readonly ReportsService _reportsService;
        private readonly ReportDocumentService _documentService;
        private readonly RelayCommand _refreshReportCommand;
        private readonly RelayCommand _exportCsvCommand;
        private readonly RelayCommand _printReportCommand;
        private ReportsReportTypeOption? _selectedReportType;
        private AyudaProgramFilterOption? _selectedProgramFilter;
        private DateTime _dateFrom = new(DateTime.Today.Year, DateTime.Today.Month, 1);
        private DateTime _dateTo = DateTime.Today;
        private string _statusMessage = "Loading the reports workspace...";
        private Brush _statusBrush = NeutralBrush;
        private DataView? _previewRows;
        private string _reportTitle = "Reports";
        private string _reportSubtitle = "Centralized summaries and print-ready exports across the ayuda workflows.";
        private string _rangeSummary = string.Empty;
        private string _programSummary = "All Programs";
        private string _layoutSummary = "Suggested layout: Portrait";
        private string _exportHint = "Print and choose Microsoft Print to PDF when a PDF file is needed.";
        private string _templateDescription = string.Empty;
        private ReportsSnapshot? _currentSnapshot;
        private bool _isBusy;

        public ReportsViewModel(User currentUser)
        {
            _currentUser = currentUser;
            _reportsService = new ReportsService();
            _documentService = new ReportDocumentService();
            ReportTypeOptions = new ObservableCollection<ReportsReportTypeOption>
            {
                new(ReportsReportType.AidRequests, "Aid Request Summary", "Case volume, status mix, and assistance totals.", true),
                new(ReportsReportType.ValidatedBeneficiaries, "Validated Beneficiaries", "Verification queue and approved-beneficiary registry snapshot.", false),
                new(ReportsReportType.BudgetUtilization, "Budget Utilization", "Program caps versus released amounts and alert thresholds.", true),
                new(ReportsReportType.DistributionClaims, "Distribution Claims", "One-claim-per-project distribution log.", true)
            };

            ProgramFilters = new ObservableCollection<AyudaProgramFilterOption> { AyudaProgramFilterOption.AllPrograms };
            Metrics = new ObservableCollection<ReportsMetricItem>();
            Highlights = new ObservableCollection<string>();
            _selectedReportType = ReportTypeOptions[0];
            _selectedProgramFilter = ProgramFilters[0];

            _refreshReportCommand = new RelayCommand(async _ => await LoadReportAsync(), _ => !IsBusy);
            _exportCsvCommand = new RelayCommand(_ => ExportCsv(), _ => !IsBusy && CurrentSnapshotHasRows);
            _printReportCommand = new RelayCommand(_ => PrintReport(), _ => !IsBusy && _currentSnapshot != null);

            _ = InitializeAsync();
        }

        public ObservableCollection<ReportsReportTypeOption> ReportTypeOptions { get; }
        public ObservableCollection<AyudaProgramFilterOption> ProgramFilters { get; }
        public ObservableCollection<ReportsMetricItem> Metrics { get; }
        public ObservableCollection<string> Highlights { get; }
        public ReportsReportTypeOption? SelectedReportType
        {
            get => _selectedReportType;
            set
            {
                if (SetProperty(ref _selectedReportType, value) && value != null)
                {
                    TemplateDescription = value.Description;
                    OnPropertyChanged(nameof(IsProgramFilterRelevant));
                    if (!IsProgramFilterRelevant)
                    {
                        SelectedProgramFilter = ProgramFilters.FirstOrDefault();
                    }

                    _ = LoadReportAsync();
                }
            }
        }

        public AyudaProgramFilterOption? SelectedProgramFilter
        {
            get => _selectedProgramFilter;
            set => SetProperty(ref _selectedProgramFilter, value);
        }

        public DateTime DateFrom
        {
            get => _dateFrom;
            set => SetProperty(ref _dateFrom, value);
        }

        public DateTime DateTo
        {
            get => _dateTo;
            set => SetProperty(ref _dateTo, value);
        }

        public string StatusMessage
        {
            get => _statusMessage;
            private set => SetProperty(ref _statusMessage, value);
        }

        public Brush StatusBrush
        {
            get => _statusBrush;
            private set => SetProperty(ref _statusBrush, value);
        }

        public DataView? PreviewRows
        {
            get => _previewRows;
            private set => SetProperty(ref _previewRows, value);
        }

        public string ReportTitle
        {
            get => _reportTitle;
            private set => SetProperty(ref _reportTitle, value);
        }

        public string ReportSubtitle
        {
            get => _reportSubtitle;
            private set => SetProperty(ref _reportSubtitle, value);
        }

        public string RangeSummary
        {
            get => _rangeSummary;
            private set => SetProperty(ref _rangeSummary, value);
        }

        public string ProgramSummary
        {
            get => _programSummary;
            private set => SetProperty(ref _programSummary, value);
        }

        public string LayoutSummary
        {
            get => _layoutSummary;
            private set => SetProperty(ref _layoutSummary, value);
        }

        public string ExportHint
        {
            get => _exportHint;
            private set => SetProperty(ref _exportHint, value);
        }

        public string TemplateDescription
        {
            get => _templateDescription;
            private set => SetProperty(ref _templateDescription, value);
        }

        public bool IsBusy
        {
            get => _isBusy;
            private set
            {
                if (SetProperty(ref _isBusy, value))
                {
                    _refreshReportCommand.RaiseCanExecuteChanged();
                    _exportCsvCommand.RaiseCanExecuteChanged();
                    _printReportCommand.RaiseCanExecuteChanged();
                }
            }
        }

        public bool IsProgramFilterRelevant => SelectedReportType?.SupportsProgramFilter == true;
        public bool CurrentSnapshotHasRows => _currentSnapshot?.Table.Rows.Count > 0;
        public ICommand RefreshReportCommand => _refreshReportCommand;
        public ICommand ExportCsvCommand => _exportCsvCommand;
        public ICommand PrintReportCommand => _printReportCommand;

        private async Task InitializeAsync()
        {
            try
            {
                await LoadProgramFiltersAsync();
                TemplateDescription = SelectedReportType?.Description ?? string.Empty;
                await LoadReportAsync();
            }
            catch (Exception ex)
            {
                SetErrorStatus($"Unable to initialize the reports workspace: {ex.Message}");
            }
        }

        private async Task LoadProgramFiltersAsync()
        {
            await using var context = new AppDbContext();
            var programs = await context.AyudaPrograms
                .AsNoTracking()
                .OrderBy(item => item.ProgramName)
                .Select(item => new AyudaProgramFilterOption(item.Id, item.ProgramName))
                .ToListAsync();

            ProgramFilters.Clear();
            ProgramFilters.Add(AyudaProgramFilterOption.AllPrograms);
            foreach (var program in programs)
            {
                ProgramFilters.Add(program);
            }

            SelectedProgramFilter = ProgramFilters.FirstOrDefault();
        }

        private async Task LoadReportAsync()
        {
            if (SelectedReportType == null)
            {
                return;
            }

            IsBusy = true;
            SetNeutralStatus("Building report preview...");

            try
            {
                var snapshot = await _reportsService.BuildSnapshotAsync(new ReportsQueryOptions
                {
                    ReportType = SelectedReportType.Type,
                    DateFrom = DateFrom,
                    DateTo = DateTo,
                    AyudaProgramId = IsProgramFilterRelevant ? SelectedProgramFilter?.Id : null
                });

                _currentSnapshot = snapshot;
                ApplySnapshot(snapshot);
                SetSuccessStatus($"Loaded {snapshot.Table.Rows.Count:N0} row(s) for {snapshot.Title.ToLowerInvariant()}.");
            }
            catch (Exception ex)
            {
                _currentSnapshot = null;
                Metrics.Clear();
                Highlights.Clear();
                PreviewRows = null;
                SetErrorStatus($"Unable to build the selected report: {ex.Message}");
            }
            finally
            {
                IsBusy = false;
            }
        }

        private void ApplySnapshot(ReportsSnapshot snapshot)
        {
            ReportTitle = snapshot.Title;
            ReportSubtitle = snapshot.Subtitle;
            RangeSummary = snapshot.RangeLabel;
            ProgramSummary = snapshot.ProgramLabel;
            LayoutSummary = $"Suggested layout: A4 {snapshot.SuggestedOrientation}";
            ExportHint = string.Equals(snapshot.SuggestedOrientation, "Landscape", StringComparison.OrdinalIgnoreCase)
                ? "Use landscape when printing or choosing Microsoft Print to PDF."
                : "Portrait layout is suitable for printing or saving through Microsoft Print to PDF.";

            Metrics.Clear();
            foreach (var metric in snapshot.Metrics)
            {
                Metrics.Add(metric);
            }

            Highlights.Clear();
            foreach (var highlight in snapshot.Highlights)
            {
                Highlights.Add(highlight);
            }

            PreviewRows = snapshot.Table.DefaultView;
            _exportCsvCommand.RaiseCanExecuteChanged();
            _printReportCommand.RaiseCanExecuteChanged();
        }

        private void ExportCsv()
        {
            if (_currentSnapshot == null)
            {
                return;
            }

            var dialog = new SaveFileDialog
            {
                Filter = "CSV files (*.csv)|*.csv",
                FileName = $"{_currentSnapshot.ExportFilePrefix}-{DateTime.Now:yyyyMMdd-HHmm}.csv"
            };

            if (dialog.ShowDialog() != true)
            {
                return;
            }

            var lines = new List<string>
            {
                string.Join(",", _currentSnapshot.Table.Columns.Cast<DataColumn>().Select(column => EscapeCsv(column.ColumnName)))
            };

            foreach (DataRow row in _currentSnapshot.Table.Rows)
            {
                lines.Add(string.Join(",", _currentSnapshot.Table.Columns.Cast<DataColumn>().Select(column => EscapeCsv(Convert.ToString(row[column], CultureInfo.CurrentCulture) ?? string.Empty))));
            }

            File.WriteAllLines(dialog.FileName, lines);
            SetSuccessStatus($"Exported {_currentSnapshot.Table.Rows.Count:N0} row(s) to {Path.GetFileName(dialog.FileName)}.");
        }

        private void PrintReport()
        {
            if (_currentSnapshot == null)
            {
                return;
            }

            var document = _documentService.BuildDocument(_currentSnapshot, new ReportDocumentOptions
            {
                PreparedBy = string.IsNullOrWhiteSpace(_currentUser.Username) ? _currentUser.Email : _currentUser.Username,
                IncludeLogo = true
            });

            var previewWindow = new ReportPrintPreviewWindow(document, _currentSnapshot.Title)
            {
                Owner = Application.Current.Windows.OfType<Window>().FirstOrDefault(window => window.IsActive)
            };

            previewWindow.ShowDialog();
        }

        private void SetNeutralStatus(string message)
        {
            StatusMessage = message;
            StatusBrush = NeutralBrush;
        }

        private void SetSuccessStatus(string message)
        {
            StatusMessage = message;
            StatusBrush = SuccessBrush;
        }

        private void SetErrorStatus(string message)
        {
            StatusMessage = message;
            StatusBrush = ErrorBrush;
        }

        private static SolidColorBrush CreateBrush(string colorCode)
        {
            return new SolidColorBrush((Color)ColorConverter.ConvertFromString(colorCode));
        }

        private static string EscapeCsv(string value)
        {
            if (!value.Contains('"') && !value.Contains(',') && !value.Contains('\n') && !value.Contains('\r'))
            {
                return value;
            }

            return $"\"{value.Replace("\"", "\"\"", StringComparison.Ordinal)}\"";
        }
    }

    public sealed class ReportsReportTypeOption
    {
        public ReportsReportTypeOption(ReportsReportType type, string label, string description, bool supportsProgramFilter)
        {
            Type = type;
            Label = label;
            Description = description;
            SupportsProgramFilter = supportsProgramFilter;
        }

        public ReportsReportType Type { get; }
        public string Label { get; }
        public string Description { get; }
        public bool SupportsProgramFilter { get; }
    }

    public sealed class AyudaProgramFilterOption
    {
        public static readonly AyudaProgramFilterOption AllPrograms = new(null, "All Programs");

        public AyudaProgramFilterOption(int? id, string name)
        {
            Id = id;
            Name = name;
        }

        public int? Id { get; }
        public string Name { get; }
    }
}
