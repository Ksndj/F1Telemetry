using Xunit;

// WPF pack resource loading is process-global, so keep this test assembly out
// of xUnit's parallel lanes to avoid intermittent PackagePart failures on CI.
[assembly: CollectionBehavior(DisableTestParallelization = true)]
