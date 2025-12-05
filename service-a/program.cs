var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();

app.MapGet("/", () => "thissss is 96th tryyyy");

app.Run();
