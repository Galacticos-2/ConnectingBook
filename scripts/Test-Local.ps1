[CmdletBinding()]
param([string]$Server = '.\SQLEXPRESS')
$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
Push-Location $projectRoot
try {
    if (-not $env:ConnectionStrings__Test) {
        $env:ConnectionStrings__Test = "Server=$Server;Database=master;Integrated Security=True;Encrypt=True;TrustServerCertificate=True"
    }
    dotnet test tests/ConnectingBook.Server.Tests/ConnectingBook.Server.Tests.csproj -p:BuildClient=false
    if ($LASTEXITCODE -ne 0) { throw 'Backend tests failed.' }
    Push-Location project-web-connectingbook.client
    try {
        npm run test -- --run
        if ($LASTEXITCODE -ne 0) { throw 'Frontend tests failed.' }
        npm run build
        if ($LASTEXITCODE -ne 0) { throw 'Frontend build failed.' }
        npm run lint
        if ($LASTEXITCODE -ne 0) { throw 'Frontend lint failed.' }
    }
    finally { Pop-Location }
}
finally { Pop-Location }
