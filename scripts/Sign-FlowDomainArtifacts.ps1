<#
.SYNOPSIS
  Sign FlowAD release PE executables with AzureSignTool + Azure Key Vault (FlowDevs Cert).

.DESCRIPTION
  Used in official FlowDevs CI release workflows to sign flowad.exe with Authenticode.
  Supports both Azure Key Vault (production) and local PFX certificate (development/testing).

  Required environment variables for Azure Key Vault:
    FLOWDEVS_AZURE_SIGN_VAULT_URL
    FLOWDEVS_AZURE_SIGN_CERT_NAME
    FLOWDEVS_AZURE_SIGN_CLIENT_ID
    FLOWDEVS_AZURE_SIGN_CLIENT_SECRET
    FLOWDEVS_AZURE_SIGN_TENANT_ID
  Optional:
    FLOWDEVS_AZURE_SIGN_TIMESTAMP_URL (default GlobalSign advanced TSA)
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$PublishDir,

    [Parameter(Mandatory = $false)]
    [string]$PfxPath,

    [Parameter(Mandatory = $false)]
    [string]$PfxPassword = ''
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

. (Join-Path $PSScriptRoot '_AuthenticodeValidation.ps1')

function Get-AzureSignToolPath {
    $tool = @(Get-Command AzureSignTool -ErrorAction SilentlyContinue) | Select-Object -First 1
    if ($null -ne $tool -and -not [string]::IsNullOrWhiteSpace([string]$tool.Source)) {
        return [string]$tool.Source
    }

    Write-Host 'Installing AzureSignTool global tool...'
    & dotnet tool install --global --version 7.0.1 AzureSignTool | Out-Host

    $dotnetTools = Join-Path $env:USERPROFILE '.dotnet\tools'
    if ((Test-Path -LiteralPath $dotnetTools) -and ($env:PATH -notlike "*${dotnetTools}*")) {
        $env:PATH = "$dotnetTools;$env:PATH"
    }

    $tool = @(Get-Command AzureSignTool -ErrorAction SilentlyContinue) | Select-Object -First 1
    if ($null -eq $tool -or [string]::IsNullOrWhiteSpace([string]$tool.Source)) {
        $fallback = Join-Path $dotnetTools 'AzureSignTool.exe'
        if (Test-Path -LiteralPath $fallback -PathType Leaf) {
            return [string]$fallback
        }
        throw 'AzureSignTool was installed but could not be resolved on PATH.'
    }

    return [string]$tool.Source
}

if (!(Test-Path -LiteralPath $PublishDir -PathType Container)) {
    throw "Target publish directory does not exist: $PublishDir"
}

$files = @(
    Get-ChildItem -LiteralPath $PublishDir -File -Recurse |
        Where-Object { Test-FlowADPeExecutable -Path $_.FullName }
)

if ($files.Count -eq 0) {
    throw "No PE executables found under publish directory: $PublishDir"
}

Write-Host "Found $($files.Count) executable(s) to sign in $PublishDir."

# Check for Azure Key Vault configuration
$vaultUrl = $env:FLOWDEVS_AZURE_SIGN_VAULT_URL
if ([string]::IsNullOrWhiteSpace($vaultUrl)) {
    $vaultUrl = $env:FLOWRMM_AZURE_SIGN_VAULT_URL
}

if (-not [string]::IsNullOrWhiteSpace($vaultUrl)) {
    $certName = $env:FLOWDEVS_AZURE_SIGN_CERT_NAME ?? $env:FLOWRMM_AZURE_SIGN_CERT_NAME
    $clientId = $env:FLOWDEVS_AZURE_SIGN_CLIENT_ID ?? $env:FLOWRMM_AZURE_SIGN_CLIENT_ID
    $clientSecret = $env:FLOWDEVS_AZURE_SIGN_CLIENT_SECRET ?? $env:FLOWRMM_AZURE_SIGN_CLIENT_SECRET
    $tenantId = $env:FLOWDEVS_AZURE_SIGN_TENANT_ID ?? $env:FLOWRMM_AZURE_SIGN_TENANT_ID
    $tsaUrl = $env:FLOWDEVS_AZURE_SIGN_TIMESTAMP_URL ?? $env:FLOWRMM_AZURE_SIGN_TIMESTAMP_URL ?? 'http://timestamp.globalsign.com/tsa/advanced'

    $toolPath = Get-AzureSignToolPath
    Write-Host "Signing with FlowDevs Azure Key Vault certificate: $certName"
    Write-Host "Timestamp Authority: $tsaUrl"

    foreach ($file in $files) {
        Write-Host "Signing $($file.Name)..."
        & $toolPath sign `
            -kvu $vaultUrl `
            -kvc $certName `
            -kvi $clientId `
            -kvs $clientSecret `
            -kvt $tenantId `
            -tr $tsaUrl `
            -td sha256 `
            -fd sha256 `
            -v `
            $file.FullName
        if ($LASTEXITCODE -ne 0) {
            throw "AzureSignTool failed for $($file.Name) with exit code $LASTEXITCODE."
        }
    }
}
elseif (-not [string]::IsNullOrWhiteSpace($PfxPath) -and (Test-Path -LiteralPath $PfxPath)) {
    Write-Host "Signing using local PFX certificate: $PfxPath"
    $cert = Get-PfxCertificate -FilePath $PfxPath
    foreach ($file in $files) {
        Set-AuthenticodeSignature -FilePath $file.FullName -Certificate $cert -HashAlgorithm SHA256 -TimestampServer 'http://timestamp.digicert.com'
    }
}
else {
    Write-Warning "No Azure Key Vault credentials or PFX path provided. Skipping code signing for local build."
}

Write-Host "Code signing process completed successfully."
