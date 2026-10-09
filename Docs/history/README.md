# History

Write-ups of problems met while first dockerizing nInvoices with Keycloak (early 2026), kept for
the reasoning behind parts of the current setup. They describe how things were then: for how to
build, deploy and troubleshoot today, see [`docker/README.md`](../../docker/README.md),
[`docker/TROUBLESHOOTING.md`](../../docker/TROUBLESHOOTING.md) and [`CLAUDE.md`](../../CLAUDE.md).

| Note | What it explains |
|------|------------------|
| [JWT-AUTHENTICATION-FIX.md](JWT-AUTHENTICATION-FIX.md) | Why the API validates Keycloak tokens through a `BackchannelHttpHandler` inside Docker |
| [RESOLUTION-SUMMARY.md](RESOLUTION-SUMMARY.md) | How that authentication issue was resolved, step by step |
| [KEYCLOAK-PERMISSION-FIX.md](KEYCLOAK-PERMISSION-FIX.md) | Keycloak failing to start on volume permissions |
| [BUILD-ISSUES.md](BUILD-ISSUES.md) | Early Docker build failures and their fixes |
