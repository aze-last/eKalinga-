using AttendanceShiftingManagement.Views;
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
            // If running in dev, fallback to project path
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
                    Width = 220,
                    Opacity = 0.22,
                    Stretch = Stretch.Uniform,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center,
                    Margin = new Thickness(0, 30, 0, 0),
                    SnapsToDevicePixels = true
                };
                RenderOptions.SetBitmapScalingMode(watermark, BitmapScalingMode.HighQuality);
                
                Grid.SetRowSpan(watermark, 2); // Span both Header (Row 0) and Body (Row 1)
                root.Children.Add(watermark);
            }

            root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(38) });
            root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

            // HEADER with GRADIENT
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
                FontSize = 15,
                FontWeight = FontWeights.ExtraBold,
                Foreground = Brushes.White,
                VerticalAlignment = VerticalAlignment.Center
            });

            var cardTypeBadge = new Border
            {
                Background = new SolidColorBrush(Color.FromArgb(51, 255, 255, 255)), // rgba(255,255,255,0.2)
                CornerRadius = new CornerRadius(4),
                Padding = new Thickness(8, 3, 8, 3),
                VerticalAlignment = VerticalAlignment.Center,
                Child = new TextBlock
                {
                    Text = "Barangay Beneficiary ID".ToUpper(),
                    FontSize = 8,
                    FontWeight = FontWeights.Bold,
                    Foreground = Brushes.White
                }
            };
            Grid.SetColumn(cardTypeBadge, 1);
            headerContent.Children.Add(cardTypeBadge);
            header.Child = headerContent;
            root.Children.Add(header);

            // BODY
            var body = new Grid
            {
                Margin = new Thickness(14, 12, 14, 12)
            };
            body.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(78) });
            body.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(14) });
            body.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            Grid.SetRow(body, 1);
            root.Children.Add(body);

            // PHOTO
            var photoBorder = new Border
            {
                Width = 78,
                Height = 104,
                CornerRadius = new CornerRadius(8),
                Background = BrushFromHex("#F8FAFC"),
                BorderBrush = Brushes.White,
                BorderThickness = new Thickness(3),
                ClipToBounds = true,
                VerticalAlignment = VerticalAlignment.Top,
                Effect = new System.Windows.Media.Effects.DropShadowEffect
                {
                    BlurRadius = 10,
                    ShadowDepth = 6,
                    Direction = 315,
                    Opacity = 0.15,
                    Color = Colors.Black
                }
            };

            photoBorder.Child = request.PhotoImage == null
                ? new TextBlock
                {
                    Text = "NO PHOTO",
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center,
                    FontSize = 10,
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
                VerticalAlignment = VerticalAlignment.Top
            };

            var nameBlock = new TextBlock
            {
                Text = request.FullName.ToUpper(),
                FontSize = 16,
                FontWeight = FontWeights.Black,
                Foreground = BrushFromHex("#0F172A"),
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 0, 0, 8),
                Effect = CreateTextShadow()
            };
            details.Children.Add(nameBlock);

            details.Children.Add(CreateDetailGroup("Beneficiary ID", request.BeneficiaryId));
            details.Children.Add(CreateDetailGroup("Civil Registry ID", request.CivilRegistryId));

            Grid.SetColumn(details, 2);
            body.Children.Add(details);

            // FOOTER - Card Number Badge
            var cardNumberBadge = new Border
            {
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Bottom,
                Margin = new Thickness(14, 0, 0, 12),
                Padding = new Thickness(10, 4, 10, 4),
                CornerRadius = new CornerRadius(6),
                Background = new SolidColorBrush(Color.FromArgb(230, 241, 245, 249)), // rgba(241, 245, 249, 0.9)
                BorderBrush = BrushFromHex("#CBD5E1"),
                BorderThickness = new Thickness(1),
                Child = new TextBlock
                {
                    Text = request.CardNumber,
                    FontSize = 9.5,
                    FontWeight = FontWeights.ExtraBold,
                    Foreground = BrushFromHex("#334155")
                }
            };
            Grid.SetRow(cardNumberBadge, 1);
            root.Children.Add(cardNumberBadge);

            // QR CODE
            var qrBorder = new Border
            {
                Width = 54,
                Height = 54,
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Bottom,
                Margin = new Thickness(0, 0, 14, 12),
                Padding = new Thickness(2),
                Background = Brushes.White,
                BorderBrush = BrushFromHex("#CBD5E1"),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(6),
                Effect = new System.Windows.Media.Effects.DropShadowEffect
                {
                    BlurRadius = 4,
                    ShadowDepth = 2,
                    Opacity = 0.05,
                    Color = Colors.Black
                },
                Child = request.QrImage == null ? null : BuildQrImage(request.QrImage)
            };
            Grid.SetRow(qrBorder, 1);
            root.Children.Add(qrBorder);

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
            var panel = new StackPanel { Margin = new Thickness(0, 0, 0, 6) };
            panel.Children.Add(new TextBlock
            {
                Text = label.ToUpper(),
                FontSize = 7.5,
                FontWeight = FontWeights.Bold,
                Foreground = BrushFromHex("#64748B")
            });
            panel.Children.Add(new TextBlock
            {
                Text = Fallback(value),
                FontSize = 10.5,
                FontWeight = FontWeights.Bold,
                Foreground = BrushFromHex("#1E293B"),
                Effect = CreateTextShadow()
            });
            return panel;
        }

        private static System.Windows.Media.Effects.DropShadowEffect CreateTextShadow()
        {
            return new System.Windows.Media.Effects.DropShadowEffect
            {
                Color = Colors.White,
                Opacity = 0.8,
                BlurRadius = 0,
                ShadowDepth = 1,
                Direction = 315
            };
        }

        private static SolidColorBrush BrushFromHex(string value)
        {
            return new SolidColorBrush((Color)ColorConverter.ConvertFromString(value));
        }

        private static string Fallback(string? value)
        {
            return string.IsNullOrWhiteSpace(value) ? "--" : value.Trim();
        }

        private static Image BuildQrImage(BitmapSource source)
        {
            var image = new Image
            {
                Source = source,
                Stretch = Stretch.Uniform,
                SnapsToDevicePixels = true
            };

            RenderOptions.SetBitmapScalingMode(image, BitmapScalingMode.NearestNeighbor);
            return image;
        }
    }
}
