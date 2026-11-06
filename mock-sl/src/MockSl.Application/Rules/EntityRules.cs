using MockSl.Application.Entities;
using MockSl.Domain.Errors;

namespace MockSl.Application.Rules;

/// <summary>
/// What is special about one object. The generic handler calls these steps inside its transaction,
/// and an object with no special behaviour keeps the empty defaults.
/// </summary>
public abstract class EntityRules
{
    /// <summary>Validates the draft and fills in what the server decides, such as totals.</summary>
    public virtual Task BeforeInsertAsync(EntityDraft draft, IEntitySession session, CancellationToken cancellationToken) =>
        Task.CompletedTask;

    /// <summary>Changes other objects after the row exists, such as stock and balances.</summary>
    public virtual Task AfterInsertAsync(
        EntityDraft draft,
        EntityRow row,
        IEntitySession session,
        CancellationToken cancellationToken) =>
        Task.CompletedTask;

    public virtual Task BeforeUpdateAsync(
        IReadOnlyList<FieldValue> key,
        DraftRow changes,
        IEntitySession session,
        CancellationToken cancellationToken) =>
        Task.CompletedTask;

    public virtual Task BeforeDeleteAsync(
        IReadOnlyList<FieldValue> key,
        IEntitySession session,
        CancellationToken cancellationToken) =>
        Task.CompletedTask;

    public virtual Task RunActionAsync(
        string action,
        IReadOnlyList<FieldValue> key,
        IEntitySession session,
        CancellationToken cancellationToken) =>
        throw SapErrors.ActionNotSupported();
}
