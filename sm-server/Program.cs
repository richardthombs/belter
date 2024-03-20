using Microsoft.AspNetCore.SignalR;

var builder = WebApplication.CreateBuilder(args);
builder.Logging.ClearProviders().AddConsole();

// Add services to the container.
builder.Services.AddSignalR(x => x.EnableDetailedErrors = true);
builder.Services.AddSingleton<IUserIdProvider, DodgyUserIdProvider>();
builder.Services.AddHostedService<GameEngine>();
builder.Services.AddCors();

// Build and configure
var app = builder.Build();

app.UseCors(x => x
	.WithOrigins("http://localhost:3000")
	.AllowAnyHeader()
	.AllowAnyMethod()
	.AllowCredentials()
);
app.UseAuthorization();

app.MapHub<GameHub>("/hub");

app.Run();
