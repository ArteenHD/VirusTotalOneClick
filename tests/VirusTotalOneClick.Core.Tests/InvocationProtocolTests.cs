using System.Buffers.Binary;
using System.Text;
using System.Text.Json;

namespace VirusTotalOneClick.Core.Tests;

public class InvocationProtocolTests
{
    private static async Task WriteRawFrameAsync(Stream stream, int declaredLength, byte[] payload)
    {
        var header = new byte[4];
        BinaryPrimitives.WriteInt32LittleEndian(header, declaredLength);
        await stream.WriteAsync(header);
        await stream.WriteAsync(payload);
    }

    [Fact]
    public async Task RoundTripsPathsThroughAStream()
    {
        using var stream = new MemoryStream();
        var invocation = new CoalescedInvocation(
            [@"C:\files\report one.pdf", @"C:\files\日本語 назва.txt"]);

        await InvocationProtocol.WriteAsync(stream, invocation, CancellationToken.None);
        stream.Position = 0;
        var decoded = await InvocationProtocol.ReadAsync(stream, CancellationToken.None);

        Assert.NotNull(decoded);
        Assert.Equal(invocation.Paths, decoded.Paths);
    }

    [Fact]
    public async Task SequentialMessagesReadBackInOrder()
    {
        using var stream = new MemoryStream();
        await InvocationProtocol.WriteAsync(stream, new CoalescedInvocation([@"C:\a.txt"]), CancellationToken.None);
        await InvocationProtocol.WriteAsync(stream, new CoalescedInvocation([@"C:\b.txt"]), CancellationToken.None);
        stream.Position = 0;

        var first = await InvocationProtocol.ReadAsync(stream, CancellationToken.None);
        var second = await InvocationProtocol.ReadAsync(stream, CancellationToken.None);

        Assert.Equal([@"C:\a.txt"], first!.Paths);
        Assert.Equal([@"C:\b.txt"], second!.Paths);
    }

    [Fact]
    public async Task AnEmptyStreamYieldsNothing()
    {
        using var stream = new MemoryStream();

        Assert.Null(await InvocationProtocol.ReadAsync(stream, CancellationToken.None));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    [InlineData(InvocationProtocol.MaxMessageBytes + 1)]
    public async Task AnOutOfRangeDeclaredLengthIsRejected(int declaredLength)
    {
        using var stream = new MemoryStream();
        await WriteRawFrameAsync(stream, declaredLength, Encoding.UTF8.GetBytes("{\"Paths\":[]}"));
        stream.Position = 0;

        Assert.Null(await InvocationProtocol.ReadAsync(stream, CancellationToken.None));
    }

    [Fact]
    public async Task ATruncatedPayloadIsRejected()
    {
        using var stream = new MemoryStream();
        await WriteRawFrameAsync(stream, declaredLength: 100, Encoding.UTF8.GetBytes("short"));
        stream.Position = 0;

        Assert.Null(await InvocationProtocol.ReadAsync(stream, CancellationToken.None));
    }

    [Fact]
    public async Task GarbagePayloadsAreRejected()
    {
        using var stream = new MemoryStream();
        var garbage = Encoding.UTF8.GetBytes("this is not json");
        await WriteRawFrameAsync(stream, garbage.Length, garbage);
        stream.Position = 0;

        Assert.Null(await InvocationProtocol.ReadAsync(stream, CancellationToken.None));
    }

    [Fact]
    public async Task WritingRefusesAnOverCapPathCount()
    {
        using var stream = new MemoryStream();
        var paths = Enumerable.Repeat(@"C:\x.txt", InvocationProtocol.MaxPathCount + 1).ToList();

        await Assert.ThrowsAsync<ArgumentException>(
            () => InvocationProtocol.WriteAsync(stream, new CoalescedInvocation(paths), CancellationToken.None));
    }

    [Fact]
    public async Task ReadingRejectsAnOverCapPathCount()
    {
        using var stream = new MemoryStream();
        var paths = Enumerable.Repeat(@"C:\x.txt", InvocationProtocol.MaxPathCount + 1).ToList();
        var payload = JsonSerializer.SerializeToUtf8Bytes(new CoalescedInvocation(paths));
        await WriteRawFrameAsync(stream, payload.Length, payload);
        stream.Position = 0;

        Assert.Null(await InvocationProtocol.ReadAsync(stream, CancellationToken.None));
    }

    [Fact]
    public async Task APayloadMissingThePathsFieldIsRejected()
    {
        using var stream = new MemoryStream();
        var payload = Encoding.UTF8.GetBytes("{}");
        await WriteRawFrameAsync(stream, payload.Length, payload);
        stream.Position = 0;

        Assert.Null(await InvocationProtocol.ReadAsync(stream, CancellationToken.None));
    }
}
