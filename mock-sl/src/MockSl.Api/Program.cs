using MockSl.Api;
using MockSl.Api.Endpoints;
using MockSl.Api.Http;
using MockSl.Application.Auth;
using MockSl.Application.Entities;
using MockSl.Application.Rules;
using MockSl.Application.SqlQueries;
using MockSl.Infrastructure;

var builder = WebApplication.CreateBuilder(args);
var settings = MockSettings.From(builder.Configuration);

builder.Services.AddInfrastructure(settings.ConnectionString);
builder.Services.AddSingleton(new AuthOptions(settings.CompanyDb, settings.SessionTimeoutMinutes));
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton<AuthService>();
builder.Services.AddSingleton<RulesCatalog>();
builder.Services.AddSingleton<EntityHandler>();
builder.Services.AddSingleton<SqlQueryRunner>();

var app = builder.Build();

app.UseMiddleware<ServiceLayerErrorMiddleware>();
app.MapAuthEndpoints();
app.MapCompanyServiceEndpoints();
app.MapResourceEndpoints();

app.Run();
