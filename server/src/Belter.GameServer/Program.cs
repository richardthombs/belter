using Belter.GameServer;

var builder = Host.CreateApplicationBuilder(args);
builder.Services.AddHostedService<GameEngine>();
builder.Services.AddSingleton<GameWorld>();

var host = builder.Build();
host.Run();
