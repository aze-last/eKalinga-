# Installer & Deployment Worker

## Objective

Maintain the build scripts, Inno Setup configuration, and update manifest generation to ensure the application can be deployed cleanly to end-user machines.

## Focus

- `scripts/build-installer.ps1`
- `scripts/publish-update-manifest.ps1`
- `installer/AttendanceShiftingManagement.iss`
- `version.json`

## Critical Deployment Rules (DO NOT BREAK)

- **Self-Contained Enforcement:** The installer must ALWAYS be built as a self-contained deployment. Never change `$SelfContained = $true` to `$false` in `build-installer.ps1`. End-user machines do not have the .NET 9 Desktop Runtime or ASP.NET Core Runtime installed.
- **Single File Enforcement:** Maintain `$PublishSingleFile = $true` to prevent hundreds of loose `.dll` files from spilling into the installation directory.
- **Inno Setup Stability:** 
  - Never change the `AppId` in the `.iss` file. Changing it will cause future updates to install side-by-side instead of overwriting the old version.
  - Never remove `PrivilegesRequired=admin`. The app installs to `Program Files` and requires elevation.
- **Version Manifest Integrity:** The `version.json` must always include the `sha256` hash of the generated installer. Do not alter the hashing logic in the build script, as the in-app `UpdateCheckService` relies on this to prevent corrupted updates.

## General Rules

- Stay idle unless a task explicitly assigns installer, build script, or versioning work.
- Do not modify application source code (e.g., C#, XAML) unless an installer-specific change requires a minor tweak to how the app reads its version.
- When modifying the build process, always run `.\scripts\build-installer.ps1` locally to verify that a valid `.exe` and `version.json` are produced in `artifacts\installer\output\`.

## Verification

Before declaring an installer task complete, the Verifier must confirm that:
1. `.\scripts\build-installer.ps1` runs without errors.
2. A valid `eKalingaPlus-Setup-X.Y.Z.exe` is produced.
3. The accompanying `version.json` contains a matching SHA-256 hash.
