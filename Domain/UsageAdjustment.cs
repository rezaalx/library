namespace Workspace.Domain;

public sealed class UsageAdjustment
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }
    public int MonthYYYYMM { get; set; }
    public int DeltaSeconds { get; set; }
    public string Reason { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }

    public User? User { get; set; }
}
