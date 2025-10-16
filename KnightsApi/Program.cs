using System.Text.Json;
using KnightsApi.Data;
using KnightsApi.Logging;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Npgsql;

var builder = WebApplication.CreateBuilder(args);

// ---------- DB (PostgreSQL) ----------
// Prefer environment DATABASE_URL (Render). Fallback to DefaultConnection in config.
var rawConn = Environment.GetEnvironmentVariable("DATABASE_URL")
              ?? builder.Configuration.GetConnectionString("DefaultConnection");

if (string.IsNullOrWhiteSpace(rawConn))
    throw new InvalidOperationException("Connection string not configured. Set DATABASE_URL or DefaultConnection.");

// If DATABASE_URL is in the form postgres:// or postgresql:// -> convert to Npgsql connection string
string BuildNpgsqlConnection(string input)
{
    if (input.StartsWith("postgres://", StringComparison.OrdinalIgnoreCase) ||
        input.StartsWith("postgresql://", StringComparison.OrdinalIgnoreCase))
    {
        var uri = new Uri(input);
        var userInfo = uri.UserInfo.Split(':', StringSplitOptions.RemoveEmptyEntries);
        var username = userInfo.Length > 0 ? userInfo[0] : "";
        var password = userInfo.Length > 1 ? userInfo[1] : "";
        var host = uri.Host;
        var port = uri.Port != -1 ? uri.Port : 5432;
        var database = uri.AbsolutePath.TrimStart('/');

        var csb = new NpgsqlConnectionStringBuilder
        {
            Host = host,
            Port = port,
            Database = database,
            Username = username,
            Password = password,
            SslMode = SslMode.Require,
            TrustServerCertificate = true
        };

        return csb.ToString();
    }

    // otherwise assume it's already a Npgsql-style connection string; ensure SSL
    string EnsureSslLegacy(string cs)
    {
        if (cs.IndexOf("sslmode", StringComparison.OrdinalIgnoreCase) >= 0 ||
            cs.IndexOf("Ssl Mode", StringComparison.OrdinalIgnoreCase) >= 0)
            return cs;

        // if it looks like a URL, don't touch; otherwise append parameter style
        return cs.Contains("://")
            ? (cs.Contains("?") ? cs + "&sslmode=require" : cs + "?sslmode=require")
            : cs + ";Ssl Mode=Require;Trust Server Certificate=true";
    }

    return EnsureSslLegacy(input);
}

var npgsqlConn = BuildNpgsqlConnection(rawConn);

builder.Services.AddDbContext<AppDbContext>(opts =>
    opts.UseNpgsql(npgsqlConn));

// ---------- Controllers + JSON (camelCase) ----------
builder.Services
    .AddControllers()
    .AddJsonOptions(o =>
    {
        o.JsonSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
        o.JsonSerializerOptions.DictionaryKeyPolicy = JsonNamingPolicy.CamelCase;
        o.JsonSerializerOptions.DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull;
    });

// ---------- CORS ----------
builder.Services.AddCors(opts =>
{
    opts.AddPolicy("any", p =>
        p.AllowAnyOrigin()
         .AllowAnyHeader()
         .AllowAnyMethod());
});

// ---------- Swagger ----------
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// ---------- JWT ----------
string? jwtKey = builder.Configuration["Jwt:Key"];
if (string.IsNullOrWhiteSpace(jwtKey))
    throw new InvalidOperationException("JWT Key not configured.");

var signingKey = new SymmetricSecurityKey(System.Text.Encoding.UTF8.GetBytes(jwtKey));

builder.Services
    .AddAuthentication(o =>
    {
        o.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
        o.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
    })
    .AddJwtBearer(o =>
    {
        o.RequireHttpsMetadata = false;
        o.SaveToken = true;
        o.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = false,
            ValidateAudience = false,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = signingKey
        };
    });

builder.Services.AddAuthorization();

// no global logging providers (we use explicit InMemoryLogStore events)

var app = builder.Build();

app.UseCors("any");

// ---------- Static files (wwwroot) ----------
app.UseStaticFiles();

// ---------- Swagger UI ----------
app.UseSwagger();
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "KnightsApi API v1");
    c.RoutePrefix = "swagger";
});

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

// ---------- Auto-migrate + Seed (explicit minimal events) ----------
InMemoryLogStore.Add(new LogEvent { Level = Microsoft.Extensions.Logging.LogLevel.Information, Message = "Server starting..." });

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.Migrate();
    InMemoryLogStore.Add(new LogEvent { Level = Microsoft.Extensions.Logging.LogLevel.Information, Message = "Database migrated." });

    SeedData.Initialize(db);
    InMemoryLogStore.Add(new LogEvent { Level = Microsoft.Extensions.Logging.LogLevel.Information, Message = "Seed data ensured." });
}

InMemoryLogStore.Add(new LogEvent { Level = Microsoft.Extensions.Logging.LogLevel.Information, Message = "Server started." });

// ===================== ADMIN ROUTES =====================

// /admin — minimal static HTML
app.MapGet("/admin", async context =>
{
    var webRoot = app.Environment.WebRootPath ?? Path.Combine(AppContext.BaseDirectory, "wwwroot");
    var adminIndex = Path.Combine(webRoot, "admin", "index.html");

    if (!System.IO.File.Exists(adminIndex))
    {
        context.Response.StatusCode = 404;
        await context.Response.WriteAsync("Admin UI not found. Place file at /wwwroot/admin/index.html");
        return;
    }

    context.Response.ContentType = "text/html; charset=utf-8";
    await context.Response.SendFileAsync(adminIndex);
});

// /admin/logs — logs page (HTML)
app.MapGet("/admin/logs", async context =>
{
    var webRoot = app.Environment.WebRootPath ?? Path.Combine(AppContext.BaseDirectory, "wwwroot");
    var logsHtml = Path.Combine(webRoot, "admin", "logs.html");

    if (!System.IO.File.Exists(logsHtml))
    {
        context.Response.StatusCode = 404;
        await context.Response.WriteAsync("Logs UI not found. Place file at /wwwroot/admin/logs.html");
        return;
    }

    context.Response.ContentType = "text/html; charset=utf-8";
    await context.Response.SendFileAsync(logsHtml);
});

// common JSON options
var camel = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

// /admin/logs/stream — SSE that sends ONLY new events since lastSeq
app.MapGet("/admin/logs/stream", async context =>
{
    context.Response.Headers.Add("Content-Type", "text/event-stream");
    context.Response.Headers.Add("Cache-Control", "no-cache");
    context.Response.Headers.Add("X-Accel-Buffering", "no");

    var abort = context.RequestAborted;

    // 1) send an initial snapshot once
    long lastSeq = 0;
    var initial = InMemoryLogStore.GetRecent(200);
    if (initial.Count > 0)
    {
        lastSeq = initial[^1].Seq;
        var payload = JsonSerializer.Serialize(initial, camel);
        await context.Response.WriteAsync($"data: {payload}\n\n", abort);
        await context.Response.Body.FlushAsync(abort);
    }

    // 2) then stream only deltas
    try
    {
        while (!abort.IsCancellationRequested)
        {
            await Task.Delay(1000, abort);

            var delta = InMemoryLogStore.GetSince(lastSeq, 200);
            if (delta.Count > 0)
            {
                lastSeq = delta[^1].Seq;
                var payload = JsonSerializer.Serialize(delta, camel);
                await context.Response.WriteAsync($"data: {payload}\n\n", abort);
                await context.Response.Body.FlushAsync(abort);
            }
        }
    }
    catch (OperationCanceledException) { }
});

// /admin/logs/snapshot — one-time JSON (last N)
app.MapGet("/admin/logs/snapshot", () =>
{
    var recent = InMemoryLogStore.GetRecent(200);
    return Results.Json(recent, camel);
});

// /admin/logs/clear — clear buffer (and record the action as a new event)
app.MapPost("/admin/logs/clear", () =>
{
    InMemoryLogStore.Clear();
    InMemoryLogStore.Add(new LogEvent { Level = Microsoft.Extensions.Logging.LogLevel.Information, Message = "Log buffer cleared." });
    return Results.Ok(new { ok = true, cleared = true });
});

app.Run();
