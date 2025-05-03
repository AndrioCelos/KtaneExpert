using AngelAiml;

namespace KtaneExpert.Conditions;
public class PortCondition<TData>(PortType portType, bool exactKey) : Condition<TData>(
	exactKey ? "Port" + portType : "Port",
	$"Port {portType}",
	$"there is {Utils.GetPortDescriptionAn(portType)} present on the bomb"
) {
	public PortType PortType { get; } = portType;

	public PortCondition(PortType portType) : this(portType, false) { }

	public override ConditionResult Query(RequestProcess process, TData data)
		=> bool.TryParse(process.User.GetPredicate("Port" + PortType), out var result)
			? result
			: ConditionResult.Unknown("NeedEdgework Port " + PortType);
}
