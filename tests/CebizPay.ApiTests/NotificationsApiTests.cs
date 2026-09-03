#pragma warning disable CS1591
using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Encodings.Web;
using Asp.Versioning;
using CebizPay.Api.Controllers.v1;
using CebizPay.Application.Common.Models;
using CebizPay.Application.UseCases.Notifications;
using CebizPay.Domain.Communication.Enums;
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

public sealed class NotificationsApiTests
{
    private static async Task<(IHost host, HttpClient client)> CreateTestServer(IMediator mediator, bool authenticated = true)
    {
        var host = await new HostBuilder()
            .ConfigureWebHost(webBuilder =>
            {
                webBuilder.UseTestServer();
                webBuilder.ConfigureServices(services =>
                {
                    services.AddControllers().AddApplicationPart(typeof(NotificationsController).Assembly);
                    services.AddAuthentication("NotificationTestScheme")
                            .AddScheme<AuthenticationSchemeOptions, TestNotificationAuthHandler>("NotificationTestScheme", _ => { });
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
                    services.AddSingleton(new TestNotificationAuthContext(authenticated));
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
    public async Task GetNotifications_Returns200OkWithPagedResult()
    {
        var mediator = Substitute.For<IMediator>();
        var items = new List<InAppNotificationDto>
        {
            new(Guid.NewGuid(), "test_user_001", null, NotificationType.LoanApproved, "Loan Approved", "Principal disbursed", NotificationPriority.High, "/loans/1", DateTime.UtcNow, null, false)
        };
        var pagedResult = new PagedResult<InAppNotificationDto>(items, 1, 1, 20);

        mediator.Send(Arg.Any<GetNotificationsQuery>(), Arg.Any<CancellationToken>())
            .Returns(pagedResult);

        var (host, client) = await CreateTestServer(mediator);
        using (host)
        using (client)
        {
            var response = await client.GetAsync("/api/v1/notifications?pageNumber=1&pageSize=20");
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);

            var body = await response.Content.ReadFromJsonAsync<PagedResult<InAppNotificationDto>>();
            Assert.NotNull(body);
            Assert.Equal(1, body.TotalCount);
            Assert.Single(body.Items);
            Assert.Equal("Loan Approved", body.Items[0].Title);
        }
    }

    [Fact]
    public async Task GetUnreadCount_Returns200OkWithCount()
    {
        var mediator = Substitute.For<IMediator>();
        mediator.Send(Arg.Any<GetUnreadNotificationCountQuery>(), Arg.Any<CancellationToken>())
            .Returns(4);

        var (host, client) = await CreateTestServer(mediator);
        using (host)
        using (client)
        {
            var response = await client.GetAsync("/api/v1/notifications/unread-count");
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);

            var body = await response.Content.ReadFromJsonAsync<UnreadCountResponse>();
            Assert.NotNull(body);
            Assert.Equal(4, body.Count);
        }
    }

    [Fact]
    public async Task MarkAsRead_PatchAndPost_Returns200Ok()
    {
        var mediator = Substitute.For<IMediator>();
        mediator.Send(Arg.Any<MarkNotificationReadCommand>(), Arg.Any<CancellationToken>())
            .Returns(true);

        var notificationId = Guid.NewGuid();
        var (host, client) = await CreateTestServer(mediator);
        using (host)
        using (client)
        {
            var patchResponse = await client.PatchAsync($"/api/v1/notifications/{notificationId}/read", null);
            Assert.Equal(HttpStatusCode.OK, patchResponse.StatusCode);

            var postResponse = await client.PostAsync($"/api/v1/notifications/{notificationId}/read", null);
            Assert.Equal(HttpStatusCode.OK, postResponse.StatusCode);
        }
    }

    [Fact]
    public async Task MarkAllAsRead_Returns200OkWithCount()
    {
        var mediator = Substitute.For<IMediator>();
        mediator.Send(Arg.Any<MarkAllNotificationsReadCommand>(), Arg.Any<CancellationToken>())
            .Returns(7);

        var (host, client) = await CreateTestServer(mediator);
        using (host)
        using (client)
        {
            var response = await client.PostAsync("/api/v1/notifications/read-all", null);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);

            var body = await response.Content.ReadFromJsonAsync<MarkAllReadResponse>();
            Assert.NotNull(body);
            Assert.Equal(7, body.Count);
        }
    }

    [Fact]
    public async Task RegisterDevice_Returns200Ok()
    {
        var mediator = Substitute.For<IMediator>();
        mediator.Send(Arg.Any<RegisterDeviceTokenCommand>(), Arg.Any<CancellationToken>())
            .Returns(true);

        var (host, client) = await CreateTestServer(mediator);
        using (host)
        using (client)
        {
            var response = await client.PostAsJsonAsync("/api/v1/notifications/devices", new
            {
                token = "fcm_reg_token_abc_123",
                platform = DevicePlatform.Android,
                deviceModel = "Pixel 8 Pro"
            });

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }
    }

    [Fact]
    public async Task DeactivateDevice_Returns204NoContent()
    {
        var mediator = Substitute.For<IMediator>();
        mediator.Send(Arg.Any<DeactivateDeviceTokenCommand>(), Arg.Any<CancellationToken>())
            .Returns(true);

        var (host, client) = await CreateTestServer(mediator);
        using (host)
        using (client)
        {
            var response = await client.DeleteAsync("/api/v1/notifications/devices/fcm_reg_token_abc_123");
            Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        }
    }

    [Fact]
    public async Task GetPreferences_Returns200OkWithList()
    {
        var mediator = Substitute.For<IMediator>();
        var prefs = new List<NotificationPreferenceDto>
        {
            new(NotificationType.SecurityAlert, true, true, true, true, true),
            new(NotificationType.LoanApproved, true, true, false, false, false)
        };

        mediator.Send(Arg.Any<GetNotificationPreferencesQuery>(), Arg.Any<CancellationToken>())
            .Returns(prefs);

        var (host, client) = await CreateTestServer(mediator);
        using (host)
        using (client)
        {
            var response = await client.GetAsync("/api/v1/notifications/preferences");
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);

            var body = await response.Content.ReadFromJsonAsync<List<NotificationPreferenceDto>>();
            Assert.NotNull(body);
            Assert.Equal(2, body.Count);
            Assert.True(body[0].IsMandatory);
        }
    }

    [Fact]
    public async Task UpdatePreferences_Returns200OkWithUpdatedList()
    {
        var mediator = Substitute.For<IMediator>();
        var updated = new List<NotificationPreferenceDto>
        {
            new(NotificationType.LoanApproved, true, false, false, false, false)
        };

        mediator.Send(Arg.Any<UpdateNotificationPreferencesCommand>(), Arg.Any<CancellationToken>())
            .Returns(updated);

        var (host, client) = await CreateTestServer(mediator);
        using (host)
        using (client)
        {
            var response = await client.HttpPutAsync("/api/v1/notifications/preferences", new UpdateNotificationPreferencesRequest(
                new List<UpdatePreferenceItem>
                {
                    new(NotificationType.LoanApproved, false, false, false)
                }));

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }
    }

    [Fact]
    public async Task UnauthenticatedRequest_Returns401Unauthorized()
    {
        var mediator = Substitute.For<IMediator>();
        var (host, client) = await CreateTestServer(mediator, authenticated: false);
        using (host)
        using (client)
        {
            var response = await client.GetAsync("/api/v1/notifications");
            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }
    }

    private sealed record TestNotificationAuthContext(bool Authenticated);

    private sealed record UnreadCountResponse(int Count);
    private sealed record MarkAllReadResponse(bool Succeeded, int Count, string Message);

    private sealed class TestNotificationAuthHandler : AuthenticationHandler<AuthenticationSchemeOptions>
    {
        private readonly TestNotificationAuthContext _context;

        public TestNotificationAuthHandler(
            IOptionsMonitor<AuthenticationSchemeOptions> options,
            ILoggerFactory logger,
            UrlEncoder encoder,
            TestNotificationAuthContext context)
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

            var claims = new List<Claim>
            {
                new(ClaimTypes.NameIdentifier, "test_user_notification_001"),
                new(ClaimTypes.Name, "user@cebizpay.com")
            };

            var identity = new ClaimsIdentity(claims, "NotificationTestScheme");
            var principal = new ClaimsPrincipal(identity);
            var ticket = new AuthenticationTicket(principal, "NotificationTestScheme");
            return Task.FromResult(AuthenticateResult.Success(ticket));
        }
    }
}

internal static class HttpClientExtensions
{
    public static Task<HttpResponseMessage> HttpPutAsync<T>(this HttpClient client, string requestUri, T value)
    {
        return client.PutAsJsonAsync(requestUri, value);
    }
}
