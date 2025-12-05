using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Hosting;

var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();

// Serve static files from wwwroot
app.UseStaticFiles();

// Minimal API endpoint
app.MapGet("/", () => Results.Text(@"
<!DOCTYPE html>
<html>
<head>
    <title>Service D</title>
    <link rel='stylesheet' href='/style.css'>
</head>
<body>
    <h1>Hello from Service D!</h1>
</body>
</html>", "text/html"));

app.Run();
