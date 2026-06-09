using AttendanceShiftingManagement.Views;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
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
        BitmapSource? BarcodeImage);

    public sealed class DigitalIdPrintService
    {
        public bool PrintCard(DigitalIdPrintRequest request)
        {
            var previewCard = BuildCard(request);
            PrepareCard(previewCard);

            var previewWindow = new DigitalIdPrintPreviewWindow(previewCard, request.FullName);
            var owner = ResolveOwnerWindow();
            if (owner != null)
            {
                previewWindow.Owner = owner;
            }

            if (previewWindow.ShowDialog() != true)
            {
                return false;
            }

            var dialog = new PrintDialog();
            if (dialog.ShowDialog() != true)
            {
                return false;
            }

            var printCard = BuildCard(request);
            PrepareCard(printCard);
            dialog.PrintVisual(printCard, $"Beneficiary Digital ID - {request.FullName}");
            return true;
        }

        private static void PrepareCard(Border card)
        {
            card.Measure(new Size(324, 204));
            card.Arrange(new Rect(new Size(324, 204)));
            card.UpdateLayout();
        }

        private static Window? ResolveOwnerWindow()
        {
            return Application.Current?.Windows
                .OfType<Window>()
                .FirstOrDefault(window => window.IsActive)
                ?? Application.Current?.MainWindow;
        }

        private static Border BuildCard(DigitalIdPrintRequest request)
        {
            var root = new Grid
            {
                Width = 324,
                Height = 204,
                Background = Brushes.White,
                ClipToBounds = true
            };

            // WATERMARK
            var watermarkPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Images", "Gemini_Generated_Image_1ivs1t1ivs1t1ivs-removebg-preview.png");
            if (!System.IO.File.Exists(watermarkPath))
            {
                watermarkPath = "Images/Gemini_Generated_Image_1ivs1t1ivs1t1ivs-removebg-preview.png";
            }

            var watermarkImage = Helpers.LocalImageLoader.Load(watermarkPath);
            if (watermarkImage != null)
            {
                var watermark = new Image
                {
                    Source = watermarkImage,
                    Width = 180,
                    Opacity = 0.05,
                    Stretch = Stretch.Uniform,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center,
                    Margin = new Thickness(0),
                    SnapsToDevicePixels = true
                };
                RenderOptions.SetBitmapScalingMode(watermark, BitmapScalingMode.HighQuality);
                
                Grid.SetRowSpan(watermark, 3);
                root.Children.Add(watermark);
            }

            root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(38) });
            root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(45) });

            // HEADER
            var header = new Border
            {
                Background = new LinearGradientBrush(
                    BrushFromHex("#1E3A8A").Color,
                    BrushFromHex("#3B82F6").Color,
                    0),
                Padding = new Thickness(14, 0, 14, 0)
            };
            var headerContent = new Grid();
            headerContent.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            headerContent.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            headerContent.Children.Add(new TextBlock
            {
                Text = "eKalinga+",
                FontSize = 14,
                FontWeight = FontWeights.ExtraBold,
                Foreground = Brushes.White,
                VerticalAlignment = VerticalAlignment.Center
            });

            var cardTypeBadge = new Border
            {
                Background = new SolidColorBrush(Color.FromArgb(51, 255, 255, 255)),
                BorderBrush = new SolidColorBrush(Color.FromArgb(76, 255, 255, 255)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(10),
                Padding = new Thickness(6, 2, 6, 2),
                VerticalAlignment = VerticalAlignment.Center,
                Child = new TextBlock
                {
                    Text = "Barangay Beneficiary ID".ToUpper(),
                    FontSize = 7,
                    FontWeight = FontWeights.Bold,
                    Foreground = Brushes.White
                }
            };
            Grid.SetColumn(cardTypeBadge, 1);
            headerContent.Children.Add(cardTypeBadge);
            header.Child = headerContent;
            Grid.SetRow(header, 0);
            root.Children.Add(header);

            // BODY
            var body = new Grid
            {
                Margin = new Thickness(14, 12, 14, 0)
            };
            body.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(72) });
            body.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(14) });
            body.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            Grid.SetRow(body, 1);
            root.Children.Add(body);

            // PHOTO
            var photoBorder = new Border
            {
                Width = 72,
                Height = 90,
                CornerRadius = new CornerRadius(8),
                Background = BrushFromHex("#F8FAFC"),
                BorderBrush = Brushes.White,
                BorderThickness = new Thickness(2),
                ClipToBounds = true,
                VerticalAlignment = VerticalAlignment.Top,
                Effect = new System.Windows.Media.Effects.DropShadowEffect
                {
                    BlurRadius = 4,
                    ShadowDepth = 2,
                    Opacity = 0.1,
                    Color = Colors.Black
                }
            };

            photoBorder.Child = request.PhotoImage == null
                ? new TextBlock
                {
                    Text = "NO PHOTO",
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center,
                    FontSize = 9,
                    FontWeight = FontWeights.Bold,
                    Foreground = BrushFromHex("#94A3B8")
                }
                : new Image
                {
                    Source = request.PhotoImage,
                    Stretch = Stretch.UniformToFill
                };

            Grid.SetColumn(photoBorder, 0);
            body.Children.Add(photoBorder);

            // DETAILS
            var details = new StackPanel
            {
                VerticalAlignment = VerticalAlignment.Top,
                Margin = new Thickness(0, 2, 0, 0)
            };

            var nameLabel = new TextBlock
            {
                Text = "FULL NAME",
                FontSize = 6,
                FontWeight = FontWeights.Bold,
                Foreground = BrushFromHex("#64748B"),
                Margin = new Thickness(0, 0, 0, 1)
            };
            details.Children.Add(nameLabel);

            var nameBlock = new TextBlock
            {
                Text = request.FullName.ToUpper(),
                FontSize = 14,
                FontWeight = FontWeights.Black,
                Foreground = BrushFromHex("#0F172A"),
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 0, 0, 6)
            };
            details.Children.Add(nameBlock);

            var fieldsGrid = new UniformGrid { Columns = 2 };
            fieldsGrid.Children.Add(CreateDetailGroup("BENEFICIARY ID", request.BeneficiaryId));
            fieldsGrid.Children.Add(CreateDetailGroup("CIVIL REGISTRY ID", request.CivilRegistryId));
            details.Children.Add(fieldsGrid);

            Grid.SetColumn(details, 2);
            body.Children.Add(details);

            // FOOTER & BARCODE
            var footer = new Grid
            {
                Margin = new Thickness(14, 0, 14, 12),
                VerticalAlignment = VerticalAlignment.Bottom
            };
            footer.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            footer.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            Grid.SetRow(footer, 2);
            root.Children.Add(footer);

            var cardNumberBadge = new Border
            {
                HorizontalAlignment = HorizontalAlignment.Left,
                Margin = new Thickness(0, 0, 0, 4),
                Padding = new Thickness(6, 2, 6, 2),
                CornerRadius = new CornerRadius(4),
                Background = BrushFromHex("#F1F5F9"),
                BorderBrush = BrushFromHex("#E2E8F0"),
                BorderThickness = new Thickness(1),
                Child = new TextBlock
                {
                    Text = $"CARD NUMBER: {request.CardNumber}",
                    FontSize = 6,
                    FontWeight = FontWeights.Bold,
                    Foreground = BrushFromHex("#64748B")
                }
            };
            Grid.SetRow(cardNumberBadge, 0);
            footer.Children.Add(cardNumberBadge);

            var barcodeContainer = new Border
            {
                Width = 220,
                Height = 32,
                HorizontalAlignment = HorizontalAlignment.Left,
                Background = Brushes.White,
                Child = request.BarcodeImage == null ? null : new Image
                {
                    Source = request.BarcodeImage,
                    Stretch = Stretch.Fill,
                    SnapsToDevicePixels = true
                }
            };
            if (request.BarcodeImage != null)
            {
                RenderOptions.SetBitmapScalingMode(barcodeContainer.Child, BitmapScalingMode.NearestNeighbor);
            }
            Grid.SetRow(barcodeContainer, 1);
            footer.Children.Add(barcodeContainer);

            return new Border
            {
                Width = 324,
                Height = 204,
                CornerRadius = new CornerRadius(12),
                Background = Brushes.White,
                BorderBrush = BrushFromHex("#CBD5E1"),
                BorderThickness = new Thickness(1),
                ClipToBounds = true,
                Child = root
            };
        }

        private static StackPanel CreateDetailGroup(string label, string? value)
        {
            var panel = new StackPanel { Margin = new Thickness(0, 0, 8, 4) };
            panel.Children.Add(new TextBlock
            {
                Text = label.ToUpper(),
                FontSize = 6,
                FontWeight = FontWeights.Bold,
                Foreground = BrushFromHex("#64748B")
            });
            panel.Children.Add(new TextBlock
            {
                Text = Fallback(value),
                FontSize = 9,
                FontWeight = FontWeights.Bold,
                Foreground = BrushFromHex("#0F172A")
            });
            return panel;
        }

        private static SolidColorBrush BrushFromHex(string value)
        {
            return new SolidColorBrush((Color)ColorConverter.ConvertFromString(value));
        }

        private static string Fallback(string? value)
        {
            return string.IsNullOrWhiteSpace(value) ? "--" : value.Trim();
        }
    }
}
