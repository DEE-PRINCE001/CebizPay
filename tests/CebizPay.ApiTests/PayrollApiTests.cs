using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Encodings.Web;
using System.Text.Json;
using Asp.Versioning;
using CebizPay.Api.Controllers.v1;
using CebizPay.Application.Common.Interfaces.Payroll;
using CebizPay.Application.Common.Interfaces.Security;
using CebizPay.Domain.Finance.Enums;
using CebizPay.Domain.Payroll.Enums;
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

public sealed class PayrollApiTests
{
    private static async Task<(IHost host, HttpClient client)> CreateTestServer(
        IPayrollCalculationService calculationService,
        IPayrollBatchService batchService,
        ICurrentUserService currentUserService,
        ICurrentOrganizationContext orgContext)
    {
        var host = await new HostBuilder()
            .ConfigureWebHost(webBuilder =>
            {
                webBuilder.UseTestServer();
                webBuilder.ConfigureServices(services =>
                {
                    services.AddControllers().AddApplicationPart(typeof(PayrollController).Assembly);
                    services.AddAuthentication("TestScheme")
                            .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>("TestScheme", _ => { });
                    services.AddAuthorization();
                    services.AddApiVersioning(options =>
                    {
                        options.DefaultApiVersion = new ApiVersion(1, 0);
                        options.AssumeDefaultVersionWhenUnspecified = true;
                        options.ReportApiVersions = true;
                        options.ApiVersionReader = new UrlSegmentApiVersionReader();
                    });
                    services.AddSingleton(calculationService);
                    services.AddSingleton(batchService);
                    services.AddSingleton(currentUserService);
                    services.AddSingleton(orgContext);
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

        var client = host.GetTestClient();
        return (host, client);
    }

    [Fact]
    public async Task Calculate_Returns200OkWithCalculatedDryRun()
    {
        var calcService = Substitute.For<IPayrollCalculationService>();
        var batchService = Substitute.For<IPayrollBatchService>();
        var userService = Substitute.For<ICurrentUserService>();
        var orgContext = Substitute.For<ICurrentOrganizationContext>();

        var orgId = Guid.NewGuid();
        orgContext.CurrentOrganizationId.Returns(orgId);
        userService.UserId.Returns("usr_ceo");

        var expectedResult = new PayrollCalculationResultDto(
            OrganizationId: orgId,
            Currency: Currency.NGN,
            TotalEmployees: 1,
            TotalGrossAmount: 500000m,
            TotalDeductionsAmount: 50000m,
            TotalNetAmount: 450000m,
            Items: new[]
            {
                new PayrollCalculationItemDto("emp_1", "Alice", "alice@cebizpay.internal", null, null, null, null, null, null, 500000m, 50000m, 450000m, Currency.NGN, null)
            });

        calcService.CalculatePayrollAsync(orgId, Currency.NGN, Arg.Any<PayrollSelectionCriteria>(), Arg.Any<CancellationToken>())
            .Returns(expectedResult);

        var (host, client) = await CreateTestServer(calcService, batchService, userService, orgContext);
        using (host)
        {
            var response = await client.PostAsJsonAsync("/api/v1/org/payroll/calculate", new CalculatePayrollApiRequest(Currency.NGN, null));

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var content = await response.Content.ReadFromJsonAsync<PayrollCalculationResultDto>();
            Assert.NotNull(content);
            Assert.Equal(450000m, content.TotalNetAmount);
            Assert.Equal(1, content.TotalEmployees);
        }
    }

    [Fact]
    public async Task Execute_Returns202AcceptedWithBatchDetails()
    {
        var calcService = Substitute.For<IPayrollCalculationService>();
        var batchService = Substitute.For<IPayrollBatchService>();
        var userService = Substitute.For<ICurrentUserService>();
        var orgContext = Substitute.For<ICurrentOrganizationContext>();

        var orgId = Guid.NewGuid();
        orgContext.CurrentOrganizationId.Returns(orgId);
        userService.UserId.Returns("usr_ceo");

        var batchId = Guid.NewGuid();
        var expectedBatch = new PayrollBatchDto(
            BatchId: batchId,
            BatchReference: "PB-202608-ABC12345",
            OrganizationId: orgId,
            Currency: Currency.NGN,
            Status: PayrollBatchStatus.Pending,
            TotalEmployees: 10,
            TotalGrossAmount: 5000000m,
            TotalDeductionsAmount: 500000m,
            TotalNetAmount: 4500000m,
            PeriodStart: DateTime.UtcNow.AddDays(-30),
            PeriodEnd: DateTime.UtcNow,
            CreatedAtUtc: DateTime.UtcNow);

        batchService.CreateAndEnqueueBatchAsync(orgId, "usr_ceo", Currency.NGN, Arg.Any<DateTime>(), Arg.Any<DateTime>(), Arg.Any<PayrollSelectionCriteria>(), Arg.Any<CancellationToken>())
            .Returns(expectedBatch);

        var (host, client) = await CreateTestServer(calcService, batchService, userService, orgContext);
        using (host)
        {
            var req = new ExecutePayrollApiRequest(Currency.NGN, DateTime.UtcNow.AddDays(-30), DateTime.UtcNow, null);
            var response = await client.PostAsJsonAsync("/api/v1/org/payroll/execute", req);

            Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
            var content = await response.Content.ReadFromJsonAsync<PayrollBatchDto>();
            Assert.NotNull(content);
            Assert.Equal("PB-202608-ABC12345", content.BatchReference);
            Assert.Equal(PayrollBatchStatus.Pending, content.Status);
        }
    }

    [Fact]
    public async Task GetProgress_Returns200OkWithBatchProgress()
    {
        var calcService = Substitute.For<IPayrollCalculationService>();
        var batchService = Substitute.For<IPayrollBatchService>();
        var userService = Substitute.For<ICurrentUserService>();
        var orgContext = Substitute.For<ICurrentOrganizationContext>();

        var orgId = Guid.NewGuid();
        var batchId = Guid.NewGuid();
        orgContext.CurrentOrganizationId.Returns(orgId);

        var progressDto = new PayrollBatchProgressDto(
            BatchId: batchId,
            BatchReference: "PB-202608-ABC",
            OrganizationId: orgId,
            Currency: Currency.NGN,
            Status: PayrollBatchStatus.Processing,
            TotalEmployees: 10,
            CompletedCount: 7,
            ProcessingCount: 2,
            PendingCount: 1,
            FailedCount: 0,
            RetryPendingCount: 0,
            ProgressPercentage: 70m,
            TotalGrossAmount: 5000000m,
            TotalDeductionsAmount: 0m,
            TotalNetAmount: 5000000m,
            CreatedAtUtc: DateTime.UtcNow,
            StartedAtUtc: DateTime.UtcNow,
            CompletedAtUtc: null,
            FailureReason: null,
            Items: Array.Empty<PayrollItemProgressDto>());

        batchService.GetBatchProgressAsync(orgId, batchId, 1, 50, Arg.Any<CancellationToken>())
            .Returns(progressDto);

        var (host, client) = await CreateTestServer(calcService, batchService, userService, orgContext);
        using (host)
        {
            var response = await client.GetAsync($"/api/v1/org/payroll/{batchId}");
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);

            var content = await response.Content.ReadFromJsonAsync<PayrollBatchProgressDto>();
            Assert.NotNull(content);
            Assert.Equal(70m, content.ProgressPercentage);
            Assert.Equal(7, content.CompletedCount);
        }
    }

    [Fact]
    public async Task UpdateVoucherMetadata_Returns200OkWithUpdatedVoucher()
    {
        var calcService = Substitute.For<IPayrollCalculationService>();
        var batchService = Substitute.For<IPayrollBatchService>();
        var userService = Substitute.For<ICurrentUserService>();
        var orgContext = Substitute.For<ICurrentOrganizationContext>();

        var orgId = Guid.NewGuid();
        var voucherId = Guid.NewGuid();
        orgContext.CurrentOrganizationId.Returns(orgId);
        userService.UserId.Returns("usr_ceo");

        var updatedVoucher = new PaymentVoucherDto(
            Id: voucherId,
            VoucherReference: "PV-202608-ABC",
            PayrollBatchId: Guid.NewGuid(),
            PayrollItemId: Guid.NewGuid(),
            LedgerTransactionId: Guid.NewGuid(),
            OrganizationId: orgId,
            EmployeeUserId: "emp_1",
            EmployeeName: "Alice",
            GrossPay: 500000m,
            Deductions: 0m,
            NetPay: 500000m,
            Currency: Currency.NGN,
            Status: VoucherStatus.Generated,
            BankName: "Zenith Bank",
            Remarks: "Approved Bonus",
            Description: null,
            CreatedAtUtc: DateTime.UtcNow,
            UpdatedAtUtc: DateTime.UtcNow);

        batchService.UpdatePaymentVoucherMetadataAsync(orgId, voucherId, "usr_ceo", Arg.Any<UpdatePaymentVoucherMetadataRequest>(), Arg.Any<CancellationToken>())
            .Returns(updatedVoucher);

        var (host, client) = await CreateTestServer(calcService, batchService, userService, orgContext);
        using (host)
        {
            var req = new UpdatePaymentVoucherMetadataRequest("Zenith Bank", "Approved Bonus", null);
            var response = await client.PutAsJsonAsync($"/api/v1/org/payroll/vouchers/{voucherId}", req);

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var content = await response.Content.ReadFromJsonAsync<PaymentVoucherDto>();
            Assert.NotNull(content);
            Assert.Equal("Zenith Bank", content.BankName);
            Assert.Equal("Approved Bonus", content.Remarks);
        }
    }

    private sealed class TestAuthHandler : AuthenticationHandler<AuthenticationSchemeOptions>
    {
        public TestAuthHandler(
            IOptionsMonitor<AuthenticationSchemeOptions> options,
            ILoggerFactory logger,
            UrlEncoder encoder) : base(options, logger, encoder) { }

        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            var claims = new[]
            {
                new Claim(ClaimTypes.NameIdentifier, "usr_ceo"),
                new Claim("sub", "usr_ceo"),
                new Claim(ClaimTypes.Role, "Admin")
            };
            var identity = new ClaimsIdentity(claims, "TestScheme");
            var principal = new ClaimsPrincipal(identity);
            var ticket = new AuthenticationTicket(principal, "TestScheme");
            return Task.FromResult(AuthenticateResult.Success(ticket));
        }
    }
}
