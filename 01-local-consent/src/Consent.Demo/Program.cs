using Consent.Core;

var store = new ConsentStore([new("C-001", "P-001", "research-demo", ConsentStatus.Granted, 1)]);
foreach (var result in new[] {
    store.Withdraw("P-002", "C-001", 1, "request-denied"),
    store.Withdraw("P-001", "C-001", 1, "request-accepted"),
    store.Withdraw("P-001", "C-001", 1, "request-repeated") })
    Console.WriteLine($"{result.Code}: success={result.Success.ToString().ToLowerInvariant()}");
Console.WriteLine($"Audit records: {store.Audit.Length}; pending events: {store.Outbox.Length}");
