using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace AttendanceShiftingManagement.Views
{
    public partial class PhotoCropDialog : Window
    {
        // ── Public result ─────────────────────────────────────────────
        public BitmapSource? CroppedImage { get; private set; }

        // ── Internal state ────────────────────────────────────────────
        private BitmapSource _source;
        private double _scale = 1.0;
        private Point _dragStart;
        private Point _originOnDragStart;
        private bool _isDragging;

        // Crop frame size on screen (matches XAML: 156×208)
        private const double FrameW = 156;
        private const double FrameH = 208;

        // Output size — 3× card dimensions for sharp print quality (78×104 @ 3×)
        private const double OutW = 234;
        private const double OutH = 312;

        public PhotoCropDialog(BitmapSource source)
        {
            _source = source;        // must be before InitializeComponent
            InitializeComponent();
            DragImage.Source = source;
            Loaded += OnLoaded;
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            // Fit image to fill the crop frame initially
            double scaleX = FrameW / _source.PixelWidth;
            double scaleY = FrameH / _source.PixelHeight;
            _scale = Math.Max(scaleX, scaleY);

            ZoomSlider.Minimum = _scale * 0.8;
            ZoomSlider.Maximum = _scale * 4.0;
            ZoomSlider.Value   = _scale;

            ApplyScale(_scale);
            CenterImage();
            UpdatePreview();
        }

        // ── Scale ─────────────────────────────────────────────────────
        private void ApplyScale(double scale)
        {
            _scale = scale;
            DragImage.Width  = _source.PixelWidth  * _scale;
            DragImage.Height = _source.PixelHeight * _scale;
        }

        private void CenterImage()
        {
            // Center of canvas
            double canvasW = DragCanvas.ActualWidth;
            double canvasH = DragCanvas.ActualHeight;

            ImageTranslate.X = (canvasW - DragImage.Width)  / 2.0;
            ImageTranslate.Y = (canvasH - DragImage.Height) / 2.0;
        }

        // ── Drag ──────────────────────────────────────────────────────
        private void Canvas_MouseDown(object sender, MouseButtonEventArgs e)
        {
            _isDragging = true;
            _dragStart  = e.GetPosition(DragCanvas);
            _originOnDragStart = new Point(ImageTranslate.X, ImageTranslate.Y);
            DragCanvas.CaptureMouse();
        }

        private void Canvas_MouseUp(object sender, MouseButtonEventArgs e)
        {
            _isDragging = false;
            DragCanvas.ReleaseMouseCapture();
            UpdatePreview();
        }

        private void Canvas_MouseMove(object sender, MouseEventArgs e)
        {
            if (!_isDragging) return;
            var pos   = e.GetPosition(DragCanvas);
            var delta = pos - _dragStart;
            ImageTranslate.X = _originOnDragStart.X + delta.X;
            ImageTranslate.Y = _originOnDragStart.Y + delta.Y;
        }

        // ── Scroll to zoom ────────────────────────────────────────────
        private void Canvas_MouseWheel(object sender, MouseWheelEventArgs e)
        {
            double factor = e.Delta > 0 ? 1.1 : 0.9;
            double newScale = Math.Clamp(_scale * factor,
                                         ZoomSlider.Minimum,
                                         ZoomSlider.Maximum);

            // Zoom toward mouse position
            var mouse = e.GetPosition(DragCanvas);
            double ratioX = (mouse.X - ImageTranslate.X) / DragImage.Width;
            double ratioY = (mouse.Y - ImageTranslate.Y) / DragImage.Height;

            ApplyScale(newScale);
            ZoomSlider.Value = newScale;

            ImageTranslate.X = mouse.X - ratioX * DragImage.Width;
            ImageTranslate.Y = mouse.Y - ratioY * DragImage.Height;

            UpdatePreview();
        }

        private void ZoomSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (DragImage == null) return;

            // Zoom toward center of crop frame
            double cx = DragCanvas.ActualWidth  / 2.0;
            double cy = DragCanvas.ActualHeight / 2.0;

            double ratioX = (cx - ImageTranslate.X) / DragImage.Width;
            double ratioY = (cy - ImageTranslate.Y) / DragImage.Height;

            ApplyScale(e.NewValue);

            ImageTranslate.X = cx - ratioX * DragImage.Width;
            ImageTranslate.Y = cy - ratioY * DragImage.Height;

            UpdatePreview();
        }

        // ── Live preview ──────────────────────────────────────────────
        private void UpdatePreview()
        {
            var cropped = RenderCrop(OutW, OutH);
            if (cropped != null)
                PreviewImage.Source = cropped;
        }

        // ── Core crop render ──────────────────────────────────────────
        private BitmapSource? RenderCrop(double outW, double outH)
        {
            if (!IsLoaded || DragCanvas.ActualWidth == 0) return null;

            double canvasW = DragCanvas.ActualWidth;
            double canvasH = DragCanvas.ActualHeight;

            // Top-left of crop frame in canvas coords
            double frameLeft = (canvasW - FrameW) / 2.0;
            double frameTop  = (canvasH - FrameH) / 2.0;

            // What part of the original image does the crop frame cover?
            double srcX = (frameLeft - ImageTranslate.X) / _scale;
            double srcY = (frameTop  - ImageTranslate.Y) / _scale;
            double srcW = FrameW / _scale;
            double srcH = FrameH / _scale;

            // Target size in WPF units (1/96 inch)
            // We want OutW (234) pixels at 288 DPI.
            // That corresponds to 78 units (234 * 96 / 288).
            double targetUnitsW = outW * (96.0 / 288.0);
            double targetUnitsH = outH * (96.0 / 288.0);

            var visual = new DrawingVisual();
            using (var dc = visual.RenderOpen())
            {
                // Draw the source image shifted so the cropped area starts at 0,0
                // And scaled so the srcW pixels cover targetUnitsW units.
                double drawScale = targetUnitsW / srcW;
                
                dc.DrawImage(_source,
                    new Rect(-srcX * drawScale,
                             -srcY * drawScale,
                             _source.PixelWidth * drawScale,
                             _source.PixelHeight * drawScale));
            }

            var rtb = new RenderTargetBitmap(
                (int)outW, (int)outH, 288, 288, PixelFormats.Pbgra32);
            rtb.Render(visual);
            rtb.Freeze();
            return rtb;
        }

        // ── Buttons ───────────────────────────────────────────────────
        private void Confirm_Click(object sender, RoutedEventArgs e)
        {
            CroppedImage = RenderCrop(OutW, OutH);
            DialogResult = true;
        }

        private void ChangePhoto_Click(object sender, RoutedEventArgs e)
        {
            var openFileDialog = new Microsoft.Win32.OpenFileDialog
            {
                Filter = "Image Files|*.jpg;*.jpeg;*.png;*.bmp",
                Title = "Select New Photo"
            };

            if (openFileDialog.ShowDialog() == true)
            {
                var newSource = Helpers.LocalImageLoader.Load(openFileDialog.FileName) as BitmapSource;
                if (newSource != null)
                {
                    _source = newSource;
                    DragImage.Source = _source;
                    OnLoaded(null!, null!); // Re-init scale/center
                }
            }
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
            => DialogResult = false;

        private void Close_Click(object sender, RoutedEventArgs e)
            => DialogResult = false;
    }
}
