using System.Buffers.Binary;
using System.Text;
using Zhengyan.MikuMikuDance.Formats;

namespace Zhengyan.MikuMikuDance.Formats.Nmd;

internal enum ProtoWireType
{
    Varint = 0,
    Fixed64 = 1,
    LengthDelimited = 2,
    Fixed32 = 5
}

internal readonly record struct ProtoField(int Number, ProtoWireType WireType, ReadOnlyMemory<byte> Payload);

internal sealed class ProtoReader
{
    private readonly ReadOnlyMemory<byte> _data;

    public ProtoReader(ReadOnlyMemory<byte> data)
    {
        _data = data;
    }

    public IEnumerable<ProtoField> ReadFields()
    {
        var offset = 0;
        while (offset < _data.Length)
        {
            var key = ReadVarint(_data.Span, ref offset);
            var number = checked((int)(key >> 3));
            var wireType = (ProtoWireType)(key & 0x7);
            var start = offset;
            int length;
            switch (wireType)
            {
                case ProtoWireType.Varint:
                    ReadVarint(_data.Span, ref offset);
                    length = offset - start;
                    break;
                case ProtoWireType.Fixed64:
                    EnsureAvailable(offset, sizeof(double));
                    offset += sizeof(double);
                    length = sizeof(double);
                    break;
                case ProtoWireType.LengthDelimited:
                    length = checked((int)ReadVarint(_data.Span, ref offset));
                    EnsureAvailable(offset, length);
                    start = offset;
                    offset += length;
                    break;
                case ProtoWireType.Fixed32:
                    EnsureAvailable(offset, sizeof(float));
                    offset += sizeof(float);
                    length = sizeof(float);
                    break;
                default:
                    throw new MmdFormatException($"Unsupported protobuf wire type {(int)wireType} at byte offset {offset}.");
            }

            yield return new ProtoField(number, wireType, _data.Slice(start, length));
        }
    }

    public static ulong ReadVarint(ReadOnlySpan<byte> data)
    {
        var offset = 0;
        var value = ReadVarint(data, ref offset);
        if (offset != data.Length)
        {
            throw new MmdFormatException("Unexpected trailing bytes in protobuf varint field.");
        }

        return value;
    }

    public static uint ReadFixed32(ReadOnlySpan<byte> data)
    {
        if (data.Length != sizeof(uint))
        {
            throw new MmdFormatException($"Invalid protobuf fixed32 length {data.Length}.");
        }

        return BinaryPrimitives.ReadUInt32LittleEndian(data);
    }

    public static float ReadSingle(ReadOnlySpan<byte> data)
    {
        return BitConverter.UInt32BitsToSingle(ReadFixed32(data));
    }

    public static string ReadUtf8(ReadOnlySpan<byte> data)
    {
        return Encoding.UTF8.GetString(data);
    }

    private static ulong ReadVarint(ReadOnlySpan<byte> data, ref int offset)
    {
        ulong result = 0;
        var shift = 0;
        while (offset < data.Length && shift < 64)
        {
            var value = data[offset++];
            result |= ((ulong)(value & 0x7f)) << shift;
            if ((value & 0x80) == 0)
            {
                return result;
            }

            shift += 7;
        }

        throw new MmdFormatException("Truncated protobuf varint.");
    }

    private void EnsureAvailable(int offset, int length)
    {
        if (offset < 0 || length < 0 || offset + length > _data.Length)
        {
            throw new MmdFormatException($"Truncated protobuf field at byte offset {offset}.");
        }
    }
}

internal sealed class ProtoWriter
{
    private readonly MemoryStream _stream = new();

    public void WriteVarint(int fieldNumber, ulong value)
    {
        WriteKey(fieldNumber, ProtoWireType.Varint);
        WriteRawVarint(value);
    }

    public void WriteBool(int fieldNumber, bool value)
    {
        WriteVarint(fieldNumber, value ? 1u : 0u);
    }

    public void WriteInt32(int fieldNumber, int value)
    {
        WriteVarint(fieldNumber, unchecked((ulong)value));
    }

    public void WriteFloat(int fieldNumber, float value)
    {
        WriteKey(fieldNumber, ProtoWireType.Fixed32);
        Span<byte> bytes = stackalloc byte[sizeof(uint)];
        BinaryPrimitives.WriteUInt32LittleEndian(bytes, BitConverter.SingleToUInt32Bits(value));
        _stream.Write(bytes);
    }

    public void WriteString(int fieldNumber, string value)
    {
        WriteBytes(fieldNumber, Encoding.UTF8.GetBytes(value));
    }

    public void WriteMessage(int fieldNumber, Action<ProtoWriter> write)
    {
        var nested = new ProtoWriter();
        write(nested);
        WriteBytes(fieldNumber, nested.ToArray());
    }

    public byte[] ToArray()
    {
        return _stream.ToArray();
    }

    private void WriteBytes(int fieldNumber, ReadOnlySpan<byte> bytes)
    {
        WriteKey(fieldNumber, ProtoWireType.LengthDelimited);
        WriteRawVarint((ulong)bytes.Length);
        _stream.Write(bytes);
    }

    private void WriteKey(int fieldNumber, ProtoWireType wireType)
    {
        if (fieldNumber <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(fieldNumber), "Protobuf field number must be positive.");
        }

        WriteRawVarint(((ulong)fieldNumber << 3) | (uint)wireType);
    }

    private void WriteRawVarint(ulong value)
    {
        while (value >= 0x80)
        {
            _stream.WriteByte((byte)(value | 0x80));
            value >>= 7;
        }

        _stream.WriteByte((byte)value);
    }
}
