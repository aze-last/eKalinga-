$path = "c:\Users\ASUS\source\repos\eKalinga-\ViewModels\ProjectDistributionViewModel.cs"
$code = Get-Content $path -Raw -Encoding UTF8

$target = @"
        public string HouseholdContextSummary
        {
            get => _householdContextSummary;
            private set => SetProperty(ref _householdContextSummary, value);
        }
"@

$replacement = @"
        public string HouseholdContextSummary
        {
            get => _householdContextSummary;
            private set => SetProperty(ref _householdContextSummary, value);
        }

        public string HouseholdDemographicsSummary
        {
            get => _householdDemographicsSummary;
            private set => SetProperty(ref _householdDemographicsSummary, value);
        }
"@
$code = $code.Replace($target, $replacement)

$target2 = @"
            HouseholdContextSummary = string.Empty;
            HouseholdWarningMessage = null;
"@

$replacement2 = @"
            HouseholdContextSummary = string.Empty;
            HouseholdDemographicsSummary = string.Empty;
            HouseholdWarningMessage = null;
"@
$code = $code.Replace($target2, $replacement2)

$target3 = @"
            HouseholdContextSummary = string.Empty;
            HouseholdAidReceivedSummary = string.Empty;
"@

$replacement3 = @"
            HouseholdContextSummary = string.Empty;
            HouseholdDemographicsSummary = string.Empty;
            HouseholdAidReceivedSummary = string.Empty;
"@
$code = $code.Replace($target3, $replacement3)

$target4 = @"
            RequiresHouseholdOverride = householdContext.AnyMemberAlreadyReceived;
            HouseholdOverrideAcknowledged = false;
"@

$replacement4 = @"
            RequiresHouseholdOverride = householdContext.AnyMemberAlreadyReceived;
            HouseholdOverrideAcknowledged = false;

            var cachedDemographics = default(AttendanceShiftingManagement.Models.LocalOnlyModels.CrsDemographicsCache);
            await using (var context = new LocalDbContext())
            {
                var stagingRow = await context.BeneficiaryStaging
                    .AsNoTracking()
                    .FirstOrDefaultAsync(b => b.Id == beneficiaryStagingId);
                    
                if (stagingRow != null && !string.IsNullOrWhiteSpace(stagingRow.BeneficiaryId))
                {
                    cachedDemographics = await context.CrsDemographicsCaches
                        .AsNoTracking()
                        .FirstOrDefaultAsync(row => row.BeneficiaryId == stagingRow.BeneficiaryId);
                }
            }
            HouseholdDemographicsSummary = FormatDemographicsLine(cachedDemographics);
"@
$code = $code.Replace($target4, $replacement4)

$target5 = @"
        /// <summary>
        /// Confirm on the scan overlay. Always opens the Household Review modal so the operator sees
"@

$replacement5 = @"
        /// <summary>One-line demographics summary from the CRS cache, or a hint when nothing is cached yet.</summary>
        private static string FormatDemographicsLine(AttendanceShiftingManagement.Models.LocalOnlyModels.CrsDemographicsCache? demographics)
        {
            if (demographics == null)
            {
                return ""No CRS demographics cached yet — refresh the masterlist while online."";
            }

            var parts = new List<string>();
            if (!string.IsNullOrWhiteSpace(demographics.MaritalStatus))
            {
                parts.Add($""Marital status: {demographics.MaritalStatus}"");
            }
            if (!string.IsNullOrWhiteSpace(demographics.Ethnicity))
            {
                parts.Add($""Ethnicity: {demographics.Ethnicity}"");
            }
            if (!string.IsNullOrWhiteSpace(demographics.Tribe))
            {
                parts.Add($""Tribe: {demographics.Tribe}"");
            }

            return parts.Count > 0
                ? string.Join(""  •  "", parts)
                : ""CRS demographics on file, but no marital status, ethnicity, or tribe recorded."";
        }

        /// <summary>
        /// Confirm on the scan overlay. Always opens the Household Review modal so the operator sees
"@
$code = $code.Replace($target5, $replacement5)

[IO.File]::WriteAllText($path, $code, [Text.Encoding]::UTF8)

