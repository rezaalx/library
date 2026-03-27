namespace Minit.Api.Domain;

public sealed class CallSession
{
    public Guid Id { get; set; }
    public Guid CreatedByUserId { get; set; }
    public string Provider { get; set; } = string.Empty;
    public string? ProviderRoomId { get; set; }
    public CallSessionStatus Status { get; set; } = CallSessionStatus.Created;
    public DateTime CreatedAt { get; set; }
    public DateTime? StartedAt { get; set; }
    public DateTime? EndedAt { get; set; }

    public User? CreatedByUser { get; set; }
    public ICollection<CallParticipant> Participants { get; set; } = new List<CallParticipant>();
}
