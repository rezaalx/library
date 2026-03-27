namespace Minit.Api.Domain;

public sealed class CallParticipant
{
    public Guid Id { get; set; }
    public Guid CallSessionId { get; set; }
    public Guid UserId { get; set; }
    public DateTime JoinedAt { get; set; }
    public DateTime? LeftAt { get; set; }
    public int BilledSeconds { get; set; }

    public CallSession? CallSession { get; set; }
    public User? User { get; set; }
}
