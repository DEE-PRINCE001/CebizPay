#pragma warning disable CS1591
using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Encodings.Web;
using Asp.Versioning;
using CebizPay.Api.Controllers.v1;
using CebizPay.Application.Common.Interfaces.Payments;
using CebizPay.Application.UseCases.Reconciliation;
using CebizPay.Domain.Finance.Enums;
using CebizPay.Domain.Payments.Enums;
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

public sealed class AdminReconciliationApiTests
{
    private static async Task<(IHost host, HttpClient client)> CreateTestServer(IMediator mediator)
    {
        var host = await new HostBuilder()
            .ConfigureWebHost(webBuilder =>
            {
                webBuilder.UseTestServer();
                webBuilder.ConfigureServices(services =>
                {
                    services.AddControllers().AddApplicationPart(typeof(AdminReconciliationController).Assembly);
                    services.AddAuthentication("AdminTestScheme")
                            .AddScheme<AuthenticationSchemeOptions, AdminReconTestAuthHandler>("AdminTestScheme", _ => { });
                    services.AddAuthorization();
                    services.AddApiVersioning(options =>
                    {
                        options.DefaultApiVersion = new ApiVersion(1, 0);
                        options.AssumeDefaultVersionWhenUnspecified = true;
                        options.ReportApiVersions = true;
                        options.ApiVersionReader = new UrlSegmentApiVersionReader();
                    });
                    services.AddSingleton(mediator);
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
    public async Task GetRecords_Returns200WithRecordsList()
    {
        var mediator = Substitute.For<IMediator>();
        var sampleList = new List<ReconciliationRecordDto>
        {
            new(
                Id: Guid.NewGuid(),
                ReconciliationType: ReconciliationType.PaymentAttempt,
                SourceReference: "CBZ-TX-001",
                Provider: "Flutterwave",
                ProviderReference: "FLW_123",
                ExpectedAmount: 10000m,
                ReconciledAmount: 10000m,
                Currency: Currency.NGN,
                Status: ReconciliationStatus.ResolvedSuccess,
                DiscrepancyReason: null,
                AttemptCount: 1,
                MaxAttempts: 5,
                NextPollAtUtc: null,
                LastPolledAtUtc: DateTime.UtcNow,
                ResolvedAtUtc: DateTime.UtcNow,
                CreatedAtUtc: DateTime.UtcNow,
                UpdatedAtUtc: DateTime.UtcNow)
        };

        mediator.Send(Arg.Any<GetReconciliationRecordsQuery>(), Arg.Any<CancellationToken>())
            .Returns(sampleList);

        var (host, client) = await CreateTestServer(mediator);
        using (host)
        {
            var response = await client.GetAsync("/api/v1/admin/reconciliation/records");
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);

            var items = await response.Content.ReadFromJsonAsync<IReadOnlyList<ReconciliationRecordDto>>();
            Assert.NotNull(items);
            Assert.Single(items);
            Assert.Equal("CBZ-TX-001", items[0].SourceReference);
        }
    }

    [Fact]
    public async Task GetRecoveries_Returns200WithOutstandingList()
    {
        var mediator = Substitute.For<IMediator>();
        var sampleList = new List<RecoveryOutstandingRecordDto>
        {
            new(
                Id: Guid.NewGuid(),
                WalletId: Guid.NewGuid(),
                SourceTransactionType: "CardRefund",
                SourceReference: "REF-001",
                Provider: PaymentProvider.Flutterwave,
                AmountOwed: 5000m,
                AmountRecovered: 0m,
                Currency: Currency.NGN,
                Reason: "Dispute chargeback",
                Status: RecoveryStatus.Pending,
                CreatedAtUtc: DateTime.UtcNow,
                ResolvedAtUtc: null,
                LastActionDetails: null)
        };

        mediator.Send(Arg.Any<GetOutstandingRecoveriesQuery>(), Arg.Any<CancellationToken>())
            .Returns(sampleList);

        var (host, client) = await CreateTestServer(mediator);
        using (host)
        {
            var response = await client.GetAsync("/api/v1/admin/reconciliation/recoveries");
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);

            var items = await response.Content.ReadFromJsonAsync<IReadOnlyList<RecoveryOutstandingRecordDto>>();
            Assert.NotNull(items);
            Assert.Single(items);
            Assert.Equal("REF-001", items[0].SourceReference);
        }
    }

    [Fact]
    public async Task RequeryStatus_WithValidReference_Returns200WithResult()
    {
        var mediator = Substitute.For<IMediator>();
        var result = UnifiedReconciliationResult.Succeeded("CBZ-REQ-001", "Monnify", "MNFY_123", 15000m);

        mediator.Send(Arg.Any<RequeryPaymentStatusCommand>(), Arg.Any<CancellationToken>())
            .Returns(result);

        var (host, client) = await CreateTestServer(mediator);
        using (host)
        {
            var response = await client.PostAsJsonAsync("/api/v1/admin/reconciliation/requery", new RequeryStatusRequest("CBZ-REQ-001"));
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);

            var body = await response.Content.ReadFromJsonAsync<UnifiedReconciliationResult>();
            Assert.NotNull(body);
            Assert.Equal(ReconciliationOutcome.Success, body.Outcome);
        }
    }

    [Fact]
    public async Task SubmitManualReview_WithValidDecision_Returns200()
    {
        var mediator = Substitute.For<IMediator>();
        var result = UnifiedReconciliationResult.Succeeded("CBZ-TX-001", "Paystack", "PSTK_999", 5000m);

        mediator.Send(Arg.Any<SubmitManualReviewDecisionCommand>(), Arg.Any<CancellationToken>())
            .Returns(result);

        var (host, client) = await CreateTestServer(mediator);
        using (host)
        {
            var recordId = Guid.NewGuid();
            var response = await client.PostAsJsonAsync($"/api/v1/admin/reconciliation/records/{recordId}/review", new SubmitReviewRequest(
                ManualReviewDecision.ConfirmSuccess,
                "Bank credit advice verified"));

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }
    }
}

public sealed class AdminReconTestAuthHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    public AdminReconTestAuthHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder) : base(options, logger, encoder) { }

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, "ADMIN_RECON_TEST"),
            new Claim(ClaimTypes.Role, "SuperAdmin")
        };
        var identity = new ClaimsIdentity(claims, "AdminTestScheme");
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, "AdminTestScheme");

        return Task.FromResult(AuthenticateResult.Success(ticket));
    }
}
