namespace Aurvangar.Sim.Tests.Support;

/// <summary>Body for acceptance tests that are specified but not yet written. Un-skipping one of these fails
/// until the test body is written, so a placeholder can never pass by accident.</summary>
public static class Placeholder
{
    public static void Write(string what) =>
        throw new NotImplementedException($"Acceptance test not written yet. Write it before implementing: {what}");
}
