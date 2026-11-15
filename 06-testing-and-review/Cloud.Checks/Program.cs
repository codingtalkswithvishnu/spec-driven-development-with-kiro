using System.Reflection;
using System.Text.Json;
using Amazon;
using Amazon.Runtime;
using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using Amazon.Lambda.Core;
using Amazon.EventBridge;
using Amazon.EventBridge.Model;
using Consent.Cloud;

Environment.SetEnvironmentVariable("TABLE_NAME", "test-only-table");
var context = DispatchProxy.Create<ILambdaContext, ContextProxy>();
var checks = new List<(string Name, Func<Task> Run)>{
 ("missing caller rejected before storage",async()=>{using var db=new FakeDb(); Check(await Status(new ApiFunction(db),Request(""),context)==401 && db.Reads==0);}),
 ("other owner hidden and not changed",async()=>{using var db=new FakeDb();Check(await Status(new ApiFunction(db),Request("other"),context)==404 && db.Writes==0);}),
 ("unknown record not created",async()=>{using var db=new FakeDb{Missing=true};Check(await Status(new ApiFunction(db),Request("owner"),context)==404 && db.Writes==0);}),
 ("stale version rejected",async()=>{using var db=new FakeDb();Check(await Status(new ApiFunction(db),Request("owner",0),context)==400 && db.Writes==0);}),
 ("conflicting saved version rejected",async()=>{using var db=new FakeDb{Version=2};Check(await Status(new ApiFunction(db),Request("owner"),context)==409 && db.Writes==0);}),
 ("repeat avoids another transaction",async()=>{using var db=new FakeDb{Withdrawn=true};Check(await Status(new ApiFunction(db),Request("owner"),context)==200 && db.Writes==0);}),
 ("accepted change constructs three transactional writes",async()=>{using var db=new FakeDb();Check(await Status(new ApiFunction(db),Request("owner"),context)==200 && db.Last!.TransactItems.Count==3);Check(db.Last!.TransactItems[0].Update.ExpressionAttributeValues[":owner"].S=="owner");Check(db.Last!.TransactItems[1].Put.Item["Before"].S=="Granted");Check(db.Last!.TransactItems[2].Put.Item["Delivery"].S=="Pending");}),
 ("conditional conflict is not reported as success",async()=>{using var db=new FakeDb{Conflict=true};Check(await Status(new ApiFunction(db),Request("owner"),context)==409);}),
 ("malformed body rejected",async()=>{using var db=new FakeDb();Check(await Status(new ApiFunction(db),Request("owner",raw:"bad-json"),context)==400 && db.Reads==0);})
};
foreach (var raw in new[] { "null", "[]", "true", "1", "{}", "{\"expectedVersion\":\"1\"}", "{\"expectedVersion\":null}", "{\"expectedVersion\":1.5}", "{\"expectedVersion\":2147483648}" })
    checks.Add(($"wrong-shaped body rejected: {raw}", async () =>
    {
        using var db = new FakeDb();
        Check(await Status(new ApiFunction(db), Request("owner", raw: raw), context) == 400 && db.Reads == 0);
    }
    ));
checks.Add(("malformed authorizer context fails closed", async () =>
{
    using var db = new FakeDb();
    Check(await Status(new ApiFunction(db), JsonSerializer.SerializeToElement(new { requestContext = "bad" }), context) == 401 && db.Reads == 0);
}
));
checks.Add(("reserved DynamoDB owner name is aliased", async () =>
{
    using var db = new FakeDb();
    await new ApiFunction(db).Handle(Request("owner"), context);
    var update = db.Last!.TransactItems[0].Update;
    Check(update.ExpressionAttributeNames["#owner"] == "Owner" && update.ConditionExpression.Contains("#owner = :owner"));
}
));
checks.Add(("service failure is not hidden as a conflict", async () =>
{
    using var db = new FakeDb { CancelCodes = ["ConditionalCheckFailed", "ProvisionedThroughputExceeded"] };
    await Expect<TransactionCanceledException>(() => new ApiFunction(db).Handle(Request("owner"), context));
}
));
checks.Add(("publisher skips unrelated stream writes", async () =>
{
    using var db = new FakeDb(); using var events = new FakeEvents();
    Check(await Failures(new PublisherFunction(db, events).Handle(Stream("MODIFY"), context)) == 0 && db.Reads == 0 && events.Calls == 0);
}
));
checks.Add(("publisher skips an already sent outbox", async () =>
{
    using var db = new FakeDb { Outbox = true, Sent = true }; using var events = new FakeEvents();
    Check(await Failures(new PublisherFunction(db, events).Handle(Stream(), context)) == 0 && events.Calls == 0 && db.Updates == 0);
}
));
checks.Add(("publisher sends the stored detail then marks sent", async () =>
{
    using var db = new FakeDb { Outbox = true }; using var events = new FakeEvents();
    Check(await Failures(new PublisherFunction(db, events).Handle(Stream(), context)) == 0 && events.Calls == 1 && db.Updates == 1);
    Check(events.Last!.Entries[0].Detail == Detail() && db.LastUpdate!.ExpressionAttributeValues[":sent"].S == "Sent");
}
));
checks.Add(("rejected publication stays pending and identifies stream retry", async () =>
{
    using var db = new FakeDb { Outbox = true }; using var events = new FakeEvents { Reject = true };
    var result = await FailureIds(new PublisherFunction(db, events).Handle(Stream(), context));
    Check(result.SequenceEqual(new[] { "sequence-1" }) && db.Updates == 0);
}
));
checks.Add(("failed sent marker is retried after accepted publication", async () =>
{
    using var db = new FakeDb { Outbox = true, FailUpdate = true }; using var events = new FakeEvents();
    Check(await Failures(new PublisherFunction(db, events).Handle(Stream(), context)) == 1 && events.Calls == 1);
}
));
checks.Add(("consumer atomically constructs dedup and newer-view writes", async () =>
{
    using var db = new FakeDb();
    Check(await Failures(new ConsumerFunction(db).Handle(Messages(Detail()), context)) == 0 && db.Writes == 1);
    Check(db.Last!.TransactItems[0].Put.Item["PK"].S == "PROCESSED#C-001:2");
    Check(db.Last.TransactItems[1].Update.ConditionExpression.Contains("#v < :next"));
}
));
foreach (var reason in new[] { new[] { "ConditionalCheckFailed", "None" }, new[] { "None", "ConditionalCheckFailed" } })
    checks.Add(("duplicate or stale consumer event is acknowledged: " + string.Join(",", reason), async () =>
    {
        using var db = new FakeDb { CancelCodes = reason };
        Check(await Failures(new ConsumerFunction(db).Handle(Messages(Detail()), context)) == 0);
    }
    ));
checks.Add(("consumer retries service failures even with a condition failure", async () =>
{
    using var db = new FakeDb { CancelCodes = ["ConditionalCheckFailed", "ProvisionedThroughputExceeded"] };
    Check((await FailureIds(new ConsumerFunction(db).Handle(Messages(Detail()), context))).SequenceEqual(new[] { "message-1" }));
}
));
checks.Add(("poison consumer message does not block a valid batch member", async () =>
{
    using var db = new FakeDb();
    Check((await FailureIds(new ConsumerFunction(db).Handle(Messages("bad-json", Detail()), context))).SequenceEqual(new[] { "message-1" }) && db.Writes == 1);
}
));
checks.Add(("inconsistent event identity rejected before storage", async () =>
{
    using var db = new FakeDb();
    Check(await Failures(new ConsumerFunction(db).Handle(Messages(Detail().Replace("C-001:2", "unrelated")), context)) == 1 && db.Writes == 0);
}
));
var failed = 0;
foreach (var (name, run) in checks) { try { await run(); Console.WriteLine("PASS " + name); } catch (Exception e) { failed++; Console.WriteLine("FAIL " + name + ": " + e.Message); } }
Console.WriteLine($"{checks.Count - failed}/{checks.Count} cloud adapter checks passed (no AWS calls)"); return failed == 0 ? 0 : 1;
static void Check(bool condition) { if (!condition) throw new Exception("Expected adapter behavior not observed"); }
static async Task Expect<T>(Func<Task<object>> run) where T : Exception { try { await run(); } catch (T) { return; } throw new Exception("Expected " + typeof(T).Name); }
static async Task<string[]> FailureIds(Task<object> run) => JsonSerializer.SerializeToElement(await run).GetProperty("batchItemFailures").EnumerateArray().Select(x => x.GetProperty("itemIdentifier").GetString()!).ToArray();
static async Task<int> Failures(Task<object> run) => (await FailureIds(run)).Length;
static string Detail() => "{\"eventId\":\"C-001:2\",\"consentId\":\"C-001\",\"version\":2,\"status\":\"Withdrawn\",\"schemaVersion\":1}";
static JsonElement Stream(string name = "INSERT") => JsonSerializer.SerializeToElement(new { Records = new[] { new { eventName = name, dynamodb = new { SequenceNumber = "sequence-1", NewImage = new { Kind = new { S = "Outbox" }, PK = new { S = "OUTBOX#C-001:2" } } } } } });
static JsonElement Messages(params string[] details) => JsonSerializer.SerializeToElement(new { Records = details.Select((d, i) => new { messageId = $"message-{i + 1}", body = d == "bad-json" ? d : "{\"detail\":" + d + "}" }) });
static async Task<int> Status(ApiFunction f, JsonElement request, ILambdaContext context) => JsonSerializer.SerializeToElement(await f.Handle(request, context)).GetProperty("statusCode").GetInt32();
static JsonElement Request(string owner, int version = 1, string? raw = null) => JsonSerializer.SerializeToElement(new { requestContext = new { authorizer = new { jwt = new { claims = new Dictionary<string, string> { { "sub", owner } } } } }, pathParameters = new { id = "C-001" }, body = raw ?? JsonSerializer.Serialize(new { expectedVersion = version }) });
public class ContextProxy : DispatchProxy { protected override object? Invoke(MethodInfo? method, object?[]? args) => method?.Name switch { "get_AwsRequestId" => "test-request", "get_Logger" => new QuietLogger(), _ => null }; }
public class QuietLogger : ILambdaLogger { public void Log(string message) { } public void LogLine(string message) { } }
public class FakeDb : AmazonDynamoDBClient
{
    public bool Missing, Withdrawn, Conflict, Outbox, Sent, FailUpdate; public int Version = 1, Reads, Writes, Updates; public string[]? CancelCodes; public TransactWriteItemsRequest? Last; public UpdateItemRequest? LastUpdate;
    public FakeDb() : base(new AnonymousAWSCredentials(), new AmazonDynamoDBConfig { RegionEndpoint = RegionEndpoint.USEast1 }) { }
    public override Task<GetItemResponse> GetItemAsync(GetItemRequest request, CancellationToken token = default) { Reads++; return Task.FromResult(new GetItemResponse { Item = Missing ? new() : Outbox ? new() { ["Delivery"] = Data.S(Sent ? "Sent" : "Pending"), ["Detail"] = Data.S("{\"eventId\":\"C-001:2\",\"consentId\":\"C-001\",\"version\":2,\"status\":\"Withdrawn\",\"schemaVersion\":1}") } : new() { ["Owner"] = Data.S("owner"), ["Version"] = Data.N(Version), ["Status"] = Data.S(Withdrawn ? "Withdrawn" : "Granted") } }); }
    public override Task<TransactWriteItemsResponse> TransactWriteItemsAsync(TransactWriteItemsRequest request, CancellationToken token = default) { Writes++; Last = request; if (Conflict || CancelCodes != null) throw new TransactionCanceledException("simulated cancellation") { CancellationReasons = (CancelCodes ?? ["ConditionalCheckFailed"]).Select(x => new CancellationReason { Code = x }).ToList() }; return Task.FromResult(new TransactWriteItemsResponse()); }
    public override Task<UpdateItemResponse> UpdateItemAsync(UpdateItemRequest request, CancellationToken token = default) { Updates++; LastUpdate = request; if (FailUpdate) throw new IOException("simulated marker failure"); return Task.FromResult(new UpdateItemResponse()); }
}
public class FakeEvents : AmazonEventBridgeClient
{
    public int Calls; public bool Reject; public PutEventsRequest? Last;
    public FakeEvents() : base(new AnonymousAWSCredentials(), new AmazonEventBridgeConfig { RegionEndpoint = RegionEndpoint.USEast1 }) { }
    public override Task<PutEventsResponse> PutEventsAsync(PutEventsRequest request, CancellationToken token = default) { Calls++; Last = request; return Task.FromResult(new PutEventsResponse { FailedEntryCount = Reject ? 1 : 0 }); }
}
