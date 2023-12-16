using System.Net.Http.Headers;
using System.Reflection;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.FileProviders;
using Microsoft.OpenApi.Models;
using Serilog;
using Serilog.Templates;
using Serilog.Templates.Themes;
using VPMReposSynchronizer.Core.DbContexts;
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
#endregion

#region API Doc
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Version = "v1",
        Title = "VPM Repos Synchronizer API",
        Description = "API for VPM Repos Synchronizer",
        Contact = new OpenApiContact
        {
            Name = "VRCD-Community",
            Url = new Uri("https://github.com/vrcd-community")
        },
        License = new OpenApiLicense
        {
            Name = "GPL3.0",
            Url = new Uri("https://github.com/vrcd-community/VPMReposSynchronizer/blob/main/LICENSE")
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
#endregion

#region DataBase & Mapper
builder.Services.AddDatabaseDeveloperPageExceptionFilter();
builder.Services.AddDbContext<PackageDbContext>(options =>
{
    options.UseSqlite(builder.Configuration.GetConnectionString("DefaultConnection"));
});

builder.Services.AddAutoMapper(typeof(VpmPackageProfile));
#endregion

#region FileHostService

if (!Enum.TryParse(builder.Configuration.GetSection("FileHost")["FileHostServiceType"],
        out FileHostServiceType fileHostServiceType))
{
    fileHostServiceType = FileHostServiceType.LocalFileHost;
}

switch (fileHostServiceType)
{
    case FileHostServiceType.LocalFileHost:
        var filesPath = builder.Configuration.GetSection("LocalFileHost")["FilesPath"] ?? new LocalFileHostOptions().FilesPath;
        builder.Services.AddTransient<LocalFileHostService>();
        builder.Services.AddDirectoryBrowser();

        if (!Directory.Exists(filesPath))
        {
            Directory.CreateDirectory(filesPath);
        }
        break;
    case FileHostServiceType.S3FileHost:
        break;
    default:
        throw new Exception("FileHostServiceType is not supported or invalid");
}
builder.Services.AddTransient<IFileHostService, LocalFileHostService>();
#endregion

#region App Services
builder.Services.AddTransient<RepoMetaDataService>();
builder.Services.AddTransient<RepoSynchronizerService>();
builder.Services.AddHostedService<RepoSynchronizerHostService>();
#endregion

builder.Services.AddControllers();

builder.Services.AddOutputCache(options =>
{
    options.AddPolicy("vpm", policyBuilder => policyBuilder.Expire(TimeSpan.FromSeconds(30)));
});

builder.Services.AddHttpClient("default", client =>
{
    client.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("VPMReposSynchronizer",
        Assembly.GetExecutingAssembly().GetName().Version?.ToString()));
});

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
    app.UseDeveloperExceptionPage();
    app.UseMigrationsEndPoint();
}

using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;

    var context = services.GetRequiredService<PackageDbContext>();
    context.Database.EnsureCreated();
}

app.UseOutputCache();

if (fileHostServiceType == FileHostServiceType.LocalFileHost)
{
    app.UseFileServer(new FileServerOptions
    {
        FileProvider = new PhysicalFileProvider(Path.Combine(builder.Environment.ContentRootPath, "files")),
        RequestPath = "/files",
        EnableDirectoryBrowsing = true,
        StaticFileOptions =
        {
            ServeUnknownFileTypes = true,
        }
    });
}

app.UseHttpsRedirection();

app.MapControllers();

app.Run();