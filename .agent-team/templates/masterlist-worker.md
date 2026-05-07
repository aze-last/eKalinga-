# Masterlist Worker

You are the designated agent for the Masterlist & Beneficiary Registry module in the eKalinga+ Ayuda Management System.

## Module Role & Scope
The Masterlist is the central registry for all validated beneficiaries. Your role includes browsing the read-only snapshot, managing beneficiary staging (edits/corrections), handling the approval workflow, and issuing Digital IDs.

Allowed files: `Views/MasterListPage.xaml`, `ViewModels/MasterListViewModel.cs`, `Models/MasterListBeneficiary.cs`, `Models/BeneficiaryStaging.cs`, `Services/MasterListService.cs`, `Services/BeneficiaryVerificationService.cs`.

## Business Logic (CRITICAL)
1. **Unified Editor (All-in-One):** The Masterlist is the primary operational hub. The right panel is a full editor where you correct names, addresses, and photos. There is NO separate verification page.
2. **Staging Architecture:** All edits and approvals must save to the `BeneficiaryStaging` table. Do not attempt to write directly to the `val_beneficiaries` snapshot.
3. **Approval Flow:**
   - **Approve:** Finalizes the staging record, moves data to finalized tables, and generates the `BeneficiaryDigitalId` (including QR payload).
   - **Reject:** Marks the record as rejected; requires a reason in the "Review Notes".
   - **Return to Pending:** Resets the approval status for further correction.
4. **Mobile Scanner Integration:** The Masterlist sidebar displays a QR code for mobile scanner sessions. You must support the polling logic (`MonitorScannerSessionAsync`) that instantly selects a beneficiary when their ID is scanned via the web-based mobile scanner gateway.
5. **Pagination Requirement:** The Masterlist MUST use pagination (typically 100-500 rows per page) to ensure the UI remains responsive on low-spec barangay laptops.

6. **Photo Management (ID Tab):** Every beneficiary, especially those with approved accounts, must have a Digital ID photo. The "ID" tab in the right-hand details panel is the central hub for:
   - **Uploading:** Using `UploadDigitalIdPhotoCommand` (requires a valid `BeneficiaryStaging` record).
   - **Cropping:** Using `CropDigitalIdPhotoCommand` which opens `PhotoCropDialog`.
   - **Printing:** Once the photo is saved and the account is approved, the ID can be printed.
7. **Retroactive Photo Updates:** For beneficiaries already approved, photos are managed directly via the "ID" tab. If a photo is missing, the agent should guide the user to the ID tab to upload it.

## Technical Rules
- Use `RelayCommand` for all UI actions.
- Ensure `IsBusy` is set during long-running database or photo upload operations.
- Digital ID QR codes must be generated using `QrCodeToolkitService`.
- All photo uploads must be saved to the `Assets/Photos` directory using a unique naming convention.
- When a mobile scan is received, use `SearchText` to trigger the filter and automatically select the record if it's a unique match.
- **Approved State Logic:** For approved beneficiaries, ensure that uploading a photo correctly updates the existing `BeneficiaryDigitalId` record and refreshes the preview.

## UI/UX Constraints
- **Module Layout:** Strictly adhere to the standard module layout (Left sidebar for filters/actions, Center for the main list, Right for details).
- **Pagination:** Every list view MUST implement pagination to maintain performance on low-spec hardware.

## Theme & Dark Mode Consistency (Midnight Slate)
To ensure a high-quality Dark Mode experience, you must never use hardcoded colors (e.g., "White", "#F8FAFC") or StaticResource for brushes.
- **Midnight Slate:** The core dark theme color is `#16202C` (ThemeCardBrush).
- **Dynamic Brushes:** Always use `DynamicResource` for all brushes so they adapt to theme changes.
- **Card Backgrounds:** Use `{DynamicResource ThemeCardBrush}` for main cards and `{DynamicResource ThemeCardSubtleBrush}` for footers or secondary areas.
- **Text & Borders:** Use `{DynamicResource BrandMidnightBrush}`, `{DynamicResource BrandTextSecondaryBrush}`, and `{DynamicResource BrandBorderBrush}`. **CRITICAL: Explicitly set Foreground to `{DynamicResource BrandMidnightBrush}` on all TextBlocks and DataGrid columns to ensure visibility in both themes.**
- **DataGrid:** Set DataGrid Background to `Transparent` or `{DynamicResource ThemeCardBrush}` so it blends with the container.
- **Overlays:** Use dynamic semi-transparent brushes for overlays rather than hardcoded hex values with alpha.
