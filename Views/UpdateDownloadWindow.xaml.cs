using AttendanceShiftingManagement.Services;
using System.ComponentModel;
using System.Windows;
using System.Windows.Input;

namespace AttendanceShiftingManagement.Views
{
    public partial class UpdateDownloadWindow : Window, INotifyPropertyChanged
    {
        private readonly CancellationTokenSource _cancellation = new();
        private string _statusText = "Preparing download...";
        private double _progressPercent;
        private bool _cancelling;

        public event PropertyChangedEventHandler? PropertyChanged;

        public UpdateDownloadWindow(string version)
        {
            InitializeComponent();
            StatusText = $"Downloading version {version}...";
            DataContext = this;
            WindowBrandingService.ApplyWindowIcon(this);
        }

        public CancellationToken CancellationToken => _cancellation.Token;

        public string StatusText
        {
            get => _statusText;
            private set
            {
                _statusText = value;
                OnPropertyChanged(nameof(StatusText));
            }
        }

        public double ProgressPercent
        {
            get => _progressPercent;
            private set
            {
                _progressPercent = value;
                OnPropertyChanged(nameof(ProgressPercent));
                OnPropertyChanged(nameof(PercentLabel));
            }
        }

        public string PercentLabel => $"{ProgressPercent:0.#}%";

        public void ReportProgress(UpdateDownloadProgress progress)
        {
            if (_cancelling)
            {
                return;
            }

            ProgressPercent = progress.PercentComplete;
            StatusText = progress.TotalBytes.HasValue && progress.TotalBytes.Value > 0
                ? $"Downloading... {FormatBytes(progress.BytesReceived)} of {FormatBytes(progress.TotalBytes.Value)}"
                : $"Downloading... {FormatBytes(progress.BytesReceived)} received";
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            if (_cancelling)
            {
                return;
            }

            _cancelling = true;
            CancelButton.IsEnabled = false;
            CancelButton.Content = "CANCELLING...";
            StatusText = "Cancelling download...";
            _cancellation.Cancel();
        }

        protected override void OnPreviewKeyDown(KeyEventArgs e)
        {
            if (e.Key == Key.Escape)
            {
                Cancel_Click(this, new RoutedEventArgs());
                e.Handled = true;
                return;
            }

            base.OnPreviewKeyDown(e);
        }

        private void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        private static string FormatBytes(long bytes)
        {
            const long oneKb = 1024;
            const long oneMb = oneKb * 1024;

            if (bytes >= oneMb)
            {
                return $"{bytes / (double)oneMb:0.#} MB";
            }

            if (bytes >= oneKb)
            {
                return $"{bytes / (double)oneKb:0.#} KB";
            }

            return $"{bytes} B";
        }
    }
}
