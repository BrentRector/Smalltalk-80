using Xunit;

/// <summary>
/// Disables test parallelization so trace output from interpreter runs in order.
/// </summary>
[assembly: CollectionBehavior(DisableTestParallelization = true)]

