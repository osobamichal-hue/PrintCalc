using System.Text.Json;
using System.Text.Json.Serialization;
using PrintCalc.Api;
using PrintCalc.Infrastructure;
using PrintCalc.Infrastructure.Persistence;
using QuestPDF.Infrastructure;

QuestPDF.Settings.License = LicenseType.Community;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddInfrastructure(builder.Configuration, forWebHost: true);
builder.Services.ConfigureHttpJsonOptions(o =>
{
    o.SerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
    o.SerializerOptions.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase, allowIntegerValues: true));
});

builder.WebHost.ConfigureKestrel(o => o.Limits.MaxRequestBodySize = 120_000_000);

var corsOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>()
                  ?? ["http://localhost:3000"];
builder.Services.AddCors(opt =>
{
    opt.AddDefaultPolicy(p =>
        p.WithOrigins(corsOrigins)
            .AllowAnyHeader()
            .AllowAnyMethod());
});

var app = builder.Build();

app.UseCors();

await using (var scope = app.Services.CreateAsyncScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    await DbInitializer.InitializeAsync(db);
}

app.MapCore();
app.MapInventory();
app.MapModelsAndCalculations();
app.MapDocuments();
app.MapPurchaseInvoices();
app.MapStatistics();

app.Run();
