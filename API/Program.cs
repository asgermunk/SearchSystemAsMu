using API.Search;
using ServiceDefaults;
using Shared;

var builder = WebApplication.CreateBuilder(args);

// metrics for Prometheus, traces and logs over OTLP, including the database timings
// and the cache hits and misses
builder.AddSearchTelemetry("search-api", InstrumentedDatabase.TelemetryName, CachedSearchLogic.TelemetryName);

// Add services to the container.
builder.Services.AddControllersWithViews();

/* The search service lives in this process. The database path comes from
 * configuration when it is set, otherwise from the shared constant. Results are
 * cached in Redis only when Search:RedisConnectionString is set.
 */
builder.Services.AddSearch(new SearchOptions
{
    SqliteDatabasePath = builder.Configuration["Search:SqliteDatabasePath"]
                         ?? Paths.SQLITE_DATABASE,
    RedisConnectionString = builder.Configuration["Search:RedisConnectionString"],
    RedisPassword = builder.Configuration["Search:RedisPassword"]
});

builder.Services.AddCors(options =>
{
    options.AddPolicy("frontend", policy => policy
        .AllowAnyOrigin()
        .AllowAnyHeader()
        .AllowAnyMethod());
});

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new Microsoft.OpenApi.OpenApiInfo
    {
        Title = "Search API",
        Version = "v1",
        Description = "HTTP front end for the SearchCore component."
    });

    // pick up the /// comments on the controller
    var xmlFile = $"{System.Reflection.Assembly.GetExecutingAssembly().GetName().Name}.xml";
    var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
    if (File.Exists(xmlPath))
        options.IncludeXmlComments(xmlPath);
});

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint("/swagger/v1/swagger.json", "Search API v1");
    });
}
else
{
    app.UseExceptionHandler("/Home/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseRouting();

app.UseCors("frontend");

app.UseAuthorization();

app.MapStaticAssets();

app.MapControllers();

// Prometheus scrapes /metrics
app.MapSearchTelemetry();

app.MapControllerRoute(
        name: "default",
        pattern: "{controller=Home}/{action=Index}/{id?}")
    .WithStaticAssets();


app.Run();
