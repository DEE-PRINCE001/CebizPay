using System.Diagnostics;
using CebizPay.Application.Common.Exceptions;
using FluentValidation;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace CebizPay.Api.Middleware;

/// <summary>
/// Global exception handler using ASP.NET Core IExceptionHandler.
/// Intercepts exceptions and returns standardized RFC 7807 ProblemDetails responses.
/// </summary>
public sealed partial class GlobalExceptionHandler : IExceptionHandler
{
    private readonly IHostEnvironment _environment;
    private readonly ILogger<GlobalExceptionHandler> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="GlobalExceptionHandler"/> class.
    /// </summary>
    public GlobalExceptionHandler(
        IHostEnvironment environment,
        ILogger<GlobalExceptionHandler> logger)
    {
        _environment = environment;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        LogUnhandledException(_logger, exception.Message, exception);

        var (statusCode, title, detail) = MapException(exception);

        var traceId = Activity.Current?.Id ?? httpContext.TraceIdentifier;

        var problemDetails = new ProblemDetails
        {
            Status = statusCode,
            Title = title,
            Detail = _environment.IsDevelopment() ? exception.ToString() : detail,
            Instance = httpContext.Request.Path
        };

        problemDetails.Extensions["traceId"] = traceId;

        var errorCode = GetErrorCode(exception);
        if (errorCode != null)
        {
            problemDetails.Extensions["code"] = errorCode;
        }

        if (exception is ValidationException validationException)
        {
            var errors = validationException.Errors
                .GroupBy(e => e.PropertyName)
                .ToDictionary(
                    g => g.Key,
                    g => g.Select(e => e.ErrorMessage).ToArray());

            problemDetails.Extensions["errors"] = errors;
        }

        httpContext.Response.StatusCode = statusCode;

        await httpContext.Response
            .WriteAsJsonAsync(problemDetails, options: (System.Text.Json.JsonSerializerOptions?)null, contentType: "application/problem+json", cancellationToken: cancellationToken)
            .ConfigureAwait(false);

        return true;
    }

    private static (int StatusCode, string Title, string Detail) MapException(Exception exception) =>
        exception switch
        {
            ValidationException => (
                StatusCodes.Status400BadRequest,
                "Validation Error",
                "One or more validation failures occurred."),

            UnauthorizedAccessException => (
                StatusCodes.Status401Unauthorized,
                "Unauthorized",
                "Authentication is required to access this resource."),

            TransferNotAuthorizedException => (
                StatusCodes.Status403Forbidden,
                "Transfer Not Authorized",
                exception.Message),

            KeyNotFoundException => (
                StatusCodes.Status404NotFound,
                "Resource Not Found",
                "The requested resource was not found."),

            IdempotencyConflictException idempEx => (
                StatusCodes.Status409Conflict,
                "Idempotency Conflict",
                idempEx.Message),

            InvalidOperationException invalidOpEx when invalidOpEx.Message.Contains("Conflict", StringComparison.OrdinalIgnoreCase) => (
                StatusCodes.Status409Conflict,
                "Resource Conflict",
                invalidOpEx.Message),

            PinLockedException => (
                423, // HTTP 423 Locked
                "PIN Locked",
                exception.Message),

            InvalidPinException => (
                StatusCodes.Status400BadRequest,
                "Invalid Transaction PIN",
                exception.Message),

            InsufficientFundsException => (
                StatusCodes.Status422UnprocessableEntity,
                "Insufficient Funds",
                exception.Message),

            WalletNotActiveException => (
                StatusCodes.Status422UnprocessableEntity,
                "Wallet Not Active",
                exception.Message),

            CurrencyMismatchException => (
                StatusCodes.Status422UnprocessableEntity,
                "Currency Mismatch",
                exception.Message),

            SelfTransferException => (
                StatusCodes.Status422UnprocessableEntity,
                "Self-Transfer Not Allowed",
                exception.Message),

            ComplianceRestrictedException => (
                StatusCodes.Status422UnprocessableEntity,
                "Compliance Restricted",
                exception.Message),

            PinRequiredException => (
                StatusCodes.Status422UnprocessableEntity,
                "PIN Required",
                exception.Message),

            VasDuplicatePurchaseException => (
                StatusCodes.Status409Conflict,
                "Duplicate VAS Purchase",
                exception.Message),

            VasLimitExceededException => (
                StatusCodes.Status422UnprocessableEntity,
                "VAS Limit Exceeded",
                exception.Message),

            VasInvalidProductException => (
                StatusCodes.Status422UnprocessableEntity,
                "Invalid VAS Product",
                exception.Message),

            _ => (
                StatusCodes.Status500InternalServerError,
                "An unexpected error occurred",
                "An error occurred while processing your request. Please try again later.")
        };

    private static string? GetErrorCode(Exception exception) => exception switch
    {
        IdempotencyConflictException ex => ex.Code,
        InsufficientFundsException ex => ex.Code,
        WalletNotActiveException ex => ex.Code,
        CurrencyMismatchException ex => ex.Code,
        SelfTransferException ex => ex.Code,
        TransferNotAuthorizedException ex => ex.Code,
        ComplianceRestrictedException ex => ex.Code,
        PinRequiredException ex => ex.Code,
        PinLockedException ex => ex.Code,
        InvalidPinException ex => ex.Code,
        VasDuplicatePurchaseException ex => ex.Code,
        VasLimitExceededException ex => ex.Code,
        VasInvalidProductException ex => ex.Code,
        _ => null
    };

    [LoggerMessage(EventId = 1, Level = LogLevel.Error, Message = "Unhandled exception occurred: {Message}")]
    private static partial void LogUnhandledException(ILogger logger, string message, Exception exception);
}
