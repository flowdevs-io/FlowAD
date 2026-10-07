# FlowAD: Lightweight Active Directory Diagnostic Suite

[![CI](https://github.com/flowdevs/FlowAD/actions/workflows/ci.yml/badge.svg)](https://github.com/flowdevs/FlowAD/actions/workflows/ci.yml)
[![License: Apache-2.0](https://img.shields.io/badge/License-Apache%202.0-blue.svg)](https://opensource.org/licenses/Apache-2.0)
[![Signed by FlowDevs](https://img.shields.io/badge/Authenticode-FlowDevs%20LLC-brightgreen.svg)](https://flowdevs.io)

**FlowAD** is an ultra-lightweight, zero-dependency diagnostic suite for Active Directory environments. Built with .NET 10 Native AOT / SingleFile, FlowAD delivers instant diagnostics for Group Policy Objects (GPO hygiene & precedence deconfliction), Active Directory DNS verification, and Domain Controller health.

Official releases are cryptographically signed with the **FlowDevs LLC Code Signing Certificate**.

---

## Key Capabilities

- **GPO Hygiene & Precedence Deconfliction Engine:**
  - Evaluates standard LSDOU precedence (Local, Site, Domain, OU), Link Order, Block Inheritance, and Enforced links.
  - Native binary parser for `Registry.pol` (PReg format) and INF Security Templates (`GptTmpl.inf`).
  - Identifies **Direct Contradictions** (e.g. 1 vs 0), **Privilege Overwrites** (non-merging User Rights Assignments), **Overrides**, and **Duplicates**.
  - Operates in both **Live Active Directory Mode** (via LDAP) and **Offline Mode** (analyzing exported SYSVOL policy trees).
- **Active Directory DNS Diagnostics:**
  - Automatically queries and validates critical AD SRV records (`_ldap`, `_kerberos`, `_kpasswd`, `_gc`, `_pdc`).
  - Verifies Domain Controller forward (A/AAAA) and reverse (PTR) consistency to prevent Kerberos ticket failures.
  - Measures DNS query latency across authoritative servers.
- **Domain Health & Controller Reachability:**
  - Probes critical AD ports (DNS:53, Kerberos:88, RPC:135, LDAP:389, LDAPS:636, SMB:445, GC:3268).
  - Inspects all 5 FSMO role owners and verifies forest consensus.
  - Measures Kerberos clock skew against the PDC Emulator without requiring open NTP UDP 123.
- **Automation & RMM Friendly:**
  - Supports `--format json` for seamless integration with FlowRMM, remote management agents, CI/CD pipelines, or SIEM log forwarders.
  - Generates Markdown reports (`--format markdown`) for executive and auditor reviews.

---

## Architecture

```
                               ┌────────────────────────────────────────────────────────┐
                               │                      FlowAD CLI                        │
                               │                     (flowad.exe)                       │
                               └──────────────────────────┬─────────────────────────────┘
                                                          │
                    ┌─────────────────────────────────────┼────────────────────────────────────┐
                    ▼                                     ▼                                    ▼
       ┌─────────────────────────┐           ┌─────────────────────────┐          ┌─────────────────────────┐
       │     GPO Engine          │           │       DNS Engine        │          │   Domain Health Engine  │
       ├─────────────────────────┤           ├─────────────────────────┤          ├─────────────────────────┤
       │ • GPC/GPT Inventory     │           │ • AD SRV Verification   │          │ • DC Port & Reachability│
       │ • Registry.pol Parser   │           │ • DC A/AAAA & PTR Check │          │ • FSMO Role Consensus   │
       │ • SOM & LSDOU Precedence│           │ • Round-Robin Querying  │          │ • Clock Skew / NTP Sync │
       │ • Conflict & Overwrite  │           │ • Delegation & Latency  │          │ • AD DRS Replication    │
       │ • GPO Hygiene & Orphans │           │ • Dynamic DNS Records   │          │ • SYSVOL/Netlogon Check │
       └────────────┬────────────┘           └────────────┬────────────┘          └────────────┬────────────┘
                    │                                     │                                    │
                    └─────────────────────────────────────┼────────────────────────────────────┘
                                                          │
                                                          ▼
                                      ┌───────────────────────────────────────┐
                                      │            Output Pipeline            │
                                      ├───────────────────────────────────────┤
                                      │ • Spectre.Console Interactive Tables  │
                                      │ • JSON Schema Output (Automation/RMM) │
                                      │ • Markdown / HTML Executive Reports   │
                                      └───────────────────────────────────────┘
```

---

## Quick Start

### Running Standalone Executable (Windows)

Download the latest signed release binary:

```powershell
# Run the complete diagnostic battery
.\flowad.exe doctor --domain contoso.local --dc dc01.contoso.local

# Run offline GPO audit on a SYSVOL backup
.\flowad.exe gpo audit --offline-path C:\SYSVOL\Policies

# Run GPO deconfliction matrix
.\flowad.exe gpo deconflict --offline-path C:\SYSVOL\Policies

# Export machine-readable JSON for FlowRMM
.\flowad.exe doctor --domain contoso.local --format json --output report.json
```

---

## CLI Reference

### `flowad doctor`
Runs the unified diagnostic battery across GPO, DNS, and Domain Health, generating an executive health score (0–100%).

```text
USAGE:
    flowad doctor [OPTIONS]

OPTIONS:
    -d, --domain <DOMAIN>       Target Active Directory domain (e.g. contoso.local)
    -s, --server, --dc <SERVER> Domain Controller host or IP address
    --offline-path <PATH>       Optional path to offline SYSVOL Policies directory
    -f, --format <FORMAT>       Output format: table (default), json, markdown
    -o, --output <FILE>         Save report to specified file path
    -u, --user <USERNAME>       Username for AD authentication
    -p, --password <PASSWORD>   Password for AD authentication
```

### `flowad gpo audit`
Audits Group Policy hygiene: version sync between AD GPC and SYSVOL GPT, unlinked GPOs, empty GPOs, and disabled policies.

```bash
flowad gpo audit --server dc01.contoso.local --domain contoso.local
flowad gpo audit --offline-path ./SysvolPolicies --format html --output gpo-report.html
```

### `flowad gpo deconflict` (alias: `conflicts`)
Evaluates policy precedence across the OU hierarchy and detects contradictory settings, overwritten parameters, and user rights collisions.

```bash
# Terminal table output with colorized collision matrix
flowad gpo deconflict --offline-path ./SysvolPolicies

# Export interactive standalone HTML report with live search & severity filters
flowad gpo deconflict --offline-path ./SysvolPolicies --format html --output deconfliction.html

# Export spreadsheet-ready CSV for Excel
flowad gpo deconflict --offline-path ./SysvolPolicies --format csv --output conflicts.csv
```

### `flowad gpo inspect`
Drills down into any specific GPO by display name or GUID, dumping all parsed Administrative Templates (registry keys) and Security Template User Rights Assignments (`GptTmpl.inf`).

```bash
flowad gpo inspect --offline-path ./SysvolPolicies --name "Default Domain Policy"
flowad gpo inspect --offline-path ./SysvolPolicies --name "Finance" --format json
```

### `flowad dns check`
Validates active directory SRV records, DC forward A/AAAA lookups, reverse PTR records, and latency.

```bash
flowad dns check --domain contoso.local
flowad dns check --domain contoso.local --dns-server 10.0.0.1
```

### `flowad health domain`
Tests Domain Controller connectivity, TCP port availability, FSMO consensus, and Kerberos clock skew.

```bash
flowad health domain --domain contoso.local --server dc01.contoso.local
```

---

## Code Signing (FlowDevs Certificate)

All official releases of FlowAD are cryptographically signed with the FlowDevs Authenticode certificate stored in Azure Key Vault:

```powershell
# Verify signature on Windows
Get-AuthenticodeSignature .\flowad.exe | Format-List
```

Expected Signer Subject:
```text
CN=FlowDevs LLC, O=FlowDevs LLC, L=Minneapolis, S=Minnesota, C=US
Status: Valid
```

To sign local builds using Azure Key Vault:
```powershell
./scripts/Sign-FlowDomainArtifacts.ps1 -PublishDir ./publish/win-x64
```

---

## Building from Source

Requirements: .NET 10 SDK

```bash
# Clone repository
git clone https://github.com/flowdevs/FlowAD.git
cd FlowAD

# Build solution
dotnet build

# Run unit tests
dotnet test

# Publish self-contained single-file Windows executable
dotnet publish src/FlowAD/FlowAD.csproj -c Release -r win-x64 -o ./dist
```

---

## Documentation

- [GPO Deconfliction Engine & LSDOU Rules](docs/gpo-deconfliction.md)
- [Active Directory DNS Diagnostics](docs/dns-troubleshooting.md)
- [Domain Health & Controller Matrix](docs/domain-health.md)

---

## License

FlowAD is open-source software licensed under the [Apache License 2.0](LICENSE).
Copyright © 2026 FlowDevs LLC.
