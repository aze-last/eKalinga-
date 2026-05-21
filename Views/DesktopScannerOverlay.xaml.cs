using AForge.Video;
using AForge.Video.DirectShow;
using AttendanceShiftingManagement.Services;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace AttendanceShiftingManagement.Views
{
    public partial class DesktopScannerOverlay : UserControl
    {
        private FilterInfoCollection? _videoDevices;
        private VideoCaptureDevice? _videoSource;
        private readonly DispatcherTimer _scanTimer;
        private bool _isScanning = true;

        public event Action<string>? QrCodeScanned;
        public event Action? Closed;

        public DesktopScannerOverlay()
        {
            InitializeComponent();
            
            _scanTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(500)
            };
            _scanTimer.Tick += ScanTimer_Tick;
            
            Loaded += DesktopScannerOverlay_Loaded;
            Unloaded += DesktopScannerOverlay_Unloaded;
        }

        private void DesktopScannerOverlay_Loaded(object sender, RoutedEventArgs e)
        {
            try
            {
                _videoDevices = new FilterInfoCollection(FilterCategory.VideoInputDevice);
                if (_videoDevices.Count == 0)
                {
                    StatusText.Text = "No camera devices found.";
                    return;
                }

                foreach (FilterInfo device in _videoDevices)
                {
                    CameraSelector.Items.Add(device.Name);
                }

                CameraSelector.SelectedIndex = 0;
            }
            catch (Exception ex)
            {
                StatusText.Text = $"Error initializing camera: {ex.Message}";
            }
        }

        private void DesktopScannerOverlay_Unloaded(object sender, RoutedEventArgs e)
        {
            StopCamera();
        }

        private void CameraSelector_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            StartCamera();
        }

        private void StartCamera()
        {
            StopCamera();

            if (CameraSelector.SelectedIndex < 0 || _videoDevices == null) return;

            try
            {
                _videoSource = new VideoCaptureDevice(_videoDevices[CameraSelector.SelectedIndex].MonikerString);
                _videoSource.NewFrame += VideoSource_NewFrame;
                _videoSource.Start();
                _isScanning = false; // Disable automatic scanning
                StatusText.Text = "Camera active. Position ID and click 'CAPTURE & SCAN ID'.";
            }
            catch (Exception ex)
            {
                StatusText.Text = $"Error starting camera: {ex.Message}";
            }
        }

        private void StopCamera()
        {
            _scanTimer.Stop();
            if (_videoSource != null && _videoSource.IsRunning)
            {
                _videoSource.SignalToStop();
                _videoSource.NewFrame -= VideoSource_NewFrame;
                _videoSource = null;
            }
        }

        private void VideoSource_NewFrame(object sender, NewFrameEventArgs eventArgs)
        {
            try
            {
                using var bitmap = (Bitmap)eventArgs.Frame.Clone();
                
                Dispatcher.Invoke(() =>
                {
                    CameraFeed.Source = BitmapToImageSource(bitmap);
                }, DispatcherPriority.Render);
            }
            catch
            {
                // Ignore frame processing errors
            }
        }

        private async void Capture_Click(object sender, RoutedEventArgs e)
        {
            if (CameraFeed.Source is not BitmapSource bitmapSource)
            {
                StatusText.Text = "No camera feed available.";
                return;
            }

            StatusText.Text = "Capturing ID and scanning QR...";
            
            var bitmap = BitmapSourceToBitmap(bitmapSource);
            if (bitmap == null) return;

            try
            {
                var result = await Task.Run(() => TryDecode(bitmap));
                if (!string.IsNullOrWhiteSpace(result))
                {
                    StatusText.Text = "ID Captured and QR Decoded!";
                    QrCodeScanned?.Invoke(result);
                }
                else
                {
                    StatusText.Text = "ID Captured, but no QR code found. Please realign.";
                }
            }
            finally
            {
                bitmap.Dispose();
            }
        }

        private void ScanTimer_Tick(object? sender, EventArgs e)
        {
            // Automatic scanning disabled as per user request for "Capture then Scan" workflow
        }

        private string? TryDecode(Bitmap bitmap)
        {
            var reader = new ZXing.BarcodeReaderGeneric
            {
                AutoRotate = true,
                Options = new ZXing.Common.DecodingOptions
                {
                    TryHarder = true,
                    TryInverted = true,
                    PossibleFormats = new List<ZXing.BarcodeFormat> 
                    { 
                        ZXing.BarcodeFormat.QR_CODE, 
                        ZXing.BarcodeFormat.PDF_417,
                        ZXing.BarcodeFormat.CODE_128
                    }
                }
            };

            try
            {
                var source = new ZXing.Windows.Compatibility.BitmapLuminanceSource(bitmap);
                var result = reader.Decode(source);
                return result?.Text;
            }
            catch
            {
                return null;
            }
        }

        private BitmapSource BitmapToImageSource(Bitmap bitmap)
        {
            using var memory = new MemoryStream();
            bitmap.Save(memory, ImageFormat.Bmp);
            memory.Position = 0;
            var bitmapImage = new BitmapImage();
            bitmapImage.BeginInit();
            bitmapImage.StreamSource = memory;
            bitmapImage.CacheOption = BitmapCacheOption.OnLoad;
            bitmapImage.EndInit();
            bitmapImage.Freeze();
            return bitmapImage;
        }

        private Bitmap? BitmapSourceToBitmap(BitmapSource bitmapSource)
        {
            try
            {
                var encoder = new BmpBitmapEncoder();
                encoder.Frames.Add(BitmapFrame.Create(bitmapSource));
                using var stream = new MemoryStream();
                encoder.Save(stream);
                return new Bitmap(stream);
            }
            catch
            {
                return null;
            }
        }

        private void Close_Click(object sender, RoutedEventArgs e)
        {
            StopCamera();
            Closed?.Invoke();
        }
    }
}
