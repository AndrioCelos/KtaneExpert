using System;
using System.Collections.Generic;
using JetBrains.Annotations;

namespace KtaneExpert;
/// <summary>Represents a collection that allows results of an expensive function to be cached.</summary>
/// <typeparam name="TParam">The type of the input to the function.</typeparam>
/// <typeparam name="TResult">The type of the output from the function.</typeparam>
/// <remarks>
///		The underlying function being cached by this object must be a pure function:
///		that is, its output must depend only on the provided input and must have no side effects.
/// </remarks>
public class Cache<TParam, TResult> where TParam : notnull {
	/// <summary>The list of inputs in the order they were added.</summary>
	private readonly List<TParam> keys;
	private readonly Dictionary<TParam, TResult> dictionary;
	
	private Func<TParam, TResult> Func { get; }

	[PublicAPI]
	public Cache(int capacity, Func<TParam, TResult> func) {
		keys = new(capacity);
		dictionary = new(capacity);
		Func = func;
	}
	[PublicAPI]
	public Cache(int capacity, IEqualityComparer<TParam> comparer, Func<TParam, TResult> func) {
		keys = new(capacity);
		dictionary = new(capacity, comparer);
		Func = func;
	}

	/// <summary>Returns the cached result if one exists; otherwise, runs the underlying function, adds the result to the cache and returns it.</summary>
	[PublicAPI]
	public TResult Get(TParam key) => dictionary.TryGetValue(key, out var value) ? value : Add(key);
	/// <summary>Refreshes the cached result for the specified input by calling the underlying function and returns the new result.</summary>
	[PublicAPI]
	public TResult Put(TParam key) {
		if (dictionary.ContainsKey(key)) {
			keys.Remove(key);
			keys.Add(key);
			return dictionary[key] = Func(key);
		} else {
			return Add(key);
		}
	}

	/// <summary>Calls the underlying function and caches the result.</summary>
	private TResult Add(TParam key) {
		if (keys.Count >= keys.Capacity) {
			var key2 = keys[0];
			keys.RemoveAt(0);
			dictionary.Remove(key2);
		}
		keys.Add(key);
		return dictionary[key] = Func(key);
	}
}
