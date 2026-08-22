using System.Globalization;
using CebizPay.Infrastructure;
using CebizPay.Workers;
using Serilog;

var builder = Host.CreateApplicationBuilder(args);

// Configure Serilog
builder.Services.AddSerilog((services, loggerConfig) =>
{
    loggerConfig
        .ReadFrom.Configuration(builder.Configuration)
        .ReadFrom.Services(services)
        .Enrich.FromLogContext()
        .Enrich.WithProperty("Application", "CebizPay.Workers")
        .Enrich.WithProperty("Environment", builder.Environment.EnvironmentName)
        .WriteTo.Console(formatProvider: CultureInfo.InvariantCulture);
});

// Configure Infrastructure & Hosted Services
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddHostedService<Worker>();
builder.Services.AddHostedService<OutboxPublisherWorker>();
builder.Services.AddHostedService<PaymentReconciliationWorker>();
builder.Services.AddHostedService<PayrollExecutionWorker>();
builder.Services.AddHostedService<LoanRepaymentWorker>();

var host = builder.Build();
host.Run();
