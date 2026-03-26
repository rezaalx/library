namespace Workspace.Domain;

public sealed class User
{
    public Guid Id { get; set; }
    public string DisplayName { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public int MonthlyLimitSeconds { get; set; } = 6000;
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; }

    public ICollection<Contact> OwnedContacts { get; set; } = new List<Contact>();
    public ICollection<Contact> ContactOfUsers { get; set; } = new List<Contact>();
    public ICollection<CallSession> CreatedCallSessions { get; set; } = new List<CallSession>();
    public ICollection<CallParticipant> CallParticipants { get; set; } = new List<CallParticipant>();
    public ICollection<UsageAdjustment> UsageAdjustments { get; set; } = new List<UsageAdjustment>();
}
