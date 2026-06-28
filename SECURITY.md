# Security Policy

Corbel is a security-focused starter template. Vulnerabilities — in the template itself and in the
defaults it ships — are taken seriously, and reports that help keep it safe to build on are appreciated.

## Supported versions

Corbel is distributed as a template you clone and own, so there is no long-lived release train. Security
fixes land on the default branch (`main`); the most recent commit on `main` is the only supported version.

| Version            | Supported          |
| ------------------ | ------------------ |
| Latest `main`      | :white_check_mark: |
| Older commits/tags | :x:                |

Once you clone Corbel, the security of your fork — and of any dependencies you add or bump — is yours to
maintain. The CI restore step fails on known-vulnerable NuGet packages (`NuGetAudit`) and Renovate keeps
dependencies current; keep both enabled.

## Reporting a vulnerability

**Do not open a public issue, pull request, or discussion for a security problem.**

Report privately through either channel:

1. **GitHub Private Vulnerability Reporting** (preferred). On the repository, go to
   **Security → Report a vulnerability**. Maintainers enable this under
   **Settings → Code security and analysis → Private vulnerability reporting**.
2. **Email** the maintainers at **security@example.com** *(replace with a real address before publishing
   the repository)*.

Please include:

- the affected component (e.g. auth, a specific endpoint, a dependency) and the version/commit,
- a description and an impact assessment,
- reproduction steps or a proof of concept,
- any suggested remediation.

## What to expect

- **Acknowledgement** within 3 business days.
- A triage assessment and a severity rating shortly after.
- Coordinated disclosure: a timeline agreed with you, and credit (if you want it) once a fix is available.

Thank you for reporting responsibly.
