using Microsoft.Extensions.Hosting;

var builder = DistributedApplication.CreateBuilder(args);

var db = builder.AddConnectionString("okozukai");

var tailnetIp = builder.Configuration["TAILNET_IP"];
var apiPort = int.Parse(builder.Configuration["TAILNET_API_PORT"] ?? "5005");
var frontendPort = int.Parse(builder.Configuration["TAILNET_FRONTEND_PORT"] ?? "5173");
var grafanaPort = int.Parse(builder.Configuration["TAILNET_GRAFANA_PORT"] ?? "3000");

var api = builder.AddProject<Projects.Okozukai_Api>("api")
    .WithReference(db)
    .WithEnvironment("ASPNETCORE_ENVIRONMENT", "Development");

if (!string.IsNullOrEmpty(tailnetIp))
{
    var aspnetUrls = "http://0.0.0.0:" + apiPort;
    api.WithEndpoint("http", e => { e.Port = apiPort; e.IsProxied = false; })
       .WithEnvironment("ASPNETCORE_URLS", aspnetUrls);
}

var frontend = builder.AddNpmApp("frontend", "../Okozukai.Frontend", "dev")
    .WithReference(api)
    .WithHttpEndpoint(
        port: !string.IsNullOrEmpty(tailnetIp) ? frontendPort : null,
        env: "PORT",
        isProxied: string.IsNullOrEmpty(tailnetIp))
    .WithExternalHttpEndpoints();

if (!string.IsNullOrEmpty(tailnetIp))
{
    var tailnetApiUrl = "http://" + tailnetIp + ":" + apiPort;
    frontend.WithEnvironment("VITE_API_URL", tailnetApiUrl);
}
else
    frontend.WithEnvironment("VITE_API_URL", api.GetEndpoint("http"));

builder.AddContainer("grafana", "grafana/grafana-oss", "11.6.1")
    .WithHttpEndpoint(
        port: !string.IsNullOrEmpty(tailnetIp) ? grafanaPort : null,
        targetPort: 3000,
        name: "http")
    .WithExternalHttpEndpoints()
    .WithBindMount("./grafana/provisioning", "/etc/grafana/provisioning", isReadOnly: true)
    .WithBindMount("./grafana/dashboards", "/var/lib/grafana/dashboards", isReadOnly: true)
    .WithEnvironment("GF_SECURITY_ADMIN_USER", builder.Configuration["GRAFANA_ADMIN_USER"] ?? "admin")
    .WithEnvironment("GF_SECURITY_ADMIN_PASSWORD", builder.Configuration["GRAFANA_ADMIN_PASSWORD"] ?? "admin")
    .WithEnvironment("GRAFANA_DB_HOST", builder.Configuration["GRAFANA_DB_HOST"] ?? "host.docker.internal")
    .WithEnvironment("GRAFANA_DB_PORT", builder.Configuration["GRAFANA_DB_PORT"] ?? "5432")
    .WithEnvironment("GRAFANA_DB_NAME", builder.Configuration["GRAFANA_DB_NAME"] ?? "okozukai")
    .WithEnvironment("GRAFANA_DB_USER", builder.Configuration["GRAFANA_DB_USER"] ?? "postgres")
    .WithEnvironment("GRAFANA_DB_PASSWORD", builder.Configuration["GRAFANA_DB_PASSWORD"] ?? "postgres")
    .WithEnvironment("GRAFANA_DB_SSLMODE", builder.Configuration["GRAFANA_DB_SSLMODE"] ?? "disable");

builder.Build().Run();
