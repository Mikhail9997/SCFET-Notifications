using System.Text;
using Application.Common.Interfaces;
using Application.Common.Services;
using Application.Configurations;
using Application.Hubs;
using Application.Profiles;
using Application.Services;
using Core.Interfaces;
using Docker.DotNet;
using dotenv.net;
using DotNetEnv;
using Infrastructure;
using Infrastructure.Repositories;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.FileProviders;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using StackExchange.Redis;


var envPath = Path.Combine(Directory.GetParent(Directory.GetCurrentDirectory())?.FullName, ".env");

// Загружаем .env файл
if (File.Exists(envPath))
{
    DotEnv.Load(options: new DotEnvOptions(
        envFilePaths: new[] { envPath },
        ignoreExceptions: false
    ));
}
else
{
    Console.WriteLine($"Warning: .env file not found at {envPath}");
}

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();
builder.Configuration.AddEnvironmentVariables();

// Add services
builder.Services.AddControllers();
builder.Services.AddHealthChecks();
builder.Services.AddAutoMapper(cfg => { }, typeof(FilterProfile), typeof(NotificationFilterProfile), typeof(UserProfile));
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "SCFET Notification API", Version = "v1" });
    
    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Description = "JWT Authorization header using the Bearer scheme",
        Name = "Authorization",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.Http,
        Scheme = "Bearer"
    });
    
    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            Array.Empty<string>()
        }
    });
});

// Database
var dbPassword = Environment.GetEnvironmentVariable("DB_PASSWORD");
var dbName = Environment.GetEnvironmentVariable("DB_NAME");

builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseNpgsql(builder.Configuration["ConnectionStrings:DefaultConnection"],
        npgsqlOptions => 
        {
            npgsqlOptions.EnableRetryOnFailure(
                maxRetryCount: 5,
                maxRetryDelay: TimeSpan.FromSeconds(30),
                errorCodesToAdd: null);
        }));

// Configurations
builder.Services.Configure<JwtSettings>(builder.Configuration.GetSection("JwtSettings"));
builder.Services.Configure<BackupSettings>(builder.Configuration.GetSection("BackupSettings"));

// Authentication
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = builder.Configuration["JwtSettings:Issuer"],
            ValidAudience = builder.Configuration["JwtSettings:Audience"],
            IssuerSigningKey = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(builder.Configuration["JwtSettings:Secret"]!))
        };
        
        // конфигурация для  SignalR JWT
        options.Events = new JwtBearerEvents
        {
            OnMessageReceived = context =>
            {
                var accessToken = context.Request.Query["access_token"];
                var path = context.HttpContext.Request.Path;
                
                if (!string.IsNullOrEmpty(accessToken) && path.StartsWithSegments("/notificationHub"))
                {
                    context.Token = accessToken;
                }
                return Task.CompletedTask;
            }
        };

    });

// CORS
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy.WithOrigins("http://localhost:3000", "https://localhost:3000")
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials();
    });
});

// HttpContext Accessor
builder.Services.AddHttpContextAccessor();

//Redis
builder.Services.AddSingleton<IConnectionMultiplexer>(provider =>
{
    string connectionString = builder.Configuration["Redis:ConnectionStrings"]
                              ?? throw new ArgumentException("redis ConnectionStrings is not configured");

    return ConnectionMultiplexer.Connect(connectionString);
});

builder.Services.AddSingleton<DockerClient>(sp =>
{
    var configuration = sp.GetRequiredService<IConfiguration>();
    var dockerUri = configuration["Docker:Uri"] ?? "unix:///var/run/docker.sock";
    
    return new DockerClientConfiguration(new Uri(dockerUri))
        .CreateClient();
});

// Dependency Injection

// Repositories
builder.Services.AddScoped(typeof(IRepository<>), typeof(BaseRepository<>));
builder.Services.AddScoped<IUserRepository, UserRepository>();
builder.Services.AddScoped<INotificationRepository, NotificationRepository>();
builder.Services.AddScoped<IGroupRepository, GroupRepository>();

// Services
builder.Services.AddScoped<IJwtTokenGenerator, JwtTokenGenerator>();
builder.Services.AddScoped<IKafkaProducer, KafkaProducer>();
builder.Services.AddScoped<IPasswordHasher, PasswordHasher>();
builder.Services.AddScoped<ICurrentUserService, CurrentUserService>();
builder.Services.AddSingleton<IRandomTokenGenerator, RandomTokenGenerator>();
builder.Services.AddScoped<IJwtTokenGenerator, JwtTokenGenerator>();

// Application Services
builder.Services.AddScoped<AuthService>();
builder.Services.AddScoped<NotificationAppService>();
builder.Services.AddScoped<FileService>();
builder.Services.AddSingleton<RedisService>();
builder.Services.AddScoped<IDatabaseBackupService, DockerBackupUniversalService>();

// Background Services
builder.Services.AddHostedService<KafkaConsumerService>();
builder.Services.AddHostedService<ScheduledBackupService>();

// SignalR
builder.Services.AddSignalR();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.UseSwagger();
    app.UseSwaggerUI();
}

// Создаем директорию uploads, если ее нет
var uploadsPath = Path.Combine(app.Environment.ContentRootPath, "uploads");
if (!Directory.Exists(uploadsPath))
{
    Directory.CreateDirectory(uploadsPath);
    Console.WriteLine($"Created uploads directory at: {uploadsPath}");
}

app.UseCors("AllowAll");

// Разрешаем доступ к статическим файлам
app.UseStaticFiles(new StaticFileOptions
{
    FileProvider = new PhysicalFileProvider(
        Path.Combine(builder.Environment.ContentRootPath, "uploads")),
    RequestPath = "/uploads"
});

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.UseHttpsRedirection();
app.MapHub<NotificationHub>("/notificationHub");
app.MapHealthChecks("/health");

// Убеждаемся, что база данных создана
using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
    
    var retries = 5;
    while (retries > 0)
    {
        try
        {
            await dbContext.Database.EnsureCreatedAsync();
            
            logger.LogInformation("Database created successfully");
            break;
        }
        catch (Exception ex)
        {
            retries--;
            logger.LogWarning("Failed to connect to database. Retries left: {Retries}. Error: {Error}", 
                retries, ex.Message);
            
            if (retries == 0) throw;
            
            await Task.Delay(5000);
        }
    }
}

app.Run();