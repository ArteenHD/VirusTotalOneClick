using System.Buffers.Binary;
using System.Text.Json;

namespace VirusTotalOneClick.Core;

/// <summary>
/// Frames invocations for the single-instance pipe: a 4-byte little-endian
/// length prefix followed by a UTF-8 JSON body, with hard size and path-count
/// caps so a malformed or hostile peer can never balloon the reader.
/// </summary>
public static class InvocationProtocol
{
    public const int MaxMessageBytes = 1024 * 1024;
    public const int MaxPathCount = 1024;

    public static async Task WriteAsync(
        Stream stream,
        CoalescedInvocation invocation,
        CancellationToken cancellationToken)
    {
        if (invocation.Paths.Count > MaxPathCount)
        {
            throw new ArgumentException(
                $"An invocation may carry at most {MaxPathCount} paths.",
                nameof(invocation));
        }

        var payload = JsonSerializer.SerializeToUtf8Bytes(invocation);
        if (payload.Length > MaxMessageBytes)
        {
            throw new ArgumentException(
                $"An invocation message may be at most {MaxMessageBytes} bytes.",
                nameof(invocation));
        }

        var header = new byte[4];
        BinaryPrimitives.WriteInt32LittleEndian(header, payload.Length);
        await stream.WriteAsync(header, cancellationToken).ConfigureAwait(false);
        await stream.WriteAsync(payload, cancellationToken).ConfigureAwait(false);
        await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Reads one framed invocation; null for end-of-stream or any malformed frame.</summary>
    public static async Task<CoalescedInvocation?> ReadAsync(
        Stream stream,
        CancellationToken cancellationToken)
    {
        var header = new byte[4];
        if (!await TryReadExactlyAsync(stream, header, cancellationToken).ConfigureAwait(false))
        {
            return null;
        }

        var length = BinaryPrimitives.ReadInt32LittleEndian(header);
        if (length <= 0 || length > MaxMessageBytes)
        {
            return null;
        }

        var payload = new byte[length];
        if (!await TryReadExactlyAsync(stream, payload, cancellationToken).ConfigureAwait(false))
        {
            return null;
        }

        try
        {
            var invocation = JsonSerializer.Deserialize<CoalescedInvocation>(payload);
            if (invocation?.Paths is null || invocation.Paths.Count > MaxPathCount)
            {
                return null;
            }

            return invocation;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static async Task<bool> TryReadExactlyAsync(
        Stream stream,
        byte[] buffer,
        CancellationToken cancellationToken)
    {
        var read = await stream
            .ReadAtLeastAsync(buffer, buffer.Length, throwOnEndOfStream: false, cancellationToken)
            .ConfigureAwait(false);
        return read == buffer.Length;
    }
}
