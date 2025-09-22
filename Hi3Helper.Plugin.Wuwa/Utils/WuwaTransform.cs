using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;
using System.Security.Cryptography;
// ReSharper disable IdentifierTypo

namespace Hi3Helper.Plugin.Wuwa.Utils;

/// <summary>
/// Perform Wuwa XOR Transformation. This method is used to De-XOR API Authentication Key, URLs and Launcher Logs.<br/>
/// This XOR implementation is just like any other implementations. But this specific XOR implementation uses SIMD (AVX2 or SSE2) for optimization, also specifically ignores line feed "\n" character (usually presents on WuWa Launcher Strings and Logs).
/// </summary>
/// <param name="secret">The secret as XOR key.</param>
internal readonly struct WuwaTransform(byte secret) : ICryptoTransform
{
    private const byte LineFeedChar = 10;

    public readonly Vector256<byte> SecretVector256 = Vector256.Create(secret);
    public readonly Vector256<byte> MaskVector256   = Vector256.Create(LineFeedChar);

    public readonly Vector128<byte> SecretVector128 = Vector128.Create(secret);
    public readonly Vector128<byte> MaskVector128   = Vector128.Create(LineFeedChar);

    public bool CanReuseTransform => true;

    public bool CanTransformMultipleBlocks => true;

    public int InputBlockSize => 1;

    public int OutputBlockSize => 1;

    // We don't dispose anything as nothing is being allocated.
    public void Dispose() {}

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int TransformBlock(byte[] inputBuffer, int inputOffset, int inputCount, byte[] outputBuffer, int outputOffset)
    {
        return TransformBlockCore(inputBuffer.AsSpan(inputOffset, inputCount), outputBuffer.AsSpan(outputOffset));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private unsafe void TransformWithAvx2IfSupported(ref int offset, int length, byte* inputBufferP, byte* outputBufferP)
    {
        if (!Vector256<byte>.IsSupported || length - offset < Vector256<byte>.Count)
        {
            return;
        }

        Start:
        // Load -> Compare Mask -> XOR All
        Vector256<byte> inputVecP = Vector256.Load(inputBufferP + offset);
        Vector256<byte> cmp       = Avx2.CompareEqual(inputVecP, MaskVector256);
        Vector256<byte> xor       = Avx2.Xor(inputVecP, SecretVector256);

        // Exclude '\n' from result vector with Blend
        Vector256<byte> result = Avx2.BlendVariable(xor, inputVecP, cmp);

        // Write and advance to the next data
        Unsafe.WriteUnaligned(ref outputBufferP[offset], result);
        offset += Vector256<byte>.Count;

        if (offset <= length - Vector256<byte>.Count)
        {
            goto Start;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private unsafe void TransformWithSse2IfSupported(ref int offset, int length, byte* inputBufferP, byte* outputBufferP)
    {
        if (!Vector128<byte>.IsSupported || length - offset < Vector128<byte>.Count)
        {
            return;
        }

        Start:
        // Load -> Compare Mask -> XOR All
        Vector128<byte> inputVecP = Vector128.Load(inputBufferP + offset);
        Vector128<byte> cmp       = Sse2.CompareEqual(inputVecP, MaskVector128);
        Vector128<byte> xor       = Sse2.Xor(inputVecP, SecretVector128);

        // Exclude '\n' from result vector. Sse2 doesn't have BlendVariable, so we use And + AndNot + Or
        Vector128<byte> result = Sse2.Or(Sse2.And(cmp, inputVecP), Sse2.AndNot(cmp, xor));

        // Write and advance to the next data
        Unsafe.WriteUnaligned(ref outputBufferP[offset], result);
        offset += Vector128<byte>.Count;

        if (offset <= length - Vector128<byte>.Count)
        {
            goto Start;
        }
    }

    public unsafe int TransformBlockCore(ReadOnlySpan<byte> inputBuffer, Span<byte> outputBuffer)
    {
        int length = inputBuffer.Length;
        int offset = 0;

        byte* inputBufferP  = (byte*)Unsafe.AsPointer(ref MemoryMarshal.GetReference(inputBuffer));
        byte* outputBufferP = (byte*)Unsafe.AsPointer(ref MemoryMarshal.GetReference(outputBuffer));

        TransformWithAvx2IfSupported(ref offset, length, inputBufferP, outputBufferP);
        TransformWithSse2IfSupported(ref offset, length, inputBufferP, outputBufferP);

        for (; offset < length; offset++)
        {
            byte b = inputBuffer[offset];
            if (b != 10)
            {
                b ^= secret;
            }
            outputBuffer[offset] = b;
        }

        return offset;
    }

    public byte[] TransformFinalBlock(byte[] inputBuffer, int inputOffset, int inputCount)
    {
        if (inputCount == 0)
        {
            return [];
        }
        byte[] array = new byte[inputCount];
        TransformBlock(inputBuffer, inputOffset, inputCount, array, 0);
        return array;
    }
}