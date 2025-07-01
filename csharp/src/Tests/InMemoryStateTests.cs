using InMemoryServer;

namespace Tests;

/// <summary>
/// Tests for InMemoryState
/// </summary>
public class InMemoryStateTests
{
    [Fact]
    public void KeyValueStore_ShouldBeEmpty_Initially()
    {
        // Arrange & Act
        var state = new InMemoryState();

        // Assert
        Assert.Empty(state.KeyValueStore);
        Assert.Empty(state.KeyWatchers);
        Assert.Empty(state.BattleStates);
    }

    [Fact]
    public void KeyValueStore_ShouldStore_KeyValuePairs()
    {
        // Arrange
        var state = new InMemoryState();
        const string key = "test_key";
        const string value = "test_value";

        // Act
        state.KeyValueStore[key] = value;

        // Assert
        Assert.True(state.KeyValueStore.ContainsKey(key));
        Assert.Equal(value, state.KeyValueStore[key]);
    }
}
