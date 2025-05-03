using AngelAiml;

namespace KtaneExpert.Conditions;
public class EmptyPortPlateCondition<TData>() : Condition<TData>("EmptyPortPlate", "EmptyPortPlate", "there is an empty port plate present on the bomb") {
	public override ConditionResult Query(RequestProcess process, TData data)
		=> bool.TryParse(process.User.GetPredicate("PortEmptyPlate"), out var result)
			? result
			: ConditionResult.Unknown("NeedEdgework EmptyPortPlate");
}
