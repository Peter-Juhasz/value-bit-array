# Allocation free BitArray implementation

## Introduction
The built-in type [BitArray](https://docs.microsoft.com/en-us/dotnet/api/system.collections.bitarray) is a very old type, which exists in the framework since [.NET Framework 1.1](https://docs.microsoft.com/en-us/dotnet/api/system.collections.bitarray?view=netframework-1.1#applies-to). It is a general use collection, implemented as a reference type.

The following implementation uses zero heap allocations, so it is suitable for high performance scenarios, but only where the number of elements in the bit array is small (or fits the the stack).

## Implementation
We are going to store bits batched as unsigned integers (instead of array of booleans, because a boolean has the size of a byte) to save memory (similarly to the built-in type [BitArray](https://github.com/dotnet/runtime/blob/main/src/libraries/System.Collections/src/System/Collections/BitArray.cs)). The largest integer type which supports native bit operations on most platforms is `ulong`, so we are going to use that as a storage primitive (we could use `nuint` as well for a platform independent native integer).

Our storage would look like this:
```
0000000000000000000000000000000000000000000000000000000000000000 000000000...
---------------------------------------------------------------- ---------...
                    sizeof(ulong) = 64 bits
                          buffer[0]                              buffer[1]...
```

A backing buffer is still needed, but with the promise of no heap allocations we must do that on the stack. And as long as we have only a relatively small number of bits (easily can be even thousands), we can allocate the buffer on the stack:
```cs
Span<ulong> buffer = stackalloc ulong[4]; // can store 4 * 64 bits = 256 bits
```

Let's wrap it into our value type `ValueBitArray` to use that buffer for its intended purpose:
```cs
public ref struct ValueBitArray
{
	public ValueBitArray(Span<ulong> buffer)
	{
		_buffer = buffer;
	}
	
	private readonly Span<ulong> _buffer;
}
```

*Note that the type must be a `ref struct` because it references a `Span<ulong>`.*

Now we need to calculate where is a bit with a specific index. For example bit 67 is in the second (with index 0) bucket, at forth (index 3) position:
```
0000000000000000000000000000000000000000000000000000000000000000 0000000000...
---------------------------------------------------------------- ----------...
                    sizeof(ulong) = 64 bits                         ^ index 67
		          0-63 index                             64-127 index  
```

To do that, we can use simple integer math:
```cs
private const int NumberOfBitsInBucket = sizeof(ulong) * 8;

public bool this[int index]
{
	get
	{
		int bucket = index / NumberOfBitsInBucket;
		int position = index % NumberOfBitsInBucket;
		// ...
	}
}
```

It takes two operations, but it can be done at once using [DivRem](https://docs.microsoft.com/en-us/dotnet/api/system.math.divrem?#system-math-divrem(system-uint64-system-uint64)):
```cs
(int bucket, int position) = Math.DivRem(index, NumberOfBitsInBucket);
```

We can decide whether a specific bit at a specified position is turned on using the following formula:
```cs
integer & (1 << position) > 0
```

And at this point we have everything to implement reads:
```cs
public bool this[int index]
{
	get
	{
		(int bucket, int position) = Math.DivRem(index, NumberOfBitsInBucket);
		return (_buffer[bucket] & (1ul << position)) > 0ul;
	}
}
```

With the following formulas we can set a bit of an integer at a specific position to either `true`:
```cs
integer = integer | (1 << position);
```

or `false`:
```cs
integer = integer & ~(1 << position);
```

So we can easily implement writes:
```cs
public bool this[int index]
{
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
```

And we are done.

## Usage
To use our new bit array type, first we need to pre-allocate the buffer (on stack), and then we can easily set bits:
```cs
Span<ulong> buffer = stackalloc ulong[8]; // 8 * 64 bits = 512 bits
var array = new ValueBitArray(buffer);

// all bits are turned off by default
var firstIsOff = array[0]; 

array[0] = true; // set first bit to true
var firstIsOn = array[0]; // true, read first bit

array[0] = false; // set first bit back to false

array[511] = true; // set very last bit to true

array[512]; // out of range, ArgumentOutOfRangeException
```

## Appendix

### Reset
Resetting all bits to zero can be easily implemented using the [Fill](https://docs.microsoft.com/en-us/dotnet/api/system.span-1.fill) method of [Span](https://docs.microsoft.com/en-us/dotnet/api/system.span-1):
```cs
public void Reset()
{
	_buffer.Fill(0ul);
}
```
