using AngelAiml;

namespace KtaneExpert.Conditions;
/// <param name="state">If <see langword="true"/>, the indicator must be lit; if <see langword="false"/>, it must be unlit; if <see langword="null"/>, the state is not checked.</param>
public class IndicatorCondition<TData>(bool? state, string label) : Condition<TData>(
	state switch { true => "IndicatorLit", false => "IndicatorUnlit", null => "Indicator" },
	$"{state switch { true => "IndicatorLit", false => "IndicatorUnlit", null => "Indicator" }} {label}",
	$"there is {state switch { true => "a lit", false => "an unlit", null => "an" }} indicator with label {label}"
) {
	public bool? State { get; } = state;
	public string Label { get; } = label;
	public string Predicate => $"Indicator{State switch { true => "Lit", false => "Unlit", null => "" }}{Label}";
	public string EdgeworkQuery => $"Indicator{State switch { true => "Lit", false => "Unlit", null => "" }} {Label}";

	public static IndicatorCondition<TData> Lit(string label) => new(true, label);
	public static IndicatorCondition<TData> Unlit(string label) => new(false, label);
	public static IndicatorCondition<TData> Any(string label) => new(null, label);

	public override ConditionResult Query(RequestProcess process, TData data)
		=> bool.TryParse(process.User.GetPredicate(Predicate), out var result)
			? result
			: ConditionResult.Unknown("NeedEdgework " + EdgeworkQuery);
}
