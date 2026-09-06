// nInvoices dev orchestrator (.NET Aspire).
//
// `aspire run` (or `dotnet run --project src/nInvoices.AppHost`) starts, as one graph:
//   - PostgreSQL          (throwaway; schema built from the EF model via EnsureCreated)
//   - Keycloak            (realm "ninvoices" imported from ./keycloak, host port 8088)
//   - nInvoices.Api       (PostgreSQL + real Keycloak auth — NOT DevAuth)
//   - nInvoices.Web       (Vite dev server; /api proxied to the api resource)
// plus the Aspire dashboard (traces / logs / metrics / resource state).
//
// This is the "prod parity" inner loop: PostgreSQL + Keycloak instead of the fast
// SQLite + DevAuth loop (`dotnet run` + `npm run dev`). It is DEV ONLY and is never
// deployed — production stays docker/deploy.sh -> Docker Hub -> compose over SSH.
//
// Requires a running Docker/Podman. Containers are recreated on every run (no data
// volumes) so each session starts clean; add .WithDataVolume() to persist.
//
// Every resource calls WithEndpointProxySupport(false): Aspire's DCP reverse proxy
// returns ERR_EMPTY_RESPONSE on this host, so containers get a direct Docker port
// mapping and the API's Kestrel binds the host port directly. It is a separate
// statement per resource because it narrows the builder's generic type (which would
// break a fluent .WaitFor / .WithEnvironment after it).

const int KeycloakPort = 8088;
const string KeycloakUrl = "http://localhost:8088"; // keep in sync with KeycloakPort

var builder = DistributedApplication.CreateBuilder(args);

var postgres = builder.AddPostgres("postgres")
    .WithImageTag("17-alpine"); // match docker-compose.prod.yml's major version
var db = postgres.AddDatabase("ninvoices");

// NOTE: this preview Aspire.Hosting.Keycloak binds AddKeycloak(port:) to the HTTPS
// endpoint (8443) and never publishes HTTP (8080). Publish HTTP -> 8088 explicitly.
var keycloak = builder.AddKeycloak("keycloak")
    .WithRealmImport("./keycloak")
    .WithEnvironment("KC_HTTP_ENABLED", "true")
    .WithEnvironment("KC_HOSTNAME_STRICT", "false")
    .WithEndpoint(port: KeycloakPort, targetPort: 8080, scheme: "http",
        name: "http-public", isProxied: false);
keycloak.WithEndpointProxySupport(false);

var api = builder.AddProject<Projects.nInvoices_Api>("api")
    .WaitFor(db)
    .WaitFor(keycloak)
    // Use PostgreSQL and build the schema from the current EF model on startup
    // (EF migrations are SQLite-scaffolded and cannot run under Npgsql).
    .WithEnvironment("Database__Type", "PostgreSQL")
    .WithEnvironment("Database__EnsureCreated", "true")
    .WithEnvironment("ConnectionStrings__Default", db.Resource.ConnectionStringExpression)
    // Real Keycloak auth instead of the dev bypass. No ExternalAuthority: the API
    // runs on the host in `aspire run` and reaches Keycloak directly at KeycloakUrl.
    .WithEnvironment("Authentication__UseDevAuth", "false")
    .WithEnvironment("Keycloak__RequireHttpsMetadata", "false")
    .WithEnvironment("Keycloak__Authority", $"{KeycloakUrl}/realms/ninvoices")
    .WithEnvironment("Keycloak__Audience", "ninvoices-api");
api.WithEndpointProxySupport(false);

// Vite dev server. Aspire assigns its port; the realm accepts any redirect URI in
// dev. The /api proxy target follows the api resource's real URL.
var web = builder.AddViteApp("web", "../nInvoices.Web")
    .WithNpm()               // run `npm install` before `npm run dev`
    .WaitFor(api)
    .WithEnvironment("VITE_PROXY_TARGET", api.GetEndpoint("http"))
    .WithEnvironment("VITE_AUTH_DISABLED", "false")
    .WithEnvironment("VITE_KEYCLOAK_URL", KeycloakUrl)
    .WithEnvironment("VITE_KEYCLOAK_REALM", "ninvoices")
    .WithEnvironment("VITE_KEYCLOAK_CLIENT_ID", "ninvoices-web")
    .WithExternalHttpEndpoints();
web.WithEndpointProxySupport(false);

builder.Build().Run();
