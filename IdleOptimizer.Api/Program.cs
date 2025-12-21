using IdleOptimizer.Api.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Register MongoDB service
builder.Services.AddScoped<IMongoService, MongoService>();

// Configure CORS for Blazor WebAssembly
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowBlazorWasm", policy =>
    {
        policy.WithOrigins("http://localhost:5000", "https://localhost:5001", 
                          "http://localhost:5173", "https://localhost:5174")
              .AllowAnyMethod()
              .AllowAnyHeader()
              .AllowCredentials();
    });
});

// Configure JSON serialization to handle Dictionary types properly
builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.PropertyNamingPolicy = null; // Keep original property names
});

// Read allowed origins from configuration
if (builder.Environment.IsDevelopment())
{
    builder.Services.AddCors(options =>
    {
        options.AddPolicy("AllowBlazorWasm", policy =>
        {
            policy.WithOrigins("http://localhost:5000", "https://localhost:5001")
                  .AllowAnyMethod()
                  .AllowAnyHeader()
                  .AllowCredentials();
        });
    });
}

var app = builder.Build();

// Configure the HTTP request pipeline
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

//app.UseHttpsRedirection(); // Handled by nginx

if (app.Environment.IsDevelopment())
{
    app.UseCors("AllowBlazorWasm");
}

app.UseAuthorization();

app.MapControllers();

app.Run();

