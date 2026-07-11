// #303 — run the integration test classes sequentially. Each class boots its own
// WebApplicationFactory (a full host with Marten + Wolverine + Identity) against one shared
// Postgres; running them in parallel makes several hosts start and serve concurrently, which
// contends on the shared database and Wolverine's first-request endpoint code-gen and produced
// intermittent failures. Serializing the classes trades a little wall-clock for determinism.
[assembly: Xunit.CollectionBehavior(DisableTestParallelization = true)]
