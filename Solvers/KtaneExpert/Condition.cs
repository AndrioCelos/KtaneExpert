using AngelAiml;
using KtaneExpert.Conditions;

namespace KtaneExpert;
/// <summary>Represents a specific, queryable condition as used with modules such as Wires.</summary>
/// <typeparam name="TData">A module-specific type passed to the delegate for the condition. Not all conditions need to use this type.</typeparam>
/// <param name="key">A value used to associate a weight with this condition for random selection.</param>
/// <param name="code">The code that should be output to query this condition.</param>
/// <param name="text">A human-readable description of this condition.</param>
public abstract class Condition<TData>(string key, string code, string text) {
	public delegate ConditionResult ConditionDelegate(RequestProcess process, TData data);

	/// <summary>Returns a value used to associate a weight with this condition for random selection.</summary>
	/// <remarks>
	///		Some modules group related conditions by giving them the same key.
	///		This means that all of those conditions will share the same weight and modifications to this weight.
	///		For example, Wires does this with port conditions, but The Button does not.
	/// </remarks>
	public string Key { get; } = key;
	/// <summary>Returns the code that should be output to query this condition.</summary>
	public string Code { get; } = code;

	/// <summary>Queries this condition for the specified <see cref="RequestProcess"/> and returns the result.</summary>
	/// <param name="process">The <see cref="RequestProcess"/> currently being processed.</param>
	/// <param name="data">A <typeparamref name="TData"/> containing any necessary module-specific data such as wire colours.</param>
	/// <returns>A <see cref="ConditionResult"/> representing the result of the query.</returns>
	/// <remarks>Currently, some conditions may access predicates such as edgework information from the <see cref="RequestProcess"/>.</remarks>
	public abstract ConditionResult Query(RequestProcess process, TData data);

	public override string ToString() => text;

	/// <summary>Returns a <see cref="Condition{TData}"/> that checks whether the bomb's serial number ends with an odd digit.</summary>
	public static SerialNumberParityCondition<TData> SerialNumberIsOdd { get; } = new(true);
	/// <summary>Returns a <see cref="Condition{TData}"/> that checks whether the bomb's serial number starts with a letter.</summary>
	public static SerialNumberStartsWithLetterCondition<TData> SerialNumberStartsWithLetter { get; } = new();
	/// <summary>Returns a <see cref="Condition{TData}"/> that checks whether the bomb has an empty port plate.</summary>
	public static EmptyPortPlateCondition<TData> EmptyPortPlate { get; } = new();
}

/// <summary>Specifies whether a condition is met or not met.</summary>
public enum ConditionResultCode {
	/// <summary>The condition is not met.</summary>
	False,
	/// <summary>The condition is met.</summary>
	True,
	/// <summary>Not enough information is available to query the condition. Details should be provided in the <see cref="ConditionResult"/> value.</summary>
	Unknown
}

/// <summary>Specifies the result of a condition query.</summary>
/// <param name="Code">The <see cref="ConditionResultCode"/> indicating whether the condition is met or not met.</param>
/// <param name="Details">If <see cref="ConditionResultCode"/> is <see cref="ConditionResultCode.Unknown"/>, returns a code indicating what information is missing, or <see langword="null"/> otherwise.</param>
public readonly record struct ConditionResult(ConditionResultCode Code, string? Details = null) {
	/// <summary>Returns the logical conjunction of two <see cref="ConditionResult"/> values.</summary>
	/// <returns>False if either value is false; otherwise, an unknown operand if either is unknown; otherwise, true.</returns>
	public static ConditionResult operator &(ConditionResult x, ConditionResult y)
		=> x.Code == ConditionResultCode.False ? x
		: y.Code == ConditionResultCode.False ? y
		: x.Code == ConditionResultCode.Unknown ? x : y;

	/// <summary>Returns <see langword="true"/> if this value is definitely true: that is, its <see cref="Code"/> property equals <see cref="ConditionResultCode.True"/>.</summary>
	public static bool operator true(ConditionResult value) => value.Code == ConditionResultCode.True;
	/// <summary>Returns <see langword="true"/> if this value is definitely false: that is, its <see cref="Code"/> property equals <see cref="ConditionResultCode.False"/>.</summary>
	public static bool operator false(ConditionResult value) => value.Code == ConditionResultCode.False;

	public static implicit operator ConditionResult(bool value) => FromBool(value);

	/// <summary>Creates a <see cref="Condition{TData}"/> from a <see cref="bool"/> value.</summary>
	public static ConditionResult FromBool(bool value) => new(value ? ConditionResultCode.True : ConditionResultCode.False);
	/// <summary>Creates an unknown <see cref="Condition{TData}"/> from a detail code.</summary>
	public static ConditionResult Unknown(string message) => new(ConditionResultCode.Unknown, message);
}
