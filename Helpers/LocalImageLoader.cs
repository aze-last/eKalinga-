using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace AttendanceShiftingManagement.Helpers
{
    public static class LocalImageLoader
    {
        public static ImageSource? Load(string? path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return null;
            }

            if (File.Exists(path))
            {
                try
                {
                    using var stream = File.OpenRead(path);
                    var fileImage = new BitmapImage();
                    fileImage.BeginInit();
                    fileImage.CacheOption = BitmapCacheOption.OnLoad;
                    fileImage.StreamSource = stream;
                    fileImage.EndInit();
                    fileImage.Freeze();
                    return fileImage;
                }
                catch
                {
                    return null;
                }
            }

            if (!Uri.TryCreate(path, UriKind.Absolute, out var uri))
            {
                return null;
            }

            try
            {
                var uriImage = new BitmapImage();
                uriImage.BeginInit();
                uriImage.CacheOption = BitmapCacheOption.OnLoad;
                uriImage.UriSource = uri;
                uriImage.EndInit();
                uriImage.Freeze();
                return uriImage;
            }
            catch
            {
                return null;
            }
        }
    }
}
