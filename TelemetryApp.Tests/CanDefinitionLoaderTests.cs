using System.Text;
using Xunit;
using TelemetryApp.Backend.Decoding;

namespace TelemetryApp.Backend.Tests;

public class CanDefinitionLoaderTests
{
    [Fact]
    public async Task LoadFromStreamAsync_WhenValidJsonProvided_ReturnsLoadedSet()
    {
        var validJson = @"{
            ""frames"": [
                {
                    ""id"": 10,
                    ""signals"": []
                }
            ]
        }";
        var stream = new MemoryStream(Encoding.UTF8.GetBytes(validJson));

        var result = await CanDefinitionLoader.LoadFromStreamAsync(stream);

        Assert.NotNull(result);
        Assert.True(result.FramesById.ContainsKey(10));
    }

    [Fact]
    public async Task LoadFromStreamAsync_WhenEmptyFrames_ThrowsInvalidOperationException()
    {
        var invalidJson = @"{ ""frames"": [] }";
        var stream = new MemoryStream(Encoding.UTF8.GetBytes(invalidJson));

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => CanDefinitionLoader.LoadFromStreamAsync(stream));
        Assert.Contains("JSON definicji CAN nie zawiera żadnych ramek", ex.Message);
    }
}