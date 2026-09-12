using System.Windows;
using System.Windows.Controls;

namespace AttendanceShiftingManagement.Helpers
{
    public static class PasswordBoxHelper
    {
        public static readonly DependencyProperty BoundPasswordProperty =
            DependencyProperty.RegisterAttached(
                "BoundPassword",
                typeof(string),
                typeof(PasswordBoxHelper),
                new FrameworkPropertyMetadata(
                    string.Empty,
                    FrameworkPropertyMetadataOptions.BindsTwoWayByDefault,
                    OnBoundPasswordChanged));

        public static readonly DependencyProperty BindPasswordProperty =
            DependencyProperty.RegisterAttached(
                "BindPassword",
                typeof(bool),
                typeof(PasswordBoxHelper),
                new PropertyMetadata(false, OnBindPasswordChanged));

        private static readonly DependencyProperty IsUpdatingProperty =
            DependencyProperty.RegisterAttached(
                "IsUpdating",
                typeof(bool),
                typeof(PasswordBoxHelper),
                new PropertyMetadata(false));

        private static readonly DependencyProperty IsHookedProperty =
            DependencyProperty.RegisterAttached(
                "IsHooked",
                typeof(bool),
                typeof(PasswordBoxHelper),
                new PropertyMetadata(false));

        public static string GetBoundPassword(DependencyObject d) =>
            (string)d.GetValue(BoundPasswordProperty);

        public static void SetBoundPassword(DependencyObject d, string value) =>
            d.SetValue(BoundPasswordProperty, value);

        public static bool GetBindPassword(DependencyObject d) =>
            (bool)d.GetValue(BindPasswordProperty);

        public static void SetBindPassword(DependencyObject d, bool value) =>
            d.SetValue(BindPasswordProperty, value);

        private static bool GetIsUpdating(DependencyObject d) =>
            (bool)d.GetValue(IsUpdatingProperty);

        private static void SetIsUpdating(DependencyObject d, bool value) =>
            d.SetValue(IsUpdatingProperty, value);

        private static bool GetIsHooked(DependencyObject d) =>
            (bool)d.GetValue(IsHookedProperty);

        private static void SetIsHooked(DependencyObject d, bool value) =>
            d.SetValue(IsHookedProperty, value);

        private static void EnsureHooked(PasswordBox box)
        {
            if (!GetIsHooked(box))
            {
                box.PasswordChanged += HandlePasswordChanged;
                SetIsHooked(box, true);
            }
        }

        private static void OnBoundPasswordChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is not PasswordBox box)
            {
                return;
            }

            EnsureHooked(box);

            if (GetIsUpdating(box))
            {
                return;
            }

            var newPassword = (string?)e.NewValue ?? string.Empty;
            if (box.Password != newPassword)
            {
                box.Password = newPassword;
            }
        }

        private static void OnBindPasswordChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is not PasswordBox box)
            {
                return;
            }

            if ((bool)e.NewValue)
            {
                EnsureHooked(box);
                var current = GetBoundPassword(box);
                if (current != null && box.Password != current)
                {
                    box.Password = current;
                }
            }
        }

        private static void HandlePasswordChanged(object sender, RoutedEventArgs e)
        {
            if (sender is not PasswordBox box)
            {
                return;
            }

            SetIsUpdating(box, true);
            try
            {
                SetBoundPassword(box, box.Password);
            }
            finally
            {
                SetIsUpdating(box, false);
            }
        }
    }
}
