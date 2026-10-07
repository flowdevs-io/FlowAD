<#
.SYNOPSIS
  Shared Windows Authenticode helpers for FlowAD release signing and verification.

.DESCRIPTION
  Used by Sign-FlowDomainArtifacts.ps1 and CI release validation pipelines.
  Validates Authenticode digital signatures against the official FlowDevs certificate.
#>

Set-StrictMode -Version Latest

function Get-FlowADNormalizedPublisherIdentity {
    param(
        [Parameter(Mandatory = $true)]
        [AllowEmptyString()]
        [string]$Value
    )

    if ([string]::IsNullOrWhiteSpace($Value)) {
        return ''
    }

    $normalized = $Value.Trim() -replace '\s+', ' '
    return $normalized.ToUpperInvariant()
}

function Test-FlowADPeExecutable {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Path
    )

    if (!(Test-Path -LiteralPath $Path -PathType Leaf)) {
        return $false
    }

    $extension = [System.IO.Path]::GetExtension($Path)
    if ($extension -notin @('.exe', '.EXE', '.dll', '.DLL')) {
        return $false
    }

    try {
        $stream = [System.IO.File]::OpenRead($Path)
        try {
            if ($stream.Length -lt 64) {
                return $false
            }

            $header = New-Object byte[] 2
            if ($stream.Read($header, 0, 2) -ne 2) {
                return $false
            }

            # Check MZ header magic (0x4D, 0x5A)
            return ($header[0] -eq 0x4D -and $header[1] -eq 0x5A)
        }
        finally {
            $stream.Dispose()
        }
    }
    catch {
        return $false
    }
}

function Assert-FlowADAuthenticodePublisher {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Path,

        [Parameter(Mandatory = $true)]
        [string]$ExpectedSubject,

        [Parameter(Mandatory = $false)]
        [string]$ExpectedThumbprint
    )

    if (!(Test-Path -LiteralPath $Path -PathType Leaf)) {
        throw "Authenticode target missing: $Path"
    }

    $signature = Get-AuthenticodeSignature -FilePath $Path
    $leafName = Split-Path -Leaf $Path

    if ($null -eq $signature -or $signature.Status -ne 'Valid') {
        $status = if ($null -eq $signature) { 'Missing' } else { [string]$signature.Status }
        throw "Authenticode status for $leafName is '$status' (expected Valid)."
    }

    if ($null -eq $signature.SignerCertificate) {
        throw "Authenticode signature for $leafName has no signer certificate."
    }

    $actualSubject = [string]$signature.SignerCertificate.Subject
    $expectedSubjectNormalized = Get-FlowADNormalizedPublisherIdentity -Value $ExpectedSubject
    $actualSubjectNormalized = Get-FlowADNormalizedPublisherIdentity -Value $actualSubject

    if ([string]::IsNullOrWhiteSpace($expectedSubjectNormalized)) {
        throw 'Expected Windows signer subject is empty.'
    }

    if ($actualSubjectNormalized -ne $expectedSubjectNormalized) {
        throw "Authenticode publisher mismatch for $leafName. Expected subject '$ExpectedSubject' but found '$actualSubject'."
    }

    if (-not [string]::IsNullOrWhiteSpace($ExpectedThumbprint)) {
        $actualThumb = ([string]$signature.SignerCertificate.Thumbprint).Trim().ToUpperInvariant()
        $expectedThumb = $ExpectedThumbprint.Trim().ToUpperInvariant() -replace '[\s:]', ''
        if ($actualThumb -ne $expectedThumb) {
            throw "Authenticode thumbprint mismatch for $leafName. Expected '$expectedThumb' but found '$actualThumb'."
        }
    }

    return $signature
}
