using CebizPay.Application.UseCases.Admin.Audit;
using Xunit;

namespace CebizPay.UnitTests.Auditing;

public sealed class GetAuditLogsQueryValidatorTests
{
    private readonly GetAuditLogsQueryValidator _validator = new();

    [Fact]
    public void Validate_ValidQuery_ShouldPass()
    {
        var query = new GetAuditLogsQuery(
            FromUtc: DateTime.UtcNow.AddDays(-7),
            ToUtc: DateTime.UtcNow,
            PageNumber: 1,
            PageSize: 20);

        var result = _validator.Validate(query);

        Assert.True(result.IsValid);
    }

    [Fact]
    public void Validate_InvalidPageNumber_ShouldFail()
    {
        var query = new GetAuditLogsQuery(PageNumber: 0);

        var result = _validator.Validate(query);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(query.PageNumber));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    [InlineData(101)]
    [InlineData(500)]
    public void Validate_InvalidPageSize_ShouldFail(int pageSize)
    {
        var query = new GetAuditLogsQuery(PageSize: pageSize);

        var result = _validator.Validate(query);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(query.PageSize));
    }

    [Fact]
    public void Validate_FromUtcGreaterThanToUtc_ShouldFail()
    {
        var query = new GetAuditLogsQuery(
            FromUtc: DateTime.UtcNow,
            ToUtc: DateTime.UtcNow.AddDays(-1));

        var result = _validator.Validate(query);

        Assert.False(result.IsValid);
    }
}
