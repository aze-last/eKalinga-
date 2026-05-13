using AttendanceShiftingManagement.Helpers;
using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace AttendanceShiftingManagement.Services
{
    public static class WindowBrandingService
    {
        private const string DefaultIconUri = SystemProfileSettingsModel.DefaultLogoUri;

        public static void ApplyWindowIcon(Window window)
        {
            ArgumentNullException.ThrowIfNull(window);
            window.Icon = GetWindowIconSource();
        }

        public static string ResolveIconSource(SystemProfileSettingsModel settings)
        {
            if (!string.IsNullOrWhiteSpace(settings.LogoPath))
            {
                var trimmedPath = settings.LogoPath.Trim();
                if (File.Exists(trimmedPath) || IsPackUri(trimmedPath))
                {
                    return trimmedPath;
                }
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
            try
            {
                var icon = new BitmapImage();
                icon.BeginInit();
                icon.CacheOption = BitmapCacheOption.OnLoad;
                icon.UriSource = new Uri(uri, UriKind.Absolute);
                icon.EndInit();
                icon.Freeze();
                return icon;
            }
            catch
            {
                // Return an empty/null source if the resource is missing
                // This prevents the application from crashing during constructor invocation
                return null!;
            }
        }

        private static bool IsPackUri(string path)
        {
            return path.StartsWith("pack://", StringComparison.OrdinalIgnoreCase);
        }
    }
}
