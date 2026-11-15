namespace Consent.Core;

public enum ConsentStatus { Granted, Withdrawn }
public sealed record ConsentRecord(string Id, string Owner, string Purpose, ConsentStatus Status, int Version);
public sealed record AuditRecord(string RequestId, string ConsentId, string Actor, ConsentStatus Before, ConsentStatus After, DateTimeOffset At);
public sealed record ConsentEvent(string EventId, string ConsentId, string Purpose, int Version, ConsentStatus Status);
public sealed record WithdrawalResult(bool Success, string Code, ConsentRecord? Consent = null);

// A local teaching implementation. The lock models an all-or-nothing change;
// cloud persistence must implement its own transaction rather than reuse this store.
public sealed class ConsentStore
{
    private readonly object gate = new();
    private readonly Dictionary<string, ConsentRecord> records;
    private readonly List<AuditRecord> audit = [];
    private readonly List<ConsentEvent> outbox = [];
    private readonly Action? beforeCommit;

    public ConsentStore(IEnumerable<ConsentRecord> seed, Action? beforeCommit = null)
    {
        records = seed.ToDictionary(x => x.Id);
        this.beforeCommit = beforeCommit;
    }

    public ConsentRecord? Find(string id) { lock (gate) return records.GetValueOrDefault(id); }
    public AuditRecord[] Audit { get { lock (gate) return audit.ToArray(); } }
    public ConsentEvent[] Outbox { get { lock (gate) return outbox.ToArray(); } }

    public WithdrawalResult Withdraw(string actor, string consentId, int expectedVersion, string requestId)
    {
        lock (gate)
        {
            if (string.IsNullOrWhiteSpace(actor)) return new(false, "Unauthenticated");
            if (string.IsNullOrWhiteSpace(consentId) || string.IsNullOrWhiteSpace(requestId)) return new(false, "InvalidRequest");
            if (!records.TryGetValue(consentId, out var current)) return new(false, "NotFound");
            if (current.Owner != actor) return new(false, "Forbidden");
            if (current.Status == ConsentStatus.Withdrawn) return new(true, "AlreadyWithdrawn", current);
            if (current.Version != expectedVersion) return new(false, "Conflict");
            var next = current with { Status = ConsentStatus.Withdrawn, Version = current.Version + 1 };
            var entry = new AuditRecord(requestId, consentId, actor, current.Status, next.Status, DateTimeOffset.UtcNow);
            var notification = new ConsentEvent($"{consentId}:{next.Version}", consentId, current.Purpose, next.Version, next.Status);
            // Failure injection happens before any of the three collections changes.
            beforeCommit?.Invoke();
            records[consentId] = next;
            audit.Add(entry);
            outbox.Add(notification);
            return new(true, "Withdrawn", next);
        }
    }
}
