using MaterialDesignThemes.Wpf;
using System.Windows;
using System.Windows.Media;

namespace AttendanceShiftingManagement.Services
{
    public enum AppAppearanceMode
    {
        Light = 0,
        Dark = 1
    }

    public static class AppThemeService
    {
        private static readonly Color PrimaryColor = Parse("#2F5B98");
        private static readonly Color SecondaryColor = Parse("#D1AE68");

        public static AppAppearanceMode CurrentMode { get; private set; } = AppAppearanceMode.Light;

        public static void ApplySavedTheme()
        {
            Apply(SystemProfileSettingsService.Load().AppearanceMode);
        }

        public static void Apply(AppAppearanceMode mode)
        {
            if (Application.Current is null)
            {
                CurrentMode = mode;
                return;
            }

            CurrentMode = mode;

            ApplyMaterialTheme(mode);

            var palette = mode == AppAppearanceMode.Dark
                ? ThemePalette.Dark
                : ThemePalette.Light;

            ApplyBrush("BrandMidnightBrush", palette.TextPrimary);
            ApplyBrush("BrandYellowBrush", palette.Gold);
            ApplyBrush("BrandGoldTextBrush", palette.GoldForeground);
            ApplyBrush("BrandSuccessBrush", palette.Success);
            ApplyBrush("BrandRedBrush", palette.Danger);
            ApplyBrush("BrandSurfaceBrush", palette.Surface);
            ApplyBrush("BrandBorderBrush", palette.Border);
            ApplyBrush("BrandTextSecondaryBrush", palette.TextSecondary);

            ApplyBrush("ThemeWindowShellBrush", palette.WindowShell);
            ApplyBrush("ThemeWindowShellBorderBrush", palette.WindowShellBorder);
            ApplyBrush("ThemeCardBrush", palette.Card);
            ApplyBrush("ThemeCardRaisedBrush", palette.CardRaised);
            ApplyBrush("ThemeCardSubtleBrush", palette.CardSubtle);
            ApplyBrush("ThemeInputSurfaceBrush", palette.InputSurface);
            ApplyBrush("ThemeInputBorderBrush", palette.InputBorder);
            ApplyBrush("ThemePrimaryActionBrush", palette.PrimaryAction);
            ApplyBrush("ThemePrimaryActionForegroundBrush", palette.PrimaryActionForeground);
            ApplyBrush("ThemeSecondaryActionBrush", palette.SecondaryAction);
            ApplyBrush("ThemeSecondaryActionBorderBrush", palette.SecondaryActionBorder);
            ApplyBrush("ThemeSecondaryActionForegroundBrush", palette.SecondaryActionForeground);
            ApplyBrush("ThemeWarningSurfaceBrush", palette.WarningSurface);
            ApplyBrush("ThemeWarningBorderBrush", palette.WarningBorder);
            ApplyBrush("ThemeWarningForegroundBrush", palette.WarningForeground);
            ApplyBrush("ThemeAccentBrush", palette.Accent);
            ApplyBrush("ThemeAccentSoftBrush", palette.AccentSoft);
            ApplyBrush("ThemeAccentBorderBrush", palette.AccentBorder);
            ApplyBrush("ThemeTabStripBrush", palette.TabStrip);
            ApplyBrush("ThemeTabStripBorderBrush", palette.TabStripBorder);
            ApplyBrush("ThemeSelectionGlowBrush", palette.SelectionGlow);
        }

        private static void ApplyMaterialTheme(AppAppearanceMode mode)
        {
            var paletteHelper = new PaletteHelper();
            Theme theme = paletteHelper.GetTheme();
            theme.SetPrimaryColor(PrimaryColor);
            theme.SetSecondaryColor(SecondaryColor);
            theme.SetBaseTheme(mode == AppAppearanceMode.Dark ? BaseTheme.Dark : BaseTheme.Light);
            paletteHelper.SetTheme(theme);
        }

        private static void ApplyBrush(string resourceKey, Color color)
        {
            if (Application.Current.Resources[resourceKey] is SolidColorBrush existingBrush && !existingBrush.IsFrozen)
            {
                existingBrush.Color = color;
                return;
            }

            Application.Current.Resources[resourceKey] = new SolidColorBrush(color);
        }

        private static Color Parse(string colorCode)
        {
            return (Color)ColorConverter.ConvertFromString(colorCode);
        }

        private sealed record ThemePalette(
            Color WindowShell,
            Color WindowShellBorder,
            Color Surface,
            Color Card,
            Color CardRaised,
            Color CardSubtle,
            Color Border,
            Color InputSurface,
            Color InputBorder,
            Color TextPrimary,
            Color TextSecondary,
            Color Accent,
            Color AccentSoft,
            Color AccentBorder,
            Color Gold,
            Color GoldForeground,
            Color PrimaryAction,
            Color PrimaryActionForeground,
            Color SecondaryAction,
            Color SecondaryActionBorder,
            Color SecondaryActionForeground,
            Color WarningSurface,
            Color WarningBorder,
            Color WarningForeground,
            Color Success,
            Color Danger,
            Color TabStrip,
            Color TabStripBorder,
            Color SelectionGlow)
        {
            public static ThemePalette Light { get; } = new(
                WindowShell: Parse("#EEF2F7"),
                WindowShellBorder: Parse("#D5DDE8"),
                Surface: Parse("#F3F6FA"),
                Card: Parse("#FFFFFF"),
                CardRaised: Parse("#FAFCFF"),
                CardSubtle: Parse("#F4F8FC"),
                Border: Parse("#D4DCE8"),
                InputSurface: Parse("#FFFFFF"),
                InputBorder: Parse("#CCD6E3"),
                TextPrimary: Parse("#10203A"),
                TextSecondary: Parse("#5F6B7A"),
                Accent: Parse("#2F5B98"),
                AccentSoft: Parse("#E9F0FB"),
                AccentBorder: Parse("#BFD0EA"),
                Gold: Parse("#D1AE68"),
                GoldForeground: Parse("#35270B"),
                PrimaryAction: Parse("#1E4E89"),
                PrimaryActionForeground: Parse("#FFFFFF"),
                SecondaryAction: Parse("#F8FAFD"),
                SecondaryActionBorder: Parse("#CCD6E3"),
                SecondaryActionForeground: Parse("#20314D"),
                WarningSurface: Parse("#FDF4DE"),
                WarningBorder: Parse("#EBCF8B"),
                WarningForeground: Parse("#7A5717"),
                Success: Parse("#217650"),
                Danger: Parse("#B24A4A"),
                TabStrip: Parse("#EFF4FA"),
                TabStripBorder: Parse("#D5DEEA"),
                SelectionGlow: Parse("#E2ECFA"));

            public static ThemePalette Dark { get; } = new(
                WindowShell: Parse("#0E1620"),
                WindowShellBorder: Parse("#213043"),
                Surface: Parse("#101823"),
                Card: Parse("#16202C"),
                CardRaised: Parse("#1A2533"),
                CardSubtle: Parse("#202D3C"),
                Border: Parse("#273447"),
                InputSurface: Parse("#111A25"),
                InputBorder: Parse("#314154"),
                TextPrimary: Parse("#F3F7FC"),
                TextSecondary: Parse("#A9B7C9"),
                Accent: Parse("#89AAE8"),
                AccentSoft: Parse("#192535"),
                AccentBorder: Parse("#355073"),
                Gold: Parse("#D4B170"),
                GoldForeground: Parse("#1A1408"),
                PrimaryAction: Parse("#5F81C6"),
                PrimaryActionForeground: Parse("#FFFFFF"),
                SecondaryAction: Parse("#1B2735"),
                SecondaryActionBorder: Parse("#314154"),
                SecondaryActionForeground: Parse("#EDF3FB"),
                WarningSurface: Parse("#332A16"),
                WarningBorder: Parse("#705A29"),
                WarningForeground: Parse("#F4D58A"),
                Success: Parse("#4FB57F"),
                Danger: Parse("#F07A7A"),
                TabStrip: Parse("#121B26"),
                TabStripBorder: Parse("#243244"),
                SelectionGlow: Parse("#1A2940"));
        }
    }
}
