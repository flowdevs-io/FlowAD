# Domain Controller Connectivity, FSMO & Clock Skew

FlowAD runs non-intrusive network and protocol health checks to verify that Domain Controllers are responsive, authoritative, and synchronized.

---

## 1. Domain Controller Port Matrix

FlowAD probes the critical TCP services on each discovered Domain Controller:

| Port | Service | Purpose |
| :--- | :--- | :--- |
| **53** | DNS | Name resolution & SRV locator |
| **88** | Kerberos | Ticket Granting Service (KDC) |
| **135** | RPC Endpoint Mapper | Remote Procedure Calls |
| **389** | LDAP | Active Directory queries |
| **636** | LDAPS | Secure LDAP over TLS |
| **445** | SMB | SYSVOL & Netlogon file shares |
| **3268** | Global Catalog (GC) | Forest-wide directory searches |

---

## 2. Active Directory FSMO Role Consensus

Active Directory partitions flexible single master operation (FSMO) roles across domain controllers. FlowAD inspects role ownership:
- **Schema Master** (`fSMORoleOwner` on Schema partition)
- **Domain Naming Master** (`fSMORoleOwner` on Partitions container)
- **PDC Emulator** (`fSMORoleOwner` on Domain root)
- **RID Master** (`fSMORoleOwner` on RID Manager$ in System container)
- **Infrastructure Master** (`fSMORoleOwner` on Infrastructure container)

FlowAD verifies that all online DCs agree on role assignments to detect split-brain or metadata residue from improper DC removals.

---

## 3. Kerberos Clock Skew Measurement

Kerberos authentication permits a default maximum clock skew of **5 minutes (300 seconds)**. Clock skew beyond this threshold prevents users and computers from authenticating against the domain.

FlowAD queries the `currentTime` attribute from RootDSE via LDAP (port 389). This avoids the need to open UDP port 123 through intermediate network firewalls, allowing precise clock skew detection from any workstation or management server.
