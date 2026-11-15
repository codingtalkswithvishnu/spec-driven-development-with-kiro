using Consent.Core;

var tests = new (string Name, Action Run)[] {
    ("R1 rejects another patient", () => { var s=Seed(); Check(!s.Withdraw("P-002","C-001",1,"r").Success); Check(s.Find("C-001")!.Status==ConsentStatus.Granted); Check(s.Audit.Length==0); }),
    ("R2 withdraws the selected consent", () => { var s=Seed(); var r=s.Withdraw("P-001","C-001",1,"r"); Check(r.Success && r.Consent!.Status==ConsentStatus.Withdrawn && r.Consent.Version==2); }),
    ("R3 preserves another purpose", () => { var s=Seed(); s.Withdraw("P-001","C-001",1,"r"); Check(s.Find("C-002")!.Status==ConsentStatus.Granted); }),
    ("R4 repeats without another transition", () => { var s=Seed(); s.Withdraw("P-001","C-001",1,"r"); Check(s.Withdraw("P-001","C-001",1,"r-again").Code=="AlreadyWithdrawn"); Check(s.Audit.Length==1 && s.Outbox.Length==1); }),
    ("R5 leaves no partial change after failure", () => { var s=Seed(()=>throw new IOException("simulated failure")); try { s.Withdraw("P-001","C-001",1,"r"); throw new Exception("Expected IOException"); } catch(IOException) {} Check(s.Find("C-001")!.Status==ConsentStatus.Granted && s.Audit.Length==0 && s.Outbox.Length==0); }),
    ("R6 does not create a missing consent", () => { var s=Seed(); Check(s.Withdraw("P-001","missing",1,"r").Code=="NotFound"); Check(s.Find("missing")==null); }),
    ("rejects stale versions", () => { var s=Seed(); Check(s.Withdraw("P-001","C-001",0,"r").Code=="Conflict"); Check(s.Audit.Length==0); }),
    ("rejects a missing identity", () => { var s=Seed(); Check(s.Withdraw("","C-001",1,"r").Code=="Unauthenticated"); }),
    ("concurrent withdrawals create one transition", () => { var s=Seed(); Parallel.For(0,32,i=>s.Withdraw("P-001","C-001",1,$"r-{i}")); Check(s.Audit.Length==1 && s.Outbox.Length==1 && s.Find("C-001")!.Version==2); }),
    ("generated retry counts preserve state", () => { for(var count=1;count<=100;count++) { var s=Seed(); for(var i=0;i<count;i++) s.Withdraw("P-001","C-001",1,$"r-{i}"); Check(s.Find("C-001")!.Version==2 && s.Audit.Length==1 && s.Outbox.Length==1); } }),
    ("rejection cannot undo a withdrawal", () => { var s=Seed(); s.Withdraw("P-001","C-001",1,"ok"); Check(!s.Withdraw("P-002","C-001",2,"denied").Success); Check(s.Find("C-001")!.Status==ConsentStatus.Withdrawn && s.Audit.Length==1); }),
    ("audit and event agree with saved version", () => { var s=Seed(); var r=s.Withdraw("P-001","C-001",1,"request-1"); Check(s.Audit[0].RequestId=="request-1" && s.Audit[0].Actor=="P-001" && s.Outbox[0].Version==r.Consent!.Version); })
};
var failures = 0;
foreach (var (name, run) in tests) { try { run(); Console.WriteLine($"PASS {name}"); } catch (Exception e) { failures++; Console.WriteLine($"FAIL {name}: {e.Message}"); } }
Console.WriteLine($"{tests.Length - failures}/{tests.Length} checks passed");
return failures == 0 ? 0 : 1;

static void Check(bool condition) { if (!condition) throw new Exception("Expected behavior was not observed"); }
static ConsentStore Seed(Action? fail = null) => new([
    new("C-001","P-001","research-demo",ConsentStatus.Granted,1),
    new("C-002","P-001","care-demo",ConsentStatus.Granted,1)], fail);
