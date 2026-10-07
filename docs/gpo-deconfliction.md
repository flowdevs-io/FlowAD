# Group Policy (GPO) Deconfliction & Precedence Engine

FlowAD provides an advanced evaluation engine for Group Policy Objects, capable of parsing binary `Registry.pol` payloads, INF security templates (`GptTmpl.inf`), and SYSVOL GPT configurations to detect policy collisions before they impact production endpoints.

---

## Precedence Resolution Architecture (LSDOU)

FlowAD evaluates GPO precedence using official Windows Active Directory application rules:

1. **Local Group Policy** (Lowest rank)
2. **Site-level GPOs**
3. **Domain-level GPOs**
4. **Organizational Unit (OU) GPOs** (from root OU down to leaf OU)

### Link Order Dynamics
Within the same Active Directory container (Domain or OU), multiple GPOs can be linked with a link order (1, 2, 3...). 
- In Windows processing, **Link Order 1 is processed last**, meaning **Link Order 1 has the highest priority** and overwrites settings from Link Order 2, 3, etc.

### Block Inheritance (`gpOptions = 1`)
- When configured on an OU container, Block Inheritance prevents child containers from inheriting policies from parent OUs, Domain, or Sites.
- **Exception:** Any GPO link marked **Enforced** overrides Block Inheritance.

### Enforced Links (`[LDAP_GPLINK_OPT_ENFORCED]`)
- Enforced GPOs cannot be blocked by downstream OUs.
- Enforced GPOs always take precedence over non-enforced GPOs.
- If multiple GPOs are Enforced, their precedence is reversed from normal LSDOU: a Domain Enforced GPO beats an OU Enforced GPO!

---

## Conflict Detection Categories

FlowAD classifies policy collisions into four critical categories:

| Collision Type | Severity | Description |
| :--- | :--- | :--- |
| **Direct Contradiction** | `Error` | Two or more GPOs define opposing values for the same setting (e.g. `DisableCMD = 1` vs `DisableCMD = 0`, or `Enabled` vs `Disabled`). |
| **User Rights Collision** | `Critical` | Multiple GPOs define user rights assignments (e.g. `SeDebugPrivilege`). Windows Security Templates do **NOT** merge identity lists; higher-precedence GPOs completely overwrite earlier assignments, potentially stripping administrative rights! |
| **Setting Overwrite** | `Warning` | A higher-precedence policy cleanly supersedes a setting configured in a lower-precedence GPO. |
| **Duplicate Definition** | `Info` | Redundant definitions where multiple GPOs define the exact same key and value, causing policy bloat and processing delays. |

---

## Binary `Registry.pol` Parser

FlowAD contains a zero-dependency, native binary parser for Microsoft PReg format files (`PReg\x01\x00\x00\x00`). It extracts:
- `REG_SZ` & `REG_EXPAND_SZ` (Unicode strings)
- `REG_DWORD` (32-bit unsigned integers)
- `REG_QWORD` (64-bit unsigned integers)
- `REG_MULTI_SZ` (Null-delimited multi-string arrays)
- `REG_BINARY` (Raw byte arrays)
