namespace CliClient.Tests;

/// <summary>
/// Simple basic test to verify test infrastructure works
/// </summary>
public class BasicTests
{
    [Fact]
    public void SimpleTest_ShouldPass()
    {
        // Arrange
        var expected = 1;

        // Act
        var actual = 1;

        // Assert
        Assert.Equal(expected, actual);
    }
}
