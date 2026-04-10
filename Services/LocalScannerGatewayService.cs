using AttendanceShiftingManagement.Data;
using AttendanceShiftingManagement.Models;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.IO;
using System.Text.Json;

namespace AttendanceShiftingManagement.Services
{
    public sealed class LocalScannerGatewayService
    {
        private static readonly Lazy<LocalScannerGatewayService> _shared = new(() => new LocalScannerGatewayService());
        private readonly SemaphoreSlim _startGate = new(1, 1);
        private WebApplication? _app;
        private int _port;
        private string _baseUrl = string.Empty;

        public static LocalScannerGatewayService Shared => _shared.Value;

        private LocalScannerGatewayService()
        {
        }

        public async Task<string> EnsureStartedAsync()
        {
            if (_app != null)
            {
                return _baseUrl;
            }

            await _startGate.WaitAsync();
            try
            {
                if (_app != null)
                {
                    return _baseUrl;
                }

                _port = FindAvailablePort();
                var lanAddress = ResolveLanAddress();
                _baseUrl = $"http://{lanAddress}:{_port}";

                var builder = WebApplication.CreateBuilder(new WebApplicationOptions
                {
                    Args = Array.Empty<string>()
                });

                builder.WebHost.UseUrls($"http://0.0.0.0:{_port}");

                var app = builder.Build();
                MapEndpoints(app);
                await app.StartAsync();
                _app = app;

                return _baseUrl;
            }
            finally
            {
                _startGate.Release();
            }
        }

        private void MapEndpoints(WebApplication app)
        {
            app.MapGet("/", () => Results.Text("Barangay Ayuda System local scanner gateway is running."));

            app.MapGet("/scanner", (HttpContext context) =>
            {
                var sessionToken = context.Request.Query["session"].ToString();
                return Results.Content(RenderScannerPage(sessionToken), "text/html; charset=utf-8");
            });

            app.MapPost("/api/session/unlock", async Task<IResult> (ScannerPinRequest request) =>
            {
                await using var db = new AppDbContext();
                var sessionService = new ScannerSessionService(db);
                var isValid = await sessionService.ValidatePinAsync(request.SessionToken, request.Pin);
                if (!isValid)
                {
                    return Results.Json(new { success = false, message = "Invalid or expired session PIN." });
                }

                var session = await sessionService.GetActiveSessionAsync(request.SessionToken);
                if (session == null)
                {
                    return Results.Json(new { success = false, message = "The scanner session is no longer active." });
                }

                string? eventTitle = null;
                string? projectTitle = null;
                if (session.CashForWorkEventId.HasValue)
                {
                    eventTitle = db.CashForWorkEvents
                        .Where(item => item.Id == session.CashForWorkEventId.Value)
                        .Select(item => item.Title)
                        .FirstOrDefault();
                }

                if (session.AyudaProgramId.HasValue)
                {
                    projectTitle = db.AyudaPrograms
                        .Where(item => item.Id == session.AyudaProgramId.Value)
                        .Select(item => item.ProgramName)
                        .FirstOrDefault();
                }

                return Results.Json(new
                {
                    success = true,
                    mode = session.Mode.ToString(),
                    expiresAt = session.ExpiresAt.ToString("yyyy-MM-dd HH:mm:ss"),
                    eventTitle,
                    projectTitle
                });
            });

            app.MapPost("/api/lookup/manual", async Task<IResult> (ScannerLookupRequest request) =>
            {
                return await LookupAsync(request.SessionToken, request.Pin, request.QrPayload);
            });

            app.MapPost("/api/lookup/upload", async Task<IResult> (HttpRequest request) =>
            {
                var form = await request.ReadFormAsync();
                var sessionToken = form["sessionToken"].ToString();
                var pin = form["pin"].ToString();
                var image = form.Files.GetFile("image");

                if (image == null || image.Length == 0)
                {
                    return Results.Json(new { success = false, message = "Upload or capture a beneficiary QR image first." });
                }

                try
                {
                    await using var stream = image.OpenReadStream();
                    var qrPayload = QrCodeToolkitService.TryDecodePayload(stream);
                    if (string.IsNullOrWhiteSpace(qrPayload))
                    {
                        return Results.Json(new { success = false, message = "No readable QR code was found in the uploaded image. Move closer, improve lighting, or try live camera scan." });
                    }

                    return await LookupAsync(sessionToken, pin, qrPayload);
                }
                catch
                {
                    return Results.Json(new { success = false, message = "The selected image could not be processed. Use a JPG or PNG photo, or try live camera scan." });
                }
            });

            app.MapPost("/api/attendance/mark", async Task<IResult> (ScannerAttendanceRequest request) =>
            {
                await using var db = new AppDbContext();
                var sessionService = new ScannerSessionService(db);
                if (!await sessionService.ValidatePinAsync(request.SessionToken, request.Pin))
                {
                    return Results.Json(new { success = false, message = "Invalid or expired session PIN." });
                }

                var session = await sessionService.GetActiveSessionAsync(request.SessionToken);
                if (session == null || session.Mode != ScannerSessionMode.Attendance || !session.CashForWorkEventId.HasValue)
                {
                    return Results.Json(new { success = false, message = "This scanner session is not allowed to record attendance." });
                }

                var cashForWorkService = new CashForWorkService(db, new AuditService(db));
                var wasSaved = await cashForWorkService.SaveScannerAttendanceAsync(
                    session.CashForWorkEventId.Value,
                    session.CreatedByUserId,
                    request.ParticipantId,
                    request.QrPayload ?? string.Empty);

                return Results.Json(new
                {
                    success = wasSaved,
                    message = wasSaved
                        ? "Attendance saved."
                        : "Attendance could not be saved. This is usually due to an ID mismatch, an invalid QR code, or attendance already being marked."
                });

            });

            app.MapPost("/api/distribution/claim", async Task<IResult> (ScannerDistributionClaimRequest request) =>
            {
                await using var db = new AppDbContext();
                var sessionService = new ScannerSessionService(db);
                if (!await sessionService.ValidatePinAsync(request.SessionToken, request.Pin))
                {
                    return Results.Json(new { success = false, message = "Invalid or expired session PIN." });
                }

                var session = await sessionService.GetActiveSessionAsync(request.SessionToken);
                if (session == null || session.Mode != ScannerSessionMode.Distribution || !session.AyudaProgramId.HasValue)
                {
                    return Results.Json(new { success = false, message = "This scanner session is not allowed to mark project claims." });
                }

                var distributionService = new ProjectDistributionService(db, new AuditService(db));
                var result = await distributionService.RecordClaimAsync(
                    session.AyudaProgramId.Value,
                    request.BeneficiaryStagingId,
                    session.CreatedByUserId,
                    request.QrPayload,
                    request.Remarks);

                return Results.Json(new
                {
                    success = result.IsSuccess,
                    message = result.Message
                });
            });

            app.MapGet("/api/photo/{stagingId:int}", async Task<IResult> (int stagingId, string session, string pin) =>
            {
                await using var db = new AppDbContext();
                var sessionService = new ScannerSessionService(db);
                if (!await sessionService.ValidatePinAsync(session, pin))
                {
                    return Results.NotFound();
                }

                var digitalIdService = new BeneficiaryDigitalIdService(db);
                var digitalId = await digitalIdService.GetByStagingIdAsync(stagingId);
                if (digitalId == null || string.IsNullOrWhiteSpace(digitalId.PhotoPath) || !File.Exists(digitalId.PhotoPath))
                {
                    return Results.NotFound();
                }

                return Results.File(digitalId.PhotoPath, GetContentType(digitalId.PhotoPath));
            });
        }

        private static async Task<IResult> LookupAsync(string sessionToken, string pin, string qrPayload)
        {
            await using var db = new AppDbContext();
            var sessionService = new ScannerSessionService(db);
            if (!await sessionService.ValidatePinAsync(sessionToken, pin))
            {
                return Results.Json(new { success = false, message = "Invalid or expired session PIN." });
            }

            var session = await sessionService.GetActiveSessionAsync(sessionToken);
            if (session == null)
            {
                return Results.Json(new { success = false, message = "The scanner session has expired." });
            }

            var digitalIdService = new BeneficiaryDigitalIdService(db);
            var lookup = await digitalIdService.LookupByQrPayloadAsync(qrPayload);
            if (lookup == null)
            {
                return Results.Json(new { success = false, message = "ID not recognized." });
            }

            CashForWorkParticipant? participant = null;
            object? distribution = null;
            if (session.Mode == ScannerSessionMode.Attendance && session.CashForWorkEventId.HasValue)
            {
                participant = db.CashForWorkParticipants
                    .AsNoTracking()
                    .FirstOrDefault(item =>
                        item.EventId == session.CashForWorkEventId.Value &&
                        item.BeneficiaryStagingId == lookup.BeneficiaryStagingId);
            }

            if (session.Mode == ScannerSessionMode.Distribution && session.AyudaProgramId.HasValue)
            {
                var project = await db.AyudaPrograms
                    .AsNoTracking()
                    .FirstOrDefaultAsync(item => item.Id == session.AyudaProgramId.Value);

                var distributionService = new ProjectDistributionService(db);
                var qualification = await distributionService.EvaluateQualificationAsync(session.AyudaProgramId.Value, lookup.BeneficiaryStagingId);

                distribution = new
                {
                    projectId = session.AyudaProgramId.Value,
                    projectName = project?.ProgramName,
                    assistanceType = project?.AssistanceType,
                    unitAmount = project?.UnitAmount?.ToString("N2"),
                    itemDescription = project?.ItemDescription,
                    isQualified = qualification.IsQualified,
                    alreadyClaimed = qualification.AlreadyClaimed,
                    canMarkReceived = qualification.IsQualified && !qualification.AlreadyClaimed,
                    message = qualification.Message
                };
            }

            var history = lookup.ReleaseHistory
                .OrderByDescending(entry => entry.ReleaseDate)
                .Select(entry => new
                {
                    source = entry.SourceModule.ToString(),
                    amount = entry.Amount.ToString("N2"),
                    releaseDate = entry.ReleaseDate.ToString("yyyy-MM-dd"),
                    remarks = entry.Remarks
                })
                .ToArray();

            var photoUrl = string.IsNullOrWhiteSpace(lookup.PhotoPath)
                ? null
                : $"/api/photo/{lookup.BeneficiaryStagingId}?session={Uri.EscapeDataString(sessionToken)}&pin={Uri.EscapeDataString(pin)}";

            return Results.Json(new
            {
                success = true,
                message = "Beneficiary found.",
                mode = session.Mode.ToString(),
                fullName = lookup.FullName,
                beneficiaryId = lookup.BeneficiaryId,
                civilRegistryId = lookup.CivilRegistryId,
                cardNumber = lookup.CardNumber,
                photoUrl,
                beneficiaryStagingId = lookup.BeneficiaryStagingId,
                participantId = participant?.Id,
                distribution,
                releaseHistory = history,
                qrPayload
            });
        }

        private static int FindAvailablePort()
        {
            var listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            try
            {
                return ((IPEndPoint)listener.LocalEndpoint).Port;
            }
            finally
            {
                listener.Stop();
            }
        }

        private static string ResolveLanAddress()
        {
            var preferredAddress = NetworkInterface.GetAllNetworkInterfaces()
                .Where(item =>
                    item.OperationalStatus == OperationalStatus.Up &&
                    item.NetworkInterfaceType != NetworkInterfaceType.Loopback)
                .SelectMany(item =>
                {
                    var properties = item.GetIPProperties();
                    var hasGateway = properties.GatewayAddresses.Any(gateway =>
                        gateway.Address.AddressFamily == AddressFamily.InterNetwork &&
                        gateway.Address.ToString() != "0.0.0.0");

                    return properties.UnicastAddresses
                        .Where(address => address.Address.AddressFamily == AddressFamily.InterNetwork)
                        .Select(address => new LanAddressCandidate(
                            address.Address.ToString(),
                            item.Name,
                            item.Description,
                            item.NetworkInterfaceType,
                            hasGateway));
                })
                .OrderBy(GetLanAddressPriority)
                .ThenBy(candidate => candidate.Address, StringComparer.Ordinal)
                .Select(candidate => candidate.Address)
                .FirstOrDefault();

            return preferredAddress ?? "127.0.0.1";
        }

        private static int GetLanAddressPriority(LanAddressCandidate candidate)
        {
            var score = 0;

            if (candidate.IsAutomaticPrivateAddress)
            {
                score += 1000;
            }

            if (!candidate.HasGateway)
            {
                score += 100;
            }

            if (candidate.IsVirtualLike)
            {
                score += 50;
            }

            if (!candidate.IsPreferredInterfaceType)
            {
                score += 10;
            }

            return score;
        }

        private static string GetContentType(string path)
        {
            return Path.GetExtension(path).ToLowerInvariant() switch
            {
                ".jpg" or ".jpeg" => "image/jpeg",
                ".bmp" => "image/bmp",
                ".webp" => "image/webp",
                _ => "image/png"
            };
        }

        private static string RenderScannerPage(string sessionToken)
        {
            var safeToken = WebUtility.HtmlEncode(sessionToken);
            return $$"""
<!doctype html>
<html lang="en">
<head>
  <meta charset="utf-8">
  <meta name="viewport" content="width=device-width, initial-scale=1">
  <title>Beneficiary Scanner</title>
  <style>
    :root {
      color-scheme: light;
      font-family: "Segoe UI Variable Text", "Segoe UI", sans-serif;
      --bg-top: #dbe4f0;
      --bg-bottom: #f8fafc;
      --panel: rgba(255,255,255,.92);
      --panel-border: rgba(148,163,184,.28);
      --ink: #0f172a;
      --muted: #475569;
      --muted-soft: #64748b;
      --hero: #0f172a;
      --hero-soft: #1e293b;
      --line: #e2e8f0;
      --surface: #f8fafc;
      --success-bg: #ecfdf5;
      --success-border: #a7f3d0;
      --success-ink: #047857;
      --danger-bg: #fef2f2;
      --danger-border: #fecaca;
      --danger-ink: #b91c1c;
      --shadow: 0 26px 50px rgba(15, 23, 42, .10);
    }
    * { box-sizing: border-box; }
    body {
      margin: 0;
      min-height: 100vh;
      background:
        radial-gradient(circle at top, rgba(15,118,110,.10), transparent 26%),
        linear-gradient(180deg, var(--bg-top) 0%, var(--bg-bottom) 100%);
      color: var(--ink);
    }
    main {
      max-width: 460px;
      margin: 0 auto;
      padding: 18px 16px 34px;
    }
    .panel,
    .result-panel,
    .history-panel {
      background: var(--panel);
      border: 1px solid var(--panel-border);
      border-radius: 30px;
      box-shadow: var(--shadow);
      backdrop-filter: blur(12px);
    }
    .panel {
      padding: 18px;
      margin-bottom: 16px;
    }
    .unlock-panel {
      padding: 22px 18px 18px;
    }
    .result-shell {
      display: grid;
      gap: 16px;
    }
    .result-panel {
      overflow: hidden;
    }
    .history-panel {
      padding: 20px 18px;
    }
    h1, h2, h3, p {
      margin: 0;
    }
    h1 {
      margin-top: 14px;
      font-size: 1.85rem;
      line-height: 1.1;
      letter-spacing: -.04em;
    }
    .eyebrow {
      display: inline-flex;
      align-items: center;
      gap: 8px;
      padding: 8px 12px;
      border-radius: 999px;
      background: rgba(15,23,42,.06);
      color: var(--muted);
      font-size: .76rem;
      font-weight: 700;
      letter-spacing: .12em;
      text-transform: uppercase;
    }
    .eyebrow::before {
      content: "";
      width: 8px;
      height: 8px;
      border-radius: 999px;
      background: #0f766e;
    }
    .lead {
      margin-top: 10px;
      color: var(--muted);
      line-height: 1.55;
      font-size: .98rem;
    }
    .session-token {
      margin-top: 14px;
      padding: 12px 14px;
      border-radius: 18px;
      background: var(--surface);
      border: 1px solid var(--line);
      color: var(--muted);
      font-size: .9rem;
      word-break: break-all;
    }
    input, button {
      width: 100%;
      font: inherit;
    }
    input {
      border: 1px solid #cbd5e1;
      border-radius: 22px;
      padding: 14px 16px;
      background: #f8fafc;
      color: var(--ink);
      outline: none;
      transition: border-color .2s ease, box-shadow .2s ease, background-color .2s ease;
    }
    input:focus {
      border-color: #0f766e;
      background: #ffffff;
      box-shadow: 0 0 0 4px rgba(15,118,110,.12);
    }
    button {
      border: 0;
      border-radius: 22px;
      padding: 14px 16px;
      font-weight: 700;
      cursor: pointer;
      transition: transform .16s ease, filter .16s ease, opacity .16s ease;
    }
    button:active {
      transform: translateY(1px);
    }
    button:disabled {
      cursor: not-allowed;
      opacity: .65;
    }
    .button-primary {
      background: var(--hero);
      color: #ffffff;
    }
    .button-secondary,
    button.alt {
      background: #ffffff;
      color: #334155;
      border: 1px solid #cbd5e1;
    }
    .button-accent {
      background: #0f766e;
      color: #ffffff;
    }
    .button-warn,
    button.warn {
      background: #92400e;
      color: #ffffff;
    }
    .unlock-panel input,
    .unlock-panel > button,
    #scannerCard > input {
      margin-top: 12px;
    }
    button:hover {
      filter: brightness(.98);
    }
    .hidden {
      display: none !important;
    }
    .scanner-banner {
      padding: 12px 14px;
      border-radius: 18px;
      font-size: .92rem;
      font-weight: 700;
      border: 1px solid transparent;
      margin-bottom: 14px;
    }
    .scanner-banner-neutral {
      background: #eff6ff;
      border-color: #bfdbfe;
      color: #1d4ed8;
    }
    .scanner-banner-success {
      background: var(--success-bg);
      border-color: var(--success-border);
      color: var(--success-ink);
    }
    .scanner-banner-error {
      background: var(--danger-bg);
      border-color: var(--danger-border);
      color: var(--danger-ink);
    }
    .scanner-title {
      font-size: 1.4rem;
      font-weight: 700;
      letter-spacing: -.03em;
    }
    .scanner-copy,
    .helper,
    .status {
      color: var(--muted);
      line-height: 1.5;
    }
    .scanner-copy {
      margin-top: 8px;
      font-size: .94rem;
    }
    .stack-actions {
      display: grid;
      gap: 10px;
      margin-top: 12px;
    }
    .action-row {
      display: grid;
      grid-template-columns: repeat(2, minmax(0, 1fr));
      gap: 10px;
      margin-top: 12px;
    }
    .action-row button {
      margin: 0;
    }
    .helper {
      margin-top: 12px;
      font-size: .92rem;
    }
    .status {
      margin-top: 12px;
      white-space: pre-wrap;
      font-size: .94rem;
    }
    video.preview {
      width: 100%;
      aspect-ratio: 4 / 3;
      object-fit: cover;
      border-radius: 26px;
      border: 1px solid #cbd5e1;
      background: #020617;
      margin-top: 14px;
    }
    .result-hero {
      padding: 22px 20px 18px;
      background:
        radial-gradient(circle at top right, rgba(16,185,129,.14), transparent 34%),
        linear-gradient(135deg, var(--hero) 0%, var(--hero-soft) 100%);
      color: #ffffff;
    }
    .result-name {
      font-size: 1.95rem;
      line-height: 1.08;
      letter-spacing: -.05em;
      font-weight: 700;
    }
    .chip-row {
      display: flex;
      flex-wrap: wrap;
      gap: 8px;
      margin-top: 14px;
    }
    .chip {
      padding: 8px 12px;
      border-radius: 999px;
      font-size: .7rem;
      font-weight: 700;
      letter-spacing: .12em;
      text-transform: uppercase;
    }
    .chip-muted {
      background: rgba(255,255,255,.10);
      color: #e2e8f0;
    }
    .chip-success {
      background: rgba(74,222,128,.18);
      color: #86efac;
    }
    .chip-warn {
      background: rgba(253,186,116,.16);
      color: #fdba74;
    }
    .chip-danger {
      background: rgba(252,165,165,.16);
      color: #fca5a5;
    }
    .result-body {
      padding: 20px;
      display: grid;
      gap: 14px;
    }
    .identity-grid {
      display: grid;
      grid-template-columns: 122px minmax(0, 1fr);
      gap: 14px;
      align-items: start;
    }
    .photo-frame {
      position: relative;
      width: 122px;
      height: 122px;
      overflow: hidden;
      border-radius: 28px;
      background: linear-gradient(180deg, #f8fafc 0%, #e2e8f0 100%);
      border: 1px solid #cbd5e1;
    }
    img.photo {
      width: 100%;
      height: 100%;
      object-fit: cover;
    }
    .photo-placeholder {
      position: absolute;
      inset: 0;
      display: flex;
      align-items: center;
      justify-content: center;
      font-size: 2rem;
      font-weight: 700;
      color: #475569;
      letter-spacing: -.06em;
    }
    .identity-meta {
      display: grid;
      gap: 10px;
    }
    .info-card {
      border-radius: 22px;
      padding: 14px 16px;
      background: var(--surface);
      border: 1px solid var(--line);
    }
    .info-label {
      font-size: .68rem;
      font-weight: 800;
      color: var(--muted-soft);
      letter-spacing: .12em;
      text-transform: uppercase;
    }
    .info-value {
      margin-top: 8px;
      font-size: 1rem;
      font-weight: 700;
      color: var(--ink);
      line-height: 1.35;
      word-break: break-word;
    }
    .info-value-tight {
      font-size: .92rem;
    }
    .detail-stack {
      margin-top: 10px;
      display: grid;
      gap: 10px;
    }
    .detail-row {
      display: flex;
      justify-content: space-between;
      align-items: flex-start;
      gap: 14px;
    }
    .detail-row-label {
      color: var(--muted-soft);
      font-size: .78rem;
      font-weight: 700;
      letter-spacing: .04em;
      text-transform: uppercase;
    }
    .detail-row-value {
      flex: 1;
      text-align: right;
      color: var(--ink);
      font-size: .92rem;
      font-weight: 600;
      line-height: 1.4;
      word-break: break-word;
    }
    .stats-grid {
      display: grid;
      grid-template-columns: repeat(2, minmax(0, 1fr));
      gap: 12px;
    }
    .stat-card {
      border-radius: 28px;
      padding: 16px;
      background: var(--surface);
      border: 1px solid var(--line);
    }
    .stat-card-dark {
      background: var(--hero);
      border-color: var(--hero);
      color: #ffffff;
    }
    .stat-card-dark .info-label,
    .stat-card-dark .stat-sub {
      color: #cbd5e1;
    }
    .stat-value {
      margin-top: 8px;
      font-size: 2rem;
      font-weight: 700;
      line-height: 1;
      letter-spacing: -.05em;
    }
    .stat-sub {
      margin-top: 8px;
      color: var(--muted);
      font-size: .92rem;
    }
    .history-header {
      display: flex;
      align-items: center;
      justify-content: space-between;
      gap: 12px;
    }
    .history-title {
      font-size: 1.45rem;
      font-weight: 700;
      letter-spacing: -.03em;
    }
    .history-count {
      padding: 8px 12px;
      border-radius: 999px;
      background: var(--surface);
      border: 1px solid var(--line);
      color: var(--muted);
      font-size: .78rem;
      font-weight: 700;
      white-space: nowrap;
    }
    .history-list {
      margin-top: 14px;
      display: grid;
      gap: 10px;
    }
    .history-item {
      border-top: 1px solid var(--line);
      padding-top: 14px;
    }
    .history-item:first-child {
      border-top: 0;
      padding-top: 0;
    }
    .history-top {
      display: flex;
      justify-content: space-between;
      align-items: flex-start;
      gap: 12px;
    }
    .history-type {
      font-size: 1rem;
      font-weight: 700;
    }
    .history-date {
      margin-top: 4px;
      color: var(--muted-soft);
      font-size: .88rem;
    }
    .history-amount {
      padding: 8px 12px;
      border-radius: 999px;
      background: var(--surface);
      border: 1px solid var(--line);
      color: #334155;
      font-size: .88rem;
      font-weight: 700;
      white-space: nowrap;
    }
    .history-note {
      margin-top: 10px;
      color: #334155;
      font-size: .94rem;
      line-height: 1.55;
      word-break: break-word;
    }
    .empty-history {
      border-radius: 22px;
      padding: 16px;
      background: var(--surface);
      border: 1px solid var(--line);
      color: var(--muted);
      line-height: 1.5;
    }
    @media (max-width: 380px) {
      .identity-grid {
        grid-template-columns: 1fr;
      }
      .photo-frame {
        width: 100%;
        max-width: 150px;
        height: 150px;
      }
      .action-row {
        grid-template-columns: 1fr;
      }
    }
  </style>
</head>
<body>
<main>
  <section class="panel unlock-panel">
    <div class="eyebrow">Barangay Ayuda Scanner</div>
    <h1>Beneficiary Lookup</h1>
    <p class="lead">Unlock this scanner session first, then scan or upload the beneficiary QR to load the organized record view.</p>
    <div class="session-token">Session token: {{safeToken}}</div>
    <input id="pin" inputmode="numeric" maxlength="6" placeholder="Enter the 6-digit session PIN">
    <button id="unlock" class="button-primary">Unlock Scanner</button>
    <div class="status" id="unlockStatus"></div>
  </section>

  <section class="panel hidden" id="scannerCard">
    <div class="scanner-banner scanner-banner-neutral" id="lookupBanner">Scanner locked until the session PIN is verified.</div>
    <div class="scanner-title">Lookup Beneficiary</div>
    <p class="scanner-copy">Use live camera scan when supported, or tap Open Camera / Photo to capture and upload a beneficiary QR image.</p>
    <video id="cameraPreview" class="preview hidden" playsinline muted></video>
    <div class="action-row">
      <button class="button-secondary hidden" id="startLiveScan">Start Live Camera Scan</button>
      <button class="button-warn hidden" id="stopLiveScan">Stop Camera</button>
    </div>
    <div class="helper" id="cameraSupportMessage">Checking live camera scan support...</div>
    <input id="payload" placeholder="Paste or type the beneficiary QR payload">
    <div class="stack-actions">
      <button class="button-primary" id="lookupManual">Lookup by QR payload</button>
      <button class="button-secondary" id="lookupImage">Open Camera / Photo</button>
    </div>
    <input id="image" class="hidden" type="file" accept="image/*" capture="environment">
    <div class="status" id="lookupStatus"></div>
  </section>

  <div class="result-shell hidden" id="resultCard">
    <section class="result-panel">
      <div class="result-hero">
        <div class="result-name" id="name">Beneficiary record</div>
        <div class="chip-row">
          <span class="chip chip-muted">Beneficiary</span>
          <span class="chip chip-success" id="statusChip">Lookup Match</span>
        </div>
      </div>

      <div class="result-body">
        <div class="identity-grid">
          <div class="photo-frame">
            <img class="photo hidden" id="photo" alt="Beneficiary photo">
            <div class="photo-placeholder" id="photoPlaceholder">BA</div>
          </div>

          <div class="identity-meta">
            <div class="info-card">
              <div class="info-label">Card Number</div>
              <div class="info-value" id="cardNumber">--</div>
            </div>
            <div class="info-card">
              <div class="info-label">Beneficiary ID</div>
              <div class="info-value info-value-tight" id="beneficiaryId">--</div>
            </div>
          </div>
        </div>

        <div class="info-card">
          <div class="info-label">Civil Registry ID</div>
          <div class="info-value" id="civilRegistryId">--</div>
        </div>

        <div class="info-card hidden" id="modeInfoCard">
          <div class="info-label" id="modeInfoLabel">Scanner Session</div>
          <div class="detail-stack" id="modeInfoContent"></div>
        </div>

        <div class="stats-grid">
          <div class="stat-card">
            <div class="info-label">Manual</div>
            <div class="stat-value" id="manualCount">0</div>
            <div class="stat-sub" id="manualAmount">₱0.00</div>
          </div>
          <div class="stat-card">
            <div class="info-label">Cases</div>
            <div class="stat-value" id="casesCount">0</div>
            <div class="stat-sub" id="casesAmount">₱0.00</div>
          </div>
          <div class="stat-card">
            <div class="info-label">Cash for Work</div>
            <div class="stat-value" id="cashForWorkCount">0</div>
            <div class="stat-sub" id="cashForWorkAmount">₱0.00</div>
          </div>
          <div class="stat-card stat-card-dark">
            <div class="info-label">Total Aid</div>
            <div class="stat-value" id="totalAidCount">0</div>
            <div class="stat-sub" id="totalAidAmount">₱0.00</div>
          </div>
        </div>

        <div class="action-row hidden" id="resultActions">
          <button class="button-accent hidden" id="markAttendance">Mark Attendance</button>
          <button class="button-warn hidden" id="markReceived">Mark as Received</button>
        </div>
      </div>
    </section>

    <section class="history-panel">
      <div class="history-header">
        <div class="history-title">Release History</div>
        <div class="history-count" id="historyCountBadge">0 records</div>
      </div>
      <div class="history-list" id="history"></div>
    </section>
  </div>
</main>
<script>
const sessionToken = {{JsonSerializer.Serialize(sessionToken)}};
let activePin = "";
let lastLookup = null;
const currencyFormatter = new Intl.NumberFormat("en-PH", {
  style: "currency",
  currency: "PHP",
  minimumFractionDigits: 2,
  maximumFractionDigits: 2
});

const unlockButton = document.getElementById("unlock");
const lookupManualButton = document.getElementById("lookupManual");
const lookupImageButton = document.getElementById("lookupImage");
const imageInput = document.getElementById("image");
const startLiveScanButton = document.getElementById("startLiveScan");
const stopLiveScanButton = document.getElementById("stopLiveScan");
const cameraPreview = document.getElementById("cameraPreview");
const cameraSupportMessage = document.getElementById("cameraSupportMessage");
const markAttendanceButton = document.getElementById("markAttendance");
const markReceivedButton = document.getElementById("markReceived");
let barcodeDetector = null;
let cameraStream = null;
let cameraScanTimer = 0;
let isCameraScanning = false;

unlockButton.addEventListener("click", unlockSession);
lookupManualButton.addEventListener("click", lookupManual);
lookupImageButton.addEventListener("click", openImageCapture);
imageInput.addEventListener("change", lookupSelectedImage);
startLiveScanButton.addEventListener("click", startLiveScan);
stopLiveScanButton.addEventListener("click", () => stopLiveScan(true));
markAttendanceButton.addEventListener("click", markAttendance);
markReceivedButton.addEventListener("click", markReceived);
document.addEventListener("visibilitychange", handleVisibilityChange);
window.addEventListener("beforeunload", cleanupCamera);

initializeLiveCameraSupport();

async function unlockSession() {
  const pin = document.getElementById("pin").value.trim();
  const response = await postJson("/api/session/unlock", { sessionToken, pin });
  document.getElementById("unlockStatus").textContent = response.message || (response.success ? "Scanner unlocked." : "Unlock failed.");
  if (!response.success) return;
  activePin = pin;
  document.getElementById("scannerCard").classList.remove("hidden");
  setLookupBanner("Scanner unlocked. Ready to scan a beneficiary QR.", "neutral");
}

async function lookupManual() {
  const qrPayload = document.getElementById("payload").value.trim();
  if (!qrPayload) {
    document.getElementById("lookupStatus").textContent = "Enter the beneficiary QR payload first.";
    return;
  }

  await handleDetectedPayload(qrPayload, false);
}

function openImageCapture() {
  stopLiveScan(false);
  imageInput.value = "";
  imageInput.click();
}

async function lookupSelectedImage() {
  if (!imageInput.files.length) {
    return;
  }

  document.getElementById("lookupStatus").textContent = "Uploading QR image...";

  const data = new FormData();
  data.append("sessionToken", sessionToken);
  data.append("pin", activePin);
  data.append("image", imageInput.files[0]);

  const response = await fetch("/api/lookup/upload", { method: "POST", body: data }).then(result => result.json());
  imageInput.value = "";
  renderLookupResponse(response);
}

function setLookupBanner(message, tone) {
  const banner = document.getElementById("lookupBanner");
  banner.textContent = message || "Ready.";
  banner.className = "scanner-banner scanner-banner-" + tone;
}

function formatCurrency(amount) {
  return currencyFormatter.format(Number.isFinite(amount) ? amount : 0);
}

function parseAmount(value) {
  if (typeof value === "number") {
    return Number.isFinite(value) ? value : 0;
  }

  const parsed = Number.parseFloat(String(value || "0").replace(/,/g, ""));
  return Number.isFinite(parsed) ? parsed : 0;
}

function humanizeMode(value) {
  if (!value) return "--";
  return String(value).replace(/([a-z])([A-Z])/g, "$1 $2").trim();
}

function humanizeSource(value) {
  switch (String(value || "").toLowerCase()) {
    case "manualhistory":
      return "Manual";
    case "assistancecase":
      return "Assistance Case";
    case "cashforwork":
      return "Cash for Work";
    case "grievance":
      return "Grievance";
    default:
      return value || "Unknown";
  }
}

function getInitials(name) {
  const tokens = String(name || "")
    .trim()
    .split(/\s+/)
    .filter(Boolean);

  if (!tokens.length) {
    return "BA";
  }

  return tokens
    .slice(0, 2)
    .map(token => token.charAt(0).toUpperCase())
    .join("");
}

function summarizeReleaseHistory(entries) {
  const summary = {
    manual: { count: 0, amount: 0 },
    cases: { count: 0, amount: 0 },
    cashForWork: { count: 0, amount: 0 },
    total: { count: 0, amount: 0 }
  };

  (entries || []).forEach(entry => {
    const amount = parseAmount(entry.amount);
    const source = String(entry.source || "").toLowerCase();

    summary.total.count += 1;
    summary.total.amount += amount;

    if (source === "manualhistory") {
      summary.manual.count += 1;
      summary.manual.amount += amount;
      return;
    }

    if (source === "cashforwork") {
      summary.cashForWork.count += 1;
      summary.cashForWork.amount += amount;
      return;
    }

    summary.cases.count += 1;
    summary.cases.amount += amount;
  });

  return summary;
}

function setStat(prefix, count, amount) {
  document.getElementById(prefix + "Count").textContent = String(count);
  document.getElementById(prefix + "Amount").textContent = formatCurrency(amount);
}

function setStatusChip(text, tone) {
  const chip = document.getElementById("statusChip");
  chip.textContent = text;
  chip.className = "chip chip-" + tone;
}

function renderModeDetails(response) {
  const card = document.getElementById("modeInfoCard");
  const label = document.getElementById("modeInfoLabel");
  const content = document.getElementById("modeInfoContent");
  content.innerHTML = "";

  const rows = [];
  rows.push(["Scanner Mode", humanizeMode(response.mode)]);

  if (response.distribution) {
    label.textContent = "Project Qualification";
    rows.push(["Project", response.distribution.projectName || "--"]);
    rows.push(["Assistance", response.distribution.assistanceType || response.distribution.itemDescription || "--"]);
    rows.push(["Unit Amount", response.distribution.unitAmount ? "₱" + response.distribution.unitAmount : "--"]);
    rows.push(["Project Status", response.distribution.message || "--"]);
  } else if (response.participantId) {
    label.textContent = "Attendance Session";
    rows.push(["Attendance", "This beneficiary can be marked present from this phone."]);
  } else {
    label.textContent = "Scanner Session";
  }

  rows.forEach(([rowLabel, rowValue]) => {
    const row = document.createElement("div");
    row.className = "detail-row";

    const left = document.createElement("div");
    left.className = "detail-row-label";
    left.textContent = rowLabel;

    const right = document.createElement("div");
    right.className = "detail-row-value";
    right.textContent = rowValue;

    row.appendChild(left);
    row.appendChild(right);
    content.appendChild(row);
  });

  card.classList.remove("hidden");
}

function renderHistory(entries) {
  const history = document.getElementById("history");
  history.innerHTML = "";

  const count = (entries || []).length;
  document.getElementById("historyCountBadge").textContent = count + (count === 1 ? " record" : " records");

  if (!count) {
    const empty = document.createElement("div");
    empty.className = "empty-history";
    empty.textContent = "No release history found for this beneficiary yet.";
    history.appendChild(empty);
    return;
  }

  entries.forEach(entry => {
    const item = document.createElement("div");
    item.className = "history-item";

    const top = document.createElement("div");
    top.className = "history-top";

    const meta = document.createElement("div");
    const type = document.createElement("div");
    type.className = "history-type";
    type.textContent = humanizeSource(entry.source);

    const date = document.createElement("div");
    date.className = "history-date";
    date.textContent = entry.releaseDate || "--";

    meta.appendChild(type);
    meta.appendChild(date);

    const amount = document.createElement("div");
    amount.className = "history-amount";
    amount.textContent = "₱" + (entry.amount || "0.00");

    top.appendChild(meta);
    top.appendChild(amount);

    const note = document.createElement("div");
    note.className = "history-note";
    note.textContent = entry.remarks || "No notes recorded.";

    item.appendChild(top);
    item.appendChild(note);
    history.appendChild(item);
  });
}

async function initializeLiveCameraSupport() {
  if (!("BarcodeDetector" in window) || !navigator.mediaDevices || !navigator.mediaDevices.getUserMedia) {
    cameraSupportMessage.textContent = "Live camera scan isn't available in this browser. Use Open Camera / Photo below.";
    return;
  }

  try {
    const supportedFormats = BarcodeDetector.getSupportedFormats
      ? await BarcodeDetector.getSupportedFormats()
      : ["qr_code"];

    if (supportedFormats.length && !supportedFormats.includes("qr_code")) {
      cameraSupportMessage.textContent = "This browser can open the camera, but QR detection isn't supported here. Use Open Camera / Photo below.";
      return;
    }

    barcodeDetector = new BarcodeDetector({ formats: ["qr_code"] });
    startLiveScanButton.classList.remove("hidden");
    cameraSupportMessage.textContent = "Live camera scan is ready. Unlock the scanner, then point your camera at the beneficiary QR.";
  } catch (error) {
    console.error(error);
    cameraSupportMessage.textContent = "Live camera scan couldn't be prepared on this browser. Use Open Camera / Photo below.";
  }
}

async function startLiveScan() {
  if (!activePin) {
    document.getElementById("lookupStatus").textContent = "Unlock the scanner with the 6-digit PIN first.";
    return;
  }

  if (!barcodeDetector) {
    document.getElementById("lookupStatus").textContent = "Live camera scan isn't available on this browser. Use Open Camera / Photo below.";
    return;
  }

  stopLiveScan(false);

  try {
    cameraStream = await navigator.mediaDevices.getUserMedia({
      video: {
        facingMode: { ideal: "environment" },
        width: { ideal: 1280 },
        height: { ideal: 720 }
      },
      audio: false
    });

    cameraPreview.srcObject = cameraStream;
    cameraPreview.classList.remove("hidden");
    await cameraPreview.play();

    isCameraScanning = true;
    startLiveScanButton.classList.add("hidden");
    stopLiveScanButton.classList.remove("hidden");
    cameraSupportMessage.textContent = "Camera is live. Hold the beneficiary QR steady and centered.";
    document.getElementById("lookupStatus").textContent = "Scanning live camera feed...";
    scanLiveFrame();
  } catch (error) {
    console.error(error);
    stopLiveScan(false);
    cameraSupportMessage.textContent = "Camera access was blocked or failed. Use Open Camera / Photo below.";
    document.getElementById("lookupStatus").textContent = "Live camera scan couldn't start on this phone.";
  }
}

async function scanLiveFrame() {
  if (!isCameraScanning || !barcodeDetector) {
    return;
  }

  try {
    const barcodes = await barcodeDetector.detect(cameraPreview);
    const match = barcodes.find(code => code.rawValue && code.rawValue.trim());
    if (match) {
      await handleDetectedPayload(match.rawValue.trim(), true);
      return;
    }
  } catch (error) {
    console.error(error);
  }

  if (isCameraScanning) {
    cameraScanTimer = window.setTimeout(scanLiveFrame, 220);
  }
}

async function handleDetectedPayload(qrPayload, fromCamera) {
  stopLiveScan(false);
  document.getElementById("payload").value = qrPayload;
  document.getElementById("lookupStatus").textContent = fromCamera
    ? "QR detected. Loading beneficiary..."
    : "Looking up beneficiary...";

  const response = await postJson("/api/lookup/manual", { sessionToken, pin: activePin, qrPayload });
  renderLookupResponse(response);
}

function stopLiveScan(showStoppedMessage) {
  isCameraScanning = false;

  if (cameraScanTimer) {
    window.clearTimeout(cameraScanTimer);
    cameraScanTimer = 0;
  }

  if (cameraStream) {
    cameraStream.getTracks().forEach(track => track.stop());
    cameraStream = null;
  }

  cameraPreview.pause();
  cameraPreview.srcObject = null;
  cameraPreview.classList.add("hidden");
  stopLiveScanButton.classList.add("hidden");

  if (barcodeDetector) {
    startLiveScanButton.classList.remove("hidden");
  }

  if (showStoppedMessage && activePin) {
    document.getElementById("lookupStatus").textContent = "Live camera scan stopped.";
  }
}

function handleVisibilityChange() {
  if (document.hidden) {
    stopLiveScan(false);
  }
}

function cleanupCamera() {
  stopLiveScan(false);
}

async function markAttendance() {
  if (!lastLookup || !lastLookup.participantId) return;
  const response = await postJson("/api/attendance/mark", {
    sessionToken,
    pin: activePin,
    participantId: lastLookup.participantId,
    qrPayload: lastLookup.qrPayload
  });
  document.getElementById("lookupStatus").textContent = response.message || "";
  setLookupBanner(response.message || "Attendance update finished.", response.success ? "success" : "error");
}

async function markReceived() {
  if (!lastLookup || !lastLookup.beneficiaryStagingId) return;
  const response = await postJson("/api/distribution/claim", {
    sessionToken,
    pin: activePin,
    beneficiaryStagingId: lastLookup.beneficiaryStagingId,
    qrPayload: lastLookup.qrPayload,
    remarks: null
  });

  document.getElementById("lookupStatus").textContent = response.message || "";
  setLookupBanner(response.message || "Project claim update finished.", response.success ? "success" : "error");
  if (response.success && lastLookup.distribution) {
    lastLookup.distribution.alreadyClaimed = true;
    lastLookup.distribution.canMarkReceived = false;
    lastLookup.distribution.message = response.message || "Beneficiary already marked as received.";
    renderLookupResponse(lastLookup);
  }
}

function renderLookupResponse(response) {
  document.getElementById("lookupStatus").textContent = response.message || "";
  if (!response.success) {
    lastLookup = null;
    document.getElementById("resultCard").classList.add("hidden");
    setLookupBanner(response.message || "Lookup failed.", "error");
    return;
  }

  lastLookup = response;
  setLookupBanner(response.message || "Beneficiary found.", "success");
  document.getElementById("resultCard").classList.remove("hidden");
  document.getElementById("name").textContent = response.fullName || "Unnamed beneficiary";
  document.getElementById("cardNumber").textContent = response.cardNumber || "--";
  document.getElementById("beneficiaryId").textContent = response.beneficiaryId || "--";
  document.getElementById("civilRegistryId").textContent = response.civilRegistryId || "--";

  const photo = document.getElementById("photo");
  const placeholder = document.getElementById("photoPlaceholder");
  placeholder.textContent = getInitials(response.fullName);
  if (response.photoUrl) {
    photo.src = response.photoUrl;
    photo.classList.remove("hidden");
  } else {
    photo.classList.add("hidden");
    photo.removeAttribute("src");
  }

  if (response.photoUrl) {
    placeholder.classList.add("hidden");
  } else {
    placeholder.classList.remove("hidden");
  }

  if (response.distribution) {
    if (response.distribution.canMarkReceived) {
      setStatusChip("Qualified", "success");
    } else if (response.distribution.alreadyClaimed) {
      setStatusChip("Already Claimed", "warn");
    } else {
      setStatusChip("Not Qualified", "danger");
    }
  } else if (response.participantId) {
    setStatusChip("Attendance Ready", "success");
  } else {
    setStatusChip("Lookup Match", "success");
  }

  renderModeDetails(response);

  const summary = summarizeReleaseHistory(response.releaseHistory || []);
  setStat("manual", summary.manual.count, summary.manual.amount);
  setStat("cases", summary.cases.count, summary.cases.amount);
  setStat("cashForWork", summary.cashForWork.count, summary.cashForWork.amount);
  setStat("totalAid", summary.total.count, summary.total.amount);

  const markButton = document.getElementById("markAttendance");
  if (response.participantId) {
    markButton.classList.remove("hidden");
  } else {
    markButton.classList.add("hidden");
  }

  const markReceived = document.getElementById("markReceived");
  if (response.distribution && response.distribution.canMarkReceived) {
    markReceived.classList.remove("hidden");
  } else {
    markReceived.classList.add("hidden");
  }

  const resultActions = document.getElementById("resultActions");
  if (response.participantId || (response.distribution && response.distribution.canMarkReceived)) {
    resultActions.classList.remove("hidden");
  } else {
    resultActions.classList.add("hidden");
  }

  renderHistory(response.releaseHistory || []);
}

async function postJson(url, payload) {
  const response = await fetch(url, {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify(payload)
  });
  return await response.json();
}
</script>
</body>
</html>
""";
        }

        private sealed record ScannerPinRequest(string SessionToken, string Pin);
        private sealed record ScannerLookupRequest(string SessionToken, string Pin, string QrPayload);
        private sealed record ScannerAttendanceRequest(string SessionToken, string Pin, int ParticipantId, string? QrPayload);
        private sealed record ScannerDistributionClaimRequest(string SessionToken, string Pin, int BeneficiaryStagingId, string? QrPayload, string? Remarks);
        private sealed record LanAddressCandidate(
            string Address,
            string Name,
            string Description,
            NetworkInterfaceType InterfaceType,
            bool HasGateway)
        {
            private static readonly string[] VirtualInterfaceKeywords =
            {
                "virtual",
                "hyper-v",
                "default switch",
                "virtualbox",
                "host-only",
                "vmware",
                "docker",
                "wsl",
                "vpn",
                "tunnel"
            };

            public bool IsAutomaticPrivateAddress => Address.StartsWith("169.254.", StringComparison.Ordinal);

            public bool IsPreferredInterfaceType =>
                InterfaceType == NetworkInterfaceType.Wireless80211 ||
                InterfaceType == NetworkInterfaceType.Ethernet ||
                InterfaceType == NetworkInterfaceType.GigabitEthernet ||
                InterfaceType == NetworkInterfaceType.FastEthernetFx ||
                InterfaceType == NetworkInterfaceType.FastEthernetT;

            public bool IsVirtualLike
            {
                get
                {
                    var haystack = $"{Name} {Description}";
                    return VirtualInterfaceKeywords.Any(keyword => haystack.Contains(keyword, StringComparison.OrdinalIgnoreCase));
                }
            }
        }
    }
}
