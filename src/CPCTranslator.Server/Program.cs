using CPCTranslator.Server.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddHostedService<BackgroundSocketProcessor>();

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(o => {
        o.Audience = builder.Configuration["JWTAudience"];
        o.Authority = builder.Configuration["JWTAuthority"];
    });

var app = builder.Build();

app.UseWebSockets(new WebSocketOptions()
{
    KeepAliveInterval = TimeSpan.FromSeconds(5)
});
app.MapControllers();
app.UseDefaultFiles();
app.UseStaticFiles();

app.Run();
