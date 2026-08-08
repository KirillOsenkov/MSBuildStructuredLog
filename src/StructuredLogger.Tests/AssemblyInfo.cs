using Xunit;

// Strings.ResourceSet (and other statics like SettingsService) are global mutable
// state shared by many tests, e.g. every test that reads a binlog reinitializes the
// resource culture via BinLogReader. Running test classes in parallel (xUnit's
// default) races on that state; the suite takes seconds, so just serialize it.
[assembly: CollectionBehavior(DisableTestParallelization = true)]
