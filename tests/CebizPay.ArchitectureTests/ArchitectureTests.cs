using NetArchTest.Rules;
using Xunit;

namespace CebizPay.ArchitectureTests;

public sealed class ArchitectureTests
{
    private const string DomainNamespace = "CebizPay.Domain";
    private const string ApplicationNamespace = "CebizPay.Application";
    private const string InfrastructureNamespace = "CebizPay.Infrastructure";
    private const string ApiNamespace = "CebizPay.Api";
    private const string WorkersNamespace = "CebizPay.Workers";

    [Fact]
    public void Domain_ShouldNotHaveDependencyOnOtherProjects()
    {
        var result = Types.InAssembly(typeof(Domain.AssemblyReference).Assembly)
            .ShouldNot()
            .HaveDependencyOnAny(
                ApplicationNamespace,
                InfrastructureNamespace,
                ApiNamespace,
                WorkersNamespace)
            .GetResult();

        Assert.True(result.IsSuccessful, "Domain layer should not depend on any outer layers.");
    }

    [Fact]
    public void Application_ShouldNotHaveDependencyOnInfrastructureOrApiOrWorkers()
    {
        var result = Types.InAssembly(typeof(Application.AssemblyReference).Assembly)
            .ShouldNot()
            .HaveDependencyOnAny(
                InfrastructureNamespace,
                ApiNamespace,
                WorkersNamespace)
            .GetResult();

        Assert.True(result.IsSuccessful, "Application layer should not depend on Infrastructure, API, or Workers.");
    }

    [Fact]
    public void Domain_ShouldNotDependOnExternalFrameworks()
    {
        var result = Types.InAssembly(typeof(Domain.AssemblyReference).Assembly)
            .ShouldNot()
            .HaveDependencyOnAny(
                "Microsoft.EntityFrameworkCore",
                "StackExchange.Redis",
                "RabbitMQ.Client")
            .GetResult();

        Assert.True(result.IsSuccessful, "Domain layer must remain clean without infrastructure framework references.");
    }

    [Fact]
    public void Domain_ShouldNotDependOnOutboxPersistenceImplementation()
    {
        var result = Types.InAssembly(typeof(Domain.AssemblyReference).Assembly)
            .ShouldNot()
            .HaveDependencyOn("CebizPay.Infrastructure.Persistence.Outbox")
            .GetResult();

        Assert.True(result.IsSuccessful, "Domain layer must not depend on Outbox persistence implementation.");
    }

    [Fact]
    public void Application_ShouldNotDependOnOutboxPersistenceImplementation()
    {
        var result = Types.InAssembly(typeof(Application.AssemblyReference).Assembly)
            .ShouldNot()
            .HaveDependencyOn("CebizPay.Infrastructure.Persistence.Outbox")
            .GetResult();

        Assert.True(result.IsSuccessful, "Application layer must not depend on Outbox persistence implementation.");
    }

    [Fact]
    public void Application_ShouldNotDependOnExternalFrameworks()
    {
        var result = Types.InAssembly(typeof(Application.AssemblyReference).Assembly)
            .ShouldNot()
            .HaveDependencyOnAny(
                "Microsoft.EntityFrameworkCore",
                "Npgsql",
                "StackExchange.Redis",
                "RabbitMQ.Client")
            .GetResult();

        Assert.True(result.IsSuccessful, "Application layer must not depend on EF Core, Npgsql, Redis, or RabbitMQ framework references.");
    }

    [Fact]
    public void DomainAndApplication_ShouldNotDependOnPaymentProviderSDKs()
    {
        var domainResult = Types.InAssembly(typeof(Domain.AssemblyReference).Assembly)
            .ShouldNot()
            .HaveDependencyOnAny(
                "Flutterwave",
                "Paystack",
                "Monnify",
                "Stripe")
            .GetResult();

        var appResult = Types.InAssembly(typeof(Application.AssemblyReference).Assembly)
            .ShouldNot()
            .HaveDependencyOnAny(
                "Flutterwave",
                "Paystack",
                "Monnify",
                "Stripe")
            .GetResult();

        Assert.True(domainResult.IsSuccessful, "Domain layer must not reference payment provider SDKs.");
        Assert.True(appResult.IsSuccessful, "Application layer must not reference payment provider SDKs.");
    }

    [Fact]
    public void DomainAndApplication_ShouldNotDependOnPayrollInfrastructureOrWorkers()
    {
        var domainResult = Types.InAssembly(typeof(Domain.AssemblyReference).Assembly)
            .ShouldNot()
            .HaveDependencyOnAny(
                "CebizPay.Infrastructure.Payroll",
                "CebizPay.Workers")
            .GetResult();

        var appResult = Types.InAssembly(typeof(Application.AssemblyReference).Assembly)
            .ShouldNot()
            .HaveDependencyOnAny(
                "CebizPay.Infrastructure.Payroll",
                "CebizPay.Workers")
            .GetResult();

        Assert.True(domainResult.IsSuccessful, "Domain layer must not depend on Payroll infrastructure or workers.");
        Assert.True(appResult.IsSuccessful, "Application layer must not depend on Payroll infrastructure or workers.");
    }

    [Fact]
    public void DomainAndApplication_ShouldNotDependOnPaymentProviderOptionsOrSecrets()
    {
        var domainResult = Types.InAssembly(typeof(Domain.AssemblyReference).Assembly)
            .ShouldNot()
            .HaveDependencyOnAny(
                "CebizPay.Infrastructure.Payments.Flutterwave.FlutterwaveOptions",
                "CebizPay.Infrastructure.Payments.Paystack.PaystackOptions",
                "CebizPay.Infrastructure.Options.VtuGateOptions")
            .GetResult();

        var appResult = Types.InAssembly(typeof(Application.AssemblyReference).Assembly)
            .ShouldNot()
            .HaveDependencyOnAny(
                "CebizPay.Infrastructure.Payments.Flutterwave.FlutterwaveOptions",
                "CebizPay.Infrastructure.Payments.Paystack.PaystackOptions",
                "CebizPay.Infrastructure.Options.VtuGateOptions")
            .GetResult();

        Assert.True(domainResult.IsSuccessful, "Domain layer must not depend on payment provider options or secrets.");
        Assert.True(appResult.IsSuccessful, "Application layer must not depend on payment provider options or secrets.");
    }

    [Fact]
    public void ApiControllers_ShouldNotDirectlyDependOnProviderOptionsOrSecrets()
    {
        var apiResult = Types.InAssembly(typeof(Api.Controllers.v1.StaffSavingsController).Assembly)
            .That()
            .ResideInNamespace("CebizPay.Api.Controllers")
            .ShouldNot()
            .HaveDependencyOnAny(
                "CebizPay.Infrastructure.Payments.Flutterwave.FlutterwaveOptions",
                "CebizPay.Infrastructure.Payments.Paystack.PaystackOptions",
                "CebizPay.Infrastructure.Options.VtuGateOptions")
            .GetResult();

        Assert.True(apiResult.IsSuccessful, "API Controllers must not directly depend on provider options or secrets.");
    }

    [Fact]
    public void DomainAndApplication_ShouldNotDependOnComplianceProviderImplementations()
    {
        var domainResult = Types.InAssembly(typeof(Domain.AssemblyReference).Assembly)
            .ShouldNot()
            .HaveDependencyOnAny(
                "CebizPay.Infrastructure.Compliance.Dojah",
                "CebizPay.Infrastructure.Compliance.SmileId",
                "CebizPay.Infrastructure.Compliance.Ninja",
                "CebizPay.Infrastructure.Compliance.Services",
                "CebizPay.Infrastructure.Compliance.Rules")
            .GetResult();

        var appResult = Types.InAssembly(typeof(Application.AssemblyReference).Assembly)
            .ShouldNot()
            .HaveDependencyOnAny(
                "CebizPay.Infrastructure.Compliance.Dojah",
                "CebizPay.Infrastructure.Compliance.SmileId",
                "CebizPay.Infrastructure.Compliance.Ninja",
                "CebizPay.Infrastructure.Compliance.Services",
                "CebizPay.Infrastructure.Compliance.Rules")
            .GetResult();

        Assert.True(domainResult.IsSuccessful, "Domain layer must not depend on compliance provider or infrastructure implementations.");
        Assert.True(appResult.IsSuccessful, "Application layer must not depend on compliance provider or infrastructure implementations.");
    }

    [Fact]
    public void ApiControllers_ShouldNotDirectlyDependOnComplianceProviderOptions()
    {
        var apiResult = Types.InAssembly(typeof(Api.Controllers.v1.StaffSavingsController).Assembly)
            .That()
            .ResideInNamespace("CebizPay.Api.Controllers")
            .ShouldNot()
            .HaveDependencyOnAny(
                "CebizPay.Infrastructure.Compliance.Dojah.DojahOptions",
                "CebizPay.Infrastructure.Compliance.SmileId.SmileIdOptions",
                "CebizPay.Infrastructure.Compliance.Ninja.NinjaOptions")
            .GetResult();

        Assert.True(apiResult.IsSuccessful, "API Controllers must not directly depend on compliance provider options.");
    }

    [Fact]
    public void LedgerDomain_ShouldNotDependOnComplianceRiskRules()
    {
        var ledgerResult = Types.InAssembly(typeof(Domain.AssemblyReference).Assembly)
            .That()
            .ResideInNamespace("CebizPay.Domain.Finance")
            .ShouldNot()
            .HaveDependencyOnAny(
                "CebizPay.Domain.Compliance.Entities.RiskAssessment",
                "CebizPay.Domain.Compliance.Entities.EddCase")
            .GetResult();

        Assert.True(ledgerResult.IsSuccessful, "Finance Ledger domain must remain isolated from compliance risk assessments and EDD cases.");
    }

    [Fact]
    public void DomainAndApplication_ShouldNotDependOnFirebaseAdminSdk()
    {
        var domainResult = Types.InAssembly(typeof(Domain.AssemblyReference).Assembly)
            .ShouldNot()
            .HaveDependencyOnAny("FirebaseAdmin", "Google.Apis")
            .GetResult();

        var appResult = Types.InAssembly(typeof(Application.AssemblyReference).Assembly)
            .ShouldNot()
            .HaveDependencyOnAny("FirebaseAdmin", "Google.Apis")
            .GetResult();

        Assert.True(domainResult.IsSuccessful, "Domain layer must not reference FirebaseAdmin SDK.");
        Assert.True(appResult.IsSuccessful, "Application layer must not reference FirebaseAdmin SDK.");
    }

    [Fact]
    public void ReferralDomain_ShouldNotDependOnLedgerOrWalletPersistence()
    {
        var domainResult = Types.InNamespace("CebizPay.Domain.Referrals")
            .ShouldNot()
            .HaveDependencyOnAny(
                "Microsoft.EntityFrameworkCore",
                "Npgsql",
                "CebizPay.Infrastructure")
            .GetResult();

        Assert.True(domainResult.IsSuccessful, "Referral domain must remain pure and free from infrastructure/EF Core/Npgsql dependencies.");
    }
}



