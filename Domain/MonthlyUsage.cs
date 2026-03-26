namespace Workspace.Domain;

public sealed class MonthlyUsage
{
    public Guid UserId { get; set; }
    public int MonthYYYYMM { get; set; }
    public int UsedSeconds { get; set; }
    public DateTime UpdatedAt { get; set; }

    public User? User { get; set; }
}
