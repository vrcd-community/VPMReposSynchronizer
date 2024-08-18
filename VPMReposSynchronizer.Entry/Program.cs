using System.Globalization;
using System.Net.Http.Headers;
using System.Reflection;
using System.Threading.RateLimiting;
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
using VPMReposSynchronizer.Core.Services.RepoSync;
using VPMReposSynchronizer.Entry;
using VPMReposSynchronizer.Entry.AuthenticationHandlers;

var builder = WebApplication.CreateBuilder(args);

#region Builder

#region Logger

const string logTemplate =
    "[{@t:yyyy-MM-dd HH:mm:ss} " +
    "{@l:u3}]" +
    "{#if SourceContext is not null} [{SourceContext}]{#end}" +
    "{#if @p.Scope is not null} [{#each s in Scope}{s}{#delimit} {#end}]{#end}" +
    " {@m}" +
    "\n{@x}";

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
        }
    });

    options.AddSecurityDefinition("ApiKey", new OpenApiSecurityScheme
    {
        In = ParameterLocation.Header,
        Name = "X-Api-Key",
        Type = SecuritySchemeType.ApiKey
    });

    options.OperationFilter<SecurityRequirementsOperationFilter>();

    var xmlFilename = $"{Assembly.GetExecutingAssembly().GetName().Name}.xml";
    options.IncludeXmlComments(Path.Combine(AppContext.BaseDirectory, xmlFilename));
});

#endregion

#region Configuration

builder.Configuration.AddEnvironmentVariables();

builder.Services.Configure<MirrorRepoMetaDataOptions>(builder.Configuration.GetSection("MirrorRepoMetaData"));
builder.Services.Configure<AuthOptions>(builder.Configuration.GetSection("Auth"));

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

        if (!Directory.Exists(filesPath)) Directory.CreateDirectory(filesPath);

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
builder.Services.AddTransient<RepoSyncTaskService>();
builder.Services.AddTransient<RepoSyncStatusService>();

builder.Services.AddHostedService<RepoSynchronizerHostService>();
builder.Services.AddSingleton<RepoSyncTaskScheduleService>();

builder.Services.AddHostedService<FluentSchedulerHostService>();
builder.Services.AddSingleton<FluentSchedulerService>();

#endregion

#region OpenTelemetry

builder.Services.AddOpenTelemetry()
    .WithMetrics(metrics => metrics
        .ConfigureResource(resource => resource.AddService(nameof(RepoSynchronizerService)))
        .AddAspNetCoreInstrumentation()
        .AddHttpClientInstrumentation()
        .AddProcessInstrumentation()
        .AddRuntimeInstrumentation()
        .AddPrometheusExporter());

#endregion

#region RateLimiter

builder.Services.AddRateLimiter(rateLimiterOptions =>
{
    rateLimiterOptions.OnRejected = (context, _) =>
    {
        if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
            context.HttpContext.Response.Headers.RetryAfter =
                ((int)retryAfter.TotalSeconds).ToString(NumberFormatInfo.InvariantInfo);

        context.HttpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;

        return new ValueTask();
    };

    rateLimiterOptions.AddPolicy("download", context =>
    {
        return RateLimitPartition.GetFixedWindowLimiter(context.GetIpAddress(), _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = fileHostServiceOptions.RateLimitPerWindow,
            Window = TimeSpan.FromMilliseconds(fileHostServiceOptions.RateLimitWindow),
            QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
            QueueLimit = 0
        });
    });
});

#endregion

#region Authentication

var authOptions = builder.Configuration.GetSection("Auth").Get<AuthOptions>();

if (authOptions is null) throw new InvalidOperationException("AuthOptions is not configured correctly");

builder.Services.AddAuthentication()
    .AddScheme<ApiKeyAuthenticationOptions, ApiKeyAuthenticationHandler>("ApiKey",
        options => { options.ApiKey = authOptions.ApiKey; });

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("ApiKey", policy => { policy.RequireClaim("ApiKey", authOptions.ApiKey); });
});

#endregion

#region Others

builder.Services.AddControllers();

builder.Services.AddOutputCache(options =>
{
    options.AddPolicy("vpm", policyBuilder => policyBuilder.Expire(TimeSpan.FromSeconds(10)));
});

builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
        policy.AllowAnyOrigin());
});

builder.Services.AddProblemDetails();

#endregion

#region HttpClient

builder.Services.AddHttpClient("default", client =>
{
    client.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("VPMReposSynchronizer",
        Assembly.GetExecutingAssembly().GetName().Version?.ToString()));
});

builder.Services.AddHttpClient<RepoSynchronizerService>(client =>
{
    client.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("VPMReposSynchronizer",
        Assembly.GetExecutingAssembly().GetName().Version?.ToString()));
});

#endregion

#endregion

#region App

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
    app.UseMigrationsEndPoint();
}

app.UseSwagger();
app.UseSwaggerUI(options =>
{
    options.SwaggerEndpoint("/swagger/v0/swagger.json", "VPMReposSynchronizer API v0");
    options.DisplayRequestDuration();
});

using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;

    var dbContext = services.GetRequiredService<DefaultDbContext>();

    await dbContext.Database.EnsureCreatedAsync();
}

app.UseOutputCache();

if (fileHostServiceOptions.FileHostServiceType == FileHostServiceType.LocalFileHost)
{
    var filesPath = builder.Configuration.GetSection("LocalFileHost")["FilesPath"] ??
                    new LocalFileHostOptions().FilesPath;
    builder.Services.AddDirectoryBrowser();

    if (!Directory.Exists(filesPath)) Directory.CreateDirectory(filesPath);

    app.UseFileServer(new FileServerOptions
    {
        FileProvider = new PhysicalFileProvider(Path.Combine(builder.Environment.ContentRootPath, filesPath)),
        RequestPath = "/files",
        EnableDirectoryBrowsing = true,
        StaticFileOptions =
        {
            ServeUnknownFileTypes = true
        }
    });
}

app.UseOpenTelemetryPrometheusScrapingEndpoint();

app.UseHttpsRedirection();

app.MapControllers();

app.UseAuthentication();
app.UseAuthorization();

app.UseCors();

if (fileHostServiceOptions.EnableRateLimit) app.UseRateLimiter();

app.MapGet("/api-docs", () => Results.Content(
    """
    <!doctype html>
    <html>
    <head>
        <title>Scalar API Reference</title>
        <meta charset="utf-8" />
        <meta name="viewport" content="width=device-width, initial-scale=1" />
    </head>
    <body>
        <script id="api-reference" data-url="/swagger/v0/swagger.json"></script>
        <script>
        var configuration = {}
    
        document.getElementById('api-reference').dataset.configuration =
            JSON.stringify(configuration)
        </script>
        <script src="https://cdn.jsdelivr.net/npm/@scalar/api-reference"></script>
    </body>
    </html>
    """,
    "text/html"
));

await app.RunAsync();

#endregion
