namespace CliClient.Tests;

/// <summary>
/// Tests for ConnectionOptions record struct
/// </summary>
public class ConnectionOptionsTests
{
    [Fact]
    public void ConnectionOptions_DefaultValues_HasDefaultBehavior()
    {
        // Act
        var options = new ConnectionOptions();

        // Assert
        Assert.Null(options.ServerUrl);
        Assert.Null(options.GroupName);
    }

    [Fact]
    public void ConnectionOptions_ToString_ContainsValues()
    {
        // Arrange
        var options = new ConnectionOptions
        {
            ServerUrl = "http://localhost:5000",
            GroupName = "test-group"
        };

        // Act
        var toString = options.ToString();

        // Assert
        Assert.Contains("http://localhost:5000", toString);
        Assert.Contains("test-group", toString);
    }

    [Theory]
    [InlineData("http://localhost:5000", "group1")]
    [InlineData("http://example.com", "group2")]
    [InlineData("http://127.0.0.1:8080", null)]
    public void ConnectionOptions_WithVariousValues_WorksCorrectly(string serverUrl, string? groupName)
    {
        // Act
        var options = new ConnectionOptions
        {
            ServerUrl = serverUrl,
            GroupName = groupName
        };

        // Assert
        Assert.Equal(serverUrl, options.ServerUrl);
        Assert.Equal(groupName, options.GroupName);
    }
}
