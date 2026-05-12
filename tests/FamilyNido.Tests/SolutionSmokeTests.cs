namespace FamilyNido.Tests;

/// <summary>Smoke test to keep the project compiling. Real tests land with each module.</summary>
public sealed class SolutionSmokeTests
{
    /// <summary>Sanity check that xUnit is wired up.</summary>
    [Fact]
    public void Xunit_Is_Wired() => Assert.True(true);
}
