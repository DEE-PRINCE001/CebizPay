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
}


