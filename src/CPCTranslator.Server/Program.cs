using CPCTranslator.Server.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddSingleton<IMessagingService, MessagingService>();

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(o => {
        o.Audience = builder.Configuration["JWTAudience"];
        o.Authority = builder.Configuration["JWTAuthority"];
    });

var app = builder.Build();

app.UseWebSockets();
app.MapControllers();
app.UseDefaultFiles();
app.UseStaticFiles();

app.Run();
