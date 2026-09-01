using CebizPay.Application.Common.Interfaces.Caching;
using CebizPay.Application.Common.Interfaces.Messaging;
using CebizPay.Application.Common.Interfaces.Persistence;
using CebizPay.Application.Common.Interfaces.Security;
using CebizPay.Infrastructure.Caching;
using CebizPay.Infrastructure.Identity;
using CebizPay.Infrastructure.Messaging;
using CebizPay.Infrastructure.Options;
using CebizPay.Infrastructure.Payments.Flutterwave;
using CebizPay.Infrastructure.Payments.Monnify;
using CebizPay.Infrastructure.Payments.Paystack;
using CebizPay.Infrastructure.Persistence;
using CebizPay.Infrastructure.Security;
using CebizPay.Infrastructure.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using StackExchange.Redis;

namespace CebizPay.Infrastructure;

/// <summary>
/// Service collection extensions for configuring Infrastructure dependencies.
/// </summary>
public static class DependencyInjection
{
    /// <summary>
    /// Adds infrastructure services to the specified <see cref="IServiceCollection"/>.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configuration">The application configuration.</param>
    /// <returns>The updated service collection.</returns>
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // Bind and validate options
        services.AddOptions<DatabaseOptions>()
            .Bind(configuration.GetSection(DatabaseOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddOptions<JwtOptions>()
            .Bind(configuration.GetSection(JwtOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddOptions<RedisOptions>()
            .Bind(configuration.GetSection(RedisOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddOptions<RabbitMQOptions>()
            .Bind(configuration.GetSection(RabbitMQOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddOptions<CorsOptions>()
            .Bind(configuration.GetSection(CorsOptions.SectionName));

        services.AddOptions<SuperAdminSeedOptions>()
            .Bind(configuration.GetSection(SuperAdminSeedOptions.SectionName));

        // Configure PostgreSQL & EF Core
        var dbOptions = configuration.GetSection(DatabaseOptions.SectionName).Get<DatabaseOptions>()
            ?? new DatabaseOptions();

        var connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? dbOptions.DefaultConnection;

        services.AddDbContext<ApplicationDbContext>(options =>
        {
            options.ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.RelationalEventId.PendingModelChangesWarning));
            options.UseNpgsql(connectionString, npgsql =>
            {
                npgsql.CommandTimeout(dbOptions.CommandTimeoutInSeconds);
                npgsql.EnableRetryOnFailure(
                    maxRetryCount: dbOptions.MaxRetryCount,
                    maxRetryDelay: TimeSpan.FromSeconds(10),
                    errorCodesToAdd: null);
            });
        });

        services.AddScoped<IApplicationDbContext>(sp => sp.GetRequiredService<ApplicationDbContext>());
        services.AddSingleton<IAsyncQueryExecutor, EfCoreAsyncQueryExecutor>();
        CebizPay.Application.Common.Extensions.AsyncQueryableExtensions.SetExecutor(new EfCoreAsyncQueryExecutor());

        // Configure Identity with unified ApplicationUser
        services.AddIdentityCore<ApplicationUser>(options =>
        {
            options.Password.RequireDigit = true;
            options.Password.RequireLowercase = true;
            options.Password.RequireNonAlphanumeric = true;
            options.Password.RequireUppercase = true;
            options.Password.RequiredLength = 8;
            options.User.RequireUniqueEmail = true;
        })
        .AddRoles<IdentityRole>()
        .AddEntityFrameworkStores<ApplicationDbContext>()
        .AddSignInManager<SignInManager<ApplicationUser>>()
        .AddDefaultTokenProviders();

        services.AddHttpContextAccessor();
        services.AddScoped<ICurrentOrganizationContext, CurrentOrganizationContext>();
        services.AddScoped<IMfaCodeDeliveryService, NoOpMfaCodeDeliveryService>();
        services.AddScoped<IMfaService, MfaService>();
        services.AddTransient<IIdentityService, IdentityService>();
        services.AddScoped<ITransactionPinService, TransactionPinService>();
        services.AddTransient<IOtpService, RedisOtpService>();
        services.AddScoped<CebizPay.Application.Common.Interfaces.Security.ICurrentUserService, CurrentUserService>();
        services.AddScoped<CebizPay.Application.Common.Interfaces.Security.IUserLookupService, UserLookupService>();
        services.AddSingleton<CebizPay.Application.Common.Interfaces.Security.IAuditSanitizer, CebizPay.Application.Common.Security.AuditSanitizer>();
        services.AddScoped<CebizPay.Application.Common.Interfaces.Security.IAuditContextAccessor, AuditContextAccessor>();
        services.AddScoped<CebizPay.Application.Common.Interfaces.Security.IAuditLogService, AuditLogService>();

        // Configure Finance services
        services.AddScoped<CebizPay.Application.Common.Interfaces.Finance.IWalletService, CebizPay.Infrastructure.Finance.WalletService>();
        services.AddScoped<CebizPay.Application.Common.Interfaces.Finance.ILedgerPostingService, CebizPay.Infrastructure.Finance.LedgerPostingService>();
        services.AddScoped<CebizPay.Application.Common.Interfaces.Finance.IIdempotencyService, CebizPay.Infrastructure.Finance.IdempotencyService>();
        services.AddScoped<CebizPay.Application.Common.Interfaces.Finance.IFeePolicyService, CebizPay.Infrastructure.Finance.FeePolicyService>();
        services.AddScoped<CebizPay.Application.Common.Interfaces.Finance.IBankTransferFeePolicyService, CebizPay.Infrastructure.Finance.BankTransferFeePolicyService>();
        services.AddScoped<CebizPay.Application.Common.Interfaces.Finance.IPlatformFeePolicyService, CebizPay.Infrastructure.Finance.PlatformFeePolicyService>();
        services.AddScoped<CebizPay.Application.Common.Interfaces.Finance.IExternalFundingAccountService, CebizPay.Infrastructure.Finance.ExternalFundingAccountService>();

        // Configure Payments & VAS Providers Options with validation
        services.AddOptions<FlutterwaveOptions>()
            .Bind(configuration.GetSection(FlutterwaveOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddOptions<PaystackOptions>()
            .Bind(configuration.GetSection(PaystackOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddOptions<MonnifyOptions>()
            .Bind(configuration.GetSection(MonnifyOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddOptions<VtuGateOptions>()
            .Bind(configuration.GetSection(VtuGateOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddHttpClient<CebizPay.Infrastructure.Payments.Flutterwave.FlutterwaveClient>();
        services.AddHttpClient<CebizPay.Infrastructure.Payments.Paystack.PaystackClient>();
        services.AddHttpClient<CebizPay.Infrastructure.Payments.Monnify.IMonnifyClient, CebizPay.Infrastructure.Payments.Monnify.MonnifyClient>();
        services.AddHttpClient<CebizPay.Infrastructure.Vas.VtuGate.VtuGateClient>();

        // Configure VAS Services
        services.AddScoped<CebizPay.Application.Common.Interfaces.Vas.IVasProvider, CebizPay.Infrastructure.Vas.VtuGate.VtuGateVasProvider>();
        services.AddSingleton<CebizPay.Application.Common.Interfaces.Vas.IVasDuplicateGuard, CebizPay.Infrastructure.Vas.VasDuplicateGuard>();
        services.AddScoped<CebizPay.Application.Common.Interfaces.Vas.IVasPurchaseExecutor, CebizPay.Infrastructure.Vas.VasPurchaseExecutor>();
        services.AddScoped<CebizPay.Application.Common.Interfaces.Vas.IVasReconciliationService, CebizPay.Infrastructure.Vas.VasReconciliationService>();

        // Configure Payment Routing & Providers
        services.AddScoped<CebizPay.Application.Common.Interfaces.Payments.IPaymentRoutingService, CebizPay.Infrastructure.Payments.Common.PaymentRoutingService>();
        services.AddScoped<CebizPay.Application.Common.Interfaces.Payments.IPaymentProvider, CebizPay.Infrastructure.Payments.Flutterwave.FlutterwavePaymentProvider>();
        services.AddScoped<CebizPay.Application.Common.Interfaces.Payments.IPaymentProvider, CebizPay.Infrastructure.Payments.Paystack.PaystackPaymentProvider>();
        services.AddScoped<CebizPay.Application.Common.Interfaces.Payments.IPaymentProvider, CebizPay.Infrastructure.Payments.Monnify.MonnifyPaymentProvider>();
        services.AddScoped<CebizPay.Application.Common.Interfaces.Payments.IVirtualAccountProvider, CebizPay.Infrastructure.Payments.Flutterwave.FlutterwavePaymentProvider>();
        services.AddScoped<CebizPay.Application.Common.Interfaces.Payments.IVirtualAccountProvider, CebizPay.Infrastructure.Payments.Paystack.PaystackPaymentProvider>();
        services.AddScoped<CebizPay.Application.Common.Interfaces.Payments.IVirtualAccountProvider, CebizPay.Infrastructure.Payments.Monnify.MonnifyPaymentProvider>();
        services.AddScoped<CebizPay.Application.Common.Interfaces.Payments.ICardPaymentProvider, CebizPay.Infrastructure.Payments.Flutterwave.FlutterwavePaymentProvider>();
        services.AddScoped<CebizPay.Application.Common.Interfaces.Payments.ICardPaymentProvider, CebizPay.Infrastructure.Payments.Paystack.PaystackPaymentProvider>();
        services.AddScoped<CebizPay.Application.Common.Interfaces.Payments.IPaymentProviderFactory, CebizPay.Infrastructure.Payments.Common.PaymentProviderFactory>();
        services.AddScoped<CebizPay.Application.Common.Interfaces.Finance.IBankAccountResolver, CebizPay.Infrastructure.Payments.Common.PaymentProviderBankAccountResolver>();
        services.AddScoped<CebizPay.Application.Common.Interfaces.Finance.IBankTransferExecutor, CebizPay.Infrastructure.Payments.Common.PaymentProviderBankTransferExecutor>();
        services.AddScoped<CebizPay.Application.Common.Interfaces.Payments.IWebhookSignatureVerifier, CebizPay.Infrastructure.Payments.Common.WebhookSignatureVerifier>();
        services.AddScoped<CebizPay.Application.Common.Interfaces.Payments.IWebhookProcessor, CebizPay.Infrastructure.Payments.Common.WebhookProcessor>();
        services.AddScoped<CebizPay.Application.Common.Interfaces.Payments.IWebhookEventNormalizer, CebizPay.Infrastructure.Payments.Common.WebhookEventNormalizer>();
        services.AddSingleton<CebizPay.Infrastructure.Payments.Common.ReconciliationMetrics>();
        services.AddScoped<CebizPay.Infrastructure.Payments.Common.ReconciliationEngine>();
        services.AddScoped<CebizPay.Application.Common.Interfaces.Payments.IReconciliationEngine>(sp => sp.GetRequiredService<CebizPay.Infrastructure.Payments.Common.ReconciliationEngine>());
        services.AddScoped<CebizPay.Application.Common.Interfaces.Payments.IPaymentReconciliationService>(sp => sp.GetRequiredService<CebizPay.Infrastructure.Payments.Common.ReconciliationEngine>());
        services.AddScoped<CebizPay.Application.Common.Interfaces.Payments.IWebhookProcessingService, CebizPay.Infrastructure.Payments.Common.WebhookProcessingService>();
        services.AddScoped<CebizPay.Application.Common.Interfaces.Payments.IVirtualAccountService, CebizPay.Infrastructure.Payments.VirtualAccounts.VirtualAccountService>();
        services.AddScoped<CebizPay.Application.Common.Interfaces.Payments.ICardFundingService, CebizPay.Infrastructure.Payments.Funding.CardFundingService>();
        services.AddScoped<CebizPay.Application.Common.Interfaces.Payments.ISavedCardService, CebizPay.Infrastructure.Payments.Funding.SavedCardService>();
        services.AddScoped<CebizPay.Application.Common.Interfaces.Payments.ICardRefundService, CebizPay.Infrastructure.Payments.Funding.CardRefundService>();
        services.AddScoped<CebizPay.Application.Common.Interfaces.Payments.ICardVerificationService, CebizPay.Infrastructure.Payments.Funding.CardVerificationService>();

        // Configure Compliance KYC/KYB Options with validation
        services.AddOptions<CebizPay.Infrastructure.Compliance.Dojah.DojahOptions>()
            .Bind(configuration.GetSection(CebizPay.Infrastructure.Compliance.Dojah.DojahOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddOptions<CebizPay.Infrastructure.Compliance.SmileId.SmileIdOptions>()
            .Bind(configuration.GetSection(CebizPay.Infrastructure.Compliance.SmileId.SmileIdOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddOptions<CebizPay.Infrastructure.Compliance.Ninja.NinjaOptions>()
            .Bind(configuration.GetSection(CebizPay.Infrastructure.Compliance.Ninja.NinjaOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        // Compliance HTTP Clients
        services.AddHttpClient<CebizPay.Infrastructure.Compliance.Dojah.IDojahClient, CebizPay.Infrastructure.Compliance.Dojah.DojahClient>();
        services.AddHttpClient<CebizPay.Infrastructure.Compliance.SmileId.ISmileIdClient, CebizPay.Infrastructure.Compliance.SmileId.SmileIdClient>();
        services.AddHttpClient<CebizPay.Infrastructure.Compliance.Ninja.INinjaClient, CebizPay.Infrastructure.Compliance.Ninja.NinjaClient>();

        // Compliance Provider Adapters
        services.AddScoped<CebizPay.Infrastructure.Compliance.Dojah.DojahVerificationProvider>();
        services.AddScoped<CebizPay.Infrastructure.Compliance.SmileId.SmileIdVerificationProvider>();
        services.AddScoped<CebizPay.Infrastructure.Compliance.Ninja.NinjaVerificationProvider>();

        services.AddScoped<CebizPay.Application.Common.Interfaces.Compliance.IIdentityVerificationProvider>(sp => sp.GetRequiredService<CebizPay.Infrastructure.Compliance.Dojah.DojahVerificationProvider>());
        services.AddScoped<CebizPay.Application.Common.Interfaces.Compliance.IIdentityVerificationProvider>(sp => sp.GetRequiredService<CebizPay.Infrastructure.Compliance.SmileId.SmileIdVerificationProvider>());
        services.AddScoped<CebizPay.Application.Common.Interfaces.Compliance.IIdentityVerificationProvider>(sp => sp.GetRequiredService<CebizPay.Infrastructure.Compliance.Ninja.NinjaVerificationProvider>());

        services.AddScoped<CebizPay.Application.Common.Interfaces.Compliance.IBiometricVerificationProvider>(sp => sp.GetRequiredService<CebizPay.Infrastructure.Compliance.SmileId.SmileIdVerificationProvider>());
        services.AddScoped<CebizPay.Application.Common.Interfaces.Compliance.IBiometricVerificationProvider>(sp => sp.GetRequiredService<CebizPay.Infrastructure.Compliance.Dojah.DojahVerificationProvider>());

        services.AddScoped<CebizPay.Application.Common.Interfaces.Compliance.IDocumentVerificationProvider>(sp => sp.GetRequiredService<CebizPay.Infrastructure.Compliance.SmileId.SmileIdVerificationProvider>());
        services.AddScoped<CebizPay.Application.Common.Interfaces.Compliance.IDocumentVerificationProvider>(sp => sp.GetRequiredService<CebizPay.Infrastructure.Compliance.Dojah.DojahVerificationProvider>());

        services.AddScoped<CebizPay.Application.Common.Interfaces.Compliance.IAmlScreeningProvider>(sp => sp.GetRequiredService<CebizPay.Infrastructure.Compliance.Dojah.DojahVerificationProvider>());
        services.AddScoped<CebizPay.Application.Common.Interfaces.Compliance.IAmlScreeningProvider>(sp => sp.GetRequiredService<CebizPay.Infrastructure.Compliance.SmileId.SmileIdVerificationProvider>());
        services.AddScoped<CebizPay.Application.Common.Interfaces.Compliance.IAmlScreeningProvider>(sp => sp.GetRequiredService<CebizPay.Infrastructure.Compliance.Ninja.NinjaVerificationProvider>());

        services.AddScoped<CebizPay.Application.Common.Interfaces.Compliance.IBusinessVerificationProvider>(sp => sp.GetRequiredService<CebizPay.Infrastructure.Compliance.Dojah.DojahVerificationProvider>());
        services.AddScoped<CebizPay.Application.Common.Interfaces.Compliance.IBusinessVerificationProvider>(sp => sp.GetRequiredService<CebizPay.Infrastructure.Compliance.Ninja.NinjaVerificationProvider>());
        services.AddScoped<CebizPay.Application.Common.Interfaces.Compliance.IBusinessVerificationProvider>(sp => sp.GetRequiredService<CebizPay.Infrastructure.Compliance.SmileId.SmileIdVerificationProvider>());

        // Compliance Routing, Factory, Orchestrator, Webhook Processor
        services.AddScoped<CebizPay.Application.Common.Interfaces.Compliance.IVerificationRoutingService, CebizPay.Infrastructure.Compliance.Common.VerificationRoutingService>();
        services.AddScoped<CebizPay.Application.Common.Interfaces.Compliance.IVerificationProviderFactory, CebizPay.Infrastructure.Compliance.Common.VerificationProviderFactory>();
        services.AddScoped<CebizPay.Application.Common.Interfaces.Compliance.IVerificationOrchestrator, CebizPay.Infrastructure.Compliance.Common.VerificationOrchestrator>();
        services.AddScoped<CebizPay.Application.Common.Interfaces.Compliance.IComplianceWebhookSignatureVerifier, CebizPay.Infrastructure.Compliance.Common.ComplianceWebhookSignatureVerifier>();
        services.AddScoped<CebizPay.Application.Common.Interfaces.Compliance.IComplianceWebhookProcessor, CebizPay.Infrastructure.Compliance.Common.ComplianceWebhookProcessor>();

        // Batch 6: Risk Engine, Rules, CDD, EDD & Compliance Decisioning
        services.AddSingleton<CebizPay.Infrastructure.Compliance.Services.RiskMetrics>();
        services.AddScoped<CebizPay.Application.Common.Interfaces.Compliance.IRiskRule, CebizPay.Infrastructure.Compliance.Rules.SanctionsScreeningRule>();
        services.AddScoped<CebizPay.Application.Common.Interfaces.Compliance.IRiskRule, CebizPay.Infrastructure.Compliance.Rules.PepScreeningRule>();
        services.AddScoped<CebizPay.Application.Common.Interfaces.Compliance.IRiskRule, CebizPay.Infrastructure.Compliance.Rules.IdentityVerificationRule>();
        services.AddScoped<CebizPay.Application.Common.Interfaces.Compliance.IRiskRule, CebizPay.Infrastructure.Compliance.Rules.BiometricLivenessRule>();
        services.AddScoped<CebizPay.Application.Common.Interfaces.Compliance.IRiskRule, CebizPay.Infrastructure.Compliance.Rules.CacCorporateRegistryRule>();
        services.AddScoped<CebizPay.Application.Common.Interfaces.Compliance.IRiskRule, CebizPay.Infrastructure.Compliance.Rules.BeneficialOwnershipRule>();
        services.AddScoped<CebizPay.Application.Common.Interfaces.Compliance.IRiskRule, CebizPay.Infrastructure.Compliance.Rules.AdverseMediaScreeningRule>();
        services.AddScoped<CebizPay.Application.Common.Interfaces.Compliance.IRiskRule, CebizPay.Infrastructure.Compliance.Rules.TransactionProfileVolumeRule>();

        services.AddScoped<CebizPay.Application.Common.Interfaces.Compliance.IRiskEngine, CebizPay.Infrastructure.Compliance.Services.RiskEngine>();
        services.AddScoped<CebizPay.Application.Common.Interfaces.Compliance.ICddService, CebizPay.Infrastructure.Compliance.Services.CddService>();
        services.AddScoped<CebizPay.Application.Common.Interfaces.Compliance.IEddWorkflowService, CebizPay.Infrastructure.Compliance.Services.EddWorkflowService>();
        services.AddScoped<CebizPay.Application.Common.Interfaces.Compliance.IComplianceDecisionService, CebizPay.Infrastructure.Compliance.Services.ComplianceDecisionService>();
        services.AddScoped<CebizPay.Application.Common.Interfaces.Compliance.IComplianceEligibilityService, CebizPay.Infrastructure.Compliance.Services.ComplianceEligibilityService>();
        services.AddScoped<CebizPay.Application.Common.Interfaces.Compliance.IComplianceRestrictionService, CebizPay.Infrastructure.Compliance.Services.ComplianceRestrictionService>();
        services.AddSingleton<CebizPay.Application.Common.Interfaces.Compliance.ITransactionLimitPolicyService, CebizPay.Infrastructure.Compliance.Services.TransactionLimitPolicyService>();

        // Configure Payroll & Loan services
        services.AddScoped<CebizPay.Application.Common.Interfaces.Payroll.IPayrollDeductionProvider, CebizPay.Infrastructure.Payroll.PayrollLoanDeductionProvider>();
        services.AddScoped<CebizPay.Application.Common.Interfaces.Payroll.IPayrollCalculationService, CebizPay.Infrastructure.Payroll.PayrollCalculationService>();
        services.AddScoped<CebizPay.Application.Common.Interfaces.Payroll.IPayrollExecutionService, CebizPay.Infrastructure.Payroll.PayrollExecutionService>();
        services.AddScoped<CebizPay.Application.Common.Interfaces.Payroll.IPayrollBatchService, CebizPay.Infrastructure.Payroll.PayrollBatchService>();

        // Configure Credit & Loan services
        services.AddScoped<CebizPay.Application.Common.Interfaces.Loans.ILoanCalculationService, CebizPay.Infrastructure.Loans.LoanCalculationService>();
        services.AddScoped<CebizPay.Application.Common.Interfaces.Loans.ILoanUnderwritingService, CebizPay.Infrastructure.Loans.LoanUnderwritingService>();
        services.AddScoped<CebizPay.Application.Common.Interfaces.Loans.ILoanPlanService, CebizPay.Infrastructure.Loans.LoanPlanService>();
        services.AddScoped<CebizPay.Application.Common.Interfaces.Loans.ILoanApplicationService, CebizPay.Infrastructure.Loans.LoanApplicationService>();
        services.AddScoped<CebizPay.Application.Common.Interfaces.Loans.ILoanContractService, CebizPay.Infrastructure.Loans.LoanContractService>();

        // Configure Savings services
        services.AddScoped<CebizPay.Application.Common.Interfaces.Savings.ISavingsInterestPolicyService, CebizPay.Infrastructure.Savings.SavingsInterestPolicyService>();
        services.AddScoped<CebizPay.Application.Common.Interfaces.Savings.ISavingsService, CebizPay.Infrastructure.Savings.SavingsService>();

        // Configure Thrift / Ajo / Esusu services
        services.AddScoped<CebizPay.Application.Common.Interfaces.Thrift.IThriftGroupService, CebizPay.Infrastructure.Thrift.ThriftGroupService>();
        services.AddScoped<CebizPay.Application.Common.Interfaces.Thrift.IThriftCollectionService, CebizPay.Infrastructure.Thrift.ThriftCollectionService>();
        services.AddScoped<CebizPay.Application.Common.Interfaces.Thrift.IThriftPayoutService, CebizPay.Infrastructure.Thrift.ThriftPayoutService>();

        // Configure Redis
        var redisOptions = configuration.GetSection(RedisOptions.SectionName).Get<RedisOptions>();
        var redisConnString = configuration.GetConnectionString("Redis")
            ?? redisOptions?.ConnectionString
            ?? "localhost:6379";

        services.AddSingleton<IConnectionMultiplexer>(_ => ConnectionMultiplexer.Connect(redisConnString));
        services.AddSingleton<ICacheService, RedisCacheService>();

        // Configure RabbitMQ & Outbox
        services.AddSingleton<IRabbitMqConnectionProvider, RabbitMqConnectionProvider>();
        services.AddSingleton<IEventPublisher, RabbitMQEventPublisher>();
        services.AddScoped<CebizPay.Application.Common.Interfaces.Messaging.IOutboxService, CebizPay.Infrastructure.Persistence.Outbox.OutboxService>();

        // Configure Health Checks
        var rabbitOptions = configuration.GetSection(RabbitMQOptions.SectionName).Get<RabbitMQOptions>();
        var rabbitConnString = configuration.GetConnectionString("RabbitMQ")
            ?? $"amqp://{rabbitOptions?.UserName ?? "cebizpay"}:{rabbitOptions?.Password ?? "cebizpay_dev"}@{rabbitOptions?.HostName ?? "localhost"}:{rabbitOptions?.Port ?? 5672}";

        services.AddHealthChecks()
            .AddNpgSql(connectionString, name: "postgresql")
            .AddRedis(redisConnString, name: "redis")
            .AddRabbitMQ(
                async _ =>
                {
                    var factory = new RabbitMQ.Client.ConnectionFactory { Uri = new Uri(rabbitConnString) };
                    return await factory.CreateConnectionAsync().ConfigureAwait(false);
                },
                name: "rabbitmq");

        return services;
    }
}
