namespace Corbel.Domain.Common;

/// <summary>Audit fields populated automatically by the SaveChanges interceptor. UTC, user nullable for system actions.</summary>
public interface IAuditable
{
    public DateTimeOffset CreatedAtUtc { get; set; }
    public Guid? CreatedBy { get; set; }
    public DateTimeOffset? ModifiedAtUtc { get; set; }
    public Guid? ModifiedBy { get; set; }
}
