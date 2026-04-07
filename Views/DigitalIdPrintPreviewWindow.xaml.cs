using AttendanceShiftingManagement.Services;
using System.Windows;

namespace AttendanceShiftingManagement.Views
{
    public partial class DigitalIdPrintPreviewWindow : Window
    {
        public DigitalIdPrintPreviewWindow(FrameworkElement previewCard, string fullName)
        {
            InitializeComponent();
            WindowBrandingService.ApplyWindowIcon(this);
            Title = $"{fullName} Preview";
            PreviewHost.Content = previewCard;
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        private void PrintButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = true;
            Close();
        }

        private void Minimize_Click(object sender, RoutedEventArgs e)
        {
            WindowState = WindowState.Minimized;
        }

        private void Close_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}
