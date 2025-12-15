var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();

app.MapGet("/", () => "BUILD 120");

app.Run();
