using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using MLPickingEstimator.Services;
using MLPickingEstimator.Models;
using Microsoft.Extensions.ML;
using Microsoft.AspNetCore.RateLimiting;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.HttpLogging;
using Microsoft.AspNetCore.OutputCaching;
using System.Text.Json;
using System.Text.Json.Serialization;
using MLPickingEstimator.Repositories;
using ClosedXML.Excel;

var builder = WebApplication.CreateBuilder(args);
// YapÄ±landÄ±rma: appsettings.jsonâ€™dan yollar ve ayarlar
var modelsDir = builder.Configuration["Paths:ModelsDir"] ?? "Models";
var zipModel = builder.Configuration["Paths:ZipModel"] ?? Path.Combine(modelsDir, "picking_model.zip");
var dataDir = builder.Configuration["Paths:DataDir"] ?? "Data";
var trainingData = builder.Configuration["Paths:TrainingData"] ?? Path.Combine(dataDir, "picking_data.csv");
var onnxModelPath = builder.Configuration["Paths:OnnxModel"] ?? Path.Combine(modelsDir, "picking_model.onnx");
var versionsFile = builder.Configuration["Paths:VersionsFile"] ?? Path.Combine(modelsDir, "versions.json");
var archiveDir = builder.Configuration["Paths:ArchiveDir"] ?? Path.Combine(modelsDir, "archive");
var retention = int.TryParse(builder.Configuration["Versioning:RetentionCount"], out var r) ? r : 5;

// Konsol loglama
builder.Logging.ClearProviders();
builder.Logging.AddConsole();
builder.Logging.AddJsonConsole();

// Servisleri kaydet
builder.Services.AddSingleton<MLPickingService>();
builder.Services.AddSingleton<AdvancedMLPickingService>();
builder.Services.AddSingleton<AutoMLService>();
builder.Services.AddSingleton<OnnxService>();
builder.Services.AddSingleton<ModelMonitoringService>();
builder.Services.AddSingleton<LocationResolver>();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddSingleton(sp => new ModelVersioningService(modelsDir, archiveDir, versionsFile, retention));
builder.Services.AddSingleton<DataDriftService>();
builder.Services.AddSingleton<PersonnelDispatchService>();
builder.Services.AddSingleton<RouteSimulationService>();
builder.Services.AddSingleton<DispatchOptimizationService>();
builder.Services.AddSingleton<TelemetryService>();
// ProblemDetails for RFC7807
builder.Services.AddProblemDetails();
// HealthChecks
builder.Services.AddHealthChecks()
    .AddCheck<MLPickingEstimator.Services.Health.ModelAndDbHealthCheck>("resources");
builder.Services.AddSingleton(sp =>
{
    var dataDir = builder.Configuration["Paths:DataDir"] ?? "Data";
    var dbPath = builder.Configuration["Paths:Database"] ?? Path.Combine(dataDir, "telemetry.db");
    return new JobsRepository(dbPath);
});
builder.Services.AddSingleton<IPersonnelRepository>(sp =>
{
    var dataDir = builder.Configuration["Paths:DataDir"] ?? "Data";
    return new PersonnelRepository(dataDir);
});

// JSON camelCase ve nullâ€™larÄ± gizleme
builder.Services.ConfigureHttpJsonOptions(o =>
{
    o.SerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
    o.SerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
});

// HTTP logging servisi
builder.Services.AddHttpLogging(logging =>
{
    logging.LoggingFields = HttpLoggingFields.RequestPropertiesAndHeaders | HttpLoggingFields.ResponsePropertiesAndHeaders;
});

// Rate limiting ve OutputCache servisleri
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddFixedWindowLimiter("predict", opt =>
    {
        opt.PermitLimit = 30; // 30 istek / dakika
        opt.Window = TimeSpan.FromMinutes(1);
        opt.QueueLimit = 0;
    });
    options.AddFixedWindowLimiter("batch", opt =>
    {
        opt.PermitLimit = 10;
        opt.Window = TimeSpan.FromMinutes(1);
        opt.QueueLimit = 0;
    });
    options.AddFixedWindowLimiter("train", opt =>
    {
        opt.PermitLimit = 3;
        opt.Window = TimeSpan.FromMinutes(5);
        opt.QueueLimit = 0;
    });
    options.AddFixedWindowLimiter("simulate", opt =>
    {
        opt.PermitLimit = 20;
        opt.Window = TimeSpan.FromMinutes(1);
        opt.QueueLimit = 0;
    });
    options.AddFixedWindowLimiter("general", opt =>
    {
        opt.PermitLimit = 60;
        opt.Window = TimeSpan.FromMinutes(1);
        opt.QueueLimit = 0;
    });
});
builder.Services.AddOutputCache(options =>
{
    options.AddBasePolicy(builder => builder.Expire(TimeSpan.FromSeconds(30)));
});

// PredictionEnginePool (dosyadan model yÃ¼kleme, deÄŸiÅŸiklikleri izleme)
builder.Services
    .AddPredictionEnginePool<ProductPickingData, PickingTimePrediction>()
    .FromFile(modelName: "PickingModel", filePath: zipModel, watchForChanges: true);

// CORS ayarlarÄ±
builder.Services.AddCors(options =>
{
    var origins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? Array.Empty<string>();
    options.AddPolicy("Default", policy =>
    {
        if (origins.Length > 0)
        {
            policy.WithOrigins(origins)
                  .AllowAnyMethod()
                  .AllowAnyHeader();
        }
        else
        {
            policy.AllowAnyOrigin()
                  .AllowAnyMethod()
                  .AllowAnyHeader();
        }
    });
});

var app = builder.Build();

// CORS'u etkinleÅŸtir
// Global exception handler ve HTTPS yönlendirme
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler(errorApp =>
    {
        errorApp.Run(async context =>
        {
            var feature = context.Features.Get<Microsoft.AspNetCore.Diagnostics.IExceptionHandlerFeature>();
            var ex = feature?.Error;
            var problem = new
            {
                error = ex?.Message ?? "Beklenmeyen hata",
                traceId = context.TraceIdentifier
            };
            context.Response.StatusCode = StatusCodes.Status500InternalServerError;
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsJsonAsync(problem);
        });
    });
}
app.UseHttpsRedirection();

// CORS'u etkinleştir
app.UseCors("Default");

// Swagger'Ä± etkinleÅŸtir
app.UseSwagger();
app.UseSwaggerUI();
// HTTP logging
app.UseHttpLogging();
// Statik dosyalar (wwwroot)
app.UseStaticFiles();
// Output caching
app.UseOutputCache();
// Rate limiting
app.UseRateLimiter();
// Basit API key kontrolü (opsiyonel, sadece POST istekleri için)
var requireApiKey = bool.TryParse(builder.Configuration["Security:RequireApiKey"], out var reqKey) && reqKey;
var apiKey = builder.Configuration["Security:ApiKey"] ?? string.Empty;
if (requireApiKey)
{
    app.Use(async (ctx, next) =>
    {
        if (HttpMethods.IsPost(ctx.Request.Method))
        {
            if (!ctx.Request.Headers.TryGetValue("X-API-Key", out var header) || header != apiKey)
            {
                ctx.Response.StatusCode = StatusCodes.Status401Unauthorized;
                await ctx.Response.WriteAsJsonAsync(new { error = "API key gerekli veya geçersiz" });
                return;
            }
        }
        await next();
    });
}
// /warehouse için kısa yol (yeni UI sayfasına yönlendirir)
app.MapGet("/warehouse", () => Results.Redirect("/personnel-assign.html"));
// Kozmetik depo animasyon sayfasÄ± kaldÄ±rÄ±ldÄ±

// Ä°ÅŸ listesi ingest ve export uÃ§larÄ±
app.MapPost("/jobs/ingest", (DispatchRequest req, JobsRepository repo) =>
{
    try
    {
        var jobs = req.Jobs ?? new List<JobTask>();
        repo.InsertJobs(jobs);
        return Results.Ok(new { inserted = jobs.Count });
    }
    catch (Exception ex)
    {
        return Results.BadRequest(new { error = ex.Message });
    }
})
 .RequireRateLimiting("general")
 .CacheOutput(p => p.NoCache());

app.MapGet("/jobs/export", (string? format, JobsRepository repo) =>
{
    var jobs = repo.GetAllJobs();
    format = (format ?? "csv").ToLowerInvariant();
    if (format == "csv")
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("id,x,y,weightKg,priority,earliestTime,latestTime,zone,quantity,orderId,rackId,corridor");
        foreach (var j in jobs)
        {
            sb.AppendLine(string.Join(',', new[]
            {
                j.Id,
                j.Location?.X.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? "",
                j.Location?.Y.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? "",
                j.WeightKg?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? "",
                j.Priority.ToString(),
                j.EarliestTime?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? "",
                j.LatestTime?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? "",
                j.Zone ?? "",
                j.Quantity?.ToString() ?? "",
                j.OrderId ?? "",
                j.RackId ?? "",
                j.Corridor ?? ""
            }));
        }
        var bytes = System.Text.Encoding.UTF8.GetBytes(sb.ToString());
        return Results.File(bytes, "text/csv", fileDownloadName: "jobs.csv");
    }
    else if (format == "xlsx")
    {
        using var wb = new ClosedXML.Excel.XLWorkbook();
        var ws = wb.Worksheets.Add("Jobs");
        string[] headers = { "id","x","y","weightKg","priority","earliestTime","latestTime","zone","quantity","orderId","rackId","corridor" };
        for (int i = 0; i < headers.Length; i++) ws.Cell(1, i+1).Value = headers[i];
        int row = 2;
        foreach (var j in jobs)
        {
            ws.Cell(row,1).Value = j.Id;
            ws.Cell(row,2).Value = j.Location?.X ?? 0;
            ws.Cell(row,3).Value = j.Location?.Y ?? 0;
            ws.Cell(row,4).Value = j.WeightKg ?? 0;
            ws.Cell(row,5).Value = j.Priority;
            ws.Cell(row,6).Value = j.EarliestTime ?? 0;
            ws.Cell(row,7).Value = j.LatestTime ?? 0;
            ws.Cell(row,8).Value = j.Zone ?? "";
            ws.Cell(row,9).Value = j.Quantity ?? 0;
            ws.Cell(row,10).Value = j.OrderId ?? "";
            ws.Cell(row,11).Value = j.RackId ?? "";
            ws.Cell(row,12).Value = j.Corridor ?? "";
            row++;
        }
        using var ms = new MemoryStream();
        wb.SaveAs(ms);
        ms.Position = 0;
        return Results.File(ms.ToArray(), "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", fileDownloadName: "jobs.xlsx");
    }
    else
    {
        return Results.BadRequest(new { error = "Desteklenmeyen format. csv veya xlsx kullanÄ±n." });
    }
})
 .RequireRateLimiting("general")
 .CacheOutput(p => p.NoCache());

// ML servisini baÅŸlat ve modeli yÃ¼kle
var mlService = app.Services.GetRequiredService<MLPickingService>();
var advancedService = app.Services.GetRequiredService<AdvancedMLPickingService>();
var monitoringService = app.Services.GetRequiredService<ModelMonitoringService>();
// Kalıcı depo tablolarını oluştur
app.Services.GetRequiredService<JobsRepository>().EnsureCreated();

// Basit istek doÄŸrulama yardÄ±mcÄ±larÄ±
bool ValidateRequest(PredictionRequest req, out string error)
{
    var errors = new List<string>();
    if (req.ItemCount < 1) errors.Add("ItemCount en az 1 olmalÄ±");
    if (req.Weight < 0) errors.Add("Weight negatif olamaz");
    if (req.Volume < 0) errors.Add("Volume negatif olamaz");
    if (req.Distance < 0) errors.Add("Distance negatif olamaz");
    if (req.PickerExperience < 1 || req.PickerExperience > 10) errors.Add("PickerExperience 1-10 aralÄ±ÄŸÄ±nda olmalÄ±");
    if (req.StockDensity < 0 || req.StockDensity > 1) errors.Add("StockDensity 0-1 aralÄ±ÄŸÄ±nda olmalÄ±");
    // Opsiyonel alanlar için basit doğrulama
    if (req.FragileRatio is not null && (req.FragileRatio < 0 || req.FragileRatio > 1)) errors.Add("FragileRatio 0-1 aralÄ±ÄŸÄ±nda olmalÄ±");
    if (req.AisleCrowding is not null && (req.AisleCrowding < 0 || req.AisleCrowding > 1)) errors.Add("AisleCrowding 0-1 aralÄ±ÄŸÄ±nda olmalÄ±");
    if (req.PickerFatigue is not null && (req.PickerFatigue < 0 || req.PickerFatigue > 1)) errors.Add("PickerFatigue 0-1 aralÄ±ÄŸÄ±nda olmalÄ±");
    if (req.UrgencyLevel is not null && (req.UrgencyLevel < 0 || req.UrgencyLevel > 1)) errors.Add("UrgencyLevel 0-1 aralÄ±ÄŸÄ±nda olmalÄ±");
    if (req.ZoneComplexity is not null && (req.ZoneComplexity < 0 || req.ZoneComplexity > 1)) errors.Add("ZoneComplexity 0-1 aralÄ±ÄŸÄ±nda olmalÄ±");
    if (req.CartLoadKg is not null && req.CartLoadKg < 0) errors.Add("CartLoadKg negatif olamaz");
    if (req.StopCount is not null && req.StopCount < 0) errors.Add("StopCount negatif olamaz");
    if (req.TemperatureHandling is not null && (req.TemperatureHandling < 0 || req.TemperatureHandling > 1)) errors.Add("TemperatureHandling 0-1 aralÄ±ÄŸÄ±nda olmalÄ±");
    if (errors.Count > 0)
    {
        error = string.Join("; ", errors);
        try { app.Logger.LogWarning("ValidateRequest başarısız: {Error}. İstek: {@Req}", error, req); } catch { }
        return false;
    }
    error = string.Empty;
    return true;
}

// Simülasyon istek doğrulama
bool ValidateSimulation(SimulationRequest req, out string error)
{
    if ((req.TaskLocations == null || req.TaskLocations.Count == 0) && (req.TaskLocationCodes == null || req.TaskLocationCodes.Count == 0))
    {
        error = "taskLocations veya taskLocationCodes gereklidir";
        return false;
    }
    error = string.Empty;
    return true;
}

// Dispatch istek doğrulama
bool ValidateDispatch(DispatchRequest req, out string error)
{
    var errors = new List<string>();
    if (req.Vehicles == null || req.Vehicles.Count == 0) errors.Add("vehicles boş olamaz");
    if (req.Jobs == null || req.Jobs.Count == 0) errors.Add("jobs boş olamaz");
    if (req.AverageSpeedMps is not null && req.AverageSpeedMps <= 0) errors.Add("averageSpeedMps pozitif olmalı");
    if (req.HandlingSecondsPerTask is not null && req.HandlingSecondsPerTask < 0) errors.Add("handlingSecondsPerTask negatif olamaz");
    if (errors.Count > 0)
    {
        error = string.Join("; ", errors);
        return false;
    }
    error = string.Empty;
    return true;
}

try
{
    mlService.LoadModelAsync(zipModel).Wait();
    Console.WriteLine("âœ… Model baÅŸarÄ±yla yÃ¼klendi");
}
catch (FileNotFoundException)
{
    Console.WriteLine("âš ï¸ Model dosyasÄ± bulunamadÄ±, yeni model eÄŸitilecek");
    mlService.TrainModelAsync(trainingData).Wait();
}

// Ana sayfa
app.MapGet("/", () => new
{
    Title = "ML.NET Depo OperasyonlarÄ± Tahmin API'si",
    Version = "2.0",
    Features = new[] { "Single Model Prediction", "Ensemble Prediction", "Multi-Algorithm Training", "Model Monitoring", "AutoML", "ONNX Support" },
    Endpoints = new
    {
        Predict = "POST /predict",
        PredictBatch = "POST /predict-batch",
        EnsemblePredict = "POST /ensemble-predict",
        Train = "POST /train",
        TrainMultiple = "POST /train-multiple",
        CompareModels = "GET /compare-models",
        Monitor = "GET /monitor",
        Metrics = "GET /metrics",
        Health = "GET /health",
        AssignPicking = "POST /assign-picking",
        PersonnelLocations = "GET /personnel/locations",
        PersonnelPerformance = "GET /personnel/performance"
    }
});

// Temel tahmin endpoint'i
app.MapPost("/predict", (PredictionRequest request) =>
{
    try
    {
        if (!ValidateRequest(request, out var validationError))
        {
            try { app.Logger.LogWarning("/predict doğrulama hatası: {Error}. İstek: {@Req}", validationError, request); } catch { }
            return Results.BadRequest(new { error = validationError });
        }
        var input = new ProductPickingData
        {
            ItemCount = request.ItemCount,
            Weight = request.Weight,
            Volume = request.Volume,
            Distance = request.Distance,
            PickerExperience = request.PickerExperience,
            StockDensity = request.StockDensity,
            FragileRatio = request.FragileRatio ?? 0f,
            AisleCrowding = request.AisleCrowding ?? 0f,
            CartLoadKg = request.CartLoadKg ?? 0f,
            PickerFatigue = request.PickerFatigue ?? 0f,
            StopCount = request.StopCount ?? 0f,
            UrgencyLevel = request.UrgencyLevel ?? 0f,
            ZoneComplexity = request.ZoneComplexity ?? 0f,
            TemperatureHandling = request.TemperatureHandling ?? 0f
        };

        // mlService ile tahmin
        var prediction = mlService.Predict(input);
        
        // Tahmini logla
        monitoringService.LogPrediction(input, prediction);

        return Results.Ok(new PredictionResponse
        {
            PredictedTime = prediction.PredictedTime,
            Confidence = 0.85f,
            ModelVersion = "1.0",
            PredictionTime = DateTime.UtcNow,
            Algorithm = "FastTree"
        });
    }
    catch (Exception ex)
    {
        try { app.Logger.LogError(ex, "/predict çalıştırma hatası"); } catch { }
        return Results.BadRequest(new { error = ex.Message });
    }
})
 .RequireRateLimiting("predict")
 .CacheOutput(p => p.NoCache());

// Pooled PredictionEngine endpoint'i (yüksek eşzamanlılık için alternatif)
app.MapPost("/predict-pooled", (PredictionRequest request, PredictionEnginePool<ProductPickingData, PickingTimePrediction> pool) =>
{
    try
    {
        if (!ValidateRequest(request, out var validationError))
        {
            try { app.Logger.LogWarning("/predict-pooled doğrulama hatası: {Error}. İstek: {@Req}", validationError, request); } catch { }
            return Results.BadRequest(new { error = validationError });
        }

        var input = new ProductPickingData
        {
            ItemCount = request.ItemCount,
            Weight = request.Weight,
            Volume = request.Volume,
            Distance = request.Distance,
            PickerExperience = request.PickerExperience,
            StockDensity = request.StockDensity,
            FragileRatio = request.FragileRatio ?? 0f,
            AisleCrowding = request.AisleCrowding ?? 0f,
            CartLoadKg = request.CartLoadKg ?? 0f,
            PickerFatigue = request.PickerFatigue ?? 0f,
            StopCount = request.StopCount ?? 0f,
            UrgencyLevel = request.UrgencyLevel ?? 0f,
            ZoneComplexity = request.ZoneComplexity ?? 0f,
            TemperatureHandling = request.TemperatureHandling ?? 0f
        };

        var prediction = pool.Predict(modelName: "PickingModel", example: input);
        monitoringService.LogPrediction(input, prediction);

        return Results.Ok(new PredictionResponse
        {
            PredictedTime = prediction.PredictedTime,
            Confidence = 0.85f,
            ModelVersion = "pooled",
            PredictionTime = DateTime.UtcNow,
            Algorithm = "FastTree"
        });
    }
    catch (Exception ex)
    {
        try { app.Logger.LogError(ex, "/predict-pooled çalıştırma hatası"); } catch { }
        return Results.BadRequest(new { error = ex.Message });
    }
})
 .RequireRateLimiting("predict")
 .CacheOutput(p => p.NoCache());

// Lokasyon bilgisiyle tahmin endpoint'i
app.MapPost("/predict-with-locations", (
    PredictionWithLocationsRequest request,
    PredictionEnginePool<ProductPickingData, PickingTimePrediction> pool,
    LocationResolver resolver
) =>
{
    try
    {
        // Mesafeyi belirle: override varsa onu kullan, yoksa lokasyondan hesapla
        float distance;
        if (request.DistanceOverride is float dOv && dOv >= 0)
        {
            distance = dOv;
        }
        else
        {
            var last = request.LastLocation ?? (resolver.Resolve(request.LastLocationCode));
            var task = request.TaskLocation ?? (resolver.Resolve(request.TaskLocationCode));
            var dx = (last?.X ?? 0) - (task?.X ?? 0);
            var dy = (last?.Y ?? 0) - (task?.Y ?? 0);
            distance = (float)Math.Sqrt(dx * dx + dy * dy);
        }

        // Mevcut doğrulama ile uyum için PredictionRequest'e dönüştür
        var baseReq = new PredictionRequest
        {
            ItemCount = request.ItemCount,
            Weight = request.Weight,
            Volume = request.Volume,
            Distance = distance,
            PickerExperience = request.PickerExperience,
            StockDensity = request.StockDensity,
            FragileRatio = request.FragileRatio,
            AisleCrowding = request.AisleCrowding,
            CartLoadKg = request.CartLoadKg,
            PickerFatigue = request.PickerFatigue,
            StopCount = request.StopCount,
            UrgencyLevel = request.UrgencyLevel,
            ZoneComplexity = request.ZoneComplexity,
            TemperatureHandling = request.TemperatureHandling
        };

        if (!ValidateRequest(baseReq, out var validationError))
        {
            try { app.Logger.LogWarning("/predict-with-locations doğrulama hatası: {Error}. İstek: {@Req}", validationError, request); } catch { }
            return Results.BadRequest(new { error = validationError });
        }

        var input = new ProductPickingData
        {
            ItemCount = baseReq.ItemCount,
            Weight = baseReq.Weight,
            Volume = baseReq.Volume,
            Distance = baseReq.Distance,
            PickerExperience = baseReq.PickerExperience,
            StockDensity = baseReq.StockDensity,
            FragileRatio = baseReq.FragileRatio ?? 0f,
            AisleCrowding = baseReq.AisleCrowding ?? 0f,
            CartLoadKg = baseReq.CartLoadKg ?? 0f,
            PickerFatigue = baseReq.PickerFatigue ?? 0f,
            StopCount = baseReq.StopCount ?? 0f,
            UrgencyLevel = baseReq.UrgencyLevel ?? 0f,
            ZoneComplexity = baseReq.ZoneComplexity ?? 0f,
            TemperatureHandling = baseReq.TemperatureHandling ?? 0f
        };

        var prediction = pool.Predict(modelName: "PickingModel", example: input);
        monitoringService.LogPrediction(input, prediction);

        return Results.Ok(new PredictionResponse
        {
            PredictedTime = prediction.PredictedTime,
            Confidence = 0.85f,
            ModelVersion = "1.0",
            PredictionTime = DateTime.UtcNow,
            Algorithm = "FastTree"
        });
    }
    catch (Exception ex)
    {
        try { app.Logger.LogError(ex, "/predict-with-locations çalıştırma hatası"); } catch { }
        return Results.BadRequest(new { error = ex.Message });
    }
})
 .RequireRateLimiting("predict")
 .CacheOutput(p => p.NoCache());

// Ensemble tahmin endpoint'i
app.MapPost("/ensemble-predict", (PredictionRequest request) =>
{
    try
    {
        if (!ValidateRequest(request, out var validationError))
        {
            try { app.Logger.LogWarning("/ensemble-predict doğrulama hatası: {Error}. İstek: {@Req}", validationError, request); } catch { }
            return Results.BadRequest(new { error = validationError });
        }
        var input = new ProductPickingData
        {
            ItemCount = request.ItemCount,
            Weight = request.Weight,
            Volume = request.Volume,
            Distance = request.Distance,
            PickerExperience = request.PickerExperience,
            StockDensity = request.StockDensity,
            FragileRatio = request.FragileRatio ?? 0f,
            AisleCrowding = request.AisleCrowding ?? 0f,
            CartLoadKg = request.CartLoadKg ?? 0f,
            PickerFatigue = request.PickerFatigue ?? 0f,
            StopCount = request.StopCount ?? 0f,
            UrgencyLevel = request.UrgencyLevel ?? 0f,
            ZoneComplexity = request.ZoneComplexity ?? 0f,
            TemperatureHandling = request.TemperatureHandling ?? 0f
        };

        var prediction = advancedService.PredictEnsemble(input);
        
        // Tahmini logla
        monitoringService.LogPrediction(input, prediction);

        return Results.Ok(new PredictionResponse
        {
            PredictedTime = prediction.PredictedTime,
            Confidence = 0.92f,
            ModelVersion = "2.0",
            PredictionTime = DateTime.UtcNow,
            Algorithm = "Ensemble"
        });
    }
    catch (Exception ex)
    {
        try { app.Logger.LogError(ex, "/ensemble-predict çalıştırma hatası"); } catch { }
        return Results.BadRequest(new { error = ex.Message });
    }
})
 .RequireRateLimiting("predict")
 .CacheOutput(p => p.NoCache());

// ONNX tahmin endpoint'i
app.MapPost("/predict-onnx", (PredictionRequest request) =>
{
    try
    {
        if (!ValidateRequest(request, out var validationError))
        {
            try { app.Logger.LogWarning("/predict-onnx doğrulama hatası: {Error}. İstek: {@Req}", validationError, request); } catch { }
            return Results.BadRequest(new { error = validationError });
        }

        var onnx = app.Services.GetRequiredService<OnnxService>();
        var onnxPath = Path.Combine(AppContext.BaseDirectory, onnxModelPath);
        if (!File.Exists(onnxPath))
            return Results.BadRequest(new { error = $"ONNX model dosyasÄ± bulunamadÄ±: {onnxModelPath}" });

        // Model yÃ¼klÃ¼ deÄŸilse yÃ¼kle
        onnx.LoadModel(onnxPath);

        var input = new ProductPickingData
        {
            ItemCount = request.ItemCount,
            Weight = request.Weight,
            Volume = request.Volume,
            Distance = request.Distance,
            PickerExperience = request.PickerExperience,
            StockDensity = request.StockDensity,
            FragileRatio = request.FragileRatio ?? 0f,
            AisleCrowding = request.AisleCrowding ?? 0f,
            CartLoadKg = request.CartLoadKg ?? 0f,
            PickerFatigue = request.PickerFatigue ?? 0f,
            StopCount = request.StopCount ?? 0f,
            UrgencyLevel = request.UrgencyLevel ?? 0f,
            ZoneComplexity = request.ZoneComplexity ?? 0f,
            TemperatureHandling = request.TemperatureHandling ?? 0f
        };

        var normalize = bool.TryParse(builder.Configuration["Onnx:Normalize"], out var norm) ? norm : true;
        var baselinePath = builder.Configuration["Onnx:BaselineDataPath"] ?? trainingData;
        FeatureMeans? baseline = null;
        var driftService = app.Services.GetRequiredService<DataDriftService>();
        var baselineFullPath = Path.Combine(AppContext.BaseDirectory, baselinePath);
        if (normalize && File.Exists(baselineFullPath))
        {
            baseline = driftService.ComputeTrainingMeans(baselineFullPath);
        }
        var predicted = onnx.Predict(input, normalize, baseline);
        var prediction = new PickingTimePrediction { PredictedTime = predicted };
        monitoringService.LogPrediction(input, prediction);

        return Results.Ok(new PredictionResponse
        {
            PredictedTime = predicted,
            Confidence = 0.80f,
            ModelVersion = "onnx-1.0",
            PredictionTime = DateTime.UtcNow,
            Algorithm = "ONNX"
        });
    }
    catch (Exception ex)
    {
        try { app.Logger.LogError(ex, "/predict-onnx çalıştırma hatası"); } catch { }
        return Results.BadRequest(new { error = ex.Message });
    }
})
 .RequireRateLimiting("predict")
 .CacheOutput(p => p.NoCache());

// ONNX model bilgi endpoint'i
app.MapGet("/onnx-info", () =>
{
    try
    {
        var onnx = app.Services.GetRequiredService<OnnxService>();
        var onnxPath = Path.Combine(AppContext.BaseDirectory, onnxModelPath);
        if (!File.Exists(onnxPath))
            return Results.BadRequest(new { error = $"ONNX model dosyasÄ± bulunamadÄ±: {onnxModelPath}" });
        onnx.LoadModel(onnxPath);
        onnx.ShowModelInfo();
        return Results.Ok(new { message = "ONNX model bilgileri konsola yazdÄ±rÄ±ldÄ±." });
    }
    catch (Exception ex)
    {
        return Results.BadRequest(new { error = ex.Message });
    }
})
 .RequireRateLimiting("general")
 .CacheOutput(p => p.Expire(TimeSpan.FromSeconds(30)));

// Batch tahmin endpoint'i
app.MapPost("/predict-batch", (List<PredictionRequest> requests) =>
{
    try
    {
        var responses = new List<PredictionResponse>();

        foreach (var request in requests)
        {
            if (!ValidateRequest(request, out var validationError))
            {
                try { app.Logger.LogWarning("/predict-batch doğrulama hatası: {Error}. İstek: {@Req}", validationError, request); } catch { }
                return Results.BadRequest(new { error = $"Geçersiz istek: {validationError}" });
            }
            var input = new ProductPickingData
            {
                ItemCount = request.ItemCount,
                Weight = request.Weight,
                Volume = request.Volume,
                Distance = request.Distance,
                PickerExperience = request.PickerExperience,
                StockDensity = request.StockDensity,
                FragileRatio = request.FragileRatio ?? 0f,
                AisleCrowding = request.AisleCrowding ?? 0f,
                CartLoadKg = request.CartLoadKg ?? 0f,
                PickerFatigue = request.PickerFatigue ?? 0f,
                StopCount = request.StopCount ?? 0f,
                UrgencyLevel = request.UrgencyLevel ?? 0f,
                ZoneComplexity = request.ZoneComplexity ?? 0f,
                TemperatureHandling = request.TemperatureHandling ?? 0f
            };

            var prediction = mlService.Predict(input);
            monitoringService.LogPrediction(input, prediction);

            responses.Add(new PredictionResponse
            {
                PredictedTime = prediction.PredictedTime,
                Confidence = 0.85f,
                ModelVersion = "1.0",
                PredictionTime = DateTime.UtcNow,
                Algorithm = "FastTree"
            });
        }

        return Results.Ok(responses);
    }
    catch (Exception ex)
    {
        try { app.Logger.LogError(ex, "/predict-batch çalıştırma hatası"); } catch { }
        return Results.BadRequest(new { error = ex.Message });
    }
})
 .RequireRateLimiting("batch")
 .CacheOutput(p => p.NoCache());

// Model eÄŸitimi endpoint'i
app.MapPost("/train", () =>
{
    try
    {
        var metrics = mlService.TrainModelAsync(trainingData).Result;

        // Modeli kaydet ve versiyonla
        var modelPath = Path.Combine(AppContext.BaseDirectory, zipModel);
        mlService.SaveModel(modelPath);
        var versioning = app.Services.GetRequiredService<ModelVersioningService>();
        versioning.SaveVersion(metrics, modelPath);

        return Results.Ok(new { message = "Model trained and saved.", metrics, modelPath });
    }
    catch (Exception ex)
    {
        return Results.BadRequest(new { error = ex.Message });
    }
})
 .RequireRateLimiting("train")
 .CacheOutput(p => p.NoCache());

// Ã‡oklu model eÄŸitimi endpoint'i
app.MapPost("/train-multiple", async () =>
{
    try
    {
        var results = await advancedService.TrainMultipleModelsAsync(trainingData);
        return Results.Ok(results);
    }
    catch (Exception ex)
    {
        return Results.BadRequest(new { error = ex.Message });
    }
})
 .RequireRateLimiting("train")
 .CacheOutput(p => p.NoCache());

// Model karÅŸÄ±laÅŸtÄ±rmasÄ± endpoint'i
app.MapGet("/compare-models", () =>
{
    try
    {
        advancedService.CompareModels();
        return Results.Ok(new { message = "Model karÅŸÄ±laÅŸtÄ±rmasÄ± konsola yazdÄ±rÄ±ldÄ±." });
    }
    catch (Exception ex)
    {
        return Results.BadRequest(new { error = ex.Message });
    }
})
 .RequireRateLimiting("general")
 .CacheOutput(p => p.Expire(TimeSpan.FromSeconds(30)));

// Model izleme endpoint'i
app.MapGet("/monitor", () =>
{
    try
    {
        var analysis = monitoringService.AnalyzeModelPerformance();
        var distribution = monitoringService.AnalyzePredictionDistribution();
        
        return Results.Ok(new
        {
            Analysis = analysis,
            Distribution = distribution,
            Recommendation = advancedService.RecommendModel("balanced")
        });
    }
    catch (Exception ex)
    {
        return Results.BadRequest(new { error = ex.Message });
    }
})
 .RequireRateLimiting("general")
 .CacheOutput(p => p.Expire(TimeSpan.FromSeconds(30)));

// Model metrikleri endpoint'i
app.MapGet("/metrics", () =>
{
    return Results.Ok(new
    {
        ModelVersion = "2.0",
        LastTraining = DateTime.UtcNow.AddDays(-1),
        Status = "Active",
        Features = new[]
        {
            "Single Model Prediction",
            "Ensemble Prediction", 
            "Multi-Algorithm Training",
            "Model Monitoring",
            "AutoML Support",
            "ONNX Integration"
        }
    });
})
 .RequireRateLimiting("general")
 .CacheOutput(p => p.Expire(TimeSpan.FromSeconds(15)));

// Rota simülasyonu endpoint'i
app.MapPost("/simulate-route", (SimulationRequest request) =>
{
    try
    {
        if (!ValidateSimulation(request, out var vErr))
        {
            try { app.Logger.LogWarning("/simulate-route doğrulama hatası: {Error}", vErr); } catch { }
            return Results.BadRequest(new { error = vErr });
        }
        var svc = app.Services.GetRequiredService<RouteSimulationService>();
        var result = svc.Simulate(request);
        return Results.Ok(result);
    }
    catch (Exception ex)
    {
        try { app.Logger.LogError(ex, "/simulate-route çalıştırma hatası"); } catch { }
        return Results.BadRequest(new { error = ex.Message });
    }
})
 .RequireRateLimiting("simulate")
 .CacheOutput(p => p.NoCache());

// Dispatch optimizasyonu endpoint'i
app.MapPost("/optimize-dispatch", (DispatchRequest request) =>
{
    try
    {
        if (!ValidateDispatch(request, out var vErr))
        {
            try { app.Logger.LogWarning("/optimize-dispatch doğrulama hatası: {Error}", vErr); } catch { }
            return Results.BadRequest(new { error = vErr });
        }
        var svc = app.Services.GetRequiredService<DispatchOptimizationService>();
        var result = svc.Optimize(request);
        return Results.Ok(result);
    }
    catch (Exception ex)
    {
        try { app.Logger.LogError(ex, "/optimize-dispatch çalıştırma hatası"); } catch { }
        return Results.BadRequest(new { error = ex.Message });
    }
})
 .RequireRateLimiting("simulate")
 .CacheOutput(p => p.NoCache());

// GeliÅŸmiÅŸ metrikler endpoint'i
app.MapGet("/metrics-full", (int? thresholdPercent) =>
{
    try
    {
        var versioning = app.Services.GetRequiredService<ModelVersioningService>();
        var versions = versioning.LoadVersions();
        var analysis = monitoringService.AnalyzeModelPerformance();
        var distribution = monitoringService.AnalyzePredictionDistribution();

        var drift = app.Services.GetRequiredService<DataDriftService>();
        FeatureMeans baseline;
        try
        {
            baseline = drift.ComputeTrainingMeans(Path.Combine(AppContext.BaseDirectory, trainingData));
        }
        catch
        {
            baseline = new FeatureMeans();
        }
        var live = drift.ComputeMeans(monitoringService.GetRecentInputs(100));
        var threshold = thresholdPercent ?? (int.TryParse(builder.Configuration["Drift:ThresholdPercent"], out var t) ? t : 20);
        var status = drift.Evaluate(baseline, live, threshold);

        return Results.Ok(new
        {
            CurrentModelPath = zipModel,
            Versions = versions,
            Monitoring = new { Analysis = analysis, Distribution = distribution },
            Drift = status
        });
    }
    catch (Exception ex)
    {
        return Results.BadRequest(new { error = ex.Message });
    }
})
 .RequireRateLimiting("general")
 .CacheOutput(p => p.Expire(TimeSpan.FromSeconds(30)));

// Performans raporu endpoint'i
app.MapGet("/performance-report", () =>
{
    try
    {
        monitoringService.GeneratePerformanceReport();
        return Results.Ok(new { message = "Performans raporu konsola yazdÄ±rÄ±ldÄ±." });
    }
    catch (Exception ex)
    {
        return Results.BadRequest(new { error = ex.Message });
    }
})
 .RequireRateLimiting("general")
 .CacheOutput(p => p.Expire(TimeSpan.FromSeconds(30)));

// Tahmin geÃ§miÅŸini CSV olarak dÄ±ÅŸa aktar
app.MapGet("/export-logs", () =>
{
    try
    {
        var logsPath = Path.Combine(AppContext.BaseDirectory, dataDir, "prediction_logs.csv");
        monitoringService.ExportHistoryCsv(logsPath);
        return Results.Ok(new { message = "Tahmin geÃ§miÅŸi CSV olarak kaydedildi.", path = logsPath });
    }
    catch (Exception ex)
    {
        return Results.BadRequest(new { error = ex.Message });
    }
})
 .RequireRateLimiting("general")
 .CacheOutput(p => p.NoCache());

// Health endpoint'i
app.MapGet("/health", () =>
{
    var status = mlService.IsModelLoaded ? "Healthy" : "ModelNotLoaded";
    return Results.Ok(new
    {
        Status = status,
        Timestamp = DateTime.UtcNow
    });
})
 .RequireRateLimiting("general")
 .CacheOutput(p => p.Expire(TimeSpan.FromSeconds(5)));

app.MapGet("/healthz", () => Results.Ok(new { status = "healthy", timestamp = DateTime.UtcNow }))
   .RequireRateLimiting("general")
   .CacheOutput(p => p.Expire(TimeSpan.FromSeconds(5)));

// SÃ¼rÃ¼m bilgileri
app.MapGet("/versions", () =>
{
    var versioning = app.Services.GetRequiredService<ModelVersioningService>();
    var versions = versioning.LoadVersions();
    return Results.Ok(versions);
});

// Personel Görev Atama
app.MapPost("/assign-picking", async (HttpRequest req, IPersonnelRepository repo) =>
{
    try
    {
        var people = repo.GetPersonnel().ToList();
        var dispatcher = app.Services.GetRequiredService<PersonnelDispatchService>();

        List<PickingTask> tasks = new();
        List<PersonnelInfo>? overrides = null;
        AssignmentCriteria? criteria = null;
        if (req.HasFormContentType && req.Form.Files.Count > 0)
        {
            var file = req.Form.Files[0];
            using var stream = file.OpenReadStream();
            var name = file.FileName?.ToLowerInvariant() ?? string.Empty;
            if (name.EndsWith(".csv"))
                tasks = dispatcher.ParseCsv(stream);
            else
                tasks = dispatcher.ParseExcel(stream);
        }
        else
        {
            using var reader = new StreamReader(req.Body);
            var body = await reader.ReadToEndAsync();
            var opts = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            if (body.TrimStart().StartsWith("["))
            {
                tasks = JsonSerializer.Deserialize<List<PickingTask>>(body, opts) ?? new List<PickingTask>();
            }
            else
            {
                using var doc = JsonDocument.Parse(body);
                if (doc.RootElement.TryGetProperty("tasks", out var tEl) && tEl.ValueKind == JsonValueKind.Array)
                {
                    tasks = JsonSerializer.Deserialize<List<PickingTask>>(tEl.GetRawText(), opts) ?? new List<PickingTask>();
                }
                if (doc.RootElement.TryGetProperty("personnel", out var pEl) && pEl.ValueKind == JsonValueKind.Array)
                {
                    overrides = JsonSerializer.Deserialize<List<PersonnelInfo>>(pEl.GetRawText(), opts);
                }
                if (doc.RootElement.TryGetProperty("criteria", out var cEl) && cEl.ValueKind == JsonValueKind.Object)
                {
                    criteria = JsonSerializer.Deserialize<AssignmentCriteria>(cEl.GetRawText(), opts);
                }
            }
        }

        if (overrides != null && overrides.Count > 0) people = overrides;
        if (tasks.Count == 0)
        {
            try { app.Logger.LogWarning("/assign-picking: Görev listesi boş. HasForm={HasForm}", req.HasFormContentType); } catch { }
        }
        var result = dispatcher.AssignTasks(people, tasks, criteria);
        var overall = result.Results.Count > 0 ? result.Results.Max(x => x.TotalPredictedTimeSeconds) : 0;
        return Results.Ok(new { assignments = result.Results, estimatedCompletionSeconds = overall, personnelCount = people.Count, taskCount = tasks.Count });
    }
    catch (Exception ex)
    {
        try { app.Logger.LogError(ex, "/assign-picking çalıştırma hatası"); } catch { }
        return Results.BadRequest(new { error = ex.Message });
    }
})
 .RequireRateLimiting("general")
.CacheOutput(p => p.NoCache());

// Animasyonlu rota endpointi kaldÄ±rÄ±ldÄ±

// Animasyonlu Ã¶rnek endpointi kaldÄ±rÄ±ldÄ±

// GeliÅŸmiÅŸ depo animasyonu kaldÄ±rÄ±ldÄ±

// Personel veri uçları
// Birleşik personel listesi: son lokasyon ve çalışma durumu ile
app.MapGet("/personnel", (IPersonnelRepository repo) =>
{
    var people = repo.GetPersonnel();
    var perf = repo.GetWeeklyPerformanceFactor();
    var list = people.Select(p => new
    {
        id = p.Id,
        name = p.Name,
        lastLocationCode = p.LastLocationCode,
        zone = p.Zone,
        status = (perf.TryGetValue(p.Id, out var pf) && pf >= 1.0) ? "Devam Ediyor" : "Atama Bekliyor"
    }).ToList();
    // Mock örnek personel ekle: Ad ve Son Lokasyon önceden dolu, değiştirilmez
    list.Add(new {
        id = "p4",
        name = "Ali Çelik",
        lastLocationCode = "PX-MC-1516 15 18 MX-MZ-SA01",
        zone = (string?)"MC",
        status = "Atama Bekliyor"
    });
    return Results.Ok(list);
})
 .RequireRateLimiting("general")
 .CacheOutput(p => p.Expire(TimeSpan.FromSeconds(5)));

app.MapGet("/personnel/list", (IPersonnelRepository repo) =>
{
    return Results.Ok(repo.GetPersonnel());
})
 .RequireRateLimiting("general")
 .CacheOutput(p => p.Expire(TimeSpan.FromSeconds(5)));
app.MapGet("/personnel/locations", (IPersonnelRepository repo) =>
{
    var map = repo.GetLatestLocations();
    return Results.Ok(map);
})
 .RequireRateLimiting("general")
 .CacheOutput(p => p.Expire(TimeSpan.FromSeconds(5)));

app.MapGet("/personnel/performance", (IPersonnelRepository repo) =>
{
    var perf = repo.GetWeeklyPerformanceFactor();
    return Results.Ok(perf);
})
 .RequireRateLimiting("general")
 .CacheOutput(p => p.Expire(TimeSpan.FromSeconds(30)));

// Örnek Excel şablonu (dinamik oluşturulur)
app.MapGet("/samples/tasks.xlsx", () =>
{
    using var wb = new ClosedXML.Excel.XLWorkbook();
    var ws = wb.Worksheets.Add("Tasks");
    string[] headers = new[] {
        "Görev No","Barkod","Dağılım No","Zone","Koridor","Mega İş Listedi ID","Split Tamamlandı Mı?","Miktar","Yapılacak Miktar","Tamamlanan Miktar","PK İş İstasyonu","İlk Lokasyonu","Son Lokasyonu","İş İstasyonu","Rack","Görev Başlama Zamanı","Görev Bitiş Zamanı","İlk Paleti","Son Paleti","İlk Kolisi","Orjinal Sipariş No","Son Kolisi","İlk Paket Tipi","Son Paket Tipi"
    };
    for (int i = 0; i < headers.Length; i++) ws.Cell(1, i + 1).Value = headers[i];
    ws.Cell(2,1).Value="T-001"; ws.Cell(2,4).Value="MZ"; ws.Cell(2,5).Value="SA01"; ws.Cell(2,9).Value=6; ws.Cell(2,12).Value="PX-MZ-D08-171F"; ws.Cell(2,13).Value="PX-MZ-D08-172F";
    ws.Cell(3,1).Value="T-002"; ws.Cell(3,4).Value="MC"; ws.Cell(3,5).Value="1516"; ws.Cell(3,9).Value=8; ws.Cell(3,12).Value="PX-MC-A106"; ws.Cell(3,13).Value="PX-MC-A110";
    ws.Cell(4,1).Value="T-003"; ws.Cell(4,4).Value="PM02"; ws.Cell(4,5).Value="199"; ws.Cell(4,9).Value=7; ws.Cell(4,12).Value="PX-PM02-199"; ws.Cell(4,13).Value="PX-PM02-205";
    ws.Columns().AdjustToContents();
    using var ms = new MemoryStream(); wb.SaveAs(ms); ms.Position = 0;
    return Results.File(ms.ToArray(), "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "tasks-sample.xlsx");
})
 .RequireRateLimiting("general");

// Canlı PF hesaplama için telemetri post ve PF get
app.MapPost("/telemetry", (TelemetryService ts, TelemetryInput input) =>
{
    ts.Update(input.PersonId, input.ItemsPicked, input.HoursWorked, input.Errors, input.TravelDistanceMeters, input.Timestamp);
    return Results.Ok(new { ok = true });
})
 .RequireRateLimiting("general");

app.MapGet("/personnel/performance/live", (TelemetryService ts, IPersonnelRepository repo) =>
{
    var baseline = repo.GetWeeklyPerformanceFactor();
    var result = ts.GetLivePF(120, baseline); // hedef hız: 120 ürün/saat (örnek)
    return Results.Ok(result);
})
 .RequireRateLimiting("general")
 .CacheOutput(p => p.Expire(TimeSpan.FromSeconds(2)));

// Drift durumu (thresholdPercent opsiyonel query parametresi)
app.MapGet("/drift-status", (int? thresholdPercent) =>
{
    try
    {
        var drift = app.Services.GetRequiredService<DataDriftService>();
        var monitoring = app.Services.GetRequiredService<ModelMonitoringService>();
        var trainingPath = Path.Combine(AppContext.BaseDirectory, trainingData);
        FeatureMeans baseline;
        try
        {
            baseline = drift.ComputeTrainingMeans(trainingPath);
        }
        catch
        {
            baseline = new FeatureMeans();
        }
        var live = drift.ComputeMeans(monitoring.GetRecentInputs(100));
        var threshold = thresholdPercent ?? 20;
        var status = drift.Evaluate(baseline, live, threshold);
        return Results.Ok(status);
    }
    catch (Exception ex)
    {
        return Results.BadRequest(new { error = ex.Message });
    }
})
 .RequireRateLimiting("general")
 .CacheOutput(p => p.Expire(TimeSpan.FromSeconds(30)));

// Drift tespit edilirse modeli yeniden eÄŸit
app.MapPost("/retrain-if-drift", (int? thresholdPercent) =>
{
    try
    {
        var drift = app.Services.GetRequiredService<DataDriftService>();
        var monitoring = app.Services.GetRequiredService<ModelMonitoringService>();
        var trainingPath = Path.Combine(AppContext.BaseDirectory, trainingData);

        var baseline = drift.ComputeTrainingMeans(trainingPath);
        var live = drift.ComputeMeans(monitoring.GetRecentInputs(100));
        var threshold = thresholdPercent ?? 20;
        var status = drift.Evaluate(baseline, live, threshold);

        if (!status.Alarm)
        {
            return Results.Ok(new { message = status.Message, retrained = false, threshold });
        }

        var metrics = mlService.TrainModelAsync(trainingData).Result;
        var modelPath = Path.Combine(AppContext.BaseDirectory, zipModel);
        mlService.SaveModel(modelPath);
        var versioning = app.Services.GetRequiredService<ModelVersioningService>();
        versioning.SaveVersion(metrics, modelPath);

        return Results.Ok(new { message = "Drift tespit edildi, model yeniden eÄŸitildi.", metrics, retrained = true, threshold });
    }
    catch (Exception ex)
    {
        return Results.BadRequest(new { error = ex.Message });
    }
})
 .RequireRateLimiting("train")
 .CacheOutput(p => p.NoCache());

// Swagger'Ä± etkinleÅŸtir
// Swagger UI doÄŸrudan /swagger altÄ±nda servis edilir

app.Run();