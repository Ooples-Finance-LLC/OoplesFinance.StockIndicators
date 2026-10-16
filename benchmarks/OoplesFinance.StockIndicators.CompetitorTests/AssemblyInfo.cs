using Xunit;

// Benchmark fixtures temporarily mutate process-wide worker counts and native
// library settings. Serialize collections so setup/cleanup cannot overlap.
// Hosted CI still runs independent shards in separate processes.
[assembly: CollectionBehavior(DisableTestParallelization = true)]
