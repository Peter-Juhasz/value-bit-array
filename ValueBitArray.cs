using System;

public ref struct ValueBitArray
{
	public ValueBitArray(Span<ulong> buffer)
	{
		_buffer = buffer;
	}

	private readonly Span<ulong> _buffer;
	private const int Size = sizeof(ulong) * 8;

	public bool this[int index]
	{
		get
		{
			(int bucket, int entry) = Math.DivRem(index, Size);
			return (_buffer[bucket] & (1ul << entry)) > 0ul;
		}
		set
		{
			(int bucket, int entry) = Math.DivRem(index, Size);
			_buffer[bucket] = value switch
			{
				true => _buffer[bucket] | (1ul << entry),
				false => _buffer[bucket] & ~(1ul << entry),
			};
		}
	}

	public void Reset()
	{
		_buffer.Fill(0L);
	}
}
