using System.Net.Http.Headers;
using System.Reflection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.FileProviders;
using Serilog;
using Serilog.Templates;
using Serilog.Templates.Themes;
using VPMReposSynchronizer.Core.DbContexts;
using VPMReposSynchronizer.Core.Models.Mappers;
using VPMReposSynchronizer.Core.Services;
using VPMReposSynchronizer.Core.Services.FileHost;

const string logTemplate =
    "[{@t:yyyy-MM-dd HH:mm:ss} {@l:u3}] [{Substring(SourceContext, LastIndexOf(SourceContext, '.') + 1)}] {@m}\n{@x}";

var builder = WebApplication.CreateBuilder(args);

Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(builder.Configuration)
    .WriteTo.File(new ExpressionTemplate(logTemplate), "logs/app-.log", rollingInterval: RollingInterval.Day)
    .WriteTo.Console(new ExpressionTemplate(logTemplate, theme: TemplateTheme.Code))
    .WriteTo.Debug(new ExpressionTemplate(logTemplate))
    .CreateLogger();

builder.Host.UseSerilog();

// Add services to the container.
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddControllers();

builder.Services.AddOutputCache(options =>
{
    options.AddPolicy("vpm", policyBuilder => policyBuilder.Expire(TimeSpan.FromSeconds(30)));
});
builder.Services.AddDirectoryBrowser();

builder.Services.AddDatabaseDeveloperPageExceptionFilter();
builder.Services.AddDbContext<PackageDbContext>(options =>
{
    options.UseSqlite(builder.Configuration.GetConnectionString("DefaultConnection"));
});

builder.Services.AddAutoMapper(typeof(VpmPackageProfile));

builder.Services.AddHttpClient("default", client =>
{
    client.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("VPMReposSynchronizer",
        Assembly.GetExecutingAssembly().GetName().Version?.ToString()));
});

builder.Services.AddTransient<IFileHostService, LocalFileHostService>();

builder.Services.AddTransient<RepoMetaDataService>();
builder.Services.AddTransient<RepoSynchronizerService>();
builder.Services.AddHostedService<RepoSynchronizerHostService>();

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
    // DbInitializer.Initialize(context);
}

app.UseOutputCache();

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

app.UseHttpsRedirection();

app.MapControllers();

app.Run();