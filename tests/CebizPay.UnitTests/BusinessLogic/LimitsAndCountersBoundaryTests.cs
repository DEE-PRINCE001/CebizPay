using CebizPay.Application.Common.Models;
using CebizPay.Application.UseCases.Admin.Audit;
using CebizPay.Application.UseCases.Admin.Manage;
using FluentValidation.TestHelper;
using Xunit;

namespace CebizPay.UnitTests.BusinessLogic;

public sealed class LimitsAndCountersBoundaryTests
{
    private readonly GetAuditLogsQueryValidator _auditLogsValidator = new();

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(101)]
    [InlineData(1000)]
    public void PageSize_OutOfInclusiveRange_FailsValidation(int invalidPageSize)
    {
        var query = new GetAuditLogsQuery(PageNumber: 1, PageSize: invalidPageSize);
        var result = _auditLogsValidator.TestValidate(query);

        result.ShouldHaveValidationErrorFor(x => x.PageSize);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(20)]
    [InlineData(50)]
    [InlineData(100)]
    public void PageSize_WithinInclusiveRange_PassesValidation(int validPageSize)
    {
        var query = new GetAuditLogsQuery(PageNumber: 1, PageSize: validPageSize);
        var result = _auditLogsValidator.TestValidate(query);

        result.ShouldNotHaveValidationErrorFor(x => x.PageSize);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    public void PageNumber_ZeroOrNegative_FailsValidation(int invalidPageNumber)
    {
        var query = new GetAuditLogsQuery(PageNumber: invalidPageNumber, PageSize: 20);
        var result = _auditLogsValidator.TestValidate(query);

        result.ShouldHaveValidationErrorFor(x => x.PageNumber);
    }

    private static readonly string[] SampleItems = ["a", "b"];
    private static readonly string[] SingleItem = ["a"];

    [Fact]
    public void PagedResult_WithInvalidInputs_SafelyDefaults()
    {
        // Zero or negative page size defaults to 20
        var paged1 = new PagedResult<string>(SampleItems, totalCount: 2, pageNumber: 0, pageSize: -1);
        Assert.Equal(1, paged1.PageNumber);
        Assert.Equal(20, paged1.PageSize);
        Assert.Equal(1, paged1.TotalPages);

        // Empty data gives 0 total pages
        var paged2 = new PagedResult<string>(Array.Empty<string>(), totalCount: 0, pageNumber: 1, pageSize: 20);
        Assert.Equal(0, paged2.TotalPages);
        Assert.False(paged2.HasNextPage);
        Assert.False(paged2.HasPreviousPage);

        // Exact boundary: 40 items with page size 20 gives 2 pages
        var paged3 = new PagedResult<string>(SingleItem, totalCount: 40, pageNumber: 1, pageSize: 20);
        Assert.Equal(2, paged3.TotalPages);
        Assert.True(paged3.HasNextPage);

        // 41 items with page size 20 gives 3 pages
        var paged4 = new PagedResult<string>(SingleItem, totalCount: 41, pageNumber: 2, pageSize: 20);
        Assert.Equal(3, paged4.TotalPages);
        Assert.True(paged4.HasPreviousPage);
        Assert.True(paged4.HasNextPage);
    }

    [Fact]
    public void AdminDirectoryQuery_PageSizeBoundaries_ValidatedCorrectly()
    {
        var validator = new GetAdminDirectoryQueryValidator();

        var queryMinMinus1 = new GetAdminDirectoryQuery(PageNumber: 1, PageSize: 0);
        validator.TestValidate(queryMinMinus1).ShouldHaveValidationErrorFor(x => x.PageSize);

        var queryMin = new GetAdminDirectoryQuery(PageNumber: 1, PageSize: 1);
        validator.TestValidate(queryMin).ShouldNotHaveValidationErrorFor(x => x.PageSize);

        var queryMax = new GetAdminDirectoryQuery(PageNumber: 1, PageSize: 100);
        validator.TestValidate(queryMax).ShouldNotHaveValidationErrorFor(x => x.PageSize);

        var queryMaxPlus1 = new GetAdminDirectoryQuery(PageNumber: 1, PageSize: 101);
        validator.TestValidate(queryMaxPlus1).ShouldHaveValidationErrorFor(x => x.PageSize);
    }
}
