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

                await using var stream = image.OpenReadStream();
                var qrPayload = QrCodeToolkitService.TryDecodePayload(stream);
                if (string.IsNullOrWhiteSpace(qrPayload))
                {
                    return Results.Json(new { success = false, message = "No readable QR code was found in the uploaded image." });
                }

                return await LookupAsync(sessionToken, pin, qrPayload);
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
                var wasSaved = cashForWorkService.SaveScannerAttendance(
                    session.CashForWorkEventId.Value,
                    session.CreatedByUserId,
                    request.ParticipantId,
                    request.QrPayload ?? string.Empty);

                return Results.Json(new
                {
                    success = wasSaved,
                    message = wasSaved
                        ? "Attendance saved."
                        : "Attendance was already marked for this beneficiary."
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
    :root { color-scheme: light; font-family: "Segoe UI", sans-serif; }
    body { margin: 0; background: #e2e8f0; color: #0f172a; }
    main { max-width: 720px; margin: 0 auto; padding: 20px; }
    .card { background: #fff; border-radius: 18px; padding: 18px; box-shadow: 0 12px 30px rgba(15,23,42,.08); margin-bottom: 16px; }
    h1, h2 { margin: 0 0 10px; }
    input, button { width: 100%; box-sizing: border-box; font: inherit; padding: 12px 14px; border-radius: 12px; border: 1px solid #cbd5e1; margin-top: 10px; }
    button { background: #0f766e; color: #fff; border: 0; font-weight: 600; }
    button.alt { background: #1d4ed8; }
    button.warn { background: #92400e; }
    .hidden { display: none; }
    .status { margin-top: 12px; white-space: pre-wrap; }
    .history { margin-top: 12px; }
    .history-item { padding: 10px 0; border-top: 1px solid #e2e8f0; }
    img.photo { width: 120px; height: 120px; object-fit: cover; border-radius: 18px; border: 1px solid #cbd5e1; }
    .muted { color: #475569; }
  </style>
</head>
<body>
<main>
  <div class="card">
    <h1>Beneficiary Scanner</h1>
    <div class="muted">Session token: {{safeToken}}</div>
    <input id="pin" inputmode="numeric" maxlength="6" placeholder="Enter the 6-digit session PIN">
    <button id="unlock">Unlock Scanner</button>
    <div class="status" id="unlockStatus"></div>
  </div>

  <div class="card hidden" id="scannerCard">
    <h2>Lookup Beneficiary</h2>
    <input id="payload" placeholder="Paste or type the beneficiary QR payload">
    <button class="alt" id="lookupManual">Lookup by QR payload</button>
    <input id="image" type="file" accept="image/*" capture="environment">
    <button class="alt" id="lookupImage">Lookup from camera/photo</button>
    <div class="status" id="lookupStatus"></div>
  </div>

  <div class="card hidden" id="resultCard">
    <h2 id="name"></h2>
    <img class="photo hidden" id="photo" alt="Beneficiary photo">
    <div id="ids" class="status"></div>
    <button class="warn hidden" id="markAttendance">Mark Attendance</button>
    <button class="warn hidden" id="markReceived">Mark as Received</button>
    <div class="history" id="history"></div>
  </div>
</main>
<script>
const sessionToken = {{JsonSerializer.Serialize(sessionToken)}};
let activePin = "";
let lastLookup = null;

const unlockButton = document.getElementById("unlock");
const lookupManualButton = document.getElementById("lookupManual");
const lookupImageButton = document.getElementById("lookupImage");
const markAttendanceButton = document.getElementById("markAttendance");
const markReceivedButton = document.getElementById("markReceived");

unlockButton.addEventListener("click", unlockSession);
lookupManualButton.addEventListener("click", lookupManual);
lookupImageButton.addEventListener("click", lookupImage);
markAttendanceButton.addEventListener("click", markAttendance);
markReceivedButton.addEventListener("click", markReceived);

async function unlockSession() {
  const pin = document.getElementById("pin").value.trim();
  const response = await postJson("/api/session/unlock", { sessionToken, pin });
  document.getElementById("unlockStatus").textContent = response.message || (response.success ? "Scanner unlocked." : "Unlock failed.");
  if (!response.success) return;
  activePin = pin;
  document.getElementById("scannerCard").classList.remove("hidden");
}

async function lookupManual() {
  const qrPayload = document.getElementById("payload").value.trim();
  if (!qrPayload) {
    document.getElementById("lookupStatus").textContent = "Enter the beneficiary QR payload first.";
    return;
  }

  const response = await postJson("/api/lookup/manual", { sessionToken, pin: activePin, qrPayload });
  renderLookupResponse(response);
}

async function lookupImage() {
  const fileInput = document.getElementById("image");
  if (!fileInput.files.length) {
    document.getElementById("lookupStatus").textContent = "Capture or select a QR image first.";
    return;
  }

  const data = new FormData();
  data.append("sessionToken", sessionToken);
  data.append("pin", activePin);
  data.append("image", fileInput.files[0]);

  const response = await fetch("/api/lookup/upload", { method: "POST", body: data }).then(result => result.json());
  renderLookupResponse(response);
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
    document.getElementById("resultCard").classList.add("hidden");
    return;
  }

  lastLookup = response;
  document.getElementById("resultCard").classList.remove("hidden");
  document.getElementById("name").textContent = response.fullName || "Unnamed beneficiary";

  const ids = [
    "Card Number: " + (response.cardNumber || "--"),
    "Beneficiary ID: " + (response.beneficiaryId || "--"),
    "Civil Registry ID: " + (response.civilRegistryId || "--")
  ];

  if (response.distribution) {
    ids.push("Project: " + (response.distribution.projectName || "--"));
    ids.push("Assistance: " + (response.distribution.assistanceType || response.distribution.itemDescription || "--"));
    ids.push("Unit Amount: " + (response.distribution.unitAmount || "--"));
    ids.push("Project Status: " + (response.distribution.message || "--"));
  }
  document.getElementById("ids").textContent = ids.join("\n");

  const photo = document.getElementById("photo");
  if (response.photoUrl) {
    photo.src = response.photoUrl;
    photo.classList.remove("hidden");
  } else {
    photo.classList.add("hidden");
  }

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

  const history = document.getElementById("history");
  history.innerHTML = "<h2>Release History</h2>";
  if (!response.releaseHistory || !response.releaseHistory.length) {
    history.innerHTML += '<div class="muted">No release history found.</div>';
    return;
  }

  response.releaseHistory.forEach(entry => {
    const div = document.createElement("div");
    div.className = "history-item";
    div.textContent = entry.releaseDate + " | " + entry.source + " | " + entry.amount + " | " + entry.remarks;
    history.appendChild(div);
  });
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
