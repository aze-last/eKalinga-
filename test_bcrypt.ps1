$bcryptPath = "c:\Users\ASUS\source\repos\eKalinga-\bin\Debug\net9.0-windows\BCrypt.Net-Next.dll"
Add-Type -Path $bcryptPath

$hash = '$2a$11$hUpcuCddUwCUH3PEM5g39O3lYOsDCrgMOFhFoSCBAcb40LoKasKIG'
$password = 'Ams@2026'

$isValid = [BCrypt.Net.BCrypt]::Verify($password, $hash)
Write-Output "Is valid: $isValid"
