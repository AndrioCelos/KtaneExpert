using AngelAiml;

namespace KtaneExpert.Conditions;
public class SerialNumberStartsWithLetterCondition<TData>() : Condition<TData>("SerialNumberStartsWithLetter", "SerialNumberStartsWithLetter", "the serial number starts with a letter") {
	public override ConditionResult Query(RequestProcess process, TData data)
		=> bool.TryParse(process.User.GetPredicate("SerialNumberStartsWithLetter"), out var result)
			? result
			: ConditionResult.Unknown("NeedEdgework SerialNumberStartsWithLetter");
}
