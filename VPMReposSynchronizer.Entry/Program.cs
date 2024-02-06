using System.Globalization;
using System.Net.Http.Headers;
using System.Reflection;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.FileProviders;
using Microsoft.OpenApi.Models;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using Serilog;
using Serilog.Templates;
using Serilog.Templates.Themes;
using VPMReposSynchronizer.Core.DbContexts;
using VPMReposSynchronizer.Core.Extensions;
using VPMReposSynchronizer.Core.Models.Mappers;
using VPMReposSynchronizer.Core.Options;
using VPMReposSynchronizer.Core.Services;
using VPMReposSynchronizer.Core.Services.FileHost;

var builder = WebApplication.CreateBuilder(args);

#region Logger

const string logTemplate =
    "[{@t:yyyy-MM-dd HH:mm:ss} {@l:u3}] [{Substring(SourceContext, LastIndexOf(SourceContext, '.') + 1)}] {@m}\n{@x}";

Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(builder.Configuration)
    .WriteTo.File(new ExpressionTemplate(logTemplate), "logs/app-.log", rollingInterval: RollingInterval.Day)
    .WriteTo.Console(new ExpressionTemplate(logTemplate, theme: TemplateTheme.Code))
    .WriteTo.Debug(new ExpressionTemplate(logTemplate))
    .CreateLogger();

builder.Host.UseSerilog();

builder.Logging.AddOpenTelemetry(options =>
{
    options
        .SetResourceBuilder(ResourceBuilder.CreateDefault().AddService("meter"));
});

#endregion

#region API Doc

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v0", new OpenApiInfo
    {
        Version = "v0",
        Title = "VPM Repos Synchronizer API",
        Description = "API for VPM Repos Synchronizer",
        Contact = new OpenApiContact
        {
            Name = "VRCD-Community",
            Url = new Uri("https://github.com/vrcd-community"),
            Email = "us@vrcd.org.cn"
        },
        License = new OpenApiLicense
        {
            Name = "AGPL3.0",
            Url = new Uri("https://github.com/vrcd-community/VPMReposSynchronizer/blob/main/LICENSE.md")
        },
    });

    var xmlFilename = $"{Assembly.GetExecutingAssembly().GetName().Name}.xml";
    options.IncludeXmlComments(Path.Combine(AppContext.BaseDirectory, xmlFilename));
});

#endregion

#region Configuration

builder.Configuration.AddEnvironmentVariables();

builder.Services.Configure<SynchronizerOptions>(builder.Configuration.GetSection("Synchronizer"));
builder.Services.Configure<MirrorRepoMetaDataOptions>(builder.Configuration.GetSection("MirrorRepoMetaData"));

builder.Services.Configure<FileHostServiceOptions>(builder.Configuration.GetSection("FileHost"));
builder.Services.Configure<LocalFileHostOptions>(builder.Configuration.GetSection("LocalFileHost"));
builder.Services.Configure<S3FileHostServiceOptions>(builder.Configuration.GetSection("S3FileHost"));

#endregion

#region DataBase & Mapper

builder.Services.AddDatabaseDeveloperPageExceptionFilter();
builder.Services.AddDbContext<DefaultDbContext>(options =>
{
    options.UseSqlite(builder.Configuration.GetConnectionString("DefaultConnection"));
});

builder.Services.AddAutoMapper(typeof(VpmPackageProfile));

#endregion

#region FileHostService

var fileHostServiceOptions = builder.Configuration.GetSection("FileHost").Get<FileHostServiceOptions>() ??
                             new FileHostServiceOptions();

switch (fileHostServiceOptions.FileHostServiceType)
{
    case FileHostServiceType.LocalFileHost:
        var filesPath = builder.Configuration.GetSection("LocalFileHost")["FilesPath"] ??
                        new LocalFileHostOptions().FilesPath;
        builder.Services.AddTransient<IFileHostService, LocalFileHostService>();
        builder.Services.AddDirectoryBrowser();

        if (!Directory.Exists(filesPath))
        {
            Directory.CreateDirectory(filesPath);
        }

        break;
    case FileHostServiceType.S3FileHost:
        builder.Services.AddTransient<IFileHostService, S3FileHostService>();
        break;
    default:
        throw new ArgumentException("FileHostServiceType is not supported or invalid");
}

#endregion

#region App Services

builder.Services.AddTransient<RepoMetaDataService>();
builder.Services.AddTransient<RepoSynchronizerService>();
builder.Services.AddTransient<RepoBrowserService>();
builder.Services.AddHostedService<RepoSynchronizerHostService>();
builder.Services.AddSingleton<RepoSynchronizerStatusService>();

#endregion

builder.Services.AddOpenTelemetry()
    .WithMetrics(metrics => metrics
        .ConfigureResource(resource => resource.AddService(nameof(RepoSynchronizerService)))
        .AddAspNetCoreInstrumentation()
        .AddHttpClientInstrumentation()
        .AddProcessInstrumentation()
        .AddRuntimeInstrumentation()
        .AddPrometheusExporter());

builder.Services.AddControllers();

builder.Services.AddOutputCache(options =>
{
    options.AddPolicy("vpm", policyBuilder => policyBuilder.Expire(TimeSpan.FromSeconds(10)));
});

builder.Services.AddRateLimiter(rateLimiterOptions =>
{
    rateLimiterOptions.OnRejected = (context, _) =>
    {
        if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
        {
            context.HttpContext.Response.Headers.RetryAfter =
                ((int)retryAfter.TotalSeconds).ToString(NumberFormatInfo.InvariantInfo);
        }

        context.HttpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;

        return new ValueTask();
    };

    rateLimiterOptions.AddPolicy("download", context =>
    {
        return RateLimitPartition.GetFixedWindowLimiter(context.GetIpAddress(), _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = fileHostServiceOptions.RateLimitPerWindow,
            Window =  TimeSpan.FromMilliseconds(fileHostServiceOptions.RateLimitWindow),
            QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
            QueueLimit = 0
        });
    });
});

builder.Services.AddHttpClient("default", client =>
{
    client.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("VPMReposSynchronizer",
        Assembly.GetExecutingAssembly().GetName().Version?.ToString()));
});

builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
        policy.AllowAnyOrigin());
});

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint("/swagger/v0/swagger.json", "VPMReposSynchronizer API v0");
        options.DisplayRequestDuration();
    });
    app.UseDeveloperExceptionPage();
    app.UseMigrationsEndPoint();
}

using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;

    var dbContext = services.GetRequiredService<DefaultDbContext>();

    dbContext.Database.EnsureCreated();
}

app.UseOutputCache();

if (fileHostServiceOptions.FileHostServiceType == FileHostServiceType.LocalFileHost)
{
    var filesPath = builder.Configuration.GetSection("LocalFileHost")["FilesPath"] ??
                    new LocalFileHostOptions().FilesPath;
    builder.Services.AddDirectoryBrowser();

    if (!Directory.Exists(filesPath))
    {
        Directory.CreateDirectory(filesPath);
    }

    app.UseFileServer(new FileServerOptions
    {
        FileProvider = new PhysicalFileProvider(Path.Combine(builder.Environment.ContentRootPath, filesPath)),
        RequestPath = "/files",
        EnableDirectoryBrowsing = true,
        StaticFileOptions =
        {
            ServeUnknownFileTypes = true,
        }
    });
}

app.UseOpenTelemetryPrometheusScrapingEndpoint();

app.UseHttpsRedirection();

app.MapControllers();

app.UseCors();

if (fileHostServiceOptions.EnableRateLimit)
    app.UseRateLimiter();

app.Run();