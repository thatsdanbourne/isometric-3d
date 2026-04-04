using System;

public static class DeterministicHash
{
	private const uint Fnv32Offset = 2166136261;
	private const uint Fnv32Prime = 16777619;

	private const ulong Fnv64Offset = 14695981039346656037UL;
	private const ulong Fnv64Prime = 1099511628211UL;

	public static int String32(string s)
	{
		unchecked
		{
			var hash = Fnv32Offset;

			for (var i = 0; i < s.Length; i++)
			{
				hash ^= s[i];
				hash *= Fnv32Prime;
			}

			return (int)hash;
		}
	}

	public static int Combine32(params int[] values)
	{
		unchecked
		{
			var hash = Fnv32Offset;

			for (var i = 0; i < values.Length; i++)
			{
				hash ^= (uint)values[i];
				hash *= Fnv32Prime;
			}

			return (int)hash;
		}
	}

	public static uint CombineU32(params int[] values)
	{
		unchecked
		{
			var hash = Fnv32Offset;

			for (var i = 0; i < values.Length; i++)
			{
				hash ^= (uint)values[i];
				hash *= Fnv32Prime;
			}

			return hash;
		}
	}

	public static ulong Combine64(params int[] values)
	{
		unchecked
		{
			var hash = Fnv64Offset;

			for (var i = 0; i < values.Length; i++)
			{
				hash ^= (uint)values[i];
				hash *= Fnv64Prime;
			}

			return hash;
		}
	}

	public static int Combine32(int a, int b)
	{
		return Combine32([a, b]);
	}

	public static int Combine32(int a, int b, int c)
	{
		return Combine32([a, b, c]);
	}

	public static int Combine32(int a, int b, int c, int d)
	{
		return Combine32([a, b, c, d]);
	}

	public static ulong Combine64(int a, int b, int c, int d)
	{
		return Combine64([a, b, c, d]);
	}

	public static ulong Combine64(int a, int b, int c, int d, int e)
	{
		return Combine64([a, b, c, d, e]);
	}
}