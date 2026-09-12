namespace AttendanceShiftingManagement.Services
{
    public sealed class WhatsNewEntry
    {
        public string Version { get; init; } = string.Empty;
        public string PublishedAt { get; init; } = string.Empty;
        public List<string> Notes { get; init; } = [];
    }

    public static class WhatsNewService
    {
        private static readonly List<WhatsNewEntry> Entries =
        [
            new()
            {
                Version = "1.0.13",
                PublishedAt = "2026-09-12",
                Notes =
                [
                    "Settings tab strip is now scrollable: swipe on touch screens or use the gold chevron arrows to reach hidden tabs like Updates.",
                    "Active tab always scrolls into view, and arrows dim at each end so you know when more settings remain."
                ]
            },
            new()
            {
                Version = "1.0.12",
                PublishedAt = "2026-09-12",
                Notes =
                [
                    "New download progress window with live percentage, file size, and cancel option.",
                    "Install permission prompt before the app closes, so upgrades never surprise you.",
                    "Fixed banner and Settings staying in sync after download, cancel, or install-later."
                ]
            },
            new()
            {
                Version = "1.0.11",
                PublishedAt = "2026-09-12",
                Notes =
                [
                    "Dashboard update banner now appears even when the startup check was skipped, and offers retry when the check fails.",
                    "Update notifications stay visible on the dashboard without opening Settings."
                ]
            },
            new()
            {
                Version = "1.0.10",
                PublishedAt = "2026-09-12",
                Notes =
                [
                    "New What's New modal after login that explains what's new or updated in each version.",
                    "Release notes are shown once per version and work offline."
                ]
            },
            new()
            {
                Version = "1.0.9",
                PublishedAt = "2026-09-12",
                Notes =
                [
                    "Automatic update download, install, and restart from the dashboard banner and Settings.",
                    "New update banner on the dashboard when a newer version is posted.",
                    "Updater reliability: update client identification, longer manifest timeout, and automatic re-check before downloading.",
                    "Fixed the default update manifest URL to point at the eKalinga- repository."
                ]
            },
            new()
            {
                Version = "1.0.8",
                PublishedAt = "2026-09-12",
                Notes =
                [
                    "Updated eKalinga+ installer to version 1.0.8.",
                    "Added password asterisk masking on CRS, GGMS, and database settings.",
                    "Configured remote database connections and auto-update settings."
                ]
            }
        ];

        public static bool ShouldShowWhatsNew(out IReadOnlyList<WhatsNewEntry> entries)
        {
            var currentVersion = AppVersionService.GetCurrentVersion();
            var lastSeenVersion = AppPreferencesService.Load().LastSeenWhatsNewVersion;
            return ShouldShowWhatsNew(currentVersion, lastSeenVersion, out entries);
        }

        internal static bool ShouldShowWhatsNew(
            string currentVersion,
            string lastSeenVersion,
            out IReadOnlyList<WhatsNewEntry> entries)
        {
            entries = Array.Empty<WhatsNewEntry>();

            if (!AppVersionService.TryParseVersion(currentVersion, out var currentParsed))
            {
                return false;
            }

            var hasLastSeen = AppVersionService.TryParseVersion(lastSeenVersion, out var lastSeenParsed);
            var visible = Entries
                .Select(entry => new
                {
                    Entry = entry,
                    Parsed = AppVersionService.TryParseVersion(entry.Version, out var entryParsed) ? entryParsed : null
                })
                .Where(candidate => candidate.Parsed != null
                    && candidate.Parsed <= currentParsed
                    && (!hasLastSeen || candidate.Parsed > lastSeenParsed))
                .OrderByDescending(candidate => candidate.Parsed)
                .Select(candidate => candidate.Entry)
                .ToList();

            if (visible.Count == 0)
            {
                return false;
            }

            entries = visible;
            return true;
        }

        public static void MarkWhatsNewSeen()
        {
            var preferences = AppPreferencesService.Load();
            preferences.LastSeenWhatsNewVersion = AppVersionService.GetCurrentVersion();
            AppPreferencesService.Save(preferences);
        }
    }
}
