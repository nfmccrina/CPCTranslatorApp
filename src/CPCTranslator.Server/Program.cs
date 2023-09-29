using CPCTranslator.Server.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddSingleton<IMessagingService, MessagingService>();

var app = builder.Build();

app.UseWebSockets();
app.MapControllers();
app.UseDefaultFiles();
app.UseStaticFiles();

app.Run();
