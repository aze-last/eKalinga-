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
                    Width = 160,
                    Opacity = 0.04,
                    Stretch = Stretch.Uniform,
                    HorizontalAlignment = HorizontalAlignment.Right,
                    VerticalAlignment = VerticalAlignment.Bottom,
                    Margin = new Thickness(0, 0, -20, -20),
                    SnapsToDevicePixels = true
                };
                RenderOptions.SetBitmapScalingMode(watermark, BitmapScalingMode.HighQuality);
                
                root.Children.Add(watermark);
            }

            // HEADER
            var header = new Border
            {
                Background = BrushFromHex("#1E4E89"), // Brand Midnight Blue
                Height = 50,
                VerticalAlignment = VerticalAlignment.Top,
                Padding = new Thickness(16, 0, 16, 0)
            };
            
            var headerContent = new Grid();
            headerContent.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            headerContent.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var logoStack = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
            logoStack.Children.Add(new TextBlock
            {
                Text = "eKalinga",
                FontSize = 16,
                FontWeight = FontWeights.Black,
                Foreground = Brushes.White,
                Margin = new Thickness(0, 0, 1, 0)
            });
            logoStack.Children.Add(new TextBlock
            {
                Text = "+",
                FontSize = 16,
                FontWeight = FontWeights.Black,
                Foreground = BrushFromHex("#F59E0B"), // Brand Gold
            });
            headerContent.Children.Add(logoStack);

            var cardTypeBadge = new Border
            {
                Background = BrushFromHex("#F59E0B"),
                CornerRadius = new CornerRadius(12),
                Padding = new Thickness(10, 4, 10, 4),
                VerticalAlignment = VerticalAlignment.Center,
                Child = new TextBlock
                {
                    Text = "BENEFICIARY ID",
                    FontSize = 8,
                    FontWeight = FontWeights.Bold,
                    Foreground = BrushFromHex("#FFFFFF")
                }
            };
            Grid.SetColumn(cardTypeBadge, 1);
            headerContent.Children.Add(cardTypeBadge);
            header.Child = headerContent;
            root.Children.Add(header);

            // ACCENT LINE
            var accentLine = new Border
            {
                Height = 3,
                Background = BrushFromHex("#F59E0B"),
                VerticalAlignment = VerticalAlignment.Top,
                Margin = new Thickness(0, 50, 0, 0)
            };
            root.Children.Add(accentLine);

            // BODY
            var body = new Grid
            {
                Margin = new Thickness(16, 65, 16, 16)
            };
            body.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(80) }); // Photo
            body.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(16) }); // Spacer
            body.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) }); // Details
            
            root.Children.Add(body);

            // PHOTO
            var photoBorder = new Border
            {
                Width = 80,
                Height = 100,
                CornerRadius = new CornerRadius(8),
                Background = BrushFromHex("#F8FAFC"),
                BorderBrush = BrushFromHex("#E2E8F0"),
                BorderThickness = new Thickness(1),
                ClipToBounds = true,
                VerticalAlignment = VerticalAlignment.Top,
                Effect = new System.Windows.Media.Effects.DropShadowEffect
                {
                    BlurRadius = 8,
                    ShadowDepth = 2,
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
                VerticalAlignment = VerticalAlignment.Top,
                Margin = new Thickness(0, 0, 0, 0)
            };

            var nameLabel = new TextBlock
            {
                Text = "FULL NAME",
                FontSize = 7,
                FontWeight = FontWeights.Bold,
                Foreground = BrushFromHex("#94A3B8"),
                Margin = new Thickness(0, 0, 0, 2)
            };
            details.Children.Add(nameLabel);

            var nameBlock = new TextBlock
            {
                Text = request.FullName.ToUpper(),
                FontSize = 16,
                FontWeight = FontWeights.Black,
                Foreground = BrushFromHex("#0F172A"),
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 0, 0, 8),
                LineHeight = 18,
                LineStackingStrategy = LineStackingStrategy.BlockLineHeight
            };
            details.Children.Add(nameBlock);

            var fieldsGrid = new UniformGrid { Columns = 2 };
            fieldsGrid.Children.Add(CreateDetailGroup("BENEFICIARY ID", request.BeneficiaryId));
            fieldsGrid.Children.Add(CreateDetailGroup("CIVIL REG ID", request.CivilRegistryId));
            details.Children.Add(fieldsGrid);

            // BARCODE AND CARD NUMBER
            var footer = new Grid
            {
                Margin = new Thickness(0, 10, 0, 0),
                VerticalAlignment = VerticalAlignment.Bottom
            };
            footer.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            footer.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            
            var barcodeContainer = new Border
            {
                Height = 28,
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
            Grid.SetColumn(barcodeContainer, 0);
            footer.Children.Add(barcodeContainer);

            var cardNumberBlock = new TextBlock
            {
                Text = $"CARD NO.\n{request.CardNumber}",
                FontSize = 6,
                FontWeight = FontWeights.ExtraBold,
                Foreground = BrushFromHex("#64748B"),
                TextAlignment = TextAlignment.Right,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(8, 0, 0, 0)
            };
            Grid.SetColumn(cardNumberBlock, 1);
            footer.Children.Add(cardNumberBlock);

            details.Children.Add(footer);

            Grid.SetColumn(details, 2);
            body.Children.Add(details);

            return new Border
            {
                Width = 324,
                Height = 204,
                CornerRadius = new CornerRadius(16),
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
