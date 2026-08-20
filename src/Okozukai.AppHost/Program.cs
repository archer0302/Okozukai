using Microsoft.Extensions.Hosting;

var builder = DistributedApplication.CreateBuilder(args);

var db = builder.AddConnectionString("okozukai");

var tailnetIp = builder.Configuration["TAILNET_IP"];
var apiPort = int.Parse(builder.Configuration["TAILNET_API_PORT"] ?? "5005");
var frontendPort = int.Parse(builder.Configuration["TAILNET_FRONTEND_PORT"] ?? "5173");

var api = builder.AddProject<Projects.Okozukai_Api>("api")
    .WithReference(db)
    .WithEnvironment("ASPNETCORE_ENVIRONMENT", "Development");

if (!string.IsNullOrEmpty(tailnetIp))
{
    var aspnetUrls = "http://0.0.0.0:" + apiPort;
    api.WithEndpoint("http", e => { e.Port = apiPort; e.IsProxied = false; })
       .WithEnvironment("ASPNETCORE_URLS", aspnetUrls);
}

// AddViteApp registers the http endpoint and PORT env var itself —
// calling WithHttpEndpoint here would create a duplicate endpoint.
var frontend = builder.AddViteApp("frontend", "../Okozukai.Frontend")
    .WithReference(api)
    .WithExternalHttpEndpoints();

if (!string.IsNullOrEmpty(tailnetIp))
{
    frontend.WithEndpoint("http", e => { e.Port = frontendPort; e.IsProxied = false; });

    var tailnetApiUrl = "http://" + tailnetIp + ":" + apiPort;
    frontend.WithEnvironment("VITE_API_URL", tailnetApiUrl);
}
else
    frontend.WithEnvironment("VITE_API_URL", api.GetEndpoint("http"));

builder.Build().Run();
