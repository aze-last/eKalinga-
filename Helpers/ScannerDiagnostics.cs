namespace AttendanceShiftingManagement.Helpers
{
    /// <summary>
    /// Categorizes the reason a scan pipeline step failed or was skipped.
    /// </summary>
    public enum ScanErrorKind
    {
        /// <summary>The hidden scanner textbox lost focus; keystrokes were not captured.</summary>
        ScannerNotFocused,
        /// <summary>A scan was received while a result/dialog was already open and was queued or dropped.</summary>
        ScanSwallowedBusy,
        /// <summary>The keystroke buffer encountered a character that GetCharFromKey cannot map.</summary>
        UnmappedCharacter,
        /// <summary>The barcode buffer was reset due to a timing gap or length overflow.</summary>
        StaleBufferReset,
        /// <summary>The resolved payload returned no match in the beneficiary registry.</summary>
        PayloadNotFound,
        /// <summary>Camera/ZXing decode produced no result.</summary>
        DecodeFailure,
        /// <summary>Unclassified scanner error.</summary>
        Unknown
    }

    /// <summary>
    /// Shared, static scanner diagnostics channel used by all scan entry points
    /// (ProjectDistributionViewModel, ScanningPortalViewModel, DesktopScannerOverlay).
    /// Subscribers wire <see cref="ErrorRaised"/> to their own error-banner properties.
    /// A rolling 50-entry in-memory log is kept for "Copy Diagnostics" support.
    /// </summary>
    public static class ScannerDiagnostics
    {
        private const int MaxLogEntries = 50;

        private static readonly Queue<(DateTime Timestamp, ScanErrorKind Kind, string Message)> _log
            = new(MaxLogEntries);

        private static readonly object _logLock = new();

        /// <summary>Fired on the calling thread whenever <see cref="Report"/> is called.</summary>
        public static event Action<ScanErrorKind, string>? ErrorRaised;

        /// <summary>
        /// Records a scan error to the in-memory log, writes a debug trace line,
        /// and fires <see cref="ErrorRaised"/> for UI subscribers.
        /// </summary>
        public static void Report(ScanErrorKind kind, string message, Exception? ex = null)
        {
            var fullMessage = ex != null ? $"{message} ({ex.GetType().Name}: {ex.Message})" : message;

            System.Diagnostics.Debug.WriteLine($"[Scanner:{kind}] {fullMessage}");

            lock (_logLock)
            {
                if (_log.Count >= MaxLogEntries)
                {
                    _log.Dequeue();
                }
                _log.Enqueue((DateTime.Now, kind, fullMessage));
            }

            ErrorRaised?.Invoke(kind, fullMessage);
        }

        /// <summary>
        /// Returns the rolling diagnostic log as a multi-line string suitable for
        /// copying to a clipboard / support ticket.
        /// </summary>
        public static string GetDiagnosticsLog()
        {
            lock (_logLock)
            {
                return string.Join(Environment.NewLine,
                    _log.Select(e => $"{e.Timestamp:HH:mm:ss.fff} [{e.Kind}] {e.Message}"));
            }
        }

        /// <summary>Clears the in-memory log.</summary>
        public static void ClearLog()
        {
            lock (_logLock)
            {
                _log.Clear();
            }
        }
    }
}
