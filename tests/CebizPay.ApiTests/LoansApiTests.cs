using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Encodings.Web;
using System.Text.Json;
using Asp.Versioning;
using CebizPay.Api.Controllers.v1;
using CebizPay.Application.Common.Interfaces.Loans;
using CebizPay.Application.Common.Interfaces.Security;
using CebizPay.Domain.Loans.Enums;
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

public sealed class LoansApiTests
{
    private static async Task<(IHost host, HttpClient client)> CreateTestServer(
        ILoanPlanService planService,
        ILoanApplicationService applicationService,
        ILoanContractService contractService,
        ICurrentOrganizationContext? orgContext = null,
        ICurrentUserService? currentUserService = null)
    {
        if (orgContext == null)
        {
            var mockOrg = Substitute.For<ICurrentOrganizationContext>();
            var testOrgId = Guid.Parse("11111111-1111-1111-1111-111111111111");
            mockOrg.CurrentOrganizationId.Returns(testOrgId);
            mockOrg.HasPermissionAsync(Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(true);
            mockOrg.HasAccessToOrganizationAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns(true);
            orgContext = mockOrg;
        }

        if (currentUserService == null)
        {
            var mockUser = Substitute.For<ICurrentUserService>();
            mockUser.UserId.Returns("test-user-id");
            currentUserService = mockUser;
        }

        var host = await new HostBuilder()
            .ConfigureWebHost(webBuilder =>
            {
                webBuilder.UseTestServer();
                webBuilder.ConfigureServices(services =>
                {
                    services.AddControllers().AddApplicationPart(typeof(CorporateLoanPlansController).Assembly);
                    services.AddAuthentication("TestScheme")
                            .AddScheme<AuthenticationSchemeOptions, TestLoansAuthHandler>("TestScheme", _ => { });
                    services.AddAuthorization();
                    services.AddApiVersioning(options =>
                    {
                        options.DefaultApiVersion = new ApiVersion(1, 0);
                        options.AssumeDefaultVersionWhenUnspecified = true;
                        options.ReportApiVersions = true;
                        options.ApiVersionReader = new UrlSegmentApiVersionReader();
                    });
                    services.AddSingleton(planService);
                    services.AddSingleton(applicationService);
                    services.AddSingleton(contractService);
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
    public async Task CreatePlan_Returns201Created()
    {
        var planService = Substitute.For<ILoanPlanService>();
        var appService = Substitute.For<ILoanApplicationService>();
        var contractService = Substitute.For<ILoanContractService>();

        var expectedPlan = new CorporateLoanPlanDto(
            Id: Guid.NewGuid(),
            OrganizationId: Guid.Parse("11111111-1111-1111-1111-111111111111"),
            Name: "Staff Loan",
            Description: "Test",
            MinimumAmount: 100_000m,
            MaximumAmount: 1_000_000m,
            InterestRate: 0.10m,
            MinimumDurationMonths: 6,
            MaximumDurationMonths: 12,
            RepaymentFrequency: RepaymentFrequency.Monthly,
            MinimumMonthlySalary: 200_000m,
            IsActive: true,
            CreatedAtUtc: DateTime.UtcNow,
            UpdatedAtUtc: null);

        planService.CreatePlanAsync(Arg.Any<Guid>(), Arg.Any<CreateLoanPlanRequest>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(expectedPlan);

        var (host, client) = await CreateTestServer(planService, appService, contractService);
        using (host)
        {
            var request = new CreateLoanPlanRequest("Staff Loan", "Test", 100_000m, 1_000_000m, 0.10m, 6, 12, 200_000m, RepaymentFrequency.Monthly);
            var response = await client.PostAsJsonAsync("/api/v1/org/loan-plans", request);

            Assert.Equal(HttpStatusCode.Created, response.StatusCode);
            var dto = await response.Content.ReadFromJsonAsync<CorporateLoanPlanDto>();
            Assert.NotNull(dto);
            Assert.Equal("Staff Loan", dto.Name);
        }
    }

    [Fact]
    public async Task PreviewLoan_Returns200WithPreviewDetails()
    {
        var planService = Substitute.For<ILoanPlanService>();
        var appService = Substitute.For<ILoanApplicationService>();
        var contractService = Substitute.For<ILoanContractService>();

        var preview = new LoanCalculationPreviewDto(
            RequestedAmount: 500_000m,
            AnnualInterestRate: 0.10m,
            DurationMonths: 12,
            MonthlyPayment: 45_833.33m,
            TotalInterest: 50_000m,
            TotalRepayment: 550_000m,
            VerifiedSalary: 400_000m,
            ExistingMonthlyDebt: 0m,
            ProposedMonthlyPayment: 45_833.33m,
            TotalMonthlyDebt: 45_833.33m,
            DebtToIncomeRatio: 0.1146m,
            MaxAllowedMonthlyDebt: 132_000m,
            IsDtiCompliant: true,
            IsEligible: true,
            IneligibilityReason: null);

        appService.PreviewApplicationAsync(Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<LoanCalculationPreviewRequest>(), Arg.Any<CancellationToken>())
            .Returns(preview);

        var (host, client) = await CreateTestServer(planService, appService, contractService);
        using (host)
        {
            var request = new LoanCalculationPreviewRequest(Guid.NewGuid(), 500_000m, 12);
            var response = await client.PostAsJsonAsync("/api/v1/work/loans/preview", request);

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var dto = await response.Content.ReadFromJsonAsync<LoanCalculationPreviewDto>();
            Assert.NotNull(dto);
            Assert.True(dto.IsEligible);
            Assert.Equal(45_833.33m, dto.MonthlyPayment);
        }
    }

    [Fact]
    public async Task ApproveApplication_Returns200AndContractDto()
    {
        var planService = Substitute.For<ILoanPlanService>();
        var appService = Substitute.For<ILoanApplicationService>();
        var contractService = Substitute.For<ILoanContractService>();

        var contractDto = new LoanContractDto(
            Id: Guid.NewGuid(),
            ContractReference: "LC-202608-TEST",
            LoanApplicationId: Guid.NewGuid(),
            OrganizationId: Guid.Parse("11111111-1111-1111-1111-111111111111"),
            BorrowerUserId: "test-user-id",
            BorrowerName: "Staff User",
            LoanType: LoanType.CorporatePayrollLoan,
            OriginalPrincipal: 600_000m,
            InterestRate: 0.10m,
            TotalInterest: 60_000m,
            TotalRepayment: 660_000m,
            RepaymentFrequency: RepaymentFrequency.Monthly,
            NumberOfInstallments: 12,
            MonthlyInstallmentAmount: 55_000m,
            OutstandingPrincipal: 600_000m,
            TotalAmountPaid: 0m,
            StartDate: DateTime.UtcNow,
            ExpectedEndDate: DateTime.UtcNow.AddMonths(12),
            Status: LoanContractStatus.Active,
            DisbursementLedgerTransactionId: Guid.NewGuid(),
            DisbursedAtUtc: DateTime.UtcNow,
            ConvertedToContractId: null,
            ConvertedFromContractId: null,
            ConvertedAtUtc: null,
            ConversionReason: null,
            CreatedAtUtc: DateTime.UtcNow,
            RepaymentSchedule: []);

        appService.ApproveApplicationAsync(Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(contractDto);

        var (host, client) = await CreateTestServer(planService, appService, contractService);
        using (host)
        {
            var appId = Guid.NewGuid();
            var response = await client.PostAsync($"/api/v1/org/loans/applications/{appId}/approve", null);

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var dto = await response.Content.ReadFromJsonAsync<LoanContractDto>();
            Assert.NotNull(dto);
            Assert.Equal("LC-202608-TEST", dto.ContractReference);
        }
    }
}

public sealed class TestLoansAuthHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    public TestLoansAuthHandler(
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
            new Claim(ClaimTypes.NameIdentifier, "test-user-id"),
            new Claim("OrganizationId", "11111111-1111-1111-1111-111111111111"),
            new Claim(ClaimTypes.Role, "Admin")
        };
        var identity = new ClaimsIdentity(claims, "TestScheme");
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, "TestScheme");

        return Task.FromResult(AuthenticateResult.Success(ticket));
    }
}
