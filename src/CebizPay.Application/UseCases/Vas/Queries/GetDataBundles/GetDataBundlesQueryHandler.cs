using CebizPay.Application.Common.Interfaces.Vas;
using CebizPay.Application.Common.Models.Vas;
using CebizPay.Domain.Vas.Enums;
using MediatR;

namespace CebizPay.Application.UseCases.Vas.Queries.GetDataBundles;

/// <summary>
/// Handles <see cref="GetDataBundlesQuery"/>.
/// Delegates to <see cref="IVasProvider.GetDataBundlesAsync"/> to retrieve live provider-supported bundle catalog.
/// </summary>
public sealed class GetDataBundlesQueryHandler : IRequestHandler<GetDataBundlesQuery, IReadOnlyList<DataBundleDto>>
{
    private readonly IVasProvider _vasProvider;

    /// <summary>
    /// Initializes a new instance of <see cref="GetDataBundlesQueryHandler"/>.
    /// </summary>
    public GetDataBundlesQueryHandler(IVasProvider vasProvider)
    {
        _vasProvider = vasProvider;
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<DataBundleDto>> Handle(GetDataBundlesQuery request, CancellationToken cancellationToken)
    {
        VasNetwork? networkFilter = null;
        if (!string.IsNullOrWhiteSpace(request.Network))
        {
            if (Enum.TryParse<VasNetwork>(request.Network, ignoreCase: true, out var parsed))
            {
                networkFilter = parsed;
            }
            else if (request.Network.Equals("9MOBILE", StringComparison.OrdinalIgnoreCase))
            {
                networkFilter = VasNetwork.NineMobile;
            }
        }

        return await _vasProvider.GetDataBundlesAsync(networkFilter, cancellationToken);
    }
}
