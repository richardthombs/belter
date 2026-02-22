using BelterLife.Gateway.Auth;
using BelterLife.Gateway.Hubs;
using BelterLife.Gateway.Infrastructure;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddProblemDetails();
builder.Services.AddSignalR().AddMessagePackProtocol();
builder.Services.AddHealthChecks();
builder.Services.AddBelterIdentity(builder.Configuration);
builder.Services.AddHttpClient<ShardClient>(c =>
{
    c.BaseAddress = new Uri(builder.Configuration["Shard__BaseUrl"] ?? "http://shard:5001");
}).AddTypedClient<IShardClient, ShardClient>();

var app = builder.Build();

// Apply pending EF Core migrations on startup
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<GatewayDbContext>();
    if (db.Database.ProviderName != "Microsoft.EntityFrameworkCore.InMemory")
        db.Database.Migrate();
    else
        db.Database.EnsureCreated();
}

app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.MapHub<GameHub>("/hubs/game");
app.MapHealthChecks("/health");

app.Run();

