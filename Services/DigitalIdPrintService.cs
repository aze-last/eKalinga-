using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace AttendanceShiftingManagement.Services
{
    public sealed record DigitalIdPrintRequest(
        string FullName,
        string CardNumber,
        string BeneficiaryId,
        string CivilRegistryId,
        BitmapSource? PhotoImage,
        BitmapSource? QrImage);

    public sealed class DigitalIdPrintService
    {
        public bool PrintCard(DigitalIdPrintRequest request)
        {
            var dialog = new PrintDialog();
            if (dialog.ShowDialog() != true)
            {
                return false;
            }

            var card = BuildCard(request);
            card.Measure(new Size(324, 204));
            card.Arrange(new Rect(new Size(324, 204)));
            card.UpdateLayout();
            dialog.PrintVisual(card, $"Beneficiary Digital ID - {request.FullName}");
            return true;
        }

        private static Border BuildCard(DigitalIdPrintRequest request)
        {
            var root = new Grid
            {
                Width = 324,
                Height = 204,
                Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#F8FAFC"))
            };

            root.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(110) });
            root.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            var photoBorder = new Border
            {
                Margin = new Thickness(12),
                CornerRadius = new CornerRadius(12),
                Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#DBEAFE")),
                BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#1D4ED8")),
                BorderThickness = new Thickness(1)
            };

            photoBorder.Child = request.PhotoImage == null
                ? new TextBlock
                {
                    Text = "NO PHOTO",
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center,
                    FontWeight = FontWeights.SemiBold,
                    Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#1D4ED8"))
                }
                : new Image
                {
                    Source = request.PhotoImage,
                    Stretch = Stretch.UniformToFill
                };

            Grid.SetColumn(photoBorder, 0);
            root.Children.Add(photoBorder);

            var details = new Grid
            {
                Margin = new Thickness(0, 12, 12, 12)
            };
            details.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            details.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            details.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            details.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

            details.Children.Add(new TextBlock
            {
                Text = "BARANGAY DIGITAL ID",
                FontSize = 11,
                FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#1E3A8A"))
            });

            var nameBlock = new TextBlock
            {
                Margin = new Thickness(0, 8, 0, 0),
                Text = request.FullName,
                FontSize = 18,
                FontWeight = FontWeights.SemiBold,
                TextWrapping = TextWrapping.Wrap,
                Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#0F172A"))
            };
            Grid.SetRow(nameBlock, 1);
            details.Children.Add(nameBlock);

            var idBlock = new TextBlock
            {
                Margin = new Thickness(0, 8, 0, 0),
                Text = $"{request.CardNumber}\nBeneficiary ID: {Fallback(request.BeneficiaryId)}\nCivil Registry ID: {Fallback(request.CivilRegistryId)}",
                FontSize = 10,
                TextWrapping = TextWrapping.Wrap,
                Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#334155"))
            };
            Grid.SetRow(idBlock, 2);
            details.Children.Add(idBlock);

            var qrContainer = new StackPanel
            {
                Margin = new Thickness(0, 10, 0, 0),
                Orientation = Orientation.Horizontal,
                VerticalAlignment = VerticalAlignment.Bottom
            };

            qrContainer.Children.Add(new Border
            {
                Width = 74,
                Height = 74,
                Background = Brushes.White,
                BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#CBD5E1")),
                BorderThickness = new Thickness(1),
                Child = request.QrImage == null
                    ? null
                    : new Image
                    {
                        Source = request.QrImage,
                        Stretch = Stretch.Fill
                    }
            });

            qrContainer.Children.Add(new TextBlock
            {
                Margin = new Thickness(10, 0, 0, 0),
                VerticalAlignment = VerticalAlignment.Center,
                Text = "Present this ID for\nbeneficiary lookup,\nattendance, and\nrelease verification.",
                FontSize = 9,
                Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#475569"))
            });

            Grid.SetRow(qrContainer, 3);
            details.Children.Add(qrContainer);

            Grid.SetColumn(details, 1);
            root.Children.Add(details);

            return new Border
            {
                Width = 324,
                Height = 204,
                CornerRadius = new CornerRadius(18),
                Background = root.Background,
                BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#CBD5E1")),
                BorderThickness = new Thickness(1),
                Child = root
            };
        }

        private static string Fallback(string? value)
        {
            return string.IsNullOrWhiteSpace(value) ? "--" : value.Trim();
        }
    }
}
