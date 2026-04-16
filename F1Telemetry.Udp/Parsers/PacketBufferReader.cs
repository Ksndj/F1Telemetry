using System.Buffers.Binary;
using System.Numerics;
using System.Text;

namespace F1Telemetry.Udp.Parsers;

public ref struct PacketBufferReader
{
    private readonly ReadOnlySpan<byte> _buffer;
    private int _offset;

    public PacketBufferReader(ReadOnlyMemory<byte> buffer)
    {
        _buffer = buffer.Span;
        _offset = 0;
    }

    public int Remaining => _buffer.Length - _offset;

    public byte ReadByte()
    {
        EnsureAvailable(1);
        return _buffer[_offset++];
    }

    public sbyte ReadSByte()
    {
        return unchecked((sbyte)ReadByte());
    }

    public bool ReadBooleanByte()
    {
        return ReadByte() != 0;
    }

    public short ReadInt16()
    {
        EnsureAvailable(sizeof(short));
        var value = BinaryPrimitives.ReadInt16LittleEndian(_buffer.Slice(_offset, sizeof(short)));
        _offset += sizeof(short);
        return value;
    }

    public ushort ReadUInt16()
    {
        EnsureAvailable(sizeof(ushort));
        var value = BinaryPrimitives.ReadUInt16LittleEndian(_buffer.Slice(_offset, sizeof(ushort)));
        _offset += sizeof(ushort);
        return value;
    }

    public uint ReadUInt32()
    {
        EnsureAvailable(sizeof(uint));
        var value = BinaryPrimitives.ReadUInt32LittleEndian(_buffer.Slice(_offset, sizeof(uint)));
        _offset += sizeof(uint);
        return value;
    }

    public ulong ReadUInt64()
    {
        EnsureAvailable(sizeof(ulong));
        var value = BinaryPrimitives.ReadUInt64LittleEndian(_buffer.Slice(_offset, sizeof(ulong)));
        _offset += sizeof(ulong);
        return value;
    }

    public float ReadSingle()
    {
        var bits = ReadUInt32();
        return BitConverter.Int32BitsToSingle(unchecked((int)bits));
    }

    public double ReadDouble()
    {
        var bits = ReadUInt64();
        return BitConverter.Int64BitsToDouble(unchecked((long)bits));
    }

    public Vector3 ReadVector3()
    {
        return new Vector3(ReadSingle(), ReadSingle(), ReadSingle());
    }

    public byte[] ReadBytes(int count)
    {
        EnsureAvailable(count);
        var bytes = _buffer.Slice(_offset, count).ToArray();
        _offset += count;
        return bytes;
    }

    public string ReadFixedString(int count)
    {
        var bytes = ReadBytes(count);
        var zeroIndex = Array.IndexOf(bytes, (byte)0);
        var length = zeroIndex >= 0 ? zeroIndex : bytes.Length;
        return Encoding.UTF8.GetString(bytes, 0, length);
    }

    public void Skip(int count)
    {
        EnsureAvailable(count);
        _offset += count;
    }

    public void EnsureConsumed(string packetName)
    {
        if (Remaining != 0)
        {
            throw new InvalidOperationException(
                $"{packetName} parser did not consume the full payload. Remaining bytes: {Remaining}.");
        }
    }

    private void EnsureAvailable(int count)
    {
        if (Remaining < count)
        {
            throw new InvalidOperationException(
                $"Packet payload is truncated. Requested {count} bytes with {Remaining} bytes remaining.");
        }
    }
}
