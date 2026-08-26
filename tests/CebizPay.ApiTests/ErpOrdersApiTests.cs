#pragma warning disable CS1591
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
/// API integration tests for Phase 5D ERP endpoints (Orders, Expenses, Invoices, Receipts).
/// </summary>
public sealed class ErpOrdersApiTests
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
                    services.AddControllers().AddApplicationPart(typeof(OrgOrdersController).Assembly);
                    services.AddAuthentication("TestScheme")
                            .AddScheme<AuthenticationSchemeOptions, TestOrdersAuthHandler>("TestScheme", _ => { });
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
    public async Task PurchaseOrders_Create_Get_Confirm_Receive_Endpoints_WorkCorrectly()
    {
        var sender = Substitute.For<ISender>();
        var orgContext = Substitute.For<ICurrentOrganizationContext>();
        var userContext = Substitute.For<ICurrentUserService>();

        var orgId = Guid.NewGuid();
        var poId = Guid.NewGuid();
        var itemId = Guid.NewGuid();

        orgContext.CurrentOrganizationId.Returns(orgId);
        sender.Send(Arg.Any<CreatePurchaseOrderCommand>(), Arg.Any<CancellationToken>()).Returns(poId);
        sender.Send(Arg.Any<GetPurchaseOrdersQuery>(), Arg.Any<CancellationToken>())
            .Returns(new PagedResult<PurchaseOrderDto>(new List<PurchaseOrderDto>(), 0, 1, 20));
        sender.Send(Arg.Any<GetPurchaseOrderByIdQuery>(), Arg.Any<CancellationToken>())
            .Returns(new PurchaseOrderDto(poId, orgId, "PO-001", Guid.NewGuid(), DateTime.UtcNow, null, PurchaseOrderStatus.Draft, 1000m, 0m, 1000m, Currency.NGN, null, "user-1", DateTime.UtcNow, null, new List<PurchaseOrderItemDto>()));

        var (host, client) = await CreateTestServer(sender, orgContext, userContext);
        using (host)
        {
            // 1. Create Purchase Order
            var payload = new CreatePurchaseOrderApiRequest(
                Guid.NewGuid(),
                DateTime.UtcNow,
                null,
                Currency.NGN,
                "PO notes",
                new List<PurchaseOrderItemRequest> { new("Item line", 10, 100m) });

            var createRes = await client.PostAsJsonAsync("/api/v1/org/orders/purchase", payload);
            Assert.Equal(HttpStatusCode.Created, createRes.StatusCode);

            // 2. Get Paged Purchase Orders
            var getRes = await client.GetAsync("/api/v1/org/orders/purchase");
            Assert.Equal(HttpStatusCode.OK, getRes.StatusCode);

            // 3. Get By ID
            var getByIdRes = await client.GetAsync($"/api/v1/org/orders/purchase/{poId}");
            Assert.Equal(HttpStatusCode.OK, getByIdRes.StatusCode);

            // 4. Confirm
            var confirmRes = await client.PostAsync($"/api/v1/org/orders/purchase/{poId}/confirm", null);
            Assert.Equal(HttpStatusCode.OK, confirmRes.StatusCode);

            // 5. Receive item
            var receivePayload = new ReceivePurchaseOrderItemApiRequest(10);
            var receiveRes = await client.PostAsJsonAsync($"/api/v1/org/orders/purchase/{poId}/items/{itemId}/receive", receivePayload);
            Assert.Equal(HttpStatusCode.OK, receiveRes.StatusCode);

            // 6. Cancel
            var cancelRes = await client.PostAsync($"/api/v1/org/orders/purchase/{poId}/cancel", null);
            Assert.Equal(HttpStatusCode.OK, cancelRes.StatusCode);
        }
    }

    [Fact]
    public async Task SalesOrders_Create_Get_Confirm_Fulfill_Endpoints_WorkCorrectly()
    {
        var sender = Substitute.For<ISender>();
        var orgContext = Substitute.For<ICurrentOrganizationContext>();
        var userContext = Substitute.For<ICurrentUserService>();

        var orgId = Guid.NewGuid();
        var soId = Guid.NewGuid();
        var itemId = Guid.NewGuid();

        orgContext.CurrentOrganizationId.Returns(orgId);
        sender.Send(Arg.Any<CreateSalesOrderCommand>(), Arg.Any<CancellationToken>()).Returns(soId);
        sender.Send(Arg.Any<GetSalesOrdersQuery>(), Arg.Any<CancellationToken>())
            .Returns(new PagedResult<SalesOrderDto>(new List<SalesOrderDto>(), 0, 1, 20));
        sender.Send(Arg.Any<GetSalesOrderByIdQuery>(), Arg.Any<CancellationToken>())
            .Returns(new SalesOrderDto(soId, orgId, "SO-001", Guid.NewGuid(), DateTime.UtcNow, null, SalesOrderStatus.Draft, 2000m, 0m, 2000m, Currency.NGN, null, "user-1", DateTime.UtcNow, null, new List<SalesOrderItemDto>()));

        var (host, client) = await CreateTestServer(sender, orgContext, userContext);
        using (host)
        {
            // 1. Create Sales Order
            var payload = new CreateSalesOrderApiRequest(
                Guid.NewGuid(),
                DateTime.UtcNow,
                null,
                Currency.NGN,
                "SO notes",
                new List<SalesOrderItemRequest> { new("Widget item", 5, 400m) });

            var createRes = await client.PostAsJsonAsync("/api/v1/org/orders/sales", payload);
            Assert.Equal(HttpStatusCode.Created, createRes.StatusCode);

            // 2. Get Paged Sales Orders
            var getRes = await client.GetAsync("/api/v1/org/orders/sales");
            Assert.Equal(HttpStatusCode.OK, getRes.StatusCode);

            // 3. Get By ID
            var getByIdRes = await client.GetAsync($"/api/v1/org/orders/sales/{soId}");
            Assert.Equal(HttpStatusCode.OK, getByIdRes.StatusCode);

            // 4. Confirm
            var confirmRes = await client.PostAsync($"/api/v1/org/orders/sales/{soId}/confirm", null);
            Assert.Equal(HttpStatusCode.OK, confirmRes.StatusCode);

            // 5. Fulfill item
            var fulfillPayload = new FulfillSalesOrderItemApiRequest(5);
            var fulfillRes = await client.PostAsJsonAsync($"/api/v1/org/orders/sales/{soId}/items/{itemId}/fulfill", fulfillPayload);
            Assert.Equal(HttpStatusCode.OK, fulfillRes.StatusCode);

            // 6. Cancel
            var cancelRes = await client.PostAsync($"/api/v1/org/orders/sales/{soId}/cancel", null);
            Assert.Equal(HttpStatusCode.OK, cancelRes.StatusCode);
        }
    }

    [Fact]
    public async Task Expenses_Create_Get_Approve_Pay_Endpoints_WorkCorrectly()
    {
        var sender = Substitute.For<ISender>();
        var orgContext = Substitute.For<ICurrentOrganizationContext>();
        var userContext = Substitute.For<ICurrentUserService>();

        var orgId = Guid.NewGuid();
        var expId = Guid.NewGuid();

        orgContext.CurrentOrganizationId.Returns(orgId);
        sender.Send(Arg.Any<CreateOperatingExpenseCommand>(), Arg.Any<CancellationToken>()).Returns(expId);
        sender.Send(Arg.Any<GetOperatingExpensesQuery>(), Arg.Any<CancellationToken>())
            .Returns(new PagedResult<OperatingExpenseDto>(new List<OperatingExpenseDto>(), 0, 1, 20));
        sender.Send(Arg.Any<GetOperatingExpenseByIdQuery>(), Arg.Any<CancellationToken>())
            .Returns(new OperatingExpenseDto(expId, orgId, "EXP-001", ExpenseCategory.Utilities, "Electricity", 15000m, Currency.NGN, DateTime.UtcNow, null, ExpensePaymentMethod.Manual, ExpenseStatus.Draft, null, null, null, "user-1", null, null, null, DateTime.UtcNow, null));

        var (host, client) = await CreateTestServer(sender, orgContext, userContext);
        using (host)
        {
            // 1. Create Expense
            var payload = new CreateOperatingExpenseApiRequest(
                ExpenseCategory.Utilities,
                "Electricity",
                15000m,
                DateTime.UtcNow,
                ExpensePaymentMethod.Manual);

            var createRes = await client.PostAsJsonAsync("/api/v1/org/expenses", payload);
            Assert.Equal(HttpStatusCode.Created, createRes.StatusCode);

            // 2. Get Paged Expenses
            var getRes = await client.GetAsync("/api/v1/org/expenses");
            Assert.Equal(HttpStatusCode.OK, getRes.StatusCode);

            // 3. Get By ID
            var getByIdRes = await client.GetAsync($"/api/v1/org/expenses/{expId}");
            Assert.Equal(HttpStatusCode.OK, getByIdRes.StatusCode);

            // 4. Approve
            var approveRes = await client.PostAsync($"/api/v1/org/expenses/{expId}/approve", null);
            Assert.Equal(HttpStatusCode.OK, approveRes.StatusCode);

            // 5. Pay
            var payPayload = new PayOperatingExpenseApiRequest(ExpensePaymentMethod.Manual, Reference: "CASH-RECEIPT-01");
            var payRes = await client.PostAsJsonAsync($"/api/v1/org/expenses/{expId}/pay", payPayload);
            Assert.Equal(HttpStatusCode.OK, payRes.StatusCode);

            // 6. Cancel
            var cancelRes = await client.PostAsync($"/api/v1/org/expenses/{expId}/cancel", null);
            Assert.Equal(HttpStatusCode.OK, cancelRes.StatusCode);
        }
    }

    [Fact]
    public async Task Invoices_And_Receipts_Endpoints_WorkCorrectly()
    {
        var sender = Substitute.For<ISender>();
        var orgContext = Substitute.For<ICurrentOrganizationContext>();
        var userContext = Substitute.For<ICurrentUserService>();

        var orgId = Guid.NewGuid();
        var invId = Guid.NewGuid();
        var recId = Guid.NewGuid();

        orgContext.CurrentOrganizationId.Returns(orgId);
        sender.Send(Arg.Any<CreateInvoiceCommand>(), Arg.Any<CancellationToken>()).Returns(invId);
        sender.Send(Arg.Any<RecordInvoicePaymentCommand>(), Arg.Any<CancellationToken>()).Returns(recId);
        sender.Send(Arg.Any<GetInvoicesQuery>(), Arg.Any<CancellationToken>())
            .Returns(new PagedResult<ErpInvoiceDto>(new List<ErpInvoiceDto>(), 0, 1, 20));
        sender.Send(Arg.Any<GetInvoiceByIdQuery>(), Arg.Any<CancellationToken>())
            .Returns(new ErpInvoiceDto(invId, orgId, "INV-001", Guid.NewGuid(), null, DateTime.UtcNow, DateTime.UtcNow.AddDays(14), true, 0.075m, 100000m, 7500m, 107500m, 0m, Currency.NGN, InvoiceStatus.Draft, InvoiceSettlementMethod.Manual, null, null, "user-1", DateTime.UtcNow, null, new List<ErpInvoiceItemDto>()));
        sender.Send(Arg.Any<GetReceiptsQuery>(), Arg.Any<CancellationToken>())
            .Returns(new PagedResult<ErpReceiptDto>(new List<ErpReceiptDto>(), 0, 1, 20));
        sender.Send(Arg.Any<GetReceiptByIdQuery>(), Arg.Any<CancellationToken>())
            .Returns(new ErpReceiptDto(recId, orgId, "REC-001", invId, Guid.NewGuid(), 107500m, Currency.NGN, DateTime.UtcNow, InvoiceSettlementMethod.Manual, "BANK-01", null, "user-1", DateTime.UtcNow));
        sender.Send(Arg.Any<GetReceiptByInvoiceIdQuery>(), Arg.Any<CancellationToken>())
            .Returns(new ErpReceiptDto(recId, orgId, "REC-001", invId, Guid.NewGuid(), 107500m, Currency.NGN, DateTime.UtcNow, InvoiceSettlementMethod.Manual, "BANK-01", null, "user-1", DateTime.UtcNow));

        var (host, client) = await CreateTestServer(sender, orgContext, userContext);
        using (host)
        {
            // 1. Create Invoice
            var payload = new CreateInvoiceApiRequest(
                Guid.NewGuid(),
                DateTime.UtcNow,
                DateTime.UtcNow.AddDays(14),
                true,
                null,
                Currency.NGN,
                "Consulting",
                "finance@test.com",
                new List<InvoiceItemRequest> { new("Consulting", 1, 100000m) });

            var createRes = await client.PostAsJsonAsync("/api/v1/org/invoices", payload);
            Assert.Equal(HttpStatusCode.Created, createRes.StatusCode);

            // 2. Get Paged Invoices
            var getRes = await client.GetAsync("/api/v1/org/invoices");
            Assert.Equal(HttpStatusCode.OK, getRes.StatusCode);

            // 3. Get By ID
            var getByIdRes = await client.GetAsync($"/api/v1/org/invoices/{invId}");
            Assert.Equal(HttpStatusCode.OK, getByIdRes.StatusCode);

            // 4. Issue Invoice
            var issueRes = await client.PostAsync($"/api/v1/org/invoices/{invId}/issue", null);
            Assert.Equal(HttpStatusCode.OK, issueRes.StatusCode);

            // 5. Record Payment
            var payPayload = new RecordInvoicePaymentApiRequest(107500m, InvoiceSettlementMethod.Manual, "BANK-TX-100");
            var payRes = await client.PostAsJsonAsync($"/api/v1/org/invoices/{invId}/payments", payPayload);
            Assert.Equal(HttpStatusCode.OK, payRes.StatusCode);

            // 6. Cancel Invoice
            var cancelRes = await client.PostAsync($"/api/v1/org/invoices/{invId}/cancel", null);
            Assert.Equal(HttpStatusCode.OK, cancelRes.StatusCode);

            // 7. Get Receipts
            var getReceiptsRes = await client.GetAsync("/api/v1/org/receipts");
            Assert.Equal(HttpStatusCode.OK, getReceiptsRes.StatusCode);

            // 8. Get Receipt By ID
            var getReceiptByIdRes = await client.GetAsync($"/api/v1/org/receipts/{recId}");
            Assert.Equal(HttpStatusCode.OK, getReceiptByIdRes.StatusCode);

            // 9. Get Receipt By Invoice ID
            var getReceiptByInvRes = await client.GetAsync($"/api/v1/org/receipts/by-invoice/{invId}");
            Assert.Equal(HttpStatusCode.OK, getReceiptByInvRes.StatusCode);
        }
    }
}

public sealed class TestOrdersAuthHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    public TestOrdersAuthHandler(
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
            new Claim(ClaimTypes.NameIdentifier, "usr_test_erp_orders"),
            new Claim(ClaimTypes.Email, "orders_manager@test.com")
        };
        var identity = new ClaimsIdentity(claims, "TestScheme");
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, "TestScheme");

        return Task.FromResult(AuthenticateResult.Success(ticket));
    }
}
