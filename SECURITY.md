# Security Policy

## Threat Model

AI Exposure Scanner is a read-only local audit tool. It holds no user data, makes no network calls, and modifies no files. The primary security considerations are:

### What the scanner protects

- **User confidentiality**: API keys and secrets detected by the scanner are never stored, logged, or displayed in full. Only masked previews are shown.
- **Binary integrity**: Releases are signed (Developer ID on macOS, Azure Trusted Signing on Windows) and notarized (macOS). Users can verify authenticity.
- **Scan isolation**: The scanner reads config files using read-only file handles. The `FilesystemFacade` abstraction enforces this in the codebase.

### Network

- macOS build has `com.apple.security.network.client = false` in entitlements — OS-enforced, not just a code promise.
- Windows MSIX manifest declares no `internetClient` capability.
- CI grep gate blocks any import of `URLSession`, `URLRequest`, `HttpClient`, or `WebClient` in the Scanner library.

### Privilege

- No administrator/root rights required or requested.
- macOS: Full Disk Access via Privacy & Security (user-granted, revocable).
- Windows: Standard user token. No UAC elevation.
- No background daemons, LaunchAgents, or Scheduled Tasks.

### Integrity

- macOS: Apple notarization + stapled ticket. Gatekeeper verifies on every launch.
- Windows: Azure Trusted Signing. SmartScreen reputation builds with download count.
- Release artifacts checksummed (SHA-256) in each GitHub Release.

---

## Responsible Disclosure

If you find a security vulnerability in AI Exposure Scanner — particularly anything that could cause the scanner itself to leak data, execute code, or be tricked into misreporting risks — please report it privately.

**Do not open a public GitHub issue for security vulnerabilities.**

Report via email: *[security contact to be added before v0.1 public release]*

Include:
- Description of the vulnerability
- Steps to reproduce
- Impact assessment
- (Optional) suggested fix

We will acknowledge within 48 hours and aim to ship a fix within 14 days for critical issues.

---

## Known Limitations (v0.1)

- Browser extension scanning not implemented — extensions can have significant permissions not yet audited.
- Secret scanning is limited to known config file locations — secrets in arbitrary files are not detected.
- Project-level scans not implemented — AI agents' access to specific repositories is not assessed.
- Windows: unsigned builds (before signing is configured) will trigger SmartScreen. Build from source to verify.

These are addressed in the v0.2 and v0.3 roadmap.
