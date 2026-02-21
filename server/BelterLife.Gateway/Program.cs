using BelterLife.Gateway.Hubs;
using Microsoft.AspNetCore.SignalR;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddSignalR().AddMessagePackProtocol();
builder.Services.AddHealthChecks();

var app = builder.Build();

app.UseRouting();
app.MapControllers();
app.MapHub<GameHub>("/hubs/game");
app.MapHealthChecks("/health");

app.Run();

