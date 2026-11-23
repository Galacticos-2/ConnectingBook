[CmdletBinding()]
param(
    [string]$Server = '.\SQLEXPRESS',
    [string]$Database = 'ConnectingBookSystemDb',
    [int]$Port = 7244,
    [switch]$SkipBuild,
    [switch]$Migrate
)

$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
$serverProject = Join-Path $projectRoot 'Project-Web-ConnectingBook.Server/Project-Web-ConnectingBook.Server.csproj'
$clientProject = Join-Path $projectRoot 'project-web-connectingbook.client'

function Invoke-Checked([scriptblock]$Command) {
    & $Command
    if ($LASTEXITCODE -ne 0) { throw 'A required command failed; see the output above.' }
}

Push-Location $projectRoot
try {
    Invoke-Checked { dotnet dev-certs https --check --quiet }
    # The local Windows Authentication connection contains no stored password.
    # Never print configuration values or a JWT signing key.
    if (-not $env:ConnectionStrings__Default -or $PSBoundParameters.ContainsKey('Database') -or $PSBoundParameters.ContainsKey('Server')) {
        $env:ConnectionStrings__Default = "Server=$Server;Database=$Database;Integrated Security=True;Encrypt=True;TrustServerCertificate=True"
    }
    if (-not $env:Jwt__SigningKey) {
        $keyBytes = New-Object byte[] 48
        $random = [Security.Cryptography.RandomNumberGenerator]::Create()
        try { $random.GetBytes($keyBytes); $env:Jwt__SigningKey = [Convert]::ToBase64String($keyBytes) }
        finally { $random.Dispose(); [Array]::Clear($keyBytes, 0, $keyBytes.Length) }
    }
    $env:ASPNETCORE_ENVIRONMENT = 'Development'
    $env:ASPNETCORE_URLS = "https://localhost:$Port"
    $env:Client__Origin = "https://localhost:$Port"
    if (-not $env:Storage__Provider) { $env:Storage__Provider = 'Local' }
    if (-not $SkipBuild) {
        Push-Location $clientProject
        try { Invoke-Checked { npm ci }; Invoke-Checked { npm run build } }
        finally { Pop-Location }
        Invoke-Checked { dotnet build $serverProject -p:BuildClient=false }
    }
    $webRoot = Join-Path $projectRoot 'Project-Web-ConnectingBook.Server/wwwroot'
    New-Item -ItemType Directory -Path $webRoot -Force | Out-Null
    Copy-Item -Path (Join-Path $clientProject 'dist/*') -Destination $webRoot -Recurse -Force
    if ($Migrate) { Invoke-Checked { dotnet run --project $serverProject --no-build --no-launch-profile -- --migrate } }
    Write-Host "Connectbook local: https://localhost:$Port"
    Invoke-Checked { dotnet run --project $serverProject --no-build --no-launch-profile }
}
finally { Pop-Location }
