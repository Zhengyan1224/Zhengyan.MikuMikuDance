using System.Buffers.Binary;
using System.Numerics;
using System.Text;

namespace Zhengyan.MikuMikuDance.Formats.Binary;

public sealed class BinarySpanReader
{
    private readonly ReadOnlyMemory<byte> _buffer;

    public BinarySpanReader(ReadOnlyMemory<byte> buffer)
    {
        _buffer = buffer;
    }

    public int Position { get; private set; }

    public int Remaining => _buffer.Length - Position;

    public bool EndOfBuffer => Position >= _buffer.Length;

    public byte ReadByte()
    {
        EnsureAvailable(1);
        return _buffer.Span[Position++];
    }

    public sbyte ReadSByte()
    {
        return unchecked((sbyte)ReadByte());
    }

    public short ReadInt16()
    {
        EnsureAvailable(sizeof(short));
        var value = BinaryPrimitives.ReadInt16LittleEndian(_buffer.Span.Slice(Position, sizeof(short)));
        Position += sizeof(short);
        return value;
    }

    public ushort ReadUInt16()
    {
        EnsureAvailable(sizeof(ushort));
        var value = BinaryPrimitives.ReadUInt16LittleEndian(_buffer.Span.Slice(Position, sizeof(ushort)));
        Position += sizeof(ushort);
        return value;
    }

    public int ReadInt32()
    {
        EnsureAvailable(sizeof(int));
        var value = BinaryPrimitives.ReadInt32LittleEndian(_buffer.Span.Slice(Position, sizeof(int)));
        Position += sizeof(int);
        return value;
    }

    public uint ReadUInt32()
    {
        EnsureAvailable(sizeof(uint));
        var value = BinaryPrimitives.ReadUInt32LittleEndian(_buffer.Span.Slice(Position, sizeof(uint)));
        Position += sizeof(uint);
        return value;
    }

    public float ReadSingle()
    {
        return BitConverter.Int32BitsToSingle(ReadInt32());
    }

    public Vector2 ReadVector2()
    {
        return new Vector2(ReadSingle(), ReadSingle());
    }

    public Vector3 ReadVector3()
    {
        return new Vector3(ReadSingle(), ReadSingle(), ReadSingle());
    }

    public Vector4 ReadVector4()
    {
        return new Vector4(ReadSingle(), ReadSingle(), ReadSingle(), ReadSingle());
    }

    public Quaternion ReadQuaternionXyzw()
    {
        return new Quaternion(ReadSingle(), ReadSingle(), ReadSingle(), ReadSingle());
    }

    public byte[] ReadBytes(int length)
    {
        EnsureAvailable(length);
        var value = _buffer.Span.Slice(Position, length).ToArray();
        Position += length;
        return value;
    }

    public string ReadFixedString(int byteCount, Encoding encoding)
    {
        var bytes = ReadBytes(byteCount);
        var terminator = Array.IndexOf(bytes, (byte)0);
        var length = terminator >= 0 ? terminator : bytes.Length;
        return encoding.GetString(bytes, 0, length).TrimEnd('\0');
    }

    public string ReadLengthPrefixedString(Encoding encoding)
    {
        var byteCount = ReadInt32();
        if (byteCount < 0)
        {
            throw new InvalidDataException($"Invalid string length {byteCount} at byte offset {Position - sizeof(int)}.");
        }

        return encoding.GetString(ReadBytes(byteCount));
    }

    public int ReadSizedIndex(int size)
    {
        return size switch
        {
            1 => ReadSByte(),
            2 => ReadInt16(),
            4 => ReadInt32(),
            _ => throw new InvalidDataException($"Unsupported signed index size {size}.")
        };
    }

    public int ReadSizedVertexIndex(int size)
    {
        return size switch
        {
            1 => ReadByte(),
            2 => ReadUInt16(),
            4 => ReadInt32(),
            _ => throw new InvalidDataException($"Unsupported vertex index size {size}.")
        };
    }

    public void Skip(int byteCount)
    {
        EnsureAvailable(byteCount);
        Position += byteCount;
    }

    private void EnsureAvailable(int byteCount)
    {
        if (byteCount < 0 || Position + byteCount > _buffer.Length)
        {
            throw new EndOfStreamException($"Cannot read {byteCount} bytes at offset {Position}; buffer length is {_buffer.Length}.");
        }
    }
}
