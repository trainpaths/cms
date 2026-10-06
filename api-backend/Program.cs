using System.Reflection;
using System.Text;
using System.Threading.RateLimiting;
using api_backend;
using api_backend.Models.Auth.JWT;
using api_backend.Services.Auth;
using api_backend.Services.Auth.JWT;
using api_backend.Services.Backup;
using api_backend.Services.Cms;
using api_backend.Services.Email;
using api_backend.Services.Media;
using api_backend.Services.Menus;
using api_backend.Services.Pages;
using api_backend.Services.Rendering;
using api_backend.Services.Site;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi;

if (args.Contains("--healthcheck"))
{
	using var client = new HttpClient { BaseAddress = new Uri("http://localhost:8080") };
	try
	{
		var response = await client.GetAsync("/health");
		return response.IsSuccessStatusCode ? 0 : 1;
	}
	catch
	{
		return 1;
	}
}

var builder = WebApplication.CreateBuilder(args);

// Build-time OpenAPI gen (`dotnet getdocument`) boots the app just to list endpoints:
// no DB, no secrets, so startup guards below are relaxed.
var generatingOpenApi = Assembly.GetEntryAssembly()?.GetName().Name == "GetDocument.Insider";

// Small JSON bodies only. Page writes raise it via [RequestSizeLimit] (PagesController).
builder.WebHost.ConfigureKestrel(o => o.Limits.MaxRequestBodySize = 64 * 1024);

// Configuration
var connectionString = builder.Configuration.GetConnectionString("Postgres")
	?? (generatingOpenApi
		? "Host=localhost;Database=openapi;Username=openapi;Password=openapi"
		: throw new InvalidOperationException("ConnectionStrings:Postgres is not configured."));

builder.Services.AddDbContext<AppDbContext>(opt => opt.UseNpgsql(connectionString));

// ProblemDetails + global exception handler
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();

// Health checks
builder.Services.AddHealthChecks()
	.AddNpgSql(sp => sp.GetRequiredService<IConfiguration>().GetConnectionString("Postgres")!);

// Trust X-Forwarded-* only from proxies in ForwardedHeaders:KnownNetworks (comma-separated CIDRs).
builder.Services.AddOptions<ForwardedHeadersOptions>()
	.Configure<IConfiguration>((options, config) =>
	{
		options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
		var networks = config["ForwardedHeaders:KnownNetworks"] ?? string.Empty;
		foreach (var network in networks.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
			options.KnownIPNetworks.Add(System.Net.IPNetwork.Parse(network));
	});

// JWT
builder.Services.Configure<JwtOptions>(builder.Configuration.GetSection(JwtOptions.SectionName));
var jwt = builder.Configuration.GetSection(JwtOptions.SectionName).Get<JwtOptions>() ?? new JwtOptions();

const string devFallbackKey = "dev-only-insecure-signing-key-change-me!!";
// Publicly known keys, refused outside Development.
string[] knownPublicKeys = [devFallbackKey, "your-secret-jwt-key-must-be-32-chars"];

if (string.IsNullOrWhiteSpace(jwt.SigningKey) || jwt.SigningKey.Length < 32)
{
	if (!builder.Environment.IsDevelopment() && !generatingOpenApi)
		throw new InvalidOperationException(
			"Jwt:SigningKey must be set (>=32 chars) outside Development. Generate one with `openssl rand -base64 48`.");
	jwt.SigningKey = devFallbackKey;
}
else if (knownPublicKeys.Contains(jwt.SigningKey) && !builder.Environment.IsDevelopment() && !generatingOpenApi)
{
	throw new InvalidOperationException(
		"Jwt:SigningKey is a publicly known placeholder. Generate one with `openssl rand -base64 48`.");
}

// JwtTokenService reads key via DI, needs fallback too
if (builder.Environment.IsDevelopment() || generatingOpenApi)
{
	builder.Services.PostConfigure<JwtOptions>(opt =>
	{
		if (string.IsNullOrWhiteSpace(opt.SigningKey) || opt.SigningKey.Length < 32)
			opt.SigningKey = devFallbackKey;
	});
}

builder.Services
	.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
	.AddJwtBearer(options =>
	{
		options.MapInboundClaims = false;
		options.TokenValidationParameters = new TokenValidationParameters
		{
			ValidateIssuer = true,
			ValidateAudience = true,
			ValidateLifetime = true,
			ValidateIssuerSigningKey = true,
			ValidIssuer = jwt.Issuer,
			ValidAudience = jwt.Audience,
			IssuerSigningKey = new SymmetricSecurityKey(
				Encoding.UTF8.GetBytes(jwt.SigningKey)),
			ClockSkew = TimeSpan.FromSeconds(30),
			NameClaimType = "sub",
			RoleClaimType = "role",
		};
	});

builder.Services.AddAuthorization(options =>
{
	options.AddPolicy(AuthPolicies.CustomerOnly, p =>
		p.RequireClaim(AuthClaims.UserType, AuthClaims.CustomerUserType));
	options.AddPolicy(AuthPolicies.StaffOnly, p =>
		p.RequireClaim(AuthClaims.UserType, AuthClaims.StaffUserType));
	options.AddPolicy(AuthPolicies.SuperAdmin, p =>
		p.RequireClaim(AuthClaims.UserType, AuthClaims.StaffUserType)
		 .RequireRole(StaffBootstrapper.SuperAdminRole));
});

// services
builder.Services.AddSingleton<PasswordHashService>();
builder.Services.AddSingleton<JwtTokenService>();
builder.Services.AddScoped<RefreshTokenService>();
builder.Services.AddScoped<CustomerAuthService>();
builder.Services.AddScoped<StaffAuthService>();
builder.Services.AddHostedService<TokenCleanupService>();

// Email + account flows
builder.Services.Configure<EmailOptions>(builder.Configuration.GetSection(EmailOptions.SectionName));
builder.Services.AddScoped<AccountService>();

// Instance config (the client site's cms.config.json; defaults without it). Loaded lazily, so test overrides apply;
// resolved right after Build so an invalid file stops startup.
builder.Services.AddSingleton(sp => CmsConfigLoader.Load(
	sp.GetRequiredService<IConfiguration>(), sp.GetRequiredService<IHostEnvironment>(),
	sp.GetRequiredService<ILoggerFactory>().CreateLogger(typeof(CmsConfig))));

// CMS pages (block editor)
builder.Services.AddScoped<PageService>();

// Media library: bytes in S3-compatible storage (SeaweedFS in the compose stack)
// Validated at host start (not eagerly) so test config overrides apply.
var s3Options = builder.Services.AddOptions<S3StorageOptions>()
	.Bind(builder.Configuration.GetSection(S3StorageOptions.SectionName));
if (!generatingOpenApi)
	s3Options
		.Validate(o => !string.IsNullOrWhiteSpace(o.ServiceUrl)
					   && !string.IsNullOrWhiteSpace(o.AccessKey) && !string.IsNullOrWhiteSpace(o.SecretKey),
			"Storage:S3:ServiceUrl, AccessKey and SecretKey must be set (S3_* in .env).")
		.ValidateOnStart();
builder.Services.AddSingleton<IBlobStorage, S3BlobStorage>();
builder.Services.AddScoped<MediaService>();

// Site config (owner-defined fields, logo, icon)
builder.Services.AddScoped<SiteConfigService>();

// Menus (public navigation)
builder.Services.AddScoped<MenuService>();

// Public pages pre-rendered to HTML by the frontend `renderer` service (Services/Rendering)
builder.Services.Configure<RendererOptions>(builder.Configuration.GetSection(RendererOptions.SectionName));
builder.Services.AddSingleton<RenderQueue>();
builder.Services.AddSingleton<IRenderQueue>(sp => sp.GetRequiredService<RenderQueue>());
builder.Services.AddSingleton<RenderStatus>();
builder.Services.AddHttpClient<IRendererClient, HttpRendererClient>((sp, http) =>
{
	var baseUrl = sp.GetRequiredService<IOptions<RendererOptions>>().Value.BaseUrl;
	if (!string.IsNullOrWhiteSpace(baseUrl))
		http.BaseAddress = new Uri(baseUrl.TrimEnd('/') + "/");
	http.Timeout = TimeSpan.FromSeconds(30);
});
builder.Services.AddHostedService<RenderWorker>();
// Full backups (database + media archives in Backup:Directory) + their schedule
builder.Services.Configure<BackupOptions>(builder.Configuration.GetSection(BackupOptions.SectionName));
builder.Services.AddSingleton<BackupLock>();
builder.Services.AddSingleton<IDatabaseDumper, PgDumper>();
builder.Services.AddScoped<BackupService>();
builder.Services.AddHostedService<BackupScheduler>();

builder.Services.AddSingleton<EmailQueue>();
builder.Services.AddSingleton<IEmailQueue>(sp => sp.GetRequiredService<EmailQueue>());
builder.Services.AddHostedService<EmailDispatcher>();

var emailHost = builder.Configuration[$"{EmailOptions.SectionName}:Host"];
if (string.IsNullOrWhiteSpace(emailHost))
	builder.Services.AddSingleton<IEmailSender, ConsoleEmailSender>();
else
	builder.Services.AddSingleton<IEmailSender, MailKitEmailSender>();

builder.Services.AddControllers();

// CORS for the Vue dev server. Comma-separated so a single CORS_ORIGINS env var works in docker.
var corsOrigins = (builder.Configuration["Cors:Origins"] ?? "http://localhost:5173")
	.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
builder.Services.AddCors(o => o.AddPolicy("frontend", p => p
	.WithOrigins(corsOrigins)
	.AllowAnyHeader()
	.AllowAnyMethod()
	.AllowCredentials()));

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
	// C# nullability → exact contract: non-nullable = required, `?` = nullable (generated TS needs no mapping)
	c.SupportNonNullableReferenceTypes();
	c.NonNullableReferenceTypesAsRequired();
	c.UseAllOfToExtendReferenceSchemas();
	c.SwaggerDoc("v1", new OpenApiInfo { Title = "Website Template API", Version = "v1" });

	c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
	{
		Type = SecuritySchemeType.Http,
		Scheme = "bearer",
		BearerFormat = "JWT",
		In = ParameterLocation.Header,
		Description = "Enter the JWT access token",
	});
	c.AddSecurityRequirement(doc => new OpenApiSecurityRequirement
	{
		{
			new OpenApiSecuritySchemeReference("Bearer", doc),
			new List<string>()
		},
	});
});

// Rate limiting. Config read at limiter build time, so tests can override it.
builder.Services.AddRateLimiter(options =>
{
	options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
	options.OnRejected = async (context, cancellationToken) =>
	{
		if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
		{
			context.HttpContext.Response.Headers.RetryAfter = ((int)retryAfter.TotalSeconds).ToString();
		}
		context.HttpContext.Response.ContentType = "application/problem+json";
		await context.HttpContext.Response.WriteAsJsonAsync(new
		{
			status = 429,
			title = "Too many requests",
			detail = "Rate limit exceeded. Try again later.",
		}, cancellationToken);
	};
});

builder.Services.AddOptions<RateLimiterOptions>()
	.Configure<IConfiguration>((options, config) =>
	{
		AddPerIpFixedWindow(options, RateLimitPolicies.Credentials, config.GetSection("RateLimiting"),
			defaultPermits: 10, defaultWindowSeconds: 60);
		AddPerIpFixedWindow(options, RateLimitPolicies.General, config.GetSection("RateLimiting:General"),
			defaultPermits: 120, defaultWindowSeconds: 60);
		AddPerIpFixedWindow(options, RateLimitPolicies.Pages, config.GetSection("RateLimiting:Pages"),
			defaultPermits: 600, defaultWindowSeconds: 60);
	});

var app = builder.Build();

// Apply pending migrations, bootstrap the super admin, config pages + main menu (skipped during OpenAPI generation).
if (!generatingOpenApi)
{
	app.Services.GetRequiredService<CmsConfig>();
	using (var scope = app.Services.CreateScope())
	{
		var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
		await db.Database.MigrateAsync();
	}
	await StaffBootstrapper.EnsureSuperAdminAsync(app.Services);
	await PageSeeder.EnsureAsync(app.Services);
	await MenuService.EnsureMainAsync(app.Services);
}

app.UseForwardedHeaders();
app.UseExceptionHandler();

app.Use(async (context, next) =>
{
	context.Response.OnStarting(() =>
	{
		var headers = context.Response.Headers;
		headers["X-Content-Type-Options"] = "nosniff";
		headers["X-Frame-Options"] = "DENY";
		headers["Referrer-Policy"] = "no-referrer";
		if (context.Request.Path.StartsWithSegments("/api/auth"))
			headers.CacheControl = "no-store";
		return Task.CompletedTask;
	});
	await next(context);
});

// a restore swaps the database underneath: everything else waits
var backupLock = app.Services.GetRequiredService<BackupLock>();
app.Use(async (context, next) =>
{
	if (backupLock.Restoring && context.Request.Path.StartsWithSegments("/api"))
	{
		context.Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
		context.Response.Headers.RetryAfter = "30";
		await context.Response.WriteAsJsonAsync(new
		{
			status = 503,
			title = "Restoring a backup",
			detail = "The site is being restored from a backup. Try again in a minute.",
		}, context.RequestAborted);
		return;
	}
	await next(context);
});

if (app.Environment.IsDevelopment())
{
	app.UseSwagger();
	app.UseSwaggerUI();
}

// No UseHttpsRedirection: TLS terminates at the proxy.
app.UseCors("frontend");
app.UseAuthentication();
app.UseAuthorization();
app.UseRateLimiter();
app.MapControllers();
app.MapHealthChecks("/health");

if (app.Environment.IsDevelopment())
{
	app.MapGet("/_diagnostics/throw", () =>
	{
		throw new InvalidOperationException("Test exception for diagnostics");
	});
}

await app.RunAsync();

return 0;

static void AddPerIpFixedWindow(
	RateLimiterOptions options, string policy, IConfiguration section, int defaultPermits, int defaultWindowSeconds)
{
	var permitLimit = section.GetValue("PermitLimit", defaultPermits);
	var window = TimeSpan.FromSeconds(section.GetValue("WindowSeconds", defaultWindowSeconds));
	var queueLimit = section.GetValue("QueueLimit", 0);

	options.AddPolicy(policy, context => RateLimitPartition.GetFixedWindowLimiter(
		context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
		_ => new FixedWindowRateLimiterOptions
		{
			PermitLimit = permitLimit,
			Window = window,
			QueueLimit = queueLimit,
			QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
		}));
}

public partial class Program { }
