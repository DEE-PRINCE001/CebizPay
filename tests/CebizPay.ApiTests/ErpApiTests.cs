using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Encodings.Web;
using Asp.Versioning;
using CebizPay.Api.Controllers.v1;
using CebizPay.Application.Common.Interfaces.Security;
using CebizPay.Application.Common.Models;
using CebizPay.Application.UseCases.Organizations.Erp;
using CebizPay.Domain.Erp.Enums;
using CebizPay.Domain.Finance.Enums;
using MediatR;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NSubstitute;
using Xunit;

namespace CebizPay.ApiTests;

/// <summary>
/// API integration tests for Phase 5C ERP endpoints.
/// </summary>
public sealed class ErpApiTests
{
    private static async Task<(IHost host, HttpClient client)> CreateTestServer(
        ISender sender,
        ICurrentOrganizationContext orgContext,
        ICurrentUserService currentUserService)
    {
        var host = await new HostBuilder()
            .ConfigureWebHost(webBuilder =>
            {
                webBuilder.UseTestServer();
                webBuilder.ConfigureServices(services =>
                {
                    services.AddControllers().AddApplicationPart(typeof(OrgInventoryController).Assembly);
                    services.AddAuthentication("TestScheme")
                            .AddScheme<AuthenticationSchemeOptions, TestErpAuthHandler>("TestScheme", _ => { });
                    services.AddAuthorization();
                    services.AddApiVersioning(options =>
                    {
                        options.DefaultApiVersion = new ApiVersion(1, 0);
                        options.AssumeDefaultVersionWhenUnspecified = true;
                        options.ReportApiVersions = true;
                        options.ApiVersionReader = new UrlSegmentApiVersionReader();
                    });
                    services.AddSingleton(sender);
                    services.AddSingleton(orgContext);
                    services.AddSingleton(currentUserService);
                });
                webBuilder.Configure(app =>
                {
                    app.UseRouting();
                    app.UseAuthentication();
                    app.UseAuthorization();
                    app.UseEndpoints(endpoints =>
                    {
                        endpoints.MapControllers();
                    });
                });
            })
            .StartAsync();

        return (host, host.GetTestClient());
    }

    [Fact]
    public async Task CreateInventoryItem_ReturnsCreated_WithId()
    {
        var sender = Substitute.For<ISender>();
        var orgContext = Substitute.For<ICurrentOrganizationContext>();
        var userContext = Substitute.For<ICurrentUserService>();

        var orgId = Guid.NewGuid();
        var itemId = Guid.NewGuid();
        orgContext.CurrentOrganizationId.Returns(orgId);

        sender.Send(Arg.Any<CreateInventoryItemCommand>(), Arg.Any<CancellationToken>())
              .Returns(itemId);

        var (host, client) = await CreateTestServer(sender, orgContext, userContext);
        using (host)
        {
            var payload = new CreateInventoryItemApiRequest(
                Sku: "SKU-001",
                Name: "Widget Pro",
                UnitOfMeasure: "pcs",
                SellingPrice: 500m);

            var response = await client.PostAsJsonAsync("/api/v1/org/inventory/items", payload);
            Assert.Equal(HttpStatusCode.Created, response.StatusCode);

            var content = await response.Content.ReadFromJsonAsync<Guid>();
            Assert.Equal(itemId, content);
        }
    }

    [Fact]
    public async Task GetInventoryItems_ReturnsPagedResult()
    {
        var sender = Substitute.For<ISender>();
        var orgContext = Substitute.For<ICurrentOrganizationContext>();
        var userContext = Substitute.For<ICurrentUserService>();

        var orgId = Guid.NewGuid();
        orgContext.CurrentOrganizationId.Returns(orgId);

        var items = new List<InventoryItemDto>
        {
            new(Guid.NewGuid(), orgId, "SKU-A", "Item A", "desc", "cat", "pcs", Currency.NGN, 100m, 10m, 50m, 100m, 5000m, StockStatus.InStock, InventoryItemStatus.Active, DateTime.UtcNow, null)
        };
        var pagedResult = new PagedResult<InventoryItemDto>(items, 1, 1, 20);

        sender.Send(Arg.Any<GetInventoryItemsQuery>(), Arg.Any<CancellationToken>())
              .Returns(pagedResult);

        var (host, client) = await CreateTestServer(sender, orgContext, userContext);
        using (host)
        {
            var response = await client.GetAsync("/api/v1/org/inventory/items");
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);

            var result = await response.Content.ReadFromJsonAsync<PagedResult<InventoryItemDto>>();
            Assert.NotNull(result);
            Assert.Single(result!.Items);
        }
    }

    [Fact]
    public async Task StockInAndStockOut_ReturnOkWithMovementId()
    {
        var sender = Substitute.For<ISender>();
        var orgContext = Substitute.For<ICurrentOrganizationContext>();
        var userContext = Substitute.For<ICurrentUserService>();

        var orgId = Guid.NewGuid();
        var itemId = Guid.NewGuid();
        var movementId = Guid.NewGuid();
        orgContext.CurrentOrganizationId.Returns(orgId);

        sender.Send(Arg.Any<StockInCommand>(), Arg.Any<CancellationToken>()).Returns(movementId);
        sender.Send(Arg.Any<StockOutCommand>(), Arg.Any<CancellationToken>()).Returns(movementId);

        var (host, client) = await CreateTestServer(sender, orgContext, userContext);
        using (host)
        {
            // Stock In
            var inPayload = new StockInApiRequest(50m, 100m, "PO-123");
            var inResponse = await client.PostAsJsonAsync($"/api/v1/org/inventory/items/{itemId}/stock-in", inPayload);
            Assert.Equal(HttpStatusCode.OK, inResponse.StatusCode);

            // Stock Out
            var outPayload = new StockOutApiRequest(20m, "SO-123");
            var outResponse = await client.PostAsJsonAsync($"/api/v1/org/inventory/items/{itemId}/stock-out", outPayload);
            Assert.Equal(HttpStatusCode.OK, outResponse.StatusCode);
        }
    }

    [Fact]
    public async Task ValuationPolicy_GetAndSet_ReturnOk()
    {
        var sender = Substitute.For<ISender>();
        var orgContext = Substitute.For<ICurrentOrganizationContext>();
        var userContext = Substitute.For<ICurrentUserService>();

        var orgId = Guid.NewGuid();
        orgContext.CurrentOrganizationId.Returns(orgId);

        var policyDto = new InventoryValuationPolicyDto(Guid.NewGuid(), orgId, ValuationMethod.Wac, 1, DateTime.UtcNow, null, true, "usr", DateTime.UtcNow);

        sender.Send(Arg.Any<GetValuationPolicyQuery>(), Arg.Any<CancellationToken>()).Returns(policyDto);
        sender.Send(Arg.Any<SetValuationPolicyCommand>(), Arg.Any<CancellationToken>()).Returns(policyDto);

        var (host, client) = await CreateTestServer(sender, orgContext, userContext);
        using (host)
        {
            // GET
            var getResponse = await client.GetAsync("/api/v1/org/inventory/valuation-policy");
            Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);

            // POST
            var postPayload = new SetValuationPolicyApiRequest(ValuationMethod.Fifo);
            var postResponse = await client.PostAsJsonAsync("/api/v1/org/inventory/valuation-policy", postPayload);
            Assert.Equal(HttpStatusCode.OK, postResponse.StatusCode);
        }
    }

    [Fact]
    public async Task Services_Suppliers_Customers_CreateEndpoints_ReturnCreated()
    {
        var sender = Substitute.For<ISender>();
        var orgContext = Substitute.For<ICurrentOrganizationContext>();
        var userContext = Substitute.For<ICurrentUserService>();

        var orgId = Guid.NewGuid();
        var entityId = Guid.NewGuid();
        orgContext.CurrentOrganizationId.Returns(orgId);

        sender.Send(Arg.Any<CreateErpServiceCommand>(), Arg.Any<CancellationToken>()).Returns(entityId);
        sender.Send(Arg.Any<CreateSupplierCommand>(), Arg.Any<CancellationToken>()).Returns(entityId);
        sender.Send(Arg.Any<CreateCustomerCommand>(), Arg.Any<CancellationToken>()).Returns(entityId);

        var (host, client) = await CreateTestServer(sender, orgContext, userContext);
        using (host)
        {
            // Service
            var srvPayload = new CreateErpServiceApiRequest("SRV-01", "Service", 1000m);
            var srvResponse = await client.PostAsJsonAsync("/api/v1/org/services", srvPayload);
            Assert.Equal(HttpStatusCode.Created, srvResponse.StatusCode);

            // Supplier
            var supPayload = new CreateSupplierApiRequest("SUP-01", "Supplier");
            var supResponse = await client.PostAsJsonAsync("/api/v1/org/suppliers", supPayload);
            Assert.Equal(HttpStatusCode.Created, supResponse.StatusCode);

            // Customer
            var custPayload = new CreateCustomerApiRequest("CUST-01", "Customer");
            var custResponse = await client.PostAsJsonAsync("/api/v1/org/customers", custPayload);
            Assert.Equal(HttpStatusCode.Created, custResponse.StatusCode);
        }
    }
}

public sealed class TestErpAuthHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    public TestErpAuthHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder)
        : base(options, logger, encoder)
    {
    }

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, "usr_test_erp_manager"),
            new Claim(ClaimTypes.Email, "erp_manager@test.com")
        };
        var identity = new ClaimsIdentity(claims, "TestScheme");
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, "TestScheme");

        return Task.FromResult(AuthenticateResult.Success(ticket));
    }
}
