using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace AttendanceShiftingManagement.Helpers
{
    public static class LocalImageLoader
    {
        public static ImageSource? Load(string? path)
        {
            const string DefaultAvatarPath = @"C:\Users\ASUS\Downloads\images\default-male-avatar-profile-icon-social-media-user-free-vector.jpg";

            if (string.IsNullOrWhiteSpace(path))
            {
                path = DefaultAvatarPath;
            }

            if (!string.IsNullOrWhiteSpace(path) && !path.StartsWith("pack://") && !path.StartsWith("http://") && !path.StartsWith("https://"))
            {
                if (!File.Exists(path))
                {
                    path = DefaultAvatarPath;
                }
            }

            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            {
                if (!string.IsNullOrWhiteSpace(path) && Uri.TryCreate(path, UriKind.Absolute, out var uri))
                {
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
                        if (path != DefaultAvatarPath && File.Exists(DefaultAvatarPath))
                        {
                            return Load(DefaultAvatarPath);
                        }
                        return null;
                    }
                }

                if (path != DefaultAvatarPath && File.Exists(DefaultAvatarPath))
                {
                    return Load(DefaultAvatarPath);
                }
                return null;
            }

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
                if (path != DefaultAvatarPath && File.Exists(DefaultAvatarPath))
                {
                    return Load(DefaultAvatarPath);
                }
                return null;
            }
        }
    }
}
