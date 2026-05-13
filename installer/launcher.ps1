$exePath = Join-Path $PSScriptRoot "AttendanceShiftingManagement.exe"

Start-Process -FilePath $exePath

Start-Sleep -Seconds 10

$process = Get-Process -Name "AttendanceShiftingManagement" -ErrorAction SilentlyContinue

if (-not $process) {
    Add-Type -AssemblyName System.Windows.Forms
    [System.Windows.Forms.MessageBox]::Show("The application failed to start. Please check the application logs or contact support for assistance.", "Startup Error", [System.Windows.Forms.MessageBoxButtons]::OK, [System.Windows.Forms.MessageBoxIcon]::Error)
}