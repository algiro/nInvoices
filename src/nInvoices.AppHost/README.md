# nInvoices + .NET Aspire — integration guide

This document explains **how the Aspire pieces of this repo fit together**: which
image is used for each service, where that is decided, how the services find and
talk to each other, and where every knob lives. It is written for someone who has
not used Aspire before.

> **Scope.** Aspire is used here **only as a local development tool**. Production is
> unchanged: `docker/deploy.sh` → Docker Hub images → `docker compose` over SSH.
> Nothing in `nInvoices.AppHost` is ever built into an image or deployed.

---

## 1. What is .NET Aspire (in one page)

Aspire is two things:

| Piece | In this repo | What it does |
|---|---|---|
| **AppHost** | `src/nInvoices.AppHost` | A small C# console app that is a *description of your system*: "run a Postgres container, a Keycloak container, the API project, the Vite dev server, and wire them together." You start everything with one command. |
| **ServiceDefaults** | `src/nInvoices.ServiceDefaults` | A shared library each service references to get the same cross-cutting setup in one line: OpenTelemetry, health-check endpoints, HTTP resilience, service discovery. |

Key vocabulary:

- **Resource** — one thing in the graph: a container (`postgres`, `keycloak`), a
  .NET project (`api`), an executable/npm app (`web`), or a value like a database
  or a generated password.
- **`builder.AddXxx("name")`** — adds a resource. The `AddPostgres`, `AddKeycloak`,
  `AddViteApp`, `AddProject` methods come from **NuGet "hosting integration"
  packages** (see §8). Each integration knows the default image, ports, env vars,
  and health check for its service.
- **`.WithXxx(...)`** — configures a resource (image tag, env var, bind mount,
  endpoint, wait dependency, …). Calls chain fluently.
- **Reference expression** — a *placeholder* like `db.Resource.ConnectionStringExpression`
  or `api.GetEndpoint("http")`. It is not a string yet; Aspire resolves it at launch
  once ports/passwords are known, then injects the final value.
- **Dashboard** — a web UI Aspire opens on `aspire run` showing every resource's
  state, console logs, environment, endpoints, and (via OpenTelemetry) traces /
  metrics / structured logs. The URL + a login token are printed in the console.
- **`run` vs `publish`** — `aspire run` starts everything locally. `aspire publish`
  (a.k.a. `--publisher manifest`) emits a *deployment manifest* (JSON) describing
  the graph. We only use `publish` to sanity-check the graph; we do **not** deploy
  from it.

### How to run it

```bash
cd src/nInvoices.Web && npm install        # once
aspire run --project src/nInvoices.AppHost # or: dotnet run --project src/nInvoices.AppHost
```

Needs a running **Docker or Podman** (this machine uses Podman). Open the `web`
endpoint from the dashboard; log in to Keycloak with **`testuser` / `Test123!`**.

---

## 2. The two projects

### `nInvoices.ServiceDefaults`

`src/nInvoices.ServiceDefaults/nInvoices.ServiceDefaults.csproj`

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <IsAspireSharedProject>true</IsAspireSharedProject>   <!-- marks it for Aspire tooling -->
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="Microsoft.Extensions.Http.Resilience" .../>
    <PackageReference Include="Microsoft.Extensions.ServiceDiscovery" .../>
    <PackageReference Include="OpenTelemetry.*" .../>          <!-- traces + metrics + logs -->
  </ItemGroup>
</Project>
```

It is a **plain library** (no Aspire SDK). `src/nInvoices.Api` references it and
calls, in `Program.cs`:

```csharp
builder.AddServiceDefaults();   // OpenTelemetry, HTTP resilience, service discovery, "self" health check
...
app.MapDefaultEndpoints();      // maps GET /health (all checks) and GET /alive (liveness only)
```

The actual code is in `src/nInvoices.ServiceDefaults/Extensions.cs`. Notes specific
to this repo:

- `MapDefaultEndpoints` was edited to map `/health` + `/alive` in **all**
  environments (the container health checks call them; the shared nginx does not
  route them, so they are not public).
- The database health check (`AddDbContextCheck<ApplicationDbContext>("database")`)
  is registered in `src/nInvoices.Infrastructure/Data/DatabaseExtensions.cs`, so
  `/health` fails when Postgres/SQLite is unreachable; `/alive` does not.
- Logs still go through **Serilog** (`builder.Host.UseSerilog()`); OpenTelemetry's
  log exporter therefore receives nothing until `Serilog.Sinks.OpenTelemetry` is
  added. **Traces and metrics are unaffected** and export automatically when
  `OTEL_EXPORTER_OTLP_ENDPOINT` is set.

### `nInvoices.AppHost`

`src/nInvoices.AppHost/nInvoices.AppHost.csproj`

```xml
<Project Sdk="Aspire.AppHost.Sdk/13.5.3">   <!-- (A) special MSBuild SDK -->
  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <AspireUseCliBundle>true</AspireUseCliBundle>   <!-- ships the `aspire` CLI with the project -->
    <UserSecretsId>ninvoices-apphost</UserSecretsId> <!-- (B) where generated passwords persist -->
    <IsPackable>false</IsPackable>                   <!-- never packaged / published -->
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="Aspire.Hosting.PostgreSQL" Version="13.5.3" />               <!-- (C) -->
    <PackageReference Include="Aspire.Hosting.Keycloak"   Version="13.5.3-preview.1.26425.3" />
    <PackageReference Include="Aspire.Hosting.JavaScript" Version="13.5.3" />
  </ItemGroup>

  <ItemGroup>
    <ProjectReference Include="..\nInvoices.Api\nInvoices.Api.csproj" />   <!-- (D) -->
  </ItemGroup>
</Project>
```

- **(A)** `Aspire.AppHost.Sdk` brings the core `Aspire.Hosting` library **and a
  source generator**. The generator turns every `<ProjectReference>` into a
  strongly-typed class under the `Projects.` namespace — that is where
  `Projects.nInvoices_Api` in `AppHost.cs` comes from. It carries the csproj path
  and launch-profile info.
- **(B)** `UserSecretsId` — Aspire auto-generates passwords for Postgres and
  Keycloak (see §3) and stores them in
  `~/.microsoft/usersecrets/ninvoices-apphost/secrets.json` (outside the repo) so
  they are **stable between runs**.
- **(C)** The three **hosting integration** packages. Each one adds the `AddXxx`
  methods and, crucially, **defines the default container image and tag** for that
  service (see each resource below).
- **(D)** The only project reference. `nInvoices.Web` is *not* referenced as a
  project — it is a Node app, added by path in `AppHost.cs`.

---

## 3. `AppHost.cs`, resource by resource

The whole graph is `src/nInvoices.AppHost/AppHost.cs`. It reads top to bottom:

```csharp
var builder = DistributedApplication.CreateBuilder(args);
// ... add resources ...
builder.Build().Run();
```

### 3.1 PostgreSQL

```csharp
var postgres = builder.AddPostgres("postgres")
    .WithImageTag("17-alpine");
var db = postgres.AddDatabase("ninvoices");
```

| Question | Answer |
|---|---|
| Which method / package? | `AddPostgres` from **`Aspire.Hosting.PostgreSQL`**. |
| Which image? | `docker.io/library/postgres`. The **image name and default tag are constants inside the `Aspire.Hosting.PostgreSQL` package** (the default tag tracks the package version — it was `18.x` here). `.WithImageTag("17-alpine")` overrides the tag to match `docker-compose.prod.yml`. To change the whole image: `.WithImage("myregistry/postgres")` / `.WithImageRegistry(...)`. |
| Password? | Not set anywhere by hand. `AddPostgres` creates a hidden **`ParameterResource`** `postgres-password` with a random value, persisted in **user secrets** under `Parameters:postgres-password`. Same value every run. |
| `AddDatabase("ninvoices")` | Declares a **logical database** named `ninvoices` on that server. It is a *value resource* (`value.v0`), not a container. It exposes a connection-string reference: `Host=<host>;Port=<port>;Username=postgres;Password=<pw>;Database=ninvoices`. Aspire creates the database if it does not exist. |
| Endpoint / port | TCP `5432` inside the container, published to a **random** `127.0.0.1` host port (nothing needs a fixed Postgres port). |
| Data | **No `WithDataVolume()`** → the container and its data are thrown away every run. Add `.WithDataVolume("ninvoices-pgdata")` to persist. |

### 3.2 Keycloak

```csharp
var keycloak = builder.AddKeycloak("keycloak")
    .WithRealmImport("./keycloak")
    .WithEnvironment("KC_HTTP_ENABLED", "true")
    .WithEnvironment("KC_HOSTNAME_STRICT", "false")
    .WithEndpoint(port: 8088, targetPort: 8080, scheme: "http",
        name: "http-public", isProxied: false);
keycloak.WithEndpointProxySupport(false);
```

| Question | Answer |
|---|---|
| Which method / package? | `AddKeycloak` from **`Aspire.Hosting.Keycloak`** (a **preview** package — the only version available for Aspire 13.x). |
| Which image? | `quay.io/keycloak/keycloak`. The tag is **defined by the hosting package version** (`13.5.3-preview` → Keycloak `26.6`). Override with `.WithImageTag("26.1")`. |
| Realm / users / clients | `.WithRealmImport("./keycloak")` **bind-mounts** `src/nInvoices.AppHost/keycloak/` into the container at `/opt/keycloak/data/import` and starts Keycloak with `start-dev --import-realm`. The realm is defined entirely by **`keycloak/ninvoices-realm.json`**: realm `ninvoices`, clients `ninvoices-web` (public SPA, with an audience mapper adding `ninvoices-api` to tokens) and `ninvoices-api` (bearer-only), realm roles `user` + `admin`, and user `testuser` / `Test123!`. Redirect URIs / web origins are `"*"` — fine for local dev only. |
| Admin user | Auto-generated: `admin` + a random password parameter in **user secrets** (`Parameters:keycloak-password`). |
| `KC_*` env vars | `KC_HTTP_ENABLED=true` and `KC_HOSTNAME_STRICT=false` force Keycloak to serve plain HTTP on any host. Any Keycloak config key can be passed this way. |
| The `WithEndpoint(...)` line — **workaround** | This preview `Aspire.Hosting.Keycloak` binds `AddKeycloak(port:)` to the container's **HTTPS** port (8443) and never publishes HTTP (8080). A browser hitting `http://localhost:8088` then talks HTTP to a TLS port → `ERR_EMPTY_RESPONSE`. So we drop the `port:` argument and add an **explicit HTTP endpoint** named `http-public`: host `8088` → container `8080`, scheme `http`, not proxied. |
| Data | No data volume → the realm re-imports from the JSON on every run. Change the JSON, restart, done. |

### 3.3 The API (`nInvoices.Api`)

```csharp
var api = builder.AddProject<Projects.nInvoices_Api>("api")
    .WaitFor(db)
    .WaitFor(keycloak)
    .WithEnvironment("Database__Type", "PostgreSQL")
    .WithEnvironment("Database__EnsureCreated", "true")
    .WithEnvironment("ConnectionStrings__Default", db.Resource.ConnectionStringExpression)
    .WithEnvironment("Authentication__UseDevAuth", "false")
    .WithEnvironment("Keycloak__RequireHttpsMetadata", "false")
    .WithEnvironment("Keycloak__Authority", $"{KeycloakUrl}/realms/ninvoices")
    .WithEnvironment("Keycloak__Audience", "ninvoices-api");
api.WithEndpointProxySupport(false);
```

| Question | Answer |
|---|---|
| Which method? | `AddProject<Projects.nInvoices_Api>("api")`. `Projects.nInvoices_Api` is the **source-generated** type from the `<ProjectReference>` in the csproj. |
| Container or host? | **Host process** — Aspire runs the built project (`dotnet`), it is *not* containerised in `run` mode. |
| Which environment? | The project's launch profile `Properties/launchSettings.json` → profile `http` sets `ASPNETCORE_ENVIRONMENT=Development` and `applicationUrl=http://localhost:5297`. So the API still loads `appsettings.Development.json`. |
| How is config passed? | `WithEnvironment("A__B", value)` sets the OS env var `A__B`. .NET configuration maps `A__B` → `A:B`, and **environment variables beat `appsettings*.json`**. So the table below shows each override. |
| `db.Resource.ConnectionStringExpression` | A reference expression → resolved at launch to the full Npgsql connection string for the `ninvoices` database. |
| `WaitFor(db)` / `WaitFor(keycloak)` | Start ordering: the API is not launched until those resources report **healthy** (Aspire has a built-in health check for each integration). |
| `Database__EnsureCreated` | Read in `Program.cs`: when `true`, the API calls `dbContext.Database.EnsureCreated()` on startup, which builds the PostgreSQL schema **from the current EF model** (the EF *migrations* are SQLite-only and cannot run under Npgsql). Off in the fast SQLite loop and in production. |

**Config the AppHost overrides on the API:**

| Env var (AppHost) | .NET key | Overrides `appsettings*.json` value | Effect |
|---|---|---|---|
| `Database__Type` | `Database:Type` | `SQLite` | Use the PostgreSQL provider |
| `Database__EnsureCreated` | `Database:EnsureCreated` | *(unset)* | Build schema from the model on boot |
| `ConnectionStrings__Default` | `ConnectionStrings:Default` | `Data Source=nInvoices.db` | Point at the Aspire Postgres |
| `Authentication__UseDevAuth` | `Authentication:UseDevAuth` | `true` (in `appsettings.Development.json`) | Use real Keycloak JWT auth, not the dev bypass |
| `Keycloak__RequireHttpsMetadata` | `Keycloak:RequireHttpsMetadata` | *(unset)* | Allow the `http://` Keycloak metadata URL |
| `Keycloak__Authority` | `Keycloak:Authority` | `http://localhost:8080/realms/ninvoices` | `http://localhost:8088/realms/ninvoices` |
| `Keycloak__Audience` | `Keycloak:Audience` | `ninvoices-api` | (unchanged, set for clarity) |

### 3.4 The frontend (`nInvoices.Web`, Vite dev server)

```csharp
var web = builder.AddViteApp("web", "../nInvoices.Web")
    .WithNpm()
    .WaitFor(api)
    .WithEnvironment("VITE_PROXY_TARGET", api.GetEndpoint("http"))
    .WithEnvironment("VITE_AUTH_DISABLED", "false")
    .WithEnvironment("VITE_KEYCLOAK_URL", KeycloakUrl)
    .WithEnvironment("VITE_KEYCLOAK_REALM", "ninvoices")
    .WithEnvironment("VITE_KEYCLOAK_CLIENT_ID", "ninvoices-web")
    .WithExternalHttpEndpoints();
web.WithEndpointProxySupport(false);
```

| Question | Answer |
|---|---|
| Which method / package? | `AddViteApp` from **`Aspire.Hosting.JavaScript`** (this is where Node/JS hosting lives in Aspire 13.x — there is **no `AddNpmApp`**). |
| Container or host? | **Host process** — runs `npm run dev` (script name defaults to `dev`) in `src/nInvoices.Web`. |
| `.WithNpm()` | Runs `npm install` before `npm run dev` (Aspire does not install packages otherwise). |
| Env vars → the app | Aspire sets them as OS env vars on the `npm` process. Vite copies every `VITE_*` var into `import.meta.env`. `PORT` and `VITE_PROXY_TARGET` are consumed by **`src/nInvoices.Web/vite.config.ts`** (`server.port`, `server.proxy['/api'].target`). `VITE_AUTH_DISABLED=false` overrides the `true` in `src/nInvoices.Web/.env.development` (real `process.env` beats `.env` files). |
| `api.GetEndpoint("http")` | Reference expression → the API's real URL. Vite proxies browser `/api/*` calls there, so the SPA never makes a cross-origin request and CORS is irrelevant in this setup. |
| `WithExternalHttpEndpoints()` | Marks the endpoint as browser-facing so the dashboard shows a clickable link. |

---

## 4. How the resources connect to each other

### Startup order

`.WaitFor(x)` — do not start this resource until `x` is **healthy**.
`.WaitForCompletion(x)` — wait until `x` **exits** (used for one-shot migration
jobs; not used here). The chain is: `postgres` + `keycloak` → `api` → `web`.

### Passing data between resources — two styles

1. **Aspire's sugar (not used here):** `.WithReference(db)` injects
   `ConnectionStrings__db` plus a batch of `DB_HOST`, `DB_PORT`, … env vars and
   registers a relationship. We avoid it because the app's config key is
   `ConnectionStrings:Default`, not `:ninvoices`.
2. **Explicit (what we do):** `.WithEnvironment("ConnectionStrings__Default",
   db.Resource.ConnectionStringExpression)` — we choose the exact key and hand it
   the reference expression. Same for `VITE_PROXY_TARGET = api.GetEndpoint("http")`
   and the literal `Keycloak__Authority`.

Reference expressions matter because **ports and passwords are not known when
`AppHost.cs` runs** — only at launch. The expression is a template; Aspire fills it
in and then sets the env var.

### Service discovery

`AddServiceDefaults()` turns on `Microsoft.Extensions.ServiceDiscovery` (resolve
`http://api` → the real URL). This repo does **not** rely on it: the SPA reaches
the API through the Vite proxy, and the API reaches Keycloak through a literal
URL. It is available if you want it.

### The DCP reverse proxy — and why it is disabled

Normally Aspire puts a small **reverse proxy** in front of every endpoint: the
dashboard/browser hits a *stable* port, the proxy forwards to the app's *dynamic*
port. On this machine that proxy returns `ERR_EMPTY_RESPONSE` for the Vite dev
server and for Keycloak. So every resource calls:

```csharp
resource.WithEndpointProxySupport(false);
```

Effect: containers get a **direct Docker/Podman `-p host:container` mapping** and
Kestrel binds the host port itself — no proxy in the path. It is a **separate
statement** because `WithEndpointProxySupport` returns a *narrowed* builder type
that would break a `.WaitFor` / `.WithEnvironment` placed after it.

### Networking model (`run` mode)

- `postgres` and `keycloak` are **containers** on a Podman network, with published
  `127.0.0.1` host ports.
- `api` and `web` run **on the host** (not containers).
- Therefore host processes reach the containers at `localhost:<published port>` —
  that is why `Keycloak__Authority` is `http://localhost:8088/...` and **not**
  `http://keycloak:8080/...` (there is no `Keycloak__ExternalAuthority`, so the
  API's `KeycloakBackchannelHandler` does no host rewriting).

---

## 5. Where every setting lives (and who wins)

| Layer | File / mechanism | Applies to | Precedence |
|---|---|---|---|
| AppHost injections | `AppHost.cs` `.WithEnvironment(...)` → OS env vars | api, web, keycloak | **Highest** for those processes |
| App config files | `src/nInvoices.Api/appsettings.json`, `appsettings.Development.json` | api | Loaded, but any key the AppHost sets via env wins |
| Launch profile | `src/nInvoices.Api/Properties/launchSettings.json` (profile `http`) | api | Supplies `ASPNETCORE_ENVIRONMENT=Development` + the `:5297` URL for standalone `dotnet run`; Aspire reads it too |
| Vite config | `src/nInvoices.Web/vite.config.ts` | web | Reads `process.env.PORT` / `VITE_PROXY_TARGET` / `VITE_BASE` |
| Vite env file | `src/nInvoices.Web/.env.development` | web | `VITE_AUTH_DISABLED=true` — **overridden** by the AppHost's `process.env` |
| Keycloak realm | `src/nInvoices.AppHost/keycloak/ninvoices-realm.json` | keycloak | Imported on container start |
| Generated secrets | `~/.microsoft/usersecrets/ninvoices-apphost/secrets.json` (keyed by `<UserSecretsId>`) | postgres, keycloak | Random passwords, stable between runs |
| Image name/tag defaults | constants **inside** `Aspire.Hosting.PostgreSQL` / `Aspire.Hosting.Keycloak` | postgres, keycloak | Overridable via `.WithImageTag(...)` / `.WithImage(...)` |
| Telemetry target | env var `OTEL_EXPORTER_OTLP_ENDPOINT` (unset by default) | api (via ServiceDefaults) | Enables OTLP trace/metric export |

---

## 6. Ports

| Resource | Host port | Fixed? | Why |
|---|---|---|---|
| Keycloak (HTTP) | `8088` → container `8080` | **Fixed** | `KeycloakUrl` is a literal string used by both the API (`Keycloak__Authority`) and the browser (`VITE_KEYCLOAK_URL`) |
| Keycloak (HTTPS / mgmt) | random | no | not used |
| PostgreSQL | random (`127.0.0.1:*→5432`) | no | only referenced via `ConnectionStringExpression` |
| API | assigned by Aspire / `:5297` from launch profile | effectively fixed | Vite proxies `/api` to `api.GetEndpoint("http")` (a reference, so the exact value does not matter) |
| Vite dev server | assigned by Aspire (`PORT` env) | no | realm redirect URIs are `"*"`, so any port works |
| Aspire dashboard | printed on `aspire run` (≈ `:17xxx`) | — | — |

---

## 7. `aspire run` vs production

| | `aspire run` (this project) | Production |
|---|---|---|
| Orchestrator | `nInvoices.AppHost` | `docker/deploy.sh` + hand-written compose on the server |
| Database | throwaway Postgres container, schema via `EnsureCreated` | shared PostgreSQL, schema via `docker/migrations-postgres/*.sql` |
| Auth | Keycloak container, realm from JSON | your Keycloak, e.g. `https://your-domain.com/realms/ninvoices` |
| Frontend | Vite dev server | pre-built image behind nginx at `/nInvoices` |
| Aspire involved? | yes | **no** |

`aspire publish --publisher manifest` only prints a JSON description of the graph;
it is used here to check the wiring, never to deploy.

---

## 8. Package / capability map

| NuGet package | Version | Gives you |
|---|---|---|
| `Aspire.AppHost.Sdk` (MSBuild SDK) | `13.5.3` | `Aspire.Hosting` core, the `Projects.*` source generator, the bundled `aspire` CLI |
| `Aspire.Hosting.PostgreSQL` | `13.5.3` | `AddPostgres`, `AddDatabase`, default `postgres` image + health check |
| `Aspire.Hosting.Keycloak` | `13.5.3-preview.1.26425.3` | `AddKeycloak`, `WithRealmImport`, default `keycloak` image + health check (**preview** — the endpoint quirk in §3.2) |
| `Aspire.Hosting.JavaScript` | `13.5.3` | `AddViteApp` / `AddNodeApp` / `AddNpm`, runs `npm`/`vite` as a host process |
| `Microsoft.Extensions.Http.Resilience` | `10.8.0` | `AddStandardResilienceHandler()` (retry / circuit breaker / timeout) — via ServiceDefaults |
| `Microsoft.Extensions.ServiceDiscovery` | `10.8.0` | logical-name → URL resolution — via ServiceDefaults |
| `OpenTelemetry.*` | `1.15.x` | traces + metrics + log instrumentation and the OTLP exporter — via ServiceDefaults |

---

## 9. Common operations

**Reset the database.** No data volume → just restart `aspire run`; `EnsureCreated`
rebuilds an empty schema. If you added `.WithDataVolume("name")`, wipe it:
`podman volume rm name`.

**Change an image / tag.** `.WithImageTag("…")`, `.WithImage("…")`, or
`.WithImageRegistry("…")` on `AddPostgres` / `AddKeycloak`.

**Persist Postgres / Keycloak data.** Add `.WithDataVolume("ninvoices-pgdata")` /
`.WithDataVolume("ninvoices-kcdata")`. Then realm/schema changes need a volume wipe
to take effect.

**Change the test realm.** Edit `keycloak/ninvoices-realm.json`, restart.

**Add a new backing service** (e.g. Redis): add the `Aspire.Hosting.Redis`
package, then `var cache = builder.AddRedis("cache");` and
`api.WithReference(cache)` (or an explicit `WithEnvironment`).

**Send telemetry somewhere.** Set `OTEL_EXPORTER_OTLP_ENDPOINT` for the API (env
var, or `api.WithEnvironment(...)`), e.g. a standalone
`mcr.microsoft.com/dotnet/aspire-dashboard` container.

**Run the fast loop instead** (SQLite + dev-auth bypass, no containers): two
terminals — `cd src/nInvoices.Api && dotnet run` and
`cd src/nInvoices.Web && npm run dev`. See the repo `CLAUDE.md`.

---

## 10. Troubleshooting (issues actually hit while wiring this up)

| Symptom | Cause | Fix |
|---|---|---|
| Standalone `dotnet run` → `InvalidOperationException: MetadataAddress or Authority must use HTTPS` | `launchSettings.json` is git-ignored; without it `dotnet run` starts in **Production**, ignoring `appsettings.Development.json` (`UseDevAuth=true`) | Keep `src/nInvoices.Api/Properties/launchSettings.json` (already restored) with `ASPNETCORE_ENVIRONMENT=Development` + `applicationUrl=http://localhost:5297` |
| `api` resource "Finished", `Npgsql … 42601: syntax error at or near "["` during `EnsureCreated` | A partial-index filter was hard-coded in SQLite dialect (`HasFilter("[IsActive] = 1")`) | Now provider-switched in `ApplicationDbContext.OnModelCreating` (`Database.IsNpgsql() ? "\"IsActive\"" : "[IsActive] = 1"`); zero change for the SQLite path |
| `web` / dashboard URL → `ERR_EMPTY_RESPONSE` even though Vite logs "ready" | Aspire's DCP reverse proxy is broken on this host | `WithEndpointProxySupport(false)` on every resource |
| Keycloak URL → `ERR_EMPTY_RESPONSE`, `podman ps` shows `8088->8443/tcp` | Preview `Aspire.Hosting.Keycloak` maps `AddKeycloak(port:)` to the **HTTPS** port and never publishes HTTP | Drop `port:`; add explicit `WithEndpoint(port: 8088, targetPort: 8080, scheme: "http", name: "http-public", isProxied: false)` |
| Realm edits not applied / port already in use | A killed `aspire run` left containers behind | `podman rm -f` the leftover `keycloak-*` / `postgres-*` containers, then re-run |
| Vite not on port 3000 | Aspire assigns the port via `PORT`; `vite.config.ts` honours it | Expected — realm redirect URIs are `"*"`, so it works on any port |

---

## 11. File map

| Path | Role |
|---|---|
| `src/nInvoices.AppHost/AppHost.cs` | The whole resource graph |
| `src/nInvoices.AppHost/nInvoices.AppHost.csproj` | Aspire SDK, hosting packages, the API project reference |
| `src/nInvoices.AppHost/keycloak/ninvoices-realm.json` | Keycloak realm: clients, roles, `testuser` |
| `src/nInvoices.ServiceDefaults/Extensions.cs` | `AddServiceDefaults()` / `MapDefaultEndpoints()` |
| `src/nInvoices.Api/Program.cs` | `builder.AddServiceDefaults()`, `app.MapDefaultEndpoints()`, the `Database:EnsureCreated` block |
| `src/nInvoices.Infrastructure/Data/DatabaseExtensions.cs` | provider selection, `AddDbContextCheck` |
| `src/nInvoices.Api/Properties/launchSettings.json` | `Development` env + `:5297` for standalone runs |
| `src/nInvoices.Web/vite.config.ts` | consumes `PORT` / `VITE_PROXY_TARGET` |
| `src/nInvoices.Web/.env.development` | `VITE_AUTH_DISABLED` default (overridden by the AppHost) |
| `~/.microsoft/usersecrets/ninvoices-apphost/secrets.json` | generated Postgres / Keycloak passwords |
