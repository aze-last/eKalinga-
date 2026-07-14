# Distribution Module Refactor — Implementation Plan (Review Only)

**Status:** Plan only. No code written. No schema changes assumed until confirmed.
**Source:** Client meeting transcript `1784010365741.docx` (Cebuano/English), audited against the current eKalinga+ repository.
**Scope owner:** `distribution-worker@attendance-shifting-management` (template: `.agent-team/templates/distribution-worker.md`).

---

## 0. Phase 1 Audit — Checklist Verdict

Repository audited first-hand (files + line cites throughout this doc). Verdict against the requested checklist:

| Capability | Status | Evidence |
|---|---|---|
| Current workflow (scan → review → confirm → release) | **EXISTS** | `ProjectDistributionViewModel.cs:1660, 1773`; `ProjectDistributionService.RecordClaimAsync:561` |
| Existing services | **EXISTS** | `ProjectDistributionService`, `BeneficiaryDigitalIdService`, `BudgetManagementService`, `BeneficiaryAssistanceLedgerService`, `ReportsService`, `AuditService` |
| Existing commands | **EXISTS** | `ConfirmScannedClaimCommand`, `CancelScannedClaimCommand`, `ProcessScanCommand`, pending/released paging cmds |
| Existing dialogs / overlays | **EXISTS** | Scanned-result + release-success overlays `ProjectDistributionPage.xaml:564, 712`; Add-Beneficiary / Create-Project / Scanner-Config panels |
| Existing barcode/QR workflow | **EXISTS** | `LookupByQrPayloadAsync` `BeneficiaryDigitalIdService.cs:148`; payload = `ASM-BID\|…` (Beneficiary identity) |
| Existing beneficiary selection flow | **EXISTS (needs perf fix)** | `LoadAvailableBeneficiariesAsync:1337` loads full approved masterlist into memory + client-side filter — the "messy sidebar" |
| Project select + **search by ID** | **MISSING** | Projects render as plain `ItemsControl ProgramSummaries` `ProjectDistributionPage.xaml:163`; no search, no on-open picker → **R5/C4** |
| Manual Beneficiary-ID key-in | **MISSING** | No key-in path; only QR resolves → **R1/C1** |
| Household verification (display + members) | **MISSING** | Scanned panel shows individual only; no household fan-out → **R2/R3, C2** |
| Household-level "already received" guard | **MISSING** | `FindExistingClaimAsync:874` is per-person; `AyudaProjectClaim.HouseholdId` persisted but never queried → **C3** |
| Confirmation dialog | **EXISTS** | `IsScannedResultVisible` overlay + Confirm/Cancel `ProjectDistributionPage.xaml:706–707` |
| Ledger updates | **EXISTS — LOCKED** | `RecordClaimAsync` → `BeneficiaryAssistanceLedgerService.RecordEntryAsync:731` |
| Audit logs | **EXISTS** | `AuditService.LogActivityAsync` at every mutation (`ProjectDistributionService.cs:143, 258, 426, 551, 746`) |
| Budget deduction | **EXISTS — LOCKED** | `RecordWaterfallReleaseAsync:708`, budget-cap guard `:663`, GGMS sync `:744` |
| Report generation | **EXISTS** | `ReportsService` `DistributionClaims` type, per-program filter `ReportsService.cs:441–490` |
| No regression in Project Distribution | **CONSTRAINT** | All new work additive; locked budget/ledger/report/audit paths untouched (see Verification Plan) |

**Net:** 4 genuine gaps → **R1** (key-in), **R2/R3** (household verify + guard), **R5** (project search/scoping). Everything else exists and is reused, not rebuilt. No schema change required (see §9).

---

## 1. Client Requirements (decoded from transcript)

| # | Topic | Client ask |
|---|-------|-----------|
| R1 | Barcode + Beneficiary ID fallback | At release, scan QR/barcode (which encodes the **Beneficiary ID**). If the scanner fails, the operator must be able to **manually key-in the Beneficiary ID**, the profile must appear, then **Confirm**. "Two options: QR, or key-in." |
| R2 | Profile preview before confirm | Before confirming a release, the operator must **see the beneficiary profile** ("makita daan… kung siya ba to") to visually verify identity. |
| R3 | Household / family members validation | Before confirm, show the beneficiary's **household/family members**, and surface whether **anyone in the household already received** (transcript: three same-surname members received; "before ma-award… makita pod sa household niya"). |
| R4 | Project-scoped distribution | Beneficiaries are attached **per project** (project → budget → beneficiaries → release). Distribution is just "release." |
| R5 | Searchable project pop-up | With ~50–100 projects, "distribute" must open a **searchable pop-up to pick the project** ("dapat mag-search ra siya… 'Bigasan ng Bayan'… pop-up didto mag-select"). |

---

## 2. Architecture Overview

The Project Distribution module already implements the majority of this workflow. It is a standard MVVM slice:

- **View:** `Views/ProjectDistributionPage.xaml` (+ overlay panels: `ProjectDistributionAddBeneficiaryPanel`, `ProjectDistributionCreateProjectPanel`, `ProjectDistributionScannerConfigPanel`, `ProjectDistributionLivePreviewWindow`).
- **ViewModel:** `ViewModels/ProjectDistributionViewModel.cs` (~2941 lines; scanner-first POS logic, pagination, overlay state).
- **Service:** `Services/ProjectDistributionService.cs` (all release/claim/eligibility logic).
- **Identity resolution:** `Services/DigitalId/BeneficiaryDigitalIdService.cs` — `LookupByQrPayloadAsync`.
- **Data:** `Data/AppDbContext.cs` / `LocalDbContext` — entities `AyudaProgram` (project), `AyudaProjectBeneficiary` (per-project enrollment + status), `AyudaProjectClaim` (release record), `BeneficiaryStaging` (beneficiary), `Household` / `HouseholdMember`, `BudgetLedgerEntry`, `BeneficiaryAssistanceLedgerEntry`.

### What already exists (document — do NOT rebuild)

- **R2 Profile preview — EXISTS.** `ExecuteProcessPcScan` (`ProjectDistributionViewModel.cs:1660`) resolves the scan, populates `ScannedBeneficiary`, `ScannedBeneficiaryPhoto` (`:1712`), status, and prior-project history (`:1735`), sets `IsScannedResultVisible = true` (`:1760`), and requires an explicit `ConfirmScannedClaimCommand` → `ExecuteConfirmScannedClaimAsync` (`:1773`) before the claim is written. Confirm-before-release is already the flow.
- **R4 Project-scoped model — EXISTS.** Enrollment via `AddBeneficiaryAsync` / `BulkAddBeneficiariesAsync` (`ProjectDistributionService.cs:46`, `:153`); release via `RecordClaimAsync` (`:561`). Membership is keyed on `AyudaProgramId`. Budget waterfall is correctly coupled: `RecordClaimAsync` calls `_budgetService.RecordWaterfallReleaseAsync(... BudgetLedgerFeatureSource.ProjectDistribution ...)` (`:708`) and writes the assistance ledger (`:731`). **Do not touch this coupling** (Gemini.md budget lock).
- **Per-beneficiary duplicate guard — EXISTS.** `EvaluateQualificationAsync` (`:439`) → `FindExistingClaimAsync` (`:874`) blocks a second claim by the same individual (by ProjectBeneficiaryId / BeneficiaryId / CivilRegistryId / StagingId).
- **Pagination — EXISTS.** `DistributionPageSize = 10`, pending/released page commands (`ProjectDistributionViewModel.cs:65–66, 142–145`).
- **Scanner-first POS behavior — EXISTS.** Queue protection, success-only cooldown, `Console.Beep` cues, auto-closing success overlay (matches `distribution-worker.md` §20–28).
- **QR payload = Beneficiary identity — EXISTS.** Payload format `ASM-BID|…` / `ASMBID……` resolved by `LookupByQrPayloadAsync` (`BeneficiaryDigitalIdService.cs:148`); extracts a 6-digit staging id at `:178`.

### Gaps (the real work)

| Req | Status | Gap |
|-----|--------|-----|
| R1 manual key-in fallback | **MISSING** | No path to type a Beneficiary ID and resolve the same record. Scan path only accepts a QR payload via `LookupByQrPayloadAsync`. No `LookupByBeneficiaryIdAsync`. |
| R3 household members display | **MISSING** | Scanned-result panel shows the individual only (photo, status, prior-project history). No household member list rendered. No service method fans out across a household. |
| R3 household-level "already received" | **MISSING** | Duplicate detection is per-individual (`FindExistingClaimAsync`). It does **not** check whether another member of the same `HouseholdId` already claimed this project — even though `AyudaProjectClaim.HouseholdId` is already persisted (`ProjectDistributionService.cs:682`). Data is captured but never queried for this rule. |

---

## 3. Dependency Map

```
ProjectDistributionPage.xaml  (scanned-result / confirm overlay)
        │  binds
        ▼
ProjectDistributionViewModel.cs
   ├─ (source: SCAN)   ExecuteProcessPcScan  ─┐
   ├─ (source: KEY-IN) ExecuteManualKeyIn    ─┤ both build a BeneficiaryLookupRequest
   │                                          ▼
   │                    ONE pipeline: ResolveAndPresentAsync(request)   [R1 unified]
   │                       → BeneficiaryDigitalIdService.ResolveLookupAsync(request)
   │                            ├─ source=QrPayload      → existing LookupByQrPayloadAsync
   │                            └─ source=BeneficiaryId  → (NEW) resolve by BeneficiaryStaging.BeneficiaryId
   │                       → populates IDENTICAL VM state (ScannedBeneficiary, photo,
   │                         ScannedHouseholdMembers, IsIdentityVerified, IsScannedResultVisible)
   │
   ├─ ScannedHouseholdMembers ← (NEW) ProjectDistributionService.GetHouseholdVerificationContextAsync
   │                              → returns HouseholdVerificationContext DTO (NO EF entities) [R2/R3]
   │
   └─ ExecuteConfirmScannedClaimAsync  [gated on IsIdentityVerified — Confirm cannot fire otherwise]
                                              → ProjectDistributionService.RecordClaimAsync
                                              └─ EvaluateQualificationAsync
                                                     ├─ FindExistingClaimAsync            [per-person, exists]
                                                     └─ (NEW) FindHouseholdClaimAsync     [R3 household guard]
                                              └─ _budgetService.RecordWaterfallReleaseAsync  [LOCKED — unchanged]
                                              └─ BeneficiaryAssistanceLedgerService.RecordEntryAsync [unchanged]

Data model reuse (no schema change expected):
  BeneficiaryStaging.LinkedHouseholdId / .BeneficiaryId / .PhotoPath
  HouseholdMember.HouseholdId  (→ all members of a household)
  AyudaProjectClaim.HouseholdId (already written on every claim)
```

**No new NuGet packages. No new DbSets. No public API removals.**

---

## 3a. Architect Mandates (binding constraints on all changes below)

**M1 — Unify the lookup pipeline.** There must be exactly **one** beneficiary-resolution path. Introduce a small request object (`BeneficiaryLookupRequest { LookupSource Source; string Value; }`, `LookupSource ∈ { QrPayload, BeneficiaryId }`) and a single service entry `ResolveLookupAsync(request)` that returns the **same** `BeneficiaryDigitalIdLookupResult` regardless of source. Manual key-in does **not** get its own confirm/claim logic — it constructs a request and flows through the **existing** scan → review → confirm pipeline. QR resolution stays exactly as-is behind the QrPayload branch.

**M2 — No EF entities in the ViewModel.** New service→VM data crosses as DTOs only. Add `HouseholdVerificationContext` (household head/code + `IReadOnlyList<HouseholdMemberVerificationItem>` with name, relationship, and `AlreadyClaimedThisProject` bool). The **service** maps `Household`/`HouseholdMember`/`AyudaProjectClaim` → DTO; the VM never sees those entities. (Note: the pre-existing `BeneficiaryDigitalIdLookupResult.ReleaseHistory` already exposes an EF entity — out of scope to refactor now, but **do not add** any new EF exposure.)

**M3 — Confirm must never bypass identity verification.** Add an explicit `IsIdentityVerified` gate. `ConfirmScannedClaimCommand.CanExecute` must require it (in addition to eligibility). The operator must have a resolved, displayed profile (photo/identity shown) before Confirm is enabled — for **both** scan and key-in. Server-side, `RecordClaimAsync` keeps its own identity re-check; the key-in path must pass an equivalent verifiable token, not skip it.

**Change discipline:** additive, reuse-first, repository-first; smallest possible surface area; **no schema changes** unless a repo audit proves them strictly necessary (current audit: none required).

---

## 4. Proposed Changes (smallest safe path)

> All changes stay inside the distribution-worker allowed paths: `Views/ProjectDistribution*.xaml(.cs)`, `ViewModels/ProjectDistributionViewModel.cs`, `Services/ProjectDistributionService.cs`, plus a small reuse method in `Services/DigitalId/BeneficiaryDigitalIdService.cs`.

### C1 — R1: Unified lookup pipeline (QR + manual key-in) — satisfies M1
- **Contract (new, small):** `BeneficiaryLookupRequest { LookupSource Source; string Value; }` and `enum LookupSource { QrPayload, BeneficiaryId }`.
- **Service (single entry):** add `ResolveLookupAsync(BeneficiaryLookupRequest request)` to `BeneficiaryDigitalIdService`. Internally:
  - `QrPayload` → delegates to the **existing** `LookupByQrPayloadAsync` (unchanged).
  - `BeneficiaryId` → resolves `BeneficiaryStaging` by `BeneficiaryId` (indexed), then returns via the **same mapping** that produces `BeneficiaryDigitalIdLookupResult` at `BeneficiaryDigitalIdService.cs:292`.
  - Both branches return the **identical** `BeneficiaryDigitalIdLookupResult`. No duplicated resolution logic.
- **ViewModel:** refactor `ExecuteProcessPcScan` so its post-resolution body (populate `ScannedBeneficiary`, photo, history, household, `IsScannedResultVisible`) becomes a shared `PresentLookupResult(result)`. Both `ExecuteProcessPcScan` (builds a `QrPayload` request) and a new `ExecuteManualKeyIn` (builds a `BeneficiaryId` request) call `ResolveAndPresentAsync(request)` → `PresentLookupResult`. **One presentation path, one confirm path.**
- **View:** add a "Key-in Beneficiary ID" input + submit next to the scanner in `ProjectDistributionScannerConfigPanel.xaml` (or the page scanner area). It opens the **same** review-then-confirm overlay — no separate dialog, no separate confirm.

### C2 — R2/R3: Household verification context as a DTO — satisfies M2
- **DTO (new, UI-only):** `HouseholdVerificationContext { string HouseholdCode; string HeadName; bool HasHousehold; IReadOnlyList<HouseholdMemberVerificationItem> Members; }` and `HouseholdMemberVerificationItem { string FullName; string RelationshipToHead; bool IsScannedBeneficiary; bool AlreadyClaimedThisProject; }`. No EF types cross to the VM.
- **Service:** add `GetHouseholdVerificationContextAsync(int ayudaProgramId, int beneficiaryStagingId)` on `ProjectDistributionService`. Resolves `LinkedHouseholdId`, loads `HouseholdMember` rows for that household, LEFT-joins `AyudaProjectClaims` filtered by `AyudaProgramId` (single server-side query set, no N+1), and **maps entities → DTO inside the service**. Returns `HasHousehold=false` when the beneficiary has no `LinkedHouseholdId`.
- **ViewModel:** populate `ObservableCollection<HouseholdMemberVerificationItem> ScannedHouseholdMembers` from the DTO inside the shared `PresentLookupResult` (so scan and key-in both show it).
- **View:** render the member list in the scanned-result overlay (blurred-overlay standard already present), highlighting any member already claimed. Read-only.

### C3 — R3: Household-level duplicate guard (business rule)
- **Service:** add `FindHouseholdClaimAsync(int ayudaProgramId, AyudaProjectBeneficiary membership)` — returns any existing `AyudaProjectClaim` for the **same `HouseholdId`** and program (private helper; entity stays in the service). Call it in `EvaluateQualificationAsync` **after** the per-person `FindExistingClaimAsync`. Surface the outcome on the existing `ProjectDistributionQualificationResult` (a new flag/message), so the UI never touches the entity.
- **Policy decision required (see Open Questions):** hard block vs. soft warning + override. Implement behind the qualification result so the UI can render either. Default proposed: **soft warning + explicit override** (transcript stresses operator *visibility*, not absolute lockout).

### C3a — M3: Confirm gated on identity verification
- **ViewModel:** add `IsIdentityVerified` (set true only after `PresentLookupResult` displays a resolved profile). `ConfirmScannedClaimCommand.CanExecute` = `IsIdentityVerified && IsScannedBeneficiaryEligible && !IsBusy`. Reset on cancel/new scan.
- **Service:** `RecordClaimAsync` retains its identity re-check (`ProjectDistributionService.cs:600–624`). The key-in path supplies a resolvable token (the beneficiary's stored QR payload / BeneficiaryId) so the server-side verification runs for key-in too — Confirm never bypasses it.

### C4 — R5: Project-first selection + searchable picker + scoped sidebar
**Audit result (confirmed):** projects render as a plain `ItemsControl` bound to `ProgramSummaries` (`ProjectDistributionPage.xaml:163`) — **no search, no on-open picker**. The add-beneficiary sidebar's `LoadAvailableBeneficiariesAsync` (`ProjectDistributionViewModel.cs:1337`) already filters candidates by `SelectedProgram` (`:1342–1349`), but loads the **entire** approved masterlist into memory first (`:1355–1371`) — the "messy sidebar." So R5 is a real gap.

Two sub-parts (client's ask: *choose the project first, then the sidebar only shows that project's context*):

- **C4a — Searchable project picker (search-by-ID/name).** Add `ProgramSearchText` + a filtered view over the already-loaded `ProgramSummaries` (client-side filter is fine — project count is small/bounded). Search matches `ProgramName` and `ProgramCode`/Id. Reuses existing `SelectedProgramSummary`/`SelectedProgram` state — no new selection model.
- **C4b — Project-first gate.** On module open, require a project to be chosen before the sidebar/queue is usable. **Decision required (Open Q):**
  - *(a)* Blocking modal picker on `Loaded` (aligns with the new "Redesign Admin List Pages" Pattern A — modal list dialog on open, BROWSE/SELECT to reopen), **or**
  - *(b)* Non-blocking: keep the page, but disable add/scan until `SelectedProgram != null` and show an empty-state prompt.
  Recommendation: **(a)** for the picker UX, but scoped to *project* selection only — do **not** restructure the whole page into Pattern A/B in this pass (that's a separate, larger redesign task; keep this change minimal).
- **C4c — Sidebar scoping + perf.** Change `LoadAvailableBeneficiariesAsync` to **not** run until a project is selected, and push the "approved + not-already-enrolled" filter **server-side** (query `BeneficiaryStaging` with a `Where`/anti-join against `AyudaProjectBeneficiaries` for `SelectedProgram.Id`) instead of loading the full masterlist and filtering in memory. Add search + paging consistent with the existing pending/released pagination. This directly delivers "the messy sidebar will be lessened, only the chosen project is visible."

> Relationship to the "Redesign Admin List Pages" task file (Downloads, 2026-07-14): that spec's **Pattern A on-open modal list dialog** is the same shape as C4b(a), and **Pattern B dual-grid** matches the existing Pending|Released layout. Treat that redesign as a **separate follow-up**; this plan only adds project-first selection + scoping, not a full page re-layout, to keep the surface area small.

### Out of scope / explicitly NOT touched
- Budget waterfall / `RecordWaterfallReleaseAsync` coupling (Gemini.md lock).
- Assistance ledger writing.
- Remote-write routing (`RemoteWriteExecutionService`) — reused, not modified.
- Dashboard (Gemini.md dashboard lock).
- No DB row deletions anywhere (Gemini.md).

---

## 5. Performance Review

- **R3 household + already-claimed:** MUST be one join (members ⋈ claims-for-program), not a per-member claim lookup — otherwise N+1. Server-side `Where` on `AyudaProgramId` + `HouseholdId`.
- **R1 manual lookup:** single indexed query on `BeneficiaryStaging.BeneficiaryId` (mirror the QR path). No table scan.
- **R5 project search:** filter the already-in-memory `ProgramSummaries`; no new DB round-trip per keystroke.
- **Sidebar (C4c) — fixes an existing hotspot:** `LoadAvailableBeneficiariesAsync` currently loads the **entire** approved masterlist via `.ToListAsync()` then filters in memory (`ProjectDistributionViewModel.cs:1355–1371, 1373–1406`). Refactor to a **server-side** anti-join on `AyudaProjectBeneficiaries` for `SelectedProgram.Id` + server-side search + paging. Net: fewer rows loaded, no client-side full-list scan, and it only runs after a project is chosen.
- Existing pending/released pagination (page size 10) is preserved. Confirm the household query is only issued **on scan/key-in**, not per list row.
- **Verify no new `.ToListAsync()` over full beneficiary/claim tables** is introduced, and that C4c *removes* the existing full-masterlist load.

---

## 9. Schema Change Analysis (required by architect brief)

**Verdict: NO schema changes required.** Every gap is satisfied by existing tables and relationships:

| Requirement | Satisfied by existing schema | Why no new column/table |
|---|---|---|
| R1 manual key-in | `BeneficiaryStaging.BeneficiaryId` (indexed identity) | Same field the QR path already resolves to; just a second lookup source. |
| R2 profile preview | `BeneficiaryStaging.PhotoPath` + `BeneficiaryDigitalId` | Already surfaced for the scan path. |
| R3 household members display | `HouseholdMember.HouseholdId` ← `BeneficiaryStaging.LinkedHouseholdId` | One-to-many already modeled; fan-out is a query, not a schema change. |
| R3 household duplicate guard | `AyudaProjectClaim.HouseholdId` (already written at claim, `ProjectDistributionService.cs:682`) | Data is persisted today; only a **query** is missing, not a column. |
| R5 project search + scoping | `AyudaProgram` list + `AyudaProjectBeneficiary.AyudaProgramId` | Existing FKs support the anti-join/scoping. |

**If a future requirement forces a schema change**, it must be documented here with: the exact requirement, why the existing relationships above cannot satisfy it, the proposed migration, and a backward-compatibility note for Budget/Ledger/Reports/Audit. As of this audit, none is triggered. DTOs (`HouseholdVerificationContext`, `BeneficiaryLookupRequest`) are **code-only** types — not persisted, not migrations.

---

## 6. User Review Required

1. **R3 enforcement level:** hard-block vs. soft-warning-with-override when a household member already claimed the same project. *(Recommend: soft warning + override.)*
2. **R3 household scope:** define "already received" as **same project only**, or **cross-project within an assistance type**? Transcript implies per-project; confirm.
3. **R1 identity match on manual key-in:** should a keyed-in ID still require the operator to visually confirm the photo before Confirm is enabled (recommended), or allow blind confirm?
4. **R5 audit closed — decision needed:** no searchable project picker exists today (`ProjectDistributionPage.xaml:163`). Choose the **project-first** UX (C4b): *(a)* blocking modal picker on open (Pattern-A style) or *(b)* non-blocking with disabled add/scan + empty state until a project is chosen. *(Recommend: a.)*
5. **Scope of the "Redesign Admin List Pages" task:** confirm it is a **separate follow-up** and this pass only adds project-first selection + sidebar scoping (not a full Pattern A/B page re-layout).
6. **`/ui-ux-pro-max` skill is not installed** in this environment (only the `ui-ux-auditor-redesigner` agent template exists). Approve using the existing template/design-system for the UI additions, or provide the skill.

## 7. Open Questions

- Does an operator ever release for a **member** (`HouseholdMemberId`) distinct from the enrolled staging beneficiary, or always the enrolled beneficiary? (`AyudaProjectClaim` supports `HouseholdMemberId`, but the release flow currently claims the enrolled `BeneficiaryStaging`.)
- Should the manual key-in be permission-gated (e.g., supervisor override) since it bypasses the physical ID scan?
- For households with no `LinkedHouseholdId` on the beneficiary, R3 degrades to individual-only — acceptable? (Recommend: show "no household linked" note.)

---

## 8. Verification Plan

1. **Build:** `dotnet build` (Gemini.md verification rule). Stop after 3 failed attempts.
2. **Unit:** add/extend tests in `AttendanceShiftingManagement.Tests` for:
   - `ResolveLookupAsync` returns the **same** `BeneficiaryDigitalIdLookupResult` for a `QrPayload` request and a `BeneficiaryId` request pointing at the same beneficiary (M1). Null for unknown value on either source.
   - `GetHouseholdVerificationContextAsync` returns a DTO (no EF types), flags the correct member as `AlreadyClaimedThisProject`, and `HasHousehold=false` when unlinked (M2).
   - `FindHouseholdClaimAsync` returns a claim when a *different* member of the same household already claimed; null otherwise.
   - `EvaluateQualificationAsync` surfaces the household state without breaking existing per-person checks.
   - Confirm gate (M3): `ConfirmScannedClaimCommand.CanExecute` is false until identity is verified, for both scan and key-in.
3. **Manual smoke (POS flow):**
   - Scan QR → profile + household list appears → confirm → released.
   - Fail scan → key-in Beneficiary ID → same profile + household list → confirm → released.
   - Attempt release for a household whose member already claimed → correct block/warn per decision (1).
   - Confirm budget ledger entry and assistance ledger entry still written exactly once (no double-count).
4. **Perf check:** enable `ScanDiagnosticLogger` and confirm one household query per scan, no per-row claim queries; confirm the sidebar no longer loads the full masterlist (C4c).
5. **No DB deletions** introduced (grep the diff for `Remove(`/`RemoveRange(` outside the existing rollback at `ProjectDistributionService.cs:722`).
6. **No-regression sweep (backward compatibility):** after changes, verify each locked/adjacent module still behaves identically —
   - **Budget:** a cash release still writes one `RecordWaterfallReleaseAsync` entry; budget-cap guard still trips.
   - **Ledger:** one `BeneficiaryAssistanceLedgerService.RecordEntryAsync` per claim (no double-count).
   - **Reports:** `DistributionClaims` report still renders unchanged rows.
   - **Audit:** `LogActivityAsync` still fires on claim/status/enroll.
   - **Existing Project Distribution flow** (scan → confirm) unchanged for beneficiaries with no household link.

---

### Summary
- Module is ~80% built. **R2 (profile preview), R4 (project-scoped release), budget coupling, pagination, scanner-first POS all already exist — document, don't rebuild.**
- Real work is additive and low-risk: **R1** manual key-in via a **single unified lookup pipeline** (M1 — QR and key-in produce one result, one confirm path), **R2/R3** household verification as a **DTO** (M2 — no EF entities in the VM) plus the household-level duplicate guard, and **M3** a Confirm gate that cannot fire without a verified, displayed identity. **R5** searchable project picker only if the C4 audit confirms it's absent.
- Architect mandates M1/M2/M3 folded into §3a and C1–C3a. Repository-first, reuse-first, additive-only, **no schema changes** (audit confirms none needed), no locked logic touched.
