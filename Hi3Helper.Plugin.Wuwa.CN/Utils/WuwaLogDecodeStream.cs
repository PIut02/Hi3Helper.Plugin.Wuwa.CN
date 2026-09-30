using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

// ReSharper disable IdentifierTypo

namespace Hi3Helper.Plugin.Wuwa.CN.Utils;

/// <summary>
/// Decodes the encrypted Wuthering Waves client log while it is being streamed.
/// <br/>
/// The client started writing "<c>Client.log</c>" in an obfuscated form: every byte is XOR-ed with
/// <c>0xA5</c> when its lowest bit is set, and with <c>0xEF</c> otherwise. Without decoding, the
/// log viewer in the launcher shows garbage for an encrypted log.
/// <br/>
/// Ported from the upstream Hi3Helper.Plugin.Wuwa commit <c>0af8e8c</c>
/// ("feat(logs): Handle encrypted logs format").
/// </summary>
internal sealed class WuwaLogDecodeStream(Stream source) : Stream
{
    /// <summary>
    /// Number of bytes required to detect whether a log file is encrypted.
    /// </summary>
    internal const int EncryptedHeaderLength = 3;

    private const byte XorKeyForOddByte = 0xA5;
    private const byte XorKeyForEvenByte = 0xEF;

    public override bool CanRead => source.CanRead;
    public override bool CanSeek => false;
    public override bool CanWrite => false;
    public override long Length => source.Length;

    public override long Position
    {
        get => source.Position;
        set => throw new NotSupportedException();
    }

    /// <summary>
    /// Detects whether the given log header belongs to an encrypted log file.
    /// </summary>
    internal static bool IsEncryptedLogHeader(ReadOnlySpan<byte> header)
    {
        if (header.Length < EncryptedHeaderLength) return false;

        return header[0] switch
        {
            0x00 => header[1] == 0x54 && header[2] == 0x50,
            0xEF => header[1] == 0xBB && header[2] == 0xBF,
            0x4A => header[1] == 0x1E && header[2] == 0x1A,
            0xA5 => header[1] == 0xF1 && header[2] == 0xF5,
            _ => false
        };
    }

    public override int Read(byte[] buffer, int offset, int count)
    {
        var read = source.Read(buffer, offset, count);
        Decode(buffer.AsSpan(offset, read));
        return read;
    }

    public override int Read(Span<byte> buffer)
    {
        var read = source.Read(buffer);
        Decode(buffer[..read]);
        return read;
    }

    public override async ValueTask<int> ReadAsync(Memory<byte> buffer,
        CancellationToken cancellationToken = default)
    {
        var read = await source.ReadAsync(buffer, cancellationToken).ConfigureAwait(false);
        Decode(buffer.Span[..read]);
        return read;
    }

    public override Task<int> ReadAsync(byte[] buffer, int offset, int count,
        CancellationToken cancellationToken)
    {
        return Impl();

        async Task<int> Impl()
        {
            var read = await source.ReadAsync(buffer.AsMemory(offset, count), cancellationToken)
                .ConfigureAwait(false);
            Decode(buffer.AsSpan(offset, read));
            return read;
        }
    }

    private static void Decode(Span<byte> buffer)
    {
        foreach (ref var value in buffer)
            value = (byte)((value & 1) != 0
                ? value ^ XorKeyForOddByte
                : value ^ XorKeyForEvenByte);
    }

    public override void Flush() => throw new NotSupportedException();

    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

    public override void SetLength(long value) => throw new NotSupportedException();

    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
}
