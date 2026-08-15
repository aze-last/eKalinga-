using AForge.Video;
using AForge.Video.DirectShow;
using AttendanceShiftingManagement.Services;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Threading.Tasks;
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
        private volatile bool _isScanning = true;
        private volatile bool _isDecoding = false;
        private DateTime _lastDecodeAttempt = DateTime.MinValue;
        private INotifyPropertyChanged? _observedDataContext;

        public event Action<string>? QrCodeScanned;
        public event Action? Closed;

        public DesktopScannerOverlay()
        {
            InitializeComponent();

            Loaded += DesktopScannerOverlay_Loaded;
            Unloaded += DesktopScannerOverlay_Unloaded;
            IsVisibleChanged += DesktopScannerOverlay_IsVisibleChanged;
            DataContextChanged += DesktopScannerOverlay_DataContextChanged;
        }

        private void DesktopScannerOverlay_Loaded(object sender, RoutedEventArgs e)
        {
            InitializeCameraDevices();
            if (IsVisible)
            {
                StartCamera();
            }
        }

        private void DesktopScannerOverlay_Unloaded(object sender, RoutedEventArgs e)
        {
            StopCamera();
            UnhookDataContext();
        }

        private void DesktopScannerOverlay_IsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (IsVisible)
            {
                InitializeCameraDevices();
                _isScanning = true;
                _isDecoding = false;
                StartCamera();
            }
            else
            {
                StopCamera();
            }
        }

        private void DesktopScannerOverlay_DataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            UnhookDataContext();

            if (DataContext is INotifyPropertyChanged npc)
            {
                _observedDataContext = npc;
                _observedDataContext.PropertyChanged += ObservedDataContext_PropertyChanged;
            }
        }

        private void ObservedDataContext_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == "IsScannedResultVisible")
            {
                var isResultVisible = false;
                if (DataContext != null)
                {
                    var prop = DataContext.GetType().GetProperty("IsScannedResultVisible");
                    if (prop?.GetValue(DataContext) is bool val)
                    {
                        isResultVisible = val;
                    }
                }

                if (!isResultVisible && IsVisible)
                {
                    // Scan result was dismissed/cancelled, re-enable live QR scanner
                    _isScanning = true;
                    _isDecoding = false;
                    Dispatcher.BeginInvoke(() =>
                    {
                        StatusText.Text = "Camera active. Point at a beneficiary QR code or click Capture.";
                    });
                }
            }
        }

        private void UnhookDataContext()
        {
            if (_observedDataContext != null)
            {
                _observedDataContext.PropertyChanged -= ObservedDataContext_PropertyChanged;
                _observedDataContext = null;
            }
        }

        private void InitializeCameraDevices()
        {
            try
            {
                if (_videoDevices == null || _videoDevices.Count == 0)
                {
                    _videoDevices = new FilterInfoCollection(FilterCategory.VideoInputDevice);
                    CameraSelector.Items.Clear();

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
            }
            catch (Exception ex)
            {
                StatusText.Text = $"Error initializing camera: {ex.Message}";
            }
        }

        private void CameraSelector_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (IsVisible)
            {
                StartCamera();
            }
        }

        private void StartCamera()
        {
            StopCamera();

            if (CameraSelector.SelectedIndex < 0 || _videoDevices == null || _videoDevices.Count == 0)
            {
                return;
            }

            try
            {
                _videoSource = new VideoCaptureDevice(_videoDevices[CameraSelector.SelectedIndex].MonikerString);
                _videoSource.NewFrame += VideoSource_NewFrame;
                _videoSource.Start();

                _isScanning = true;
                _isDecoding = false;
                _lastDecodeAttempt = DateTime.MinValue;

                StatusText.Text = "Camera active. Point at a beneficiary QR code or click Capture.";
            }
            catch (Exception ex)
            {
                StatusText.Text = $"Error starting camera: {ex.Message}";
            }
        }

        private void StopCamera()
        {
            _isScanning = false;
            _isDecoding = false;

            if (_videoSource != null)
            {
                try
                {
                    _videoSource.NewFrame -= VideoSource_NewFrame;
                    if (_videoSource.IsRunning)
                    {
                        _videoSource.SignalToStop();
                    }
                }
                catch
                {
                    // Ignore stop errors
                }
                finally
                {
                    _videoSource = null;
                }
            }
        }

        private void VideoSource_NewFrame(object sender, NewFrameEventArgs eventArgs)
        {
            if (!IsVisible) return;

            try
            {
                using var bitmap = (Bitmap)eventArgs.Frame.Clone();
                var imageSource = BitmapToImageSource(bitmap);

                Dispatcher.BeginInvoke(DispatcherPriority.Render, new Action(() =>
                {
                    if (IsVisible && _videoSource != null)
                    {
                        CameraFeed.Source = imageSource;
                    }
                }));

                // Auto QR capture
                if (_isScanning && !_isDecoding && (DateTime.UtcNow - _lastDecodeAttempt).TotalMilliseconds >= 250)
                {
                    _lastDecodeAttempt = DateTime.UtcNow;
                    _isDecoding = true;

                    var cloneForDecode = (Bitmap)eventArgs.Frame.Clone();
                    _ = Task.Run(() =>
                    {
                        try
                        {
                            var text = TryDecode(cloneForDecode);
                            if (!string.IsNullOrWhiteSpace(text))
                            {
                                _isScanning = false;
                                Dispatcher.BeginInvoke(new Action(() =>
                                {
                                    if (IsVisible)
                                    {
                                        StatusText.Text = "QR Code Detected!";
                                        QrCodeScanned?.Invoke(text);
                                    }
                                }));
                            }
                        }
                        catch
                        {
                            // Ignore decode errors
                        }
                        finally
                        {
                            cloneForDecode.Dispose();
                            _isDecoding = false;
                        }
                    });
                }
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
                    _isScanning = false;
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

        private string? TryDecode(Bitmap bitmap)
        {
            try
            {
                var reader = new ZXing.Windows.Compatibility.BarcodeReader
                {
                    AutoRotate = true,
                    Options = new ZXing.Common.DecodingOptions
                    {
                        TryHarder = true,
                        TryInverted = true,
                        PossibleFormats = new List<ZXing.BarcodeFormat>
                        {
                            ZXing.BarcodeFormat.QR_CODE,
                            ZXing.BarcodeFormat.CODE_128,
                            ZXing.BarcodeFormat.PDF_417
                        }
                    }
                };

                var result = reader.Decode(bitmap);
                if (result != null && !string.IsNullOrWhiteSpace(result.Text))
                {
                    return result.Text.Trim();
                }

                // If direct decode didn't catch it, run multi-scale decode via QrCodeToolkitService
                using var ms = new MemoryStream();
                bitmap.Save(ms, ImageFormat.Png);
                ms.Position = 0;
                return QrCodeToolkitService.TryDecodePayload(ms);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[DesktopScannerOverlay] Decode error: {ex.Message}");
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
