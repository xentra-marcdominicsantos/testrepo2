var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();

app.MapGet("/", () => "THIS IS NEWWWWW");

app.Run();
