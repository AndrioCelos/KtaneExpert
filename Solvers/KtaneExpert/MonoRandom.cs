using System;
using System.Collections.Generic;
using System.Linq;

namespace KtaneExpert;
/// <summary>Emulates the pseudo-random number generator used by Unity.</summary>
public class MonoRandom : Random {
	private const int MSeed = 161803398;

	private int inext;
	private int inextp;
	private readonly int[] seedArray = new int[56];

	/// <summary>Initialises a new <see cref="MonoRandom"/> using the specified seed.</summary>
	public MonoRandom(int seed) {
		seed = seed == int.MinValue ? int.MaxValue : Math.Abs(seed);
		var num = MSeed - seed;
		seedArray[55] = num;
		var num2 = 1;
		for (var i = 1; i < 55; i++) {
			var num3 = 21 * i % 55;
			seedArray[num3] = num2;
			num2 = num - num2;
			if (num2 < 0)
				num2 += 2147483647;
			num = seedArray[num3];
		}

		for (var j = 1; j < 5; j++) for (var k = 1; k < 56; k++) {
			seedArray[k] -= seedArray[1 + (k + 30) % 55];
			if (seedArray[k] < 0)
				seedArray[k] += int.MaxValue;
		}
		inext = 0;
		inextp = 31;
	}

	/// <summary>Returns a non-negative random number that is less than 1.</summary>
	protected override double Sample() {
		if (++inext >= 56) inext = 1;
		if (++inextp >= 56) inextp = 1;
		var num = seedArray[inext] - seedArray[inextp];
		if (num < 0) num += int.MaxValue;
		seedArray[inext] = num;
		return num * (1.0 / int.MaxValue);
	}

	/// <summary>Returns a non-negative random number that is less than 1.</summary>
	public override double NextDouble() => Sample();

	/// <summary>Returns a non-negative random integer that is less than <see cref="int.MaxValue"/>.</summary>
	public override int Next() => Next(int.MaxValue);
	/// <summary>Returns a non-negative random integer that is less than the specified maximum.</summary>
	public override int Next(int maxValue) => maxValue == 1 ? 0 : (int) Math.Floor(NextDouble() * maxValue);
	/// <summary>Returns a random integer between the specified minimum (inclusive) and the specified maximum (exclusive).</summary>
	public override int Next(int minValue, int maxValue) => maxValue - minValue <= 1 ? minValue : minValue + Next(maxValue - minValue);

	/// <summary>Returns an array that contains the elements from the specified enumerable in a random order.</summary>
	public T[] Shuffle<T>(IEnumerable<T> enumerable) => [.. enumerable.OrderBy(_ => NextDouble())];
	/// <summary>Shuffles the elements of the specified list in place using a Fisher-Yates shuffle.</summary>
	public void ShuffleFisherYates<T>(IList<T> list) {
		var i = list.Count;
		while (i > 1) {
			var j = Next(i);
			--i;
			(list[i], list[j]) = (list[j], list[i]);
		}
	}

	/// <summary>Returns a random element from the specified list.</summary>
	/// <seealso cref="WeightMap{T, TKey}"/>
	public T Pick<T>(IList<T> list) => list[Next(list.Count)];
}
