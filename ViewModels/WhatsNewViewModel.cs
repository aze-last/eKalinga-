using AttendanceShiftingManagement.Helpers;
using AttendanceShiftingManagement.Services;
using System.Collections.ObjectModel;

namespace AttendanceShiftingManagement.ViewModels
{
    public sealed class WhatsNewViewModel : ObservableObject
    {
        public WhatsNewViewModel(IReadOnlyList<WhatsNewEntry> entries, string currentVersion)
        {
            CurrentVersion = currentVersion;
            Entries = new ObservableCollection<WhatsNewEntry>(entries);
        }

        public string CurrentVersion { get; }

        public ObservableCollection<WhatsNewEntry> Entries { get; }

        public string VersionTitle => $"What's new in version {CurrentVersion}";

        public string Subtitle => "Here's what changed in eKalinga+.";

        public int EntryCount => Entries.Count;

        public bool HasEntries => Entries.Count > 0;
    }
}
