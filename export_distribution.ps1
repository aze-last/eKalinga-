$sourceDir = "c:\Users\ASUS\source\repos\eKalinga-"
$stagingDir = "$sourceDir\eKalinga_Distribution_Export"
if (Test-Path $stagingDir) { Remove-Item -Recurse -Force $stagingDir }
New-Item -ItemType Directory -Force -Path $stagingDir | Out-Null

Get-ChildItem -Path $sourceDir -Recurse -File | 
Where-Object { 
    ($_.Name -match "Distribution" -or $_.Name -match "Borrowing") -and 
    $_.FullName -notmatch "\\obj\\" -and 
    $_.FullName -notmatch "\\bin\\" -and 
    $_.FullName -notmatch "\\.git\\" -and 
    $_.FullName -notmatch "\\.vs\\" -and
    $_.FullName -notmatch "eKalinga_Modules_Export" -and
    $_.FullName -notmatch "eKalinga_Distribution_Export"
} | Copy-Item -Destination $stagingDir -Force -ErrorAction SilentlyContinue

Copy-Item -Path "$sourceDir\.agent-team\templates\distribution-worker.md" -Destination "$stagingDir\distribution-worker.md" -Force

$zipPath = "$sourceDir\eKalinga_Distribution_Export.zip"
if (Test-Path $zipPath) { Remove-Item -Force $zipPath }
Compress-Archive -Path "$stagingDir\*" -DestinationPath $zipPath

Write-Host "Export completed: $zipPath"
