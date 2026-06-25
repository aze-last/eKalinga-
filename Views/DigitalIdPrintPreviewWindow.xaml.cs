using AttendanceShiftingManagement.Services;
using Microsoft.Win32;
using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace AttendanceShiftingManagement.Views
{
    public partial class DigitalIdPrintPreviewWindow : Window
    {
        private readonly string _fullName;

        public DigitalIdPrintPreviewWindow(FrameworkElement previewCard, string fullName)
        {
            InitializeComponent();
            WindowBrandingService.ApplyWindowIcon(this);
            _fullName = fullName;
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

        private void SaveImageButton_Click(object sender, RoutedEventArgs e)
        {
            if (PreviewHost.Content is not FrameworkElement card) return;

            var safeName = string.Join("_", _fullName.Split(Path.GetInvalidFileNameChars()));
            var dialog = new SaveFileDialog
            {
                Title = "Save Digital ID as Image",
                FileName = $"DigitalID_{safeName}.png",
                DefaultExt = ".png",
                Filter = "PNG Image|*.png"
            };

            if (dialog.ShowDialog() != true) return;

            try
            {
                // Render at 2x for crisp output
                const double scale = 2.0;
                var width = card.ActualWidth > 0 ? card.ActualWidth : 324;
                var height = card.ActualHeight > 0 ? card.ActualHeight : 204;

                card.Measure(new Size(width, height));
                card.Arrange(new Rect(new Size(width, height)));
                card.UpdateLayout();

                var renderBitmap = new RenderTargetBitmap(
                    (int)(width * scale),
                    (int)(height * scale),
                    96 * scale,
                    96 * scale,
                    PixelFormats.Pbgra32);

                renderBitmap.Render(card);

                var encoder = new PngBitmapEncoder();
                encoder.Frames.Add(BitmapFrame.Create(renderBitmap));

                using var stream = new FileStream(dialog.FileName, FileMode.Create);
                encoder.Save(stream);

                MessageBox.Show(
                    $"Digital ID saved to:\n{dialog.FileName}",
                    "Saved",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Failed to save: {ex.Message}",
                    "Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
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
