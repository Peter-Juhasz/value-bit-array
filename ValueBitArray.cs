using System;

public ref struct ValueBitArray
{
	public ValueBitArray(Span<ulong> buffer)
	{
		_buffer = buffer;
	}

	private readonly Span<ulong> _buffer;
	private const int NumberOfBitsInBucket = sizeof(ulong) * 8;

	public bool this[int index]
	{
		get
		{
			(int bucket, int position) = Math.DivRem(index, NumberOfBitsInBucket);
			return (_buffer[bucket] & (1ul << position)) > 0ul;
		}
		set
		{
			(int bucket, int position) = Math.DivRem(index, NumberOfBitsInBucket);
			_buffer[bucket] = value switch
			{
				true => _buffer[bucket] | (1ul << position),
				false => _buffer[bucket] & ~(1ul << position),
			};
		}
	}

	public void Reset()
	{
		_buffer.Fill(0L);
	}
}
