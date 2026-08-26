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
/// API integration tests for Phase 5E Company Vouchers and ERP Reports endpoints.
/// </summary>
public sealed class CompanyVouchersAndReportsApiTests
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
                    services.AddControllers().AddApplicationPart(typeof(OrgCompanyVouchersController).Assembly);
                    services.AddAuthentication("TestScheme")
                            .AddScheme<AuthenticationSchemeOptions, TestCompanyVouchersAuthHandler>("TestScheme", _ => { });
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
    public async Task CompanyVouchers_Endpoints_WorkCorrectly()
    {
        var sender = Substitute.For<ISender>();
        var orgContext = Substitute.For<ICurrentOrganizationContext>();
        var userContext = Substitute.For<ICurrentUserService>();

        var orgId = Guid.NewGuid();
        var voucherId = Guid.NewGuid();

        orgContext.CurrentOrganizationId.Returns(orgId);
        sender.Send(Arg.Any<CreateCompanyVoucherCommand>(), Arg.Any<CancellationToken>()).Returns(voucherId);
        sender.Send(Arg.Any<GetCompanyVouchersQuery>(), Arg.Any<CancellationToken>())
            .Returns(new PagedResult<CompanyVoucherDto>(new List<CompanyVoucherDto>(), 0, 1, 20));
        sender.Send(Arg.Any<GetCompanyVoucherByIdQuery>(), Arg.Any<CancellationToken>())
            .Returns(new CompanyVoucherDto(voucherId, orgId, "CV-001", "Payee", null, "Purpose", 50000m, Currency.NGN, CompanyVoucherPaymentMethod.Manual, CompanyVoucherStatus.Draft, "user-1", null, null, null, null, null, null, null, DateTime.UtcNow, null));

        var (host, client) = await CreateTestServer(sender, orgContext, userContext);
        using (host)
        {
            // 1. Create Company Voucher
            var payload = new CreateCompanyVoucherApiRequest("Payee", "Purpose", 50000m);
            var createRes = await client.PostAsJsonAsync("/api/v1/org/company-vouchers", payload);
            Assert.Equal(HttpStatusCode.Created, createRes.StatusCode);

            // 2. Get Paged Vouchers
            var getRes = await client.GetAsync("/api/v1/org/company-vouchers");
            Assert.Equal(HttpStatusCode.OK, getRes.StatusCode);

            // 3. Get Voucher By ID
            var getByIdRes = await client.GetAsync($"/api/v1/org/company-vouchers/{voucherId}");
            Assert.Equal(HttpStatusCode.OK, getByIdRes.StatusCode);

            // 4. Approve
            var approveRes = await client.PostAsync($"/api/v1/org/company-vouchers/{voucherId}/approve", null);
            Assert.Equal(HttpStatusCode.OK, approveRes.StatusCode);

            // 5. Pay
            var payPayload = new PayCompanyVoucherApiRequest(CompanyVoucherPaymentMethod.Manual, Reference: "CHQ-100");
            var payRes = await client.PostAsJsonAsync($"/api/v1/org/company-vouchers/{voucherId}/pay", payPayload);
            Assert.Equal(HttpStatusCode.OK, payRes.StatusCode);

            // 6. Cancel
            var cancelRes = await client.PostAsync($"/api/v1/org/company-vouchers/{voucherId}/cancel", null);
            Assert.Equal(HttpStatusCode.OK, cancelRes.StatusCode);
        }
    }

    [Fact]
    public async Task Reports_Endpoints_WorkCorrectly()
    {
        var sender = Substitute.For<ISender>();
        var orgContext = Substitute.For<ICurrentOrganizationContext>();
        var userContext = Substitute.For<ICurrentUserService>();

        var orgId = Guid.NewGuid();
        orgContext.CurrentOrganizationId.Returns(orgId);

        sender.Send(Arg.Any<GetSalesReportQuery>(), Arg.Any<CancellationToken>())
            .Returns(new SalesReportDto(orgId, null, null, 10, 2, 3, 1, 4, 0, new List<CurrencySalesSummaryDto>(), new List<CustomerSalesSummaryDto>(), new List<ItemSalesSummaryDto>()));
        sender.Send(Arg.Any<GetPurchaseReportQuery>(), Arg.Any<CancellationToken>())
            .Returns(new PurchaseReportDto(orgId, null, null, 5, 1, 1, 1, 2, 0, new List<CurrencyPurchaseSummaryDto>(), new List<SupplierPurchaseSummaryDto>(), new List<ItemPurchaseSummaryDto>()));
        sender.Send(Arg.Any<GetSettlementReportQuery>(), Arg.Any<CancellationToken>())
            .Returns(new SettlementReportDto(orgId, null, null, new List<CurrencySettlementSummaryDto>(), new PagedResult<SettlementItemDto>(new List<SettlementItemDto>(), 0, 1, 20)));
        sender.Send(Arg.Any<GetProfitLossReportQuery>(), Arg.Any<CancellationToken>())
            .Returns(new ProfitLossReportDto(orgId, null, null, new List<CurrencyProfitLossSummaryDto>()));

        var (host, client) = await CreateTestServer(sender, orgContext, userContext);
        using (host)
        {
            // 1. Sales Report
            var salesRes = await client.GetAsync("/api/v1/org/reports/sales");
            Assert.Equal(HttpStatusCode.OK, salesRes.StatusCode);

            // 2. Purchase Report
            var purchaseRes = await client.GetAsync("/api/v1/org/reports/purchases");
            Assert.Equal(HttpStatusCode.OK, purchaseRes.StatusCode);

            // 3. Settlement Report
            var settleRes = await client.GetAsync("/api/v1/org/reports/settlements");
            Assert.Equal(HttpStatusCode.OK, settleRes.StatusCode);

            // 4. Profit & Loss Report
            var plRes = await client.GetAsync("/api/v1/org/reports/profit-loss");
            Assert.Equal(HttpStatusCode.OK, plRes.StatusCode);
        }
    }
}

public sealed class TestCompanyVouchersAuthHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    public TestCompanyVouchersAuthHandler(
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
            new Claim(ClaimTypes.NameIdentifier, "usr_test_erp_vouchers"),
            new Claim(ClaimTypes.Email, "vouchers_manager@test.com")
        };
        var identity = new ClaimsIdentity(claims, "TestScheme");
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, "TestScheme");

        return Task.FromResult(AuthenticateResult.Success(ticket));
    }
}
