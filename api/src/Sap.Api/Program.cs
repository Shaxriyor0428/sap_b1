using System.Text.Json.Serialization;
using Sap.Api;
using Sap.Api.Http;
using Sap.Application.Auth;
using Sap.Application.Customers;
using Sap.Infrastructure;

var builder = WebApplication.CreateBuilder(args);
var serviceLayer = ApiSettings.ReadServiceLayerOptions(builder.Configuration);

builder.Services
    .AddControllers()
    .AddJsonOptions(options =>
        // A misspelled field in a body is an error. Otherwise PATCH would answer 204 and change nothing.
        options.JsonSerializerOptions.UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow)
    .ConfigureApiBehaviorOptions(options =>
        options.InvalidModelStateResponseFactory = InvalidRequestResponse.Create);

builder.Services.AddInfrastructure(serviceLayer);
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddScoped<AuthService>();
builder.Services.AddScoped<CustomerService>();

var app = builder.Build();

if (serviceLayer.SkipTlsVerify)
{
    app.Logger.LogWarning("TLS certificate verification for the Service Layer is turned off.");
}

app.UseMiddleware<ApiErrorMiddleware>();
app.MapControllers();

app.Run();
