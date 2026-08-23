using CebizPay.Application.Common.Models.Vas;
using MediatR;

namespace CebizPay.Application.UseCases.Vas.Queries.GetDataBundles;

/// <summary>
/// Query to fetch the available catalog of data bundle plans, optionally filtered by network.
/// </summary>
public sealed record GetDataBundlesQuery(string? Network = null) : IRequest<IReadOnlyList<DataBundleDto>>;
