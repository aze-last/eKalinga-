using AttendanceShiftingManagement.Helpers;
using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace AttendanceShiftingManagement.Services
{
    public static class WindowBrandingService
    {
        private const string DefaultIconUri = "pack://application:,,,/Images/app-icon.ico";

        public static void ApplyWindowIcon(Window window)
        {
            ArgumentNullException.ThrowIfNull(window);
            window.Icon = GetWindowIconSource();
        }

        public static string ResolveIconSource(SystemProfileSettingsModel settings)
        {
            if (!string.IsNullOrWhiteSpace(settings.LogoPath) && File.Exists(settings.LogoPath))
            {
                return settings.LogoPath.Trim();
            }

            return DefaultIconUri;
        }

        private static ImageSource GetWindowIconSource()
        {
            var settings = SystemProfileSettingsService.Load();
            var source = ResolveIconSource(settings);

            if (File.Exists(source))
            {
                return LocalImageLoader.Load(source)
                    ?? LoadPackIcon(DefaultIconUri);
            }

            return LoadPackIcon(source);
        }

        private static ImageSource LoadPackIcon(string uri)
        {
            var icon = new BitmapImage();
            icon.BeginInit();
            icon.CacheOption = BitmapCacheOption.OnLoad;
            icon.UriSource = new Uri(uri, UriKind.Absolute);
            icon.EndInit();
            icon.Freeze();
            return icon;
        }
    }
}
