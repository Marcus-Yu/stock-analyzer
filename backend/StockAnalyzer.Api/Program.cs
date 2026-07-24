using Microsoft.EntityFrameworkCore;
using StockAnalyzer.Api.Data;
using StockAnalyzer.Api.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new() { Title = "Stock Analyzer API", Version = "v1" });
});

// Database
var connectionString = builder.Configuration["STOCKAI_CONNECTION_STRING"] ?? builder.Configuration.GetConnectionString("DefaultConnection");
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(connectionString));

// HTTP clients for external services
builder.Services.AddHttpClient<IFinnhubService, FinnhubService>();
builder.Services.AddHttpClient<IAnalysisAiService, AzureOpenAiService>();

// Business services
builder.Services.AddScoped<IPredictionStorageService, PredictionStorageService>();
builder.Services.AddScoped<IEvaluationService, EvaluationService>();
builder.Services.AddScoped<IStockAnalysisService, StockAnalysisService>();
builder.Services.AddScoped<IPostMortemEngine, PostMortemEngine>();
builder.Services.AddScoped<ILearningEngine, LearningEngine>();

builder.Services.AddHostedService<DailyEvaluationWorker>();

// CORS for frontend dev server
builder.Services.AddCors(options =>
{
    options.AddPolicy("DevCors", policy =>
    {
        policy.WithOrigins("http://localhost:5173", "http://localhost:3000")
              .AllowAnyHeader()
              .AllowAnyMethod();
    });
});

var app = builder.Build();

// Ensure database is migrated and seeded
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.Migrate();

    // Seed System Settings
    if (!db.SystemSettings.Any(s => s.SettingKey == "max_daily_weight_adjustment_percent"))
    {
        db.SystemSettings.Add(new StockAnalyzer.Api.Models.SystemSetting { SettingKey = "max_daily_weight_adjustment_percent", SettingValue = "2.0" });
    }
    if (!db.SystemSettings.Any(s => s.SettingKey == "learning_mode"))
    {
        db.SystemSettings.Add(new StockAnalyzer.Api.Models.SystemSetting { SettingKey = "learning_mode", SettingValue = "AUTO" });
    }

    // Seed Predefined Factors
    var predefinedFactors = new[] { "Valuation", "Momentum", "Sentiment", "Macro", "Institutional Flow" };
    foreach (var f in predefinedFactors)
    {
        if (!db.LearningFactors.Any(lf => lf.FactorName == f))
        {
            db.LearningFactors.Add(new StockAnalyzer.Api.Models.LearningFactor
            {
                FactorName = f,
                Description = "Predefined learning factor",
                IsPredefined = true
            });
        }
    }

    db.SaveChanges();
}

// Configure the HTTP request pipeline
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseCors("DevCors");
app.MapControllers();

app.Run();
