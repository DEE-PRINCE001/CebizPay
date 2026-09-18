#pragma warning disable CS1591
using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using System.Text.Encodings.Web;
using Asp.Versioning;
using CebizPay.Api.Controllers.v1;
using CebizPay.Application.Common.Models;
using CebizPay.Application.UseCases.Admin.Individuals;
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

public sealed class AdminIndividualsApiTests
{
    private static async Task<(IHost host, HttpClient client)> CreateTestServer(IMediator mediator, string role = "Admin")
    {
        var host = await new HostBuilder()
            .ConfigureWebHost(webBuilder =>
            {
                webBuilder.UseTestServer();
                webBuilder.ConfigureServices(services =>
                {
                    services.AddControllers().AddApplicationPart(typeof(AdminIndividualsController).Assembly);
                    services.AddAuthentication("AdminTestScheme")
                            .AddScheme<AuthenticationSchemeOptions, TestAdminIndivAuthHandler>("AdminTestScheme", _ => { });
                    services.AddAuthorization();
                    services.AddApiVersioning(options =>
                    {
                        options.DefaultApiVersion = new ApiVersion(1, 0);
                        options.AssumeDefaultVersionWhenUnspecified = true;
                        options.ReportApiVersions = true;
                        options.ApiVersionReader = new UrlSegmentApiVersionReader();
                    });
                    services.AddSingleton(mediator);
                    services.AddSingleton<ISender>(mediator);
                    services.AddSingleton(new TestAdminIndivRole(role));
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
    public async Task GetIndividualsDirectory_AsAdmin_Returns200Ok()
    {
        var mediator = Substitute.For<IMediator>();
        var pageResult = new PagedResult<AdminIndividualSummaryDto>(
            new List<AdminIndividualSummaryDto>
            {
                new(Guid.NewGuid(), "Johnson Mile", "Mile@gmail.com", "0815275927", "Staff", "Cebis Company", "Suspended", "https://storage.cebizpay.com/avatars/user_01.jpg", DateTime.UtcNow)
            }, 1, 1, 10);

        mediator.Send(Arg.Any<GetAdminIndividualsDirectoryQuery>(), Arg.Any<CancellationToken>())
            .Returns(pageResult);

        var (host, client) = await CreateTestServer(mediator, "Admin");
        using (host)
        using (client)
        {
            var response = await client.GetAsync("/api/v1/admin/individuals");
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);

            var body = await response.Content.ReadFromJsonAsync<PagedResult<AdminIndividualSummaryDto>>();
            Assert.NotNull(body);
            Assert.Equal(1, body.TotalCount);
            Assert.Equal("Johnson Mile", body.Items[0].Name);
            Assert.Equal("Suspended", body.Items[0].Status);
            Assert.Equal("Staff", body.Items[0].ProfessionalStatus);
        }
    }

    [Fact]
    public async Task GetIndividualById_WhenFound_Returns200Ok()
    {
        var mediator = Substitute.For<IMediator>();
        var id = Guid.NewGuid();
        var details = new AdminIndividualDetailsDto(
            id, "Mike Johnson", "Mike@gmail.com", "0815275927", "Active", "Staff", "Cebis Company",
            "https://storage.cebizpay.com/photos/mike_johnson.jpg", DateTime.UtcNow,
            new List<AdminIndividualCredentialDto>
            {
                new("doc-001", "National Identity Card", "NIN", "12345678901", "https://storage.cebizpay.com/docs/nin_card.pdf", DateTime.UtcNow)
            });

        mediator.Send(Arg.Any<GetAdminIndividualDetailsQuery>(), Arg.Any<CancellationToken>())
            .Returns(details);

        var (host, client) = await CreateTestServer(mediator, "SuperAdmin");
        using (host)
        using (client)
        {
            var response = await client.GetAsync($"/api/v1/admin/individuals/{id}");
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);

            var body = await response.Content.ReadFromJsonAsync<AdminIndividualDetailsDto>();
            Assert.NotNull(body);
            Assert.Equal(id, body.Id);
            Assert.Equal("Mike Johnson", body.Name);
            Assert.Equal("Active", body.Status);
            Assert.Single(body.Credentials);
        }
    }

    [Fact]
    public async Task GetIndividualTransactions_WhenFound_Returns200Ok()
    {
        var mediator = Substitute.For<IMediator>();
        var pageResult = new PagedResult<AdminIndividualTransactionItemDto>(
            new List<AdminIndividualTransactionItemDto>
            {
                new("tx-1029384756", "Johnson Mike", null, 25000.00m, "Send", "7817971681ID", "Wallet ID", "156191667631", DateTime.UtcNow, "Successfull")
            }, 1, 1, 10);

        mediator.Send(Arg.Any<GetAdminIndividualTransactionsQuery>(), Arg.Any<CancellationToken>())
            .Returns(pageResult);

        var (host, client) = await CreateTestServer(mediator, "Auditor");
        using (host)
        using (client)
        {
            var response = await client.GetAsync("/api/v1/admin/individuals/3fa85f64-5717-4562-b3fc-2c963f66afa6/transactions");
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);

            var body = await response.Content.ReadFromJsonAsync<PagedResult<AdminIndividualTransactionItemDto>>();
            Assert.NotNull(body);
            Assert.Equal(1, body.TotalCount);
            Assert.Equal("tx-1029384756", body.Items[0].Id);
            Assert.Equal("Successfull", body.Items[0].Status);
        }
    }

    [Fact]
    public async Task GetIndividualWallets_WhenFound_Returns200Ok()
    {
        var mediator = Substitute.For<IMediator>();
        var walletDto = new AdminIndividualWalletDto(
            "WAL-89234710", 450000.00m, 450000.00m, "NGN", 2, "0123456789", "Wema Bank / CebizPay", "Active");

        mediator.Send(Arg.Any<GetAdminIndividualWalletQuery>(), Arg.Any<CancellationToken>())
            .Returns(walletDto);

        var (host, client) = await CreateTestServer(mediator, "Admin");
        using (host)
        using (client)
        {
            var response = await client.GetAsync("/api/v1/admin/individuals/3fa85f64-5717-4562-b3fc-2c963f66afa6/wallets");
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);

            var body = await response.Content.ReadFromJsonAsync<AdminIndividualWalletDto>();
            Assert.NotNull(body);
            Assert.Equal("WAL-89234710", body.WalletId);
            Assert.Equal(450000.00m, body.AvailableBalance);
            Assert.Equal(2, body.Tier);
            Assert.Equal("Active", body.Status);
        }
    }

    [Fact]
    public async Task GetIndividualSavings_WhenFound_Returns200Ok()
    {
        var mediator = Substitute.For<IMediator>();
        var savingsList = new AdminIndividualSavingsListDto(
            new List<AdminIndividualSavingsItemDto>
            {
                new("sav-001", "Target Savings - New Car", 2000000.00m, 650000.00m, "Monthly", 12.5m, DateTime.UtcNow, DateTime.UtcNow.AddYears(1), "Active")
            }, 1);

        mediator.Send(Arg.Any<GetAdminIndividualSavingsQuery>(), Arg.Any<CancellationToken>())
            .Returns(savingsList);

        var (host, client) = await CreateTestServer(mediator, "Admin");
        using (host)
        using (client)
        {
            var response = await client.GetAsync("/api/v1/admin/individuals/3fa85f64-5717-4562-b3fc-2c963f66afa6/savings");
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);

            var body = await response.Content.ReadFromJsonAsync<AdminIndividualSavingsListDto>();
            Assert.NotNull(body);
            Assert.Equal(1, body.TotalCount);
            Assert.Equal("Target Savings - New Car", body.Items[0].Name);
        }
    }

    [Fact]
    public async Task ExportIndividuals_ReturnsCsvFile()
    {
        var mediator = Substitute.For<IMediator>();
        var csvBytes = Encoding.UTF8.GetBytes("Individual ID,Full Name\n1,Johnson Mile");
        var exportResult = new ExportAdminIndividualsResult(csvBytes, "text/csv; charset=utf-8", "individuals_export.csv");

        mediator.Send(Arg.Any<ExportAdminIndividualsQuery>(), Arg.Any<CancellationToken>())
            .Returns(exportResult);

        var (host, client) = await CreateTestServer(mediator, "Admin");
        using (host)
        using (client)
        {
            var response = await client.GetAsync("/api/v1/admin/individuals/export");
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Equal("text/csv", response.Content.Headers.ContentType?.MediaType);
            Assert.NotNull(response.Content.Headers.ContentDisposition);
            Assert.Equal("individuals_export.csv", response.Content.Headers.ContentDisposition.FileName);
        }
    }

    [Fact]
    public async Task GetIndividualsDirectory_WhenUnauthorizedRole_Returns403Forbidden()
    {
        var mediator = Substitute.For<IMediator>();
        var (host, client) = await CreateTestServer(mediator, "StandardCustomer");
        using (host)
        using (client)
        {
            var response = await client.GetAsync("/api/v1/admin/individuals");
            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        }
    }

    [Fact]
    public async Task SuspendIndividual_AsSuperAdmin_Returns200Ok()
    {
        var mediator = Substitute.For<IMediator>();
        var profileId = Guid.NewGuid();
        var statusResult = new AdminIndividualStatusResultDto(
            profileId, "user-01", "Suspended", true, DateTime.UtcNow, "Suspicious activity");

        mediator.Send(Arg.Any<SuspendIndividualCommand>(), Arg.Any<CancellationToken>())
            .Returns(statusResult);

        var (host, client) = await CreateTestServer(mediator, "SuperAdmin");
        using (host)
        using (client)
        {
            var response = await client.PatchAsJsonAsync(
                $"/api/v1/admin/individuals/{profileId}/suspend",
                new SuspendIndividualRequest("Suspicious activity"));

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);

            var body = await response.Content.ReadFromJsonAsync<AdminIndividualStatusResultDto>();
            Assert.NotNull(body);
            Assert.True(body.IsSuspended);
            Assert.Equal("Suspended", body.Status);
            Assert.Equal("Suspicious activity", body.SuspensionReason);
        }
    }

    [Fact]
    public async Task SuspendIndividual_AsAdmin_Returns200Ok()
    {
        var mediator = Substitute.For<IMediator>();
        var profileId = Guid.NewGuid();
        var statusResult = new AdminIndividualStatusResultDto(
            profileId, "user-01", "Suspended", true, DateTime.UtcNow, "Risk mitigation");

        mediator.Send(Arg.Any<SuspendIndividualCommand>(), Arg.Any<CancellationToken>())
            .Returns(statusResult);

        var (host, client) = await CreateTestServer(mediator, "Admin");
        using (host)
        using (client)
        {
            var response = await client.PatchAsJsonAsync(
                $"/api/v1/admin/individuals/{profileId}/suspend",
                new SuspendIndividualRequest("Risk mitigation"));

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);

            var body = await response.Content.ReadFromJsonAsync<AdminIndividualStatusResultDto>();
            Assert.NotNull(body);
            Assert.True(body.IsSuspended);
            Assert.Equal("Suspended", body.Status);
        }
    }

    [Fact]
    public async Task SuspendIndividual_AsAuditor_Returns403Forbidden()
    {
        var mediator = Substitute.For<IMediator>();
        var (host, client) = await CreateTestServer(mediator, "Auditor");
        using (host)
        using (client)
        {
            var response = await client.PatchAsJsonAsync(
                $"/api/v1/admin/individuals/{Guid.NewGuid()}/suspend",
                new SuspendIndividualRequest("Auditor trying to suspend"));

            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        }
    }

    [Fact]
    public async Task SuspendIndividual_WhenNotFound_Returns404NotFound()
    {
        var mediator = Substitute.For<IMediator>();
        mediator.Send(Arg.Any<SuspendIndividualCommand>(), Arg.Any<CancellationToken>())
            .Returns<AdminIndividualStatusResultDto>(_ => throw new KeyNotFoundException("Individual profile not found."));

        var (host, client) = await CreateTestServer(mediator, "Admin");
        using (host)
        using (client)
        {
            var response = await client.PatchAsJsonAsync(
                $"/api/v1/admin/individuals/{Guid.NewGuid()}/suspend",
                new SuspendIndividualRequest("Suspicious activity"));

            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        }
    }

    [Fact]
    public async Task ReactivateIndividual_AsAdmin_Returns200Ok()
    {
        var mediator = Substitute.For<IMediator>();
        var profileId = Guid.NewGuid();
        var statusResult = new AdminIndividualStatusResultDto(
            profileId, "user-01", "Active", false, null, null);

        mediator.Send(Arg.Any<ReactivateIndividualCommand>(), Arg.Any<CancellationToken>())
            .Returns(statusResult);

        var (host, client) = await CreateTestServer(mediator, "Admin");
        using (host)
        using (client)
        {
            var response = await client.PatchAsJsonAsync(
                $"/api/v1/admin/individuals/{profileId}/reactivate",
                new ReactivateIndividualRequest("Review cleared"));

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);

            var body = await response.Content.ReadFromJsonAsync<AdminIndividualStatusResultDto>();
            Assert.NotNull(body);
            Assert.False(body.IsSuspended);
            Assert.Equal("Active", body.Status);
        }
    }

    [Fact]
    public async Task ReactivateIndividual_AsAuditor_Returns403Forbidden()
    {
        var mediator = Substitute.For<IMediator>();
        var (host, client) = await CreateTestServer(mediator, "Auditor");
        using (host)
        using (client)
        {
            var response = await client.PatchAsJsonAsync(
                $"/api/v1/admin/individuals/{Guid.NewGuid()}/reactivate",
                new ReactivateIndividualRequest("Auditor reactivating"));

            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        }
    }

    [Fact]
    public async Task ReactivateIndividual_WhenNotFound_Returns404NotFound()
    {
        var mediator = Substitute.For<IMediator>();
        mediator.Send(Arg.Any<ReactivateIndividualCommand>(), Arg.Any<CancellationToken>())
            .Returns<AdminIndividualStatusResultDto>(_ => throw new KeyNotFoundException("Individual profile not found."));

        var (host, client) = await CreateTestServer(mediator, "SuperAdmin");
        using (host)
        using (client)
        {
            var response = await client.PatchAsJsonAsync(
                $"/api/v1/admin/individuals/{Guid.NewGuid()}/reactivate",
                new ReactivateIndividualRequest("Review cleared"));

            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        }
    }
}

public sealed record TestAdminIndivRole(string Role);

public sealed class TestAdminIndivAuthHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    private readonly TestAdminIndivRole _role;

    public TestAdminIndivAuthHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder,
        TestAdminIndivRole role)
        : base(options, logger, encoder)
    {
        _role = role;
    }

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, "test_admin_user"),
            new Claim(ClaimTypes.Role, _role.Role)
        };
        var identity = new ClaimsIdentity(claims, "AdminTestScheme");
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, "AdminTestScheme");
        return Task.FromResult(AuthenticateResult.Success(ticket));
    }
}
