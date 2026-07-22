# Security policy

## Supported versions

Only the latest release is supported. Fixes go into a new release; older versions
receive no backports.

## Reporting a vulnerability

Report privately through **GitHub Security Advisories**:
[Security → Report a vulnerability](https://github.com/bolikcraft/vantah/security/advisories/new).

Please do **not** open a public issue for a suspected vulnerability.

A useful report includes the Vantah version, the output of
`adguardvpn-cli --version`, the distribution and desktop environment, and the
steps needed to reproduce the problem.

## Scope

Vantah is a graphical front-end: it runs `adguardvpn-cli` as an external process,
parses its output and displays the state. It does **not** implement a VPN, and it
does **not** store account credentials — signing in is performed by
`adguardvpn-cli` itself through the device-code flow in the browser, and the
resulting session belongs to the CLI. Consequently, most questions about the
security of the VPN connection, of the tunnel, and of the account concern
`adguardvpn-cli` and should be reported to AdGuard.

What is in scope for Vantah: how it builds and launches command lines, how it
handles the files it owns under `~/.config/vantah` (including the trust boundary
described in the [README](README.md#configuration-and-trust-model): `adguard_cmd`
and `kill_cmd` are executed as given, and the usual `kill_cmd` runs through
`pkexec`), how it treats data received from the CLI, and what ends up in logs and
exported files.

## Response time

Vantah is a hobby project maintained in spare time. Reports are read and answered
as soon as possible, but no SLA is promised.
