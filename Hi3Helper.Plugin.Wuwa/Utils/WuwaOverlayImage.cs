using System;
using System.Buffers.Binary;
using System.IO;
using System.IO.Compression;

// ReSharper disable IdentifierTypo

namespace Hi3Helper.Plugin.Wuwa.Utils;

/// <summary>
/// Composes the launcher logo overlay out of the pieces the game's API hands out.
/// <br/><br/>
/// Why this exists: the launcher renders the logo overlay as a single layer with
/// <c>Stretch=UniformToFill</c> and centered alignment, and offers no way to move it. The overlay
/// is therefore positioned purely by its own pixel content. The API's <c>slogan</c> image puts the
/// version title art in the bottom-right corner of a 2560x1520 canvas, which lands underneath the
/// launcher's social media panel, and there is no separate API asset for the wordmark that the
/// official launcher shows in the top-left corner. Both are solved by baking a composed image.
/// <br/><br/>
/// The compositor is deliberately dependency free and AOT safe: it only decodes the PNG subset the
/// CDN actually serves (8-bit truecolor/palette, non-interlaced) and bails out with
/// <c>false</c> for anything else, so the caller can fall back to the raw API image.
/// </summary>
internal static class WuwaOverlayImage
{
    /// <summary>
    /// Position of the composed layers, expressed as a ratio of the canvas size.
    /// </summary>
    /// <param name="TitleShiftRatio">
    /// How far the title art is shifted to the left so it clears the social media panel.
    /// </param>
    /// <param name="WordmarkXRatio">Left edge of the wordmark.</param>
    /// <param name="WordmarkYRatio">Top edge of the wordmark.</param>
    /// <param name="WordmarkHeightRatio">
    /// Height of the wordmark. The width follows from the artwork's aspect ratio, so that both the
    /// stacked Chinese lockups and the wide latin wordmark end up optically the same size.
    /// </param>
    internal readonly record struct OverlayLayout(float TitleShiftRatio,
                                                 float WordmarkXRatio,
                                                 float WordmarkYRatio,
                                                 float WordmarkHeightRatio)
    {
        /// <summary>
        /// Layout matched against the overlay artwork Collapse itself ships for its built-in games.
        /// <br/><br/>
        /// Those overlays are 2560x1440 (exactly 16:9) canvases whose game logo sits at
        /// <b>left 10.0% / top 7.2%</b> and measures about <b>5.9% wide and 8.3% tall</b> of the canvas.
        /// The values below reproduce that geometry once the game area is mapped onto a 16:9 window:
        /// no horizontal cropping happens there, so the left ratio carries over directly, while the
        /// vertical ratio is compensated for the 2.8% the taller canvases lose at the top and bottom.
        /// The title art is moved far enough left that the social media panel (~4% of the window
        /// width) no longer covers it.
        /// </summary>
        internal static OverlayLayout Default => new(0.07f, 0.10f, 0.095f, 0.08f);
    }

    /// <summary>
    /// Draws the wordmark onto the title art and returns the composed image as PNG bytes.
    /// </summary>
    /// <returns><c>false</c> when one of the images uses a PNG variant this compositor does not
    /// support; in that case the caller should keep using the original artwork.</returns>
    internal static bool TryCompose(ReadOnlySpan<byte> titleArtPng,
                                   ReadOnlySpan<byte> wordmarkPng,
                                   OverlayLayout layout,
                                   out byte[] composedPng,
                                   out string failureReason)
    {
        composedPng = [];
        failureReason = string.Empty;

        if (!TryDecodePng(titleArtPng, out int width, out int height, out byte[] canvas, out failureReason))
        {
            return false;
        }

        if (!TryDecodePng(wordmarkPng, out int markWidth, out int markHeight, out byte[] mark, out failureReason))
        {
            return false;
        }

        try
        {
            // 1. Shift the title art to the left so the social media panel can no longer cover it.
            //    Only whole pixels are moved, so the artwork is not resampled and loses no quality.
            int shiftPixels = (int)MathF.Round(width * layout.TitleShiftRatio);
            if (shiftPixels > 0)
            {
                for (int y = 0; y < height; y++)
                {
                    ShiftRowLeft(canvas.AsSpan(y * width * 4, width * 4), shiftPixels);
                }
            }

            // 2. Draw the wordmark into the top-left corner, sized by its height so that the stacked
            //    Chinese lockup and the wide latin wordmark look equally large.
            int targetHeight = Math.Max(1, (int)MathF.Round(height * layout.WordmarkHeightRatio));
            int targetWidth = Math.Max(1, (int)MathF.Round(markWidth * (float)targetHeight / markHeight));
            int targetX = (int)MathF.Round(width * layout.WordmarkXRatio);
            int targetY = (int)MathF.Round(height * layout.WordmarkYRatio);

            DrawResized(canvas, width, height, mark, markWidth, markHeight, targetX, targetY, targetWidth, targetHeight);

            return TryEncodePng(width, height, canvas, out composedPng, out failureReason);
        }
        catch (Exception ex)
        {
            failureReason = ex.Message;
            return false;
        }
    }

    /// <summary>
    /// Moves one scanline to the left by <paramref name="shiftPixels"/>, filling the freed space
    /// on the right with transparency.
    /// </summary>
    private static void ShiftRowLeft(Span<byte> row, int shiftPixels)
    {
        int shiftBytes = shiftPixels * 4;
        int remainingBytes = row.Length - shiftBytes;
        if (remainingBytes <= 0)
        {
            row.Clear();
            return;
        }

        // Copying from right to left is safe because the source index is always ahead of the
        // destination index.
        for (int i = 0; i < remainingBytes; i++)
        {
            row[i] = row[i + shiftBytes];
        }

        row[remainingBytes..].Clear();
    }

    /// <summary>
    /// Draws the source image resized into the destination with a box filter (area average) and
    /// straight-alpha source-over blending.
    /// </summary>
    private static void DrawResized(byte[] destination, int destinationWidth, int destinationHeight,
                                    byte[] source, int sourceWidth, int sourceHeight,
                                    int targetX, int targetY, int targetWidth, int targetHeight)
    {
        float scaleX = (float)sourceWidth / targetWidth;
        float scaleY = (float)sourceHeight / targetHeight;

        for (int y = 0; y < targetHeight; y++)
        {
            int destinationY = targetY + y;
            if ((uint)destinationY >= (uint)destinationHeight)
            {
                continue;
            }

            int sourceYStart = (int)(y * scaleY);
            int sourceYEnd = Math.Min(sourceHeight, Math.Max(sourceYStart + 1, (int)((y + 1) * scaleY)));

            for (int x = 0; x < targetWidth; x++)
            {
                int destinationX = targetX + x;
                if ((uint)destinationX >= (uint)destinationWidth)
                {
                    continue;
                }

                int sourceXStart = (int)(x * scaleX);
                int sourceXEnd = Math.Min(sourceWidth, Math.Max(sourceXStart + 1, (int)((x + 1) * scaleX)));

                // Average the source block (premultiplied so transparent pixels do not darken the edges).
                int sampleCount = 0;
                long sumRed = 0, sumGreen = 0, sumBlue = 0, sumAlpha = 0;
                for (int sourceY = sourceYStart; sourceY < sourceYEnd; sourceY++)
                {
                    int rowOffset = (sourceY * sourceWidth + sourceXStart) * 4;
                    for (int sourceX = sourceXStart; sourceX < sourceXEnd; sourceX++, rowOffset += 4)
                    {
                        int alpha = source[rowOffset + 3];
                        sumAlpha += alpha;
                        sumRed += source[rowOffset] * alpha;
                        sumGreen += source[rowOffset + 1] * alpha;
                        sumBlue += source[rowOffset + 2] * alpha;
                        sampleCount++;
                    }
                }

                if (sampleCount == 0 || sumAlpha == 0)
                {
                    continue;
                }

                int destinationOffset = (destinationY * destinationWidth + destinationX) * 4;
                int sourceAlphaAverage = (int)(sumAlpha / sampleCount);
                if (sourceAlphaAverage == 0)
                {
                    continue;
                }

                int red = (int)(sumRed / sumAlpha);
                int green = (int)(sumGreen / sumAlpha);
                int blue = (int)(sumBlue / sumAlpha);

                BlendSourceOver(destination, destinationOffset, red, green, blue, sourceAlphaAverage);
            }
        }
    }

    /// <summary>
    /// Blends one straight-alpha pixel over the destination pixel.
    /// </summary>
    private static void BlendSourceOver(byte[] destination, int offset,
                                        int red, int green, int blue, int alpha)
    {
        if (alpha >= 255)
        {
            destination[offset] = (byte)red;
            destination[offset + 1] = (byte)green;
            destination[offset + 2] = (byte)blue;
            destination[offset + 3] = 255;
            return;
        }

        int destinationAlpha = destination[offset + 3];
        int outAlpha = alpha + destinationAlpha * (255 - alpha) / 255;
        if (outAlpha == 0)
        {
            destination[offset] = destination[offset + 1] = destination[offset + 2] = destination[offset + 3] = 0;
            return;
        }

        int destinationWeight = destinationAlpha * (255 - alpha) / 255;
        destination[offset] = (byte)((red * alpha + destination[offset] * destinationWeight) / outAlpha);
        destination[offset + 1] = (byte)((green * alpha + destination[offset + 1] * destinationWeight) / outAlpha);
        destination[offset + 2] = (byte)((blue * alpha + destination[offset + 2] * destinationWeight) / outAlpha);
        destination[offset + 3] = (byte)outAlpha;
    }

    #region PNG decoding

    private static readonly byte[] PngSignature = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];

    /// <summary>
    /// Decodes an 8-bit, non-interlaced truecolor (optionally with alpha) or palette PNG into RGBA.
    /// </summary>
    private static bool TryDecodePng(ReadOnlySpan<byte> png, out int width, out int height,
                                     out byte[] rgba, out string failureReason)
    {
        width = 0;
        height = 0;
        rgba = [];
        failureReason = string.Empty;

        if (png.Length < 8 || !png[..8].SequenceEqual(PngSignature))
        {
            failureReason = "not a PNG file";
            return false;
        }

        int bitDepth = 0;
        int colorType = 0;
        int interlace = 0;
        byte[]? palette = null;
        byte[]? paletteAlpha = null;
        using MemoryStream compressed = new();

        int offset = 8;
        while (offset + 8 <= png.Length)
        {
            int chunkLength = BinaryPrimitives.ReadInt32BigEndian(png[offset..]);
            ReadOnlySpan<byte> chunkType = png.Slice(offset + 4, 4);
            int dataOffset = offset + 8;
            if (chunkLength < 0 || dataOffset + chunkLength > png.Length)
            {
                failureReason = "truncated PNG chunk";
                return false;
            }

            ReadOnlySpan<byte> chunkData = png.Slice(dataOffset, chunkLength);

            if (chunkType.SequenceEqual("IHDR"u8))
            {
                if (chunkLength < 13)
                {
                    failureReason = "invalid IHDR";
                    return false;
                }

                width = BinaryPrimitives.ReadInt32BigEndian(chunkData);
                height = BinaryPrimitives.ReadInt32BigEndian(chunkData[4..]);
                bitDepth = chunkData[8];
                colorType = chunkData[9];
                interlace = chunkData[12];
            }
            else if (chunkType.SequenceEqual("PLTE"u8))
            {
                palette = chunkData.ToArray();
            }
            else if (chunkType.SequenceEqual("tRNS"u8))
            {
                paletteAlpha = chunkData.ToArray();
            }
            else if (chunkType.SequenceEqual("IDAT"u8))
            {
                compressed.Write(chunkData);
            }
            else if (chunkType.SequenceEqual("IEND"u8))
            {
                break;
            }

            offset = dataOffset + chunkLength + 4;
        }

        if (width <= 0 || height <= 0)
        {
            failureReason = "missing IHDR";
            return false;
        }

        if (bitDepth != 8 || interlace != 0)
        {
            failureReason = $"unsupported PNG format (bitDepth={bitDepth}, interlace={interlace})";
            return false;
        }

        int channels = colorType switch
        {
            0 => 1, // grayscale
            2 => 3, // truecolor
            3 => 1, // palette
            4 => 2, // grayscale + alpha
            6 => 4, // truecolor + alpha
            _ => 0
        };

        if (channels == 0)
        {
            failureReason = $"unsupported PNG color type {colorType}";
            return false;
        }

        if (colorType == 3 && palette == null)
        {
            failureReason = "palette PNG without PLTE chunk";
            return false;
        }

        byte[] raw;
        try
        {
            compressed.Position = 0;
            using ZLibStream inflater = new(compressed, CompressionMode.Decompress);
            using MemoryStream rawStream = new();
            inflater.CopyTo(rawStream);
            raw = rawStream.ToArray();
        }
        catch (Exception ex)
        {
            failureReason = $"failed to inflate PNG data: {ex.Message}";
            return false;
        }

        int stride = width * channels;
        if (raw.Length < (long)(stride + 1) * height)
        {
            failureReason = "truncated PNG pixel data";
            return false;
        }

        rgba = new byte[width * height * 4];
        byte[] previousRow = new byte[stride];
        byte[] currentRow = new byte[stride];

        int rawOffset = 0;
        for (int y = 0; y < height; y++)
        {
            int filter = raw[rawOffset++];
            raw.AsSpan(rawOffset, stride).CopyTo(currentRow);
            rawOffset += stride;
            UnfilterRow(filter, currentRow, previousRow, channels);

            int destinationOffset = y * width * 4;
            for (int x = 0; x < width; x++, destinationOffset += 4)
            {
                int sourceIndex = x * channels;
                switch (colorType)
                {
                    case 0:
                        byte gray = currentRow[sourceIndex];
                        rgba[destinationOffset] = rgba[destinationOffset + 1] = rgba[destinationOffset + 2] = gray;
                        rgba[destinationOffset + 3] = 255;
                        break;
                    case 2:
                        rgba[destinationOffset] = currentRow[sourceIndex];
                        rgba[destinationOffset + 1] = currentRow[sourceIndex + 1];
                        rgba[destinationOffset + 2] = currentRow[sourceIndex + 2];
                        rgba[destinationOffset + 3] = 255;
                        break;
                    case 3:
                        int paletteIndex = currentRow[sourceIndex];
                        int paletteOffset = paletteIndex * 3;
                        if (paletteOffset + 2 >= palette!.Length)
                        {
                            rgba[destinationOffset + 3] = 0;
                            break;
                        }

                        rgba[destinationOffset] = palette[paletteOffset];
                        rgba[destinationOffset + 1] = palette[paletteOffset + 1];
                        rgba[destinationOffset + 2] = palette[paletteOffset + 2];
                        rgba[destinationOffset + 3] = paletteAlpha != null && paletteIndex < paletteAlpha.Length
                            ? paletteAlpha[paletteIndex]
                            : (byte)255;
                        break;
                    case 4:
                        byte grayWithAlpha = currentRow[sourceIndex];
                        rgba[destinationOffset] = rgba[destinationOffset + 1] = rgba[destinationOffset + 2] = grayWithAlpha;
                        rgba[destinationOffset + 3] = currentRow[sourceIndex + 1];
                        break;
                    case 6:
                        rgba[destinationOffset] = currentRow[sourceIndex];
                        rgba[destinationOffset + 1] = currentRow[sourceIndex + 1];
                        rgba[destinationOffset + 2] = currentRow[sourceIndex + 2];
                        rgba[destinationOffset + 3] = currentRow[sourceIndex + 3];
                        break;
                }
            }

            (previousRow, currentRow) = (currentRow, previousRow);
        }

        return true;
    }

    /// <summary>
    /// Reverses one PNG scanline filter (Sub / Up / Average / Paeth).
    /// </summary>
    private static void UnfilterRow(int filter, Span<byte> row, ReadOnlySpan<byte> previousRow, int bytesPerPixel)
    {
        switch (filter)
        {
            case 0:
                return;
            case 1:
                for (int i = bytesPerPixel; i < row.Length; i++)
                {
                    row[i] = (byte)(row[i] + row[i - bytesPerPixel]);
                }

                return;
            case 2:
                for (int i = 0; i < row.Length; i++)
                {
                    row[i] = (byte)(row[i] + previousRow[i]);
                }

                return;
            case 3:
                for (int i = 0; i < row.Length; i++)
                {
                    int left = i >= bytesPerPixel ? row[i - bytesPerPixel] : 0;
                    row[i] = (byte)(row[i] + ((left + previousRow[i]) >> 1));
                }

                return;
            case 4:
                for (int i = 0; i < row.Length; i++)
                {
                    int left = i >= bytesPerPixel ? row[i - bytesPerPixel] : 0;
                    int up = previousRow[i];
                    int upLeft = i >= bytesPerPixel ? previousRow[i - bytesPerPixel] : 0;
                    row[i] = (byte)(row[i] + PaethPredictor(left, up, upLeft));
                }

                return;
            default:
                throw new InvalidDataException($"Unknown PNG filter type {filter}.");
        }
    }

    private static int PaethPredictor(int left, int up, int upLeft)
    {
        int estimate = left + up - upLeft;
        int distanceLeft = Math.Abs(estimate - left);
        int distanceUp = Math.Abs(estimate - up);
        int distanceUpLeft = Math.Abs(estimate - upLeft);

        if (distanceLeft <= distanceUp && distanceLeft <= distanceUpLeft)
        {
            return left;
        }

        return distanceUp <= distanceUpLeft ? up : upLeft;
    }

    #endregion

    #region PNG encoding

    /// <summary>
    /// Encodes an RGBA buffer as a non-interlaced PNG with unfiltered scanlines.
    /// </summary>
    private static bool TryEncodePng(int width, int height, byte[] rgba, out byte[] png, out string failureReason)
    {
        png = [];
        failureReason = string.Empty;

        try
        {
            using MemoryStream output = new();
            output.Write(PngSignature);

            byte[] header = new byte[13];
            BinaryPrimitives.WriteInt32BigEndian(header, width);
            BinaryPrimitives.WriteInt32BigEndian(header.AsSpan(4), height);
            header[8] = 8;  // bit depth
            header[9] = 6;  // truecolor + alpha
            header[10] = 0; // deflate
            header[11] = 0; // adaptive filtering
            header[12] = 0; // no interlace
            WriteChunk(output, "IHDR"u8, header);

            using (MemoryStream rawStream = new())
            {
                int stride = width * 4;
                for (int y = 0; y < height; y++)
                {
                    rawStream.WriteByte(0); // filter type: none
                    rawStream.Write(rgba, y * stride, stride);
                }

                rawStream.Position = 0;
                using MemoryStream compressedStream = new();
                using (ZLibStream deflater = new(compressedStream, CompressionLevel.Optimal, leaveOpen: true))
                {
                    rawStream.CopyTo(deflater);
                }

                WriteChunk(output, "IDAT"u8, compressedStream.ToArray());
            }

            WriteChunk(output, "IEND"u8, []);
            png = output.ToArray();
            return true;
        }
        catch (Exception ex)
        {
            failureReason = ex.Message;
            return false;
        }
    }

    private static void WriteChunk(Stream output, ReadOnlySpan<byte> chunkType, ReadOnlySpan<byte> data)
    {
        Span<byte> lengthBuffer = stackalloc byte[4];
        BinaryPrimitives.WriteInt32BigEndian(lengthBuffer, data.Length);
        output.Write(lengthBuffer);
        output.Write(chunkType);
        output.Write(data);

        uint crc = Crc32.Compute(chunkType, data);
        Span<byte> crcBuffer = stackalloc byte[4];
        BinaryPrimitives.WriteUInt32BigEndian(crcBuffer, crc);
        output.Write(crcBuffer);
    }

    #endregion

    /// <summary>
    /// Minimal CRC-32 (IEEE 802.3) implementation used for the PNG chunk checksums.
    /// </summary>
    private static class Crc32
    {
        private static readonly uint[] Table = CreateTable();

        private static uint[] CreateTable()
        {
            uint[] table = new uint[256];
            for (uint i = 0; i < 256; i++)
            {
                uint value = i;
                for (int bit = 0; bit < 8; bit++)
                {
                    value = (value & 1) != 0 ? 0xEDB88320u ^ (value >> 1) : value >> 1;
                }

                table[i] = value;
            }

            return table;
        }

        internal static uint Compute(ReadOnlySpan<byte> first, ReadOnlySpan<byte> second)
        {
            uint crc = 0xFFFFFFFFu;
            crc = Update(crc, first);
            crc = Update(crc, second);
            return crc ^ 0xFFFFFFFFu;
        }

        private static uint Update(uint crc, ReadOnlySpan<byte> data)
        {
            foreach (byte value in data)
            {
                crc = Table[(crc ^ value) & 0xFF] ^ (crc >> 8);
            }

            return crc;
        }
    }
}
