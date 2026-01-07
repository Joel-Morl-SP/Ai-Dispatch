using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Ai_Dispatch.Services;
using Ai_Dispatch.Services.Classification;

var builder = FunctionsApplication.CreateBuilder(args);

builder.ConfigureFunctionsWebApplication();

builder.Services
    .AddApplicationInsightsTelemetryWorkerService()
    .ConfigureFunctionsApplicationInsights()
    .AddHttpClient();

builder.Logging.AddFilter("System.Net.Http.HttpClient", LogLevel.None);
builder.Logging.AddFilter("System.Net.Http.HttpClient.*", LogLevel.None);
builder.Logging.AddFilter("Microsoft.Extensions.Http", LogLevel.Warning);

var endpoint = builder.Configuration["AZURE_OPENAI_ENDPOINT"];
var apiKey = builder.Configuration["AZURE_OPENAI_API_KEY"];
var baseModel = builder.Configuration["AZURE_BASE_MODEL"];
var reasoningModel = builder.Configuration["AZURE_REASONING_MODEL"];
var connectionString = builder.Configuration["AzureWebJobsStorage"];
var tableName = builder.Configuration["DECISION_LOG_TABLE_NAME"];
var apimBaseUrl = builder.Configuration["APIM_BASE_URL"];
var apimSubscriptionKey = builder.Configuration["APIM_SUBSCRIPTION_KEY"];
var dispatchUser = builder.Configuration["DISPATCH_USER"];

if (string.IsNullOrEmpty(endpoint) || string.IsNullOrEmpty(apiKey) || string.IsNullOrEmpty(baseModel) || string.IsNullOrEmpty(reasoningModel))
{
    throw new InvalidOperationException("Missing required Azure OpenAI configuration: AZURE_OPENAI_ENDPOINT, AZURE_OPENAI_API_KEY, AZURE_BASE_MODEL, AZURE_REASONING_MODEL");
}

if (string.IsNullOrEmpty(connectionString) || string.IsNullOrEmpty(tableName))
{
    throw new InvalidOperationException("Missing required Azure Storage configuration: AzureWebJobsStorage, DECISION_LOG_TABLE_NAME");
}

if (string.IsNullOrEmpty(apimBaseUrl) || string.IsNullOrEmpty(apimSubscriptionKey) || string.IsNullOrEmpty(dispatchUser))
{
    throw new InvalidOperationException("Missing required APIM environment variables: APIM_BASE_URL, APIM_SUBSCRIPTION_KEY, DISPATCH_USER");
}

builder.Services.AddSingleton<AzureOpenAIService>(provider =>
{
    var config = provider.GetRequiredService<IConfiguration>();
    var logger = provider.GetRequiredService<ILogger<AzureOpenAIService>>();
    var httpClientFactory = provider.GetRequiredService<IHttpClientFactory>();
    return new AzureOpenAIService(config, logger, httpClientFactory);
});

builder.Services.AddSingleton<LoggingService>(provider =>
{
    var config = provider.GetRequiredService<IConfiguration>();
    var logger = provider.GetRequiredService<ILogger<LoggingService>>();
    return new LoggingService(config, logger);
});

builder.Services.AddScoped<IConnectWiseHttpClient>(provider =>
{
    var logger = provider.GetRequiredService<ILogger<ConnectWiseHttpClient>>();
    return new ConnectWiseHttpClient(apimBaseUrl, apimSubscriptionKey, dispatchUser, logger);
});

builder.Services.AddScoped<ITicketUpdateConverter, TicketUpdateConverter>();
builder.Services.AddScoped<ITicketService, ConnectWiseTicketService>();
builder.Services.AddScoped<IContactService, ConnectWiseContactService>();
builder.Services.AddScoped<IActivityService, ConnectWiseActivityService>();

builder.Services.AddScoped<IConnectWiseService>(provider =>
{
    var ticketService = provider.GetRequiredService<ITicketService>();
    var contactService = provider.GetRequiredService<IContactService>();
    var activityService = provider.GetRequiredService<IActivityService>();
    return new ConnectWiseService(ticketService, contactService, activityService);
});

builder.Services.AddSingleton<TeamsAlertService>(provider =>
{
    var config = provider.GetRequiredService<IConfiguration>();
    var logger = provider.GetRequiredService<ILogger<TeamsAlertService>>();
    var httpClientFactory = provider.GetRequiredService<IHttpClientFactory>();
    return new TeamsAlertService(config, logger, httpClientFactory);
});

builder.Services.AddSingleton<ProposedNoteService>(provider =>
{
    var config = provider.GetRequiredService<IConfiguration>();
    var logger = provider.GetRequiredService<ILogger<ProposedNoteService>>();
    var httpClientFactory = provider.GetRequiredService<IHttpClientFactory>();
    return new ProposedNoteService(config, logger, httpClientFactory);
});

builder.Services.AddScoped<IRequestValidator, RequestValidator>();
builder.Services.AddScoped<ITicketRequestLogger, TicketRequestLogger>();
builder.Services.AddScoped<IClassificationActivityCreator, ClassificationActivityCreator>();
builder.Services.AddScoped<IBoardConfidenceHandler, BoardConfidenceHandler>();
builder.Services.AddScoped<IContactLookup, ContactLookup>();
builder.Services.AddScoped<ITicketUpdateBuilder, TicketUpdateBuilder>();
builder.Services.AddScoped<ITicketUpdater, TicketUpdater>();
builder.Services.AddScoped<IProposedNoteProcessor, ProposedNoteProcessor>();
builder.Services.AddScoped<IResponseBuilder, ResponseBuilder>();

builder.Build().Run();
