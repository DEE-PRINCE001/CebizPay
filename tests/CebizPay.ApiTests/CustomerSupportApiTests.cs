using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Encodings.Web;
using Asp.Versioning;
using CebizPay.Api.Controllers.v1;
using CebizPay.Application.Common.Interfaces.Support;
using CebizPay.Application.Common.Models;
using CebizPay.Application.UseCases.Support;
using CebizPay.Domain.Support.Enums;
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

public sealed class CustomerSupportApiTests
{
    private static async Task<(IHost host, HttpClient client)> CreateTestServer(
        IMediator mediator,
        bool authenticated = true,
        string role = "User",
        string userId = "test_user_support_001")
    {
        var host = await new HostBuilder()
            .ConfigureWebHost(webBuilder =>
            {
                webBuilder.UseTestServer();
                webBuilder.ConfigureServices(services =>
                {
                    services.AddControllers()
                        .AddApplicationPart(typeof(CustomerSupportController).Assembly);

                    services.AddAuthentication("SupportTestScheme")
                        .AddScheme<AuthenticationSchemeOptions, TestSupportAuthHandler>("SupportTestScheme", _ => { });

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
                    services.AddSingleton(new TestSupportAuthContext(authenticated, role, userId));
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
    public async Task StartKolaSession_AuthenticatedUser_ReturnsOk()
    {
        var mediator = Substitute.For<IMediator>();
        var responseDto = new KolaSessionResponse(
            SessionId: "session-123",
            State: KolaSessionState.Started,
            Category: null,
            BotMessage: "Hello, I am Kola.",
            Options: new List<string> { "1. Payment / Transfer" },
            IsEscalated: false,
            Priority: SupportTicketPriority.Normal);

        mediator.Send(Arg.Any<StartKolaSessionCommand>(), Arg.Any<CancellationToken>())
            .Returns(responseDto);

        var (host, client) = await CreateTestServer(mediator);
        using (host)
        using (client)
        {
            var response = await client.PostAsJsonAsync("/api/v1/support/kola/session", new KolaStartSessionRequest());
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);

            var body = await response.Content.ReadFromJsonAsync<KolaSessionResponse>();
            Assert.NotNull(body);
            Assert.Equal("session-123", body.SessionId);
            Assert.Equal(KolaSessionState.Started, body.State);
        }
    }

    [Fact]
    public async Task InteractKolaSession_AuthenticatedUser_ReturnsOk()
    {
        var mediator = Substitute.For<IMediator>();
        var responseDto = new KolaSessionResponse(
            SessionId: "session-123",
            State: KolaSessionState.CategorySelected,
            Category: SupportTicketCategory.PaymentOrTransfer,
            BotMessage: "You selected Payment.",
            Options: new List<string> { "1. Transfer failed" },
            IsEscalated: false,
            Priority: SupportTicketPriority.Normal);

        mediator.Send(Arg.Any<InteractKolaSessionCommand>(), Arg.Any<CancellationToken>())
            .Returns(responseDto);

        var (host, client) = await CreateTestServer(mediator);
        using (host)
        using (client)
        {
            var response = await client.PostAsJsonAsync("/api/v1/support/kola/message", new KolaInteractRequest(
                SessionId: "session-123",
                CurrentState: KolaSessionState.Started,
                Category: null,
                SelectedIssueIndex: null,
                Message: "1"));

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);

            var body = await response.Content.ReadFromJsonAsync<KolaSessionResponse>();
            Assert.NotNull(body);
            Assert.Equal(SupportTicketCategory.PaymentOrTransfer, body.Category);
        }
    }

    [Fact]
    public async Task CreateTicket_AuthenticatedUser_Returns201Created()
    {
        var mediator = Substitute.For<IMediator>();
        var ticketId = Guid.NewGuid();
        var ticketDto = new SupportTicketDto(
            Id: ticketId,
            TicketNumber: "CBZ-SUP-2026-API01",
            UserId: "test_user_support_001",
            OrganizationId: null,
            Category: SupportTicketCategory.PaymentOrTransfer,
            Subject: "API test ticket",
            Description: "Testing ticket creation",
            Status: SupportTicketStatus.Open,
            Priority: SupportTicketPriority.Normal,
            CreatedAtUtc: DateTime.UtcNow,
            UpdatedAtUtc: DateTime.UtcNow,
            EscalatedAtUtc: null,
            FirstResponseAtUtc: null,
            ResolvedAtUtc: null,
            ClosedAtUtc: null,
            SlaDueAtUtc: DateTime.UtcNow.AddHours(12),
            IsSlaBreached: false,
            ResolutionSummary: null,
            Messages: new List<TicketMessageDto>());

        mediator.Send(Arg.Any<CreateSupportTicketCommand>(), Arg.Any<CancellationToken>())
            .Returns(ticketDto);

        var (host, client) = await CreateTestServer(mediator);
        using (host)
        using (client)
        {
            var response = await client.PostAsJsonAsync("/api/v1/support/tickets", new CreateSupportTicketRequest(
                Category: SupportTicketCategory.PaymentOrTransfer,
                Subject: "API test ticket",
                Description: "Testing ticket creation"));

            Assert.Equal(HttpStatusCode.Created, response.StatusCode);

            var body = await response.Content.ReadFromJsonAsync<SupportTicketDto>();
            Assert.NotNull(body);
            Assert.Equal("CBZ-SUP-2026-API01", body.TicketNumber);
        }
    }

    [Fact]
    public async Task GetMyTickets_Unauthenticated_ReturnsUnauthorized()
    {
        var mediator = Substitute.For<IMediator>();
        var (host, client) = await CreateTestServer(mediator, authenticated: false);
        using (host)
        using (client)
        {
            var response = await client.GetAsync("/api/v1/support/tickets");
            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }
    }

    [Fact]
    public async Task GetTicketById_Owner_ReturnsOk()
    {
        var mediator = Substitute.For<IMediator>();
        var ticketId = Guid.NewGuid();
        var ticketDto = new SupportTicketDto(
            Id: ticketId,
            TicketNumber: "CBZ-SUP-2026-API02",
            UserId: "test_user_support_001",
            OrganizationId: null,
            Category: SupportTicketCategory.WalletOrAccount,
            Subject: "Wallet issue",
            Description: "Testing get ticket",
            Status: SupportTicketStatus.Open,
            Priority: SupportTicketPriority.Normal,
            CreatedAtUtc: DateTime.UtcNow,
            UpdatedAtUtc: DateTime.UtcNow,
            EscalatedAtUtc: null,
            FirstResponseAtUtc: null,
            ResolvedAtUtc: null,
            ClosedAtUtc: null,
            SlaDueAtUtc: DateTime.UtcNow.AddHours(12),
            IsSlaBreached: false,
            ResolutionSummary: null,
            Messages: new List<TicketMessageDto>());

        mediator.Send(Arg.Any<GetSupportTicketByIdQuery>(), Arg.Any<CancellationToken>())
            .Returns(ticketDto);

        var (host, client) = await CreateTestServer(mediator);
        using (host)
        using (client)
        {
            var response = await client.GetAsync($"/api/v1/support/tickets/{ticketId}");
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }
    }

    [Fact]
    public async Task AddMessage_Customer_ReturnsOk()
    {
        var mediator = Substitute.For<IMediator>();
        var ticketId = Guid.NewGuid();
        var messageDto = new TicketMessageDto(
            Id: Guid.NewGuid(),
            TicketId: ticketId,
            SenderUserId: "test_user_support_001",
            SenderType: TicketMessageSenderType.Customer,
            Content: "Follow up message",
            CreatedAtUtc: DateTime.UtcNow);

        mediator.Send(Arg.Any<AddTicketMessageCommand>(), Arg.Any<CancellationToken>())
            .Returns(messageDto);

        var (host, client) = await CreateTestServer(mediator);
        using (host)
        using (client)
        {
            var response = await client.PostAsJsonAsync($"/api/v1/support/tickets/{ticketId}/messages",
                new AddTicketMessageRequest("Follow up message"));

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }
    }

    [Fact]
    public async Task CloseTicket_Customer_ReturnsOk()
    {
        var mediator = Substitute.For<IMediator>();
        var ticketId = Guid.NewGuid();
        mediator.Send(Arg.Any<CloseSupportTicketCommand>(), Arg.Any<CancellationToken>())
            .Returns(true);

        var (host, client) = await CreateTestServer(mediator);
        using (host)
        using (client)
        {
            var response = await client.PostAsync($"/api/v1/support/tickets/{ticketId}/close", null);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }
    }

    [Fact]
    public async Task AdminGetTickets_SuperAdmin_ReturnsOk()
    {
        var mediator = Substitute.For<IMediator>();
        var paged = new PagedResult<SupportTicketDto>(new List<SupportTicketDto>(), 0, 1, 20);
        mediator.Send(Arg.Any<AdminGetSupportTicketsQuery>(), Arg.Any<CancellationToken>())
            .Returns(paged);

        var (host, client) = await CreateTestServer(mediator, authenticated: true, role: "SuperAdmin");
        using (host)
        using (client)
        {
            var response = await client.GetAsync("/api/v1/admin/support/tickets");
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }
    }

    [Fact]
    public async Task AdminGetTickets_RegularUser_ReturnsForbidden()
    {
        var mediator = Substitute.For<IMediator>();
        var (host, client) = await CreateTestServer(mediator, authenticated: true, role: "User");
        using (host)
        using (client)
        {
            var response = await client.GetAsync("/api/v1/admin/support/tickets");
            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        }
    }

    [Fact]
    public async Task AdminUpdateStatus_SuperAdmin_ReturnsOk()
    {
        var mediator = Substitute.For<IMediator>();
        var ticketId = Guid.NewGuid();
        var ticketDto = new SupportTicketDto(
            Id: ticketId,
            TicketNumber: "CBZ-SUP-2026-ADMIN",
            UserId: "u1",
            OrganizationId: null,
            Category: SupportTicketCategory.PaymentOrTransfer,
            Subject: "Sub",
            Description: "Desc",
            Status: SupportTicketStatus.Resolved,
            Priority: SupportTicketPriority.Normal,
            CreatedAtUtc: DateTime.UtcNow,
            UpdatedAtUtc: DateTime.UtcNow,
            EscalatedAtUtc: null,
            FirstResponseAtUtc: null,
            ResolvedAtUtc: DateTime.UtcNow,
            ClosedAtUtc: null,
            SlaDueAtUtc: DateTime.UtcNow.AddHours(12),
            IsSlaBreached: false,
            ResolutionSummary: "Resolved by admin",
            Messages: new List<TicketMessageDto>());

        mediator.Send(Arg.Any<AdminUpdateTicketStatusCommand>(), Arg.Any<CancellationToken>())
            .Returns(ticketDto);

        var (host, client) = await CreateTestServer(mediator, authenticated: true, role: "SuperAdmin");
        using (host)
        using (client)
        {
            var response = await client.PatchAsJsonAsync($"/api/v1/admin/support/tickets/{ticketId}/status",
                new UpdateTicketStatusRequest(SupportTicketStatus.Resolved, "Resolved by admin"));

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }
    }

    [Fact]
    public async Task AdminUpdateStatus_Auditor_ReturnsForbidden()
    {
        var mediator = Substitute.For<IMediator>();
        var ticketId = Guid.NewGuid();

        var (host, client) = await CreateTestServer(mediator, authenticated: true, role: "Auditor");
        using (host)
        using (client)
        {
            var response = await client.PatchAsJsonAsync($"/api/v1/admin/support/tickets/{ticketId}/status",
                new UpdateTicketStatusRequest(SupportTicketStatus.Resolved, "Resolved"));

            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        }
    }

    [Fact]
    public async Task GetReports_SuperAdmin_ReturnsOk()
    {
        var mediator = Substitute.For<IMediator>();
        var reportsDto = new SupportReportsDto(
            TotalTickets: 10,
            OpenTickets: 5,
            EscalatedTickets: 2,
            InReviewTickets: 1,
            ResolvedTickets: 1,
            ClosedTickets: 1,
            SlaBreachedTickets: 0,
            TicketsByCategory: new Dictionary<string, int>(),
            TicketsByPriority: new Dictionary<string, int>(),
            FromUtc: DateTime.UtcNow.AddDays(-7),
            ToUtc: DateTime.UtcNow);

        mediator.Send(Arg.Any<GetSupportReportsQuery>(), Arg.Any<CancellationToken>())
            .Returns(reportsDto);

        var (host, client) = await CreateTestServer(mediator, authenticated: true, role: "SuperAdmin");
        using (host)
        using (client)
        {
            var response = await client.GetAsync("/api/v1/admin/support/reports");
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }
    }
}

internal sealed record TestSupportAuthContext(bool Authenticated, string Role = "User", string UserId = "test_user_support_001");

internal sealed class TestSupportAuthHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    private readonly TestSupportAuthContext _context;

    public TestSupportAuthHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder,
        TestSupportAuthContext context)
        : base(options, logger, encoder)
    {
        _context = context;
    }

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!_context.Authenticated)
        {
            return Task.FromResult(AuthenticateResult.Fail("Unauthenticated"));
        }

        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, _context.UserId),
            new Claim(ClaimTypes.Email, $"{_context.UserId}@cebizpay.com"),
            new Claim(ClaimTypes.Role, _context.Role)
        };

        var identity = new ClaimsIdentity(claims, "SupportTestScheme");
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, "SupportTestScheme");

        return Task.FromResult(AuthenticateResult.Success(ticket));
    }
}
