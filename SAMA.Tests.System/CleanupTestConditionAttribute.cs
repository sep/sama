namespace SAMA.Tests.System;

/// <summary>
/// Condition attribute that only runs cleanup tests when explicitly enabled.
/// These tests remove stale systest_* schemas and clear smtp4dev emails.
/// Tests run when CLEANUP_SYSTEM_TESTS environment variable is set to "true".
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = false, Inherited = false)]
public sealed class CleanupTestConditionAttribute : ConditionBaseAttribute
{
    public CleanupTestConditionAttribute()
        : base(ConditionMode.Include)
    {
        IgnoreMessage = "Cleanup tests are skipped by default. Set CLEANUP_SYSTEM_TESTS=true to run.";
    }

    public override string GroupName => nameof(CleanupTestConditionAttribute);

    public override bool IsConditionMet =>
        Environment.GetEnvironmentVariable("CLEANUP_SYSTEM_TESTS") == "true";
}
