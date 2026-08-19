using CebizPay.Application.Common.Interfaces.Persistence;
using Microsoft.EntityFrameworkCore.Storage;

namespace CebizPay.Infrastructure.Persistence;

/// <summary>
/// Infrastructure adapter implementing <see cref="IDbTransaction"/> around EF Core's <see cref="IDbContextTransaction"/>.
/// </summary>
public sealed class EfCoreDbTransaction : IDbTransaction
{
    private readonly IDbContextTransaction _transaction;

    /// <summary>
    /// Initializes a new instance of <see cref="EfCoreDbTransaction"/>.
    /// </summary>
    public EfCoreDbTransaction(IDbContextTransaction transaction)
    {
        _transaction = transaction ?? throw new ArgumentNullException(nameof(transaction));
    }

    /// <inheritdoc/>
    public Task CommitAsync(CancellationToken cancellationToken = default)
        => _transaction.CommitAsync(cancellationToken);

    /// <inheritdoc/>
    public Task RollbackAsync(CancellationToken cancellationToken = default)
        => _transaction.RollbackAsync(cancellationToken);

    /// <inheritdoc/>
    public ValueTask DisposeAsync()
        => _transaction.DisposeAsync();
}
