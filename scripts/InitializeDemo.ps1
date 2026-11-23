[CmdletBinding()]
param(
    [string]$Server = '.\SQLEXPRESS',
    [string]$Database = 'ConnectingBook_Demo_Local',
    [switch]$Performance
)
$ErrorActionPreference = 'Stop'
if (-not $Database.StartsWith('ConnectingBook_Demo_') -and -not $Database.StartsWith('ConnectingBook_Test_')) {
    throw 'Choose a dedicated ConnectingBook_Demo_ or ConnectingBook_Test_ database. Existing application databases are not seeded.'
}
$projectRoot = Split-Path -Parent $PSScriptRoot
function Read-SecretToEnvironment([string]$Name, [string]$Prompt) {
    if ([Environment]::GetEnvironmentVariable($Name)) { return }
    $secureValue = Read-Host $Prompt -AsSecureString
    $secretPointer = [Runtime.InteropServices.Marshal]::SecureStringToBSTR($secureValue)
    try { [Environment]::SetEnvironmentVariable($Name, [Runtime.InteropServices.Marshal]::PtrToStringBSTR($secretPointer)) }
    finally { [Runtime.InteropServices.Marshal]::ZeroFreeBSTR($secretPointer) }
}
Read-SecretToEnvironment 'DEMO_PASSWORD' 'Mật khẩu thành viên demo (10–128 ký tự)'
if (-not $Performance) { Read-SecretToEnvironment 'ADMIN_PASSWORD' 'Mật khẩu quản trị demo (10–128 ký tự)' }
if (-not $env:DEMO_EMAIL) { $env:DEMO_EMAIL = Read-Host 'Email thành viên demo' }
if (-not $Performance -and -not $env:ADMIN_EMAIL) { $env:ADMIN_EMAIL = Read-Host 'Email quản trị demo' }
$env:ConnectionStrings__Default = "Server=$Server;Database=$Database;Integrated Security=True;Encrypt=True;TrustServerCertificate=True"
$mode = if ($Performance) { '--seed-performance' } else { '--seed-demo' }
dotnet run --project (Join-Path $projectRoot 'Project-Web-ConnectingBook.Server/Project-Web-ConnectingBook.Server.csproj') --no-launch-profile -p:BuildClient=false -- $mode
if ($LASTEXITCODE -ne 0) { throw 'Demo seed failed.' }
Write-Host 'Dữ liệu demo đã sẵn sàng; không thay đổi ConnectingBookSystemDb.'
