using HealthCare.Appointments.Api.Constants;
using HealthCare.Appointments.Api.Service;
using MediatR;
using HealthCare.Appointments.Api.Models;
using Microsoft.EntityFrameworkCore;
using Polly;
using Polly.Extensions.Http;
using Microsoft.Extensions.Logging.EventLog;

var builder = WebApplication.CreateBuilder(args);
builder.Logging.ClearProviders();
builder.Logging.AddConsole()
    .AddEventLog(new EventLogSettings { SourceName = "Appointments Service" })
    .AddDebug()
    .AddEventSourceLogger(); 


// Add services to the container.
builder.Services.AddDbContext<AppointmentsDbContext>();
builder.Services.AddControllers();
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddHttpClient();
builder.Services.AddSingleton<ApiEndpoints>();

builder.Services.AddHttpClient<IPatientsApiRepository, PatientsApiRepository>(c =>
{
    c.BaseAddress = new Uri(builder.Configuration["ApiEndpoints:PatientsApi"]);
}).AddPolicyHandler(GetRetryPolicy())
.AddPolicyHandler(GetCircuitBreakerPolicy());

builder.Services.AddAutoMapper(AppDomain.CurrentDomain.GetAssemblies());

builder.Services.AddStackExchangeRedisCache(options =>
{
    options.Configuration = builder.Configuration.GetSection("Redis")["ConnectionString"];
});


var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

app.Run();


static IAsyncPolicy<HttpResponseMessage> GetRetryPolicy()
{
    return HttpPolicyExtensions.HandleTransientHttpError()
        .OrResult(r => !r.IsSuccessStatusCode)
        .Or<HttpRequestException>()
        .WaitAndRetryAsync(5, retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)), (exception, timeSpan, context) => {
            // Add logic to be executed before each retry, such as logging or reauthentication
        });

}

static IAsyncPolicy<HttpResponseMessage> GetCircuitBreakerPolicy()
{
    return HttpPolicyExtensions
        .HandleTransientHttpError()
        .CircuitBreakerAsync(5, TimeSpan.FromSeconds(30));
}
