# Active Directory DNS Health & SRV Record Diagnostics

Healthy DNS resolution is the single most critical dependency for Active Directory functionality, Kerberos ticket acquisition, domain joins, and SYSVOL replication.

---

## Critical AD SRV Records Audited by FlowAD

FlowAD automatically queries and validates the following SRV records:

1. **`_ldap._tcp.dc._msdcs.<domain>`**
   - **Service:** LDAP / Active Directory DC Locator.
   - **Port:** 389.
   - **Role:** Enables clients and member servers to locate domain controllers.

2. **`_kerberos._tcp.dc._msdcs.<domain>`**
   - **Service:** Kerberos Key Distribution Center (KDC).
   - **Port:** 88.
   - **Role:** Required for authentication ticket granting (TGT/TGS requests).

3. **`_kpasswd._tcp.<domain>`**
   - **Service:** Kerberos Password Change Service.
   - **Port:** 464.
   - **Role:** Handles user and computer password changes.

4. **`_gc._tcp.<domain>`**
   - **Service:** Global Catalog.
   - **Port:** 3268.
   - **Role:** Forest-wide searches, universal group membership expansion during logon.

5. **`_ldap._tcp.pdc._msdcs.<domain>`**
   - **Service:** Primary Domain Controller (PDC) Emulator.
   - **Port:** 389.
   - **Role:** Password changes, time synchronization, Group Policy management updates.

---

## Forward & Reverse (PTR) Consistency Validation

- **Forward Resolution (A/AAAA):** Ensures that every target host listed in an SRV record resolves to an active, valid IPv4 or IPv6 address.
- **Reverse Resolution (PTR):** FlowAD performs PTR lookups on every discovered DC IP address. If the PTR record points to a hostname different from the DC's configured FQDN, FlowAD flags a **PTR Mismatch** warning. PTR mismatches frequently break Kerberos SPN mutual authentication and cause fallback to legacy NTLM.
