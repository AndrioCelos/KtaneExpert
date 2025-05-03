using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using JetBrains.Annotations;

namespace KtaneExpert;
/// <summary>Represents a collection of weights used for weighted random selections.</summary>
/// <typeparam name="T">The type of the element being selected.</typeparam>
/// <typeparam name="TKey">The type of the keys used to associate weights to elements.</typeparam>
/// <remarks>
///		When performing a random selection, not all possible choices need have a weight assigned.
///		All elements have a weight of 1 by default.
///	</remarks>
public class WeightMap<T, TKey>(Func<T, TKey> keySelector) : IEnumerable<KeyValuePair<TKey, float>> where TKey : notnull {
	private readonly Dictionary<TKey, float> weights = [];

	/// <summary>Returns the weight associated with the specified item (1 by default).</summary>
	[PublicAPI]
	public float GetWeight(T item) => weights.GetValueOrDefault(keySelector(item), 1);
	/// <summary>Sets the weight for the specified item.</summary>
	[PublicAPI]
	public void SetWeight(T item, float weight) => weights[keySelector(item)] = weight;

	/// <summary>
	///		Selects a random element from the specified list with a weighted distribution,
	///		then multiplies the weight of the selected element by the specified number.
	///	</summary>
	[PublicAPI]
	public T Roll(IList<T> list, Random random, float weightReduction = 0.05f) {
		var roll = (float) (random.NextDouble() * list.Sum(GetWeight));
		foreach (var item in list) {
			var weight = GetWeight(item);
			roll -= weight;
			if (roll >= 0) continue;
			SetWeight(item, weight * weightReduction);
			return item;
		}
		return list[random.Next(list.Count)];
	}

	public IEnumerator<KeyValuePair<TKey, float>> GetEnumerator() => weights.GetEnumerator();
	IEnumerator IEnumerable.GetEnumerator() => weights.GetEnumerator();
}
