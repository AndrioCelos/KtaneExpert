using AngelAiml;

namespace KtaneExpert.Conditions;
public class SerialNumberParityCondition<TData>(bool odd) : Condition<TData>(
	$"SerialNumberIs{(odd ? "Odd" : "Even")}",
	$"SerialNumberIs{(odd ? "Odd" : "Even")}",
	$"the last digit of the serial number is {(odd ? "odd" : "even")}"
) {
	public bool Odd { get; } = odd;

	public override ConditionResult Query(RequestProcess process, TData data)
		=> bool.TryParse(process.User.GetPredicate("SerialNumberIsOdd"), out var result)
			? result == Odd
			: ConditionResult.Unknown("NeedEdgework SerialNumberIsOdd");
}
