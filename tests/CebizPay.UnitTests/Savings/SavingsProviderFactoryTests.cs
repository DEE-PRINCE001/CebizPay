using CebizPay.Application.Common.Interfaces.Savings;
using CebizPay.Infrastructure.Options;
using CebizPay.Infrastructure.Savings.Providers;
using Microsoft.Extensions.Options;
using NSubstitute;

namespace CebizPay.UnitTests.Savings;

public sealed class SavingsProviderFactoryTests
{
    [Fact]
    public void GetProvider_WithExactName_ReturnsMatchingProvider()
    {
        var cowrywise = Substitute.For<ISavingsProvider>();
        cowrywise.ProviderName.Returns("Cowrywise");

        var anchor = Substitute.For<ISavingsProvider>();
        anchor.ProviderName.Returns("Anchor");

        var options = new SavingsOptions { ActiveProvider = "Cowrywise" };
        var factory = new SavingsProviderFactory([cowrywise, anchor], Microsoft.Extensions.Options.Options.Create(options));

        var resolved = factory.GetProvider("Cowrywise");
        Assert.Same(cowrywise, resolved);
    }

    [Fact]
    public void GetProvider_CaseInsensitive_ReturnsMatchingProvider()
    {
        var anchor = Substitute.For<ISavingsProvider>();
        anchor.ProviderName.Returns("Anchor");

        var options = new SavingsOptions { ActiveProvider = "Anchor" };
        var factory = new SavingsProviderFactory([anchor], Microsoft.Extensions.Options.Options.Create(options));

        var resolved = factory.GetProvider("anchor");
        Assert.Same(anchor, resolved);
    }

    [Fact]
    public void GetActiveProvider_ReturnsConfiguredDefault()
    {
        var mock = Substitute.For<ISavingsProvider>();
        mock.ProviderName.Returns("Mock");

        var cowrywise = Substitute.For<ISavingsProvider>();
        cowrywise.ProviderName.Returns("Cowrywise");

        var options = new SavingsOptions { ActiveProvider = "Cowrywise" };
        var factory = new SavingsProviderFactory([mock, cowrywise], Microsoft.Extensions.Options.Options.Create(options));

        var active = factory.GetActiveProvider();
        Assert.Same(cowrywise, active);
    }

    [Fact]
    public void GetProvider_WithUnknownName_ThrowsInvalidOperationException()
    {
        var mock = Substitute.For<ISavingsProvider>();
        mock.ProviderName.Returns("Mock");

        var options = new SavingsOptions { ActiveProvider = "Mock" };
        var factory = new SavingsProviderFactory([mock], Microsoft.Extensions.Options.Options.Create(options));

        Assert.Throws<InvalidOperationException>(() => factory.GetProvider("NonExistent"));
    }
}
