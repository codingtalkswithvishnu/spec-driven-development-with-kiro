using System.Text.Json;
using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using Amazon.EventBridge;
using Amazon.EventBridge.Model;
using Amazon.Lambda.Core;

[assembly: LambdaSerializer(typeof(Amazon.Lambda.Serialization.SystemTextJson.DefaultLambdaJsonSerializer))]

namespace Consent.Cloud;

public static class Data
{
    public static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    public static string Table => Environment.GetEnvironmentVariable("TABLE_NAME") ?? throw new InvalidOperationException("TABLE_NAME missing");
    public static AttributeValue S(string value) => new() { S = value };
    public static AttributeValue N(int value) => new() { N = value.ToString(System.Globalization.CultureInfo.InvariantCulture) };
    public static Dictionary<string, AttributeValue> Key(string pk, string sk) => new() { ["PK"] = S(pk), ["SK"] = S(sk) };
    public static string Text(Dictionary<string, AttributeValue> item, string name) => item.TryGetValue(name, out var v) ? v.S ?? v.N ?? "" : "";
    public static string Field(JsonElement value, params string[] parts)
    {
        foreach (var part in parts)
            if (value.ValueKind != JsonValueKind.Object || !value.TryGetProperty(part, out value)) return "";
        return value.ValueKind == JsonValueKind.String ? value.GetString() ?? "" : value.ToString();
    }
    public static object Response(int status, string code, int? version = null) => new
    {
        statusCode = status,
        headers = new Dictionary<string, string> { ["content-type"] = "application/json", ["cache-control"] = "no-store" },
        body = JsonSerializer.Serialize(new { code, version }, Json)
    };
}

public sealed class ApiFunction
{
    private readonly IAmazonDynamoDB db;
    public ApiFunction() : this(new AmazonDynamoDBClient()) { }
    public ApiFunction(IAmazonDynamoDB db) { this.db = db; }

    public async Task<object> Handle(JsonElement input, ILambdaContext context)
    {
        // Only the configured API Gateway JWT authorizer supplies this context
        // on the public route. Direct Lambda invocation is privileged admin access.
        var actor = Data.Field(input, "requestContext", "authorizer", "jwt", "claims", "sub");
        if (string.IsNullOrWhiteSpace(actor)) return Data.Response(401, "Unauthenticated");
        var id = Data.Field(input, "pathParameters", "id");
        if (id.Length is < 1 or > 80 || id.Any(c => !char.IsAsciiLetterOrDigit(c) && c != '-')) return Data.Response(400, "InvalidRequest");
        JsonDocument body;
        try
        {
            var raw = Data.Field(input, "body");
            if (Data.Field(input, "isBase64Encoded") == "True" || Data.Field(input, "isBase64Encoded") == "true") raw = System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(raw));
            body = JsonDocument.Parse(raw);
        }
        catch (Exception e) when (e is JsonException or FormatException) { return Data.Response(400, "InvalidRequest"); }
        using (body)
        {
            if (body.RootElement.ValueKind != JsonValueKind.Object ||
                !body.RootElement.TryGetProperty("expectedVersion", out var v) ||
                v.ValueKind != JsonValueKind.Number || !v.TryGetInt32(out var expected) || expected < 1)
                return Data.Response(400, "InvalidVersion");
            var stored = await db.GetItemAsync(new GetItemRequest { TableName = Data.Table, Key = Data.Key($"CONSENT#{id}", "STATE"), ConsistentRead = true });
            var item = stored.Item;
            // Same response for absent and other-owned records avoids exposing existence.
            if (item == null || item.Count == 0 || Data.Text(item, "Owner") != actor) return Data.Response(404, "NotFound");
            var version = int.Parse(Data.Text(item, "Version"));
            if (Data.Text(item, "Status") == "Withdrawn") return Data.Response(200, "AlreadyWithdrawn", version);
            if (version != expected) return Data.Response(409, "Conflict");
            if (Data.Text(item, "Status") != "Granted") throw new InvalidDataException("Unexpected stored consent status");
            var next = checked(version + 1);
            var eventId = $"{id}:{next}";
            var detail = JsonSerializer.Serialize(new { eventId, consentId = id, version = next, status = "Withdrawn", schemaVersion = 1 }, Data.Json);
            var audit = Data.Key($"AUDIT#{id}", $"VERSION#{next}");
            audit["Actor"] = Data.S(actor); audit["Before"] = Data.S("Granted"); audit["After"] = Data.S("Withdrawn");
            audit["RequestId"] = Data.S(context.AwsRequestId); audit["At"] = Data.S(DateTimeOffset.UtcNow.ToString("O"));
            var pending = Data.Key($"OUTBOX#{eventId}", "EVENT");
            pending["Kind"] = Data.S("Outbox"); pending["Delivery"] = Data.S("Pending"); pending["Detail"] = Data.S(detail);
            try
            {
                await db.TransactWriteItemsAsync(new TransactWriteItemsRequest
                {
                    TransactItems = [
                    new() { Update = new Update { TableName = Data.Table, Key = Data.Key($"CONSENT#{id}","STATE"),
                        ConditionExpression = "#owner = :owner AND #v = :old AND #s = :granted",
                        UpdateExpression = "SET #s = :withdrawn, #v = :next",
                        ExpressionAttributeNames = new() { ["#owner"] = "Owner", ["#v"] = "Version", ["#s"] = "Status" },
                        ExpressionAttributeValues = new() { [":owner"] = Data.S(actor), [":old"] = Data.N(version), [":next"] = Data.N(next), [":granted"] = Data.S("Granted"), [":withdrawn"] = Data.S("Withdrawn") } } },
                    new() { Put = new Put { TableName = Data.Table, Item = audit, ConditionExpression = "attribute_not_exists(PK)" } },
                    new() { Put = new Put { TableName = Data.Table, Item = pending, ConditionExpression = "attribute_not_exists(PK)" } }
                ]
                });
            }
            catch (TransactionCanceledException e) when (
                e.CancellationReasons?.Any(x => x.Code == "ConditionalCheckFailed") == true &&
                e.CancellationReasons.All(x => x.Code is "None" or "ConditionalCheckFailed"))
            {
                return Data.Response(409, "Conflict");
            }
            return Data.Response(200, "Withdrawn", next);
        }
    }
}

public sealed class PublisherFunction
{
    private readonly IAmazonDynamoDB db;
    private readonly IAmazonEventBridge events;
    public PublisherFunction() : this(new AmazonDynamoDBClient(), new AmazonEventBridgeClient()) { }
    public PublisherFunction(IAmazonDynamoDB db, IAmazonEventBridge events) { this.db = db; this.events = events; }
    public async Task<object> Handle(JsonElement input, ILambdaContext context)
    {
        var failures = new List<object>();
        foreach (var record in input.GetProperty("Records").EnumerateArray())
        {
            if (Data.Field(record, "eventName") != "INSERT") continue;
            if (!record.GetProperty("dynamodb").TryGetProperty("NewImage", out var image)) continue;
            if (Data.Field(image, "Kind", "S") != "Outbox") continue;
            var pk = Data.Field(image, "PK", "S");
            try
            {
                var current = await db.GetItemAsync(new GetItemRequest { TableName = Data.Table, Key = Data.Key(pk, "EVENT"), ConsistentRead = true });
                if (current.Item == null || current.Item.Count == 0 || Data.Text(current.Item, "Delivery") == "Sent") continue;
                var result = await events.PutEventsAsync(new PutEventsRequest
                {
                    Entries = [new PutEventsRequestEntry {
                    EventBusName=Environment.GetEnvironmentVariable("BUS_NAME"), Source="consent.workshop", DetailType="ConsentWithdrawn", Detail=Data.Text(current.Item,"Detail")
                }]
                });
                if (result.FailedEntryCount > 0) throw new IOException("Event publication was rejected");
                await db.UpdateItemAsync(new UpdateItemRequest { TableName = Data.Table, Key = Data.Key(pk, "EVENT"), UpdateExpression = "SET Delivery = :sent", ExpressionAttributeValues = new() { [":sent"] = Data.S("Sent") } });
            }
            catch (Exception)
            {
                context.Logger.LogLine("Outbox publication failed; record will be retried.");
                failures.Add(new { itemIdentifier = Data.Field(record, "dynamodb", "SequenceNumber") });
            }
        }
        return new { batchItemFailures = failures };
    }
}

public sealed class ConsumerFunction
{
    private readonly IAmazonDynamoDB db;
    public ConsumerFunction() : this(new AmazonDynamoDBClient()) { }
    public ConsumerFunction(IAmazonDynamoDB db) { this.db = db; }
    public async Task<object> Handle(JsonElement input, ILambdaContext context)
    {
        var failures = new List<object>();
        foreach (var message in input.GetProperty("Records").EnumerateArray())
        {
            try
            {
                using var envelope = JsonDocument.Parse(Data.Field(message, "body"));
                var detail = envelope.RootElement.GetProperty("detail");
                var id = Data.Field(detail, "consentId"); var eventId = Data.Field(detail, "eventId");
                var version = detail.GetProperty("version").GetInt32();
                if (id.Length is < 1 or > 80 || id.Any(c => !char.IsAsciiLetterOrDigit(c) && c != '-') ||
                    version < 1 || eventId != $"{id}:{version}" ||
                    detail.GetProperty("schemaVersion").GetInt32() != 1 || Data.Field(detail, "status") != "Withdrawn")
                    throw new JsonException("Invalid event");
                var dedup = Data.Key($"PROCESSED#{eventId}", "EVENT");
                try
                {
                    await db.TransactWriteItemsAsync(new TransactWriteItemsRequest
                    {
                        TransactItems = [
                        new() { Put=new Put { TableName=Data.Table,Item=dedup,ConditionExpression="attribute_not_exists(PK)" } },
                        new() { Update=new Update { TableName=Data.Table,Key=Data.Key($"VIEW#{id}","STATE"),
                            ConditionExpression="attribute_not_exists(#v) OR #v < :next",UpdateExpression="SET #v = :next, #s = :withdrawn",
                            ExpressionAttributeNames=new(){["#v"]="Version",["#s"]="Status"},ExpressionAttributeValues=new(){[":next"]=Data.N(version),[":withdrawn"]=Data.S("Withdrawn")} } }
                    ]
                    });
                }
                catch (TransactionCanceledException e) when (e.CancellationReasons?.Any(x => x.Code == "ConditionalCheckFailed") == true && e.CancellationReasons.All(x => x.Code == "None" || x.Code == "ConditionalCheckFailed"))
                {
                    // A concurrent consumer, duplicate, or stale version already made
                    // this update unnecessary. Other service failures must be retried.
                }
            }
            catch (Exception)
            {
                context.Logger.LogLine("Consumer failed; message will be retried.");
                failures.Add(new { itemIdentifier = Data.Field(message, "messageId") });
            }
        }
        return new { batchItemFailures = failures };
    }
}
