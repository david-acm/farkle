// #304 — run the integration test classes sequentially. The Alba harness shares one IAlbaHost per
// collection (a full Marten + Wolverine + Identity host) and resets Marten data between tests; the
// main and scripted-id collections each own a host against the same Postgres. Running them in
// parallel makes hosts serve concurrently and race on the shared database and on Wolverine's
// durability agent, which produced intermittent failures. Serializing the classes trades a little
// wall-clock for determinism.
[assembly: Xunit.CollectionBehavior(DisableTestParallelization = true)]
