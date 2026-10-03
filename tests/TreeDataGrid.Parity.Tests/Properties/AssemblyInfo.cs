using Xunit;

// These comparisons execute the actual UI-framework binding implementations.
// Avalonia's WeakEventHandlerManager shares unsynchronized event/delegate metadata
// between generic binding types; concurrent test classes are not independent UI
// owners. Match the existing Avalonia control-test assembly's isolation policy.
// This changes only differential-test scheduling, not production synchronization,
// observer semantics, assertion coverage or the paired performance processes.
[assembly: CollectionBehavior(DisableTestParallelization = true)]
