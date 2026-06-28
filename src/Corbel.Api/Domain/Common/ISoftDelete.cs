namespace Corbel.Domain.Common;

/// <summary>Opt-in per entity. The interceptor converts Delete→update; a named query filter hides deleted rows.</summary>
public interface ISoftDelete
{
    public bool IsDeleted { get; set; }
    public DateTimeOffset? DeletedAtUtc { get; set; }
}
