using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml.Linq;
using AngelAiml;
using KtaneExpert.Conditions;

namespace KtaneExpert.Solvers;
public class WiresSolver : IModuleSolver {
	private static readonly Colour[] colours = [Colour.Black, Colour.Blue, Colour.Red, Colour.White, Colour.Yellow];

	private static readonly InstructionType cutWire1 = new(nameof(cutWire1), "cut the first wire", (_, _, _) => Result.CutFirstWire);
	private static readonly InstructionType cutWire2 = new(nameof(cutWire2), "cut the second wire", (_, _, _) => Result.CutSecondWire);
	private static readonly InstructionType cutWire3 = new(nameof(cutWire3), "cut the third wire", (_, _, _) => Result.CutThirdWire);
	private static readonly InstructionType cutWire4 = new(nameof(cutWire4), "cut the fourth wire", (_, _, _) => Result.CutFourthWire);
	private static readonly InstructionType cutWire5 = new(nameof(cutWire5), "cut the fifth wire", (_, _, _) => Result.CutFifthWire);
	private static readonly InstructionType cutWireLast = new(nameof(cutWireLast), "cut the last wire", (_, _, _) => Result.CutLastWire);
	private static readonly InstructionType cutWireColourSingle = new(nameof(cutWireColourSingle), "cut the {0} wire", ProcessCutWireColourFirst);
	private static readonly InstructionType cutWireColourFirst = new(nameof(cutWireColourFirst), "cut the first {0} wire", ProcessCutWireColourFirst);
	private static readonly InstructionType cutWireColourLast = new(nameof(cutWireColourLast), "cut the last {0} wire", ProcessCutWireColourLast);

	private static Result ProcessCutWireColourFirst(RequestProcess p, Colour colour, Colour[] wires) {
		var index = Array.IndexOf(wires, colour);
		return index < 0 ? throw new ArgumentException("No wires of the specified colour were found.")
			: index == wires.Length - 1 ? Result.CutLastWire
			: Result.CutFirstWire + index;
	}
	private static Result ProcessCutWireColourLast(RequestProcess p, Colour colour, Colour[] wires) {
		var index = Array.LastIndexOf(wires, colour);
		return index < 0 ? throw new ArgumentException("No wires of the specified colour were found.")
			: index == wires.Length - 1 ? Result.CutLastWire
			: Result.CutFirstWire + index;
	}

	private static ConditionType ExactlyOneColourWire => new(nameof(ExactlyOneColourWire), "there is exactly one {0} wire", true, 1,
		(colour, wires) => wires.Count(wire => wire == colour) == 1, cutWireColourSingle);
	private static ConditionType MoreThanOneColourWire => new(nameof(MoreThanOneColourWire), "there is more than one {0} wire", true, 2,
		(colour, wires) => wires.Count(wire => wire == colour) > 1, cutWireColourFirst, cutWireColourLast);
	private static ConditionType NoColourWire => new(nameof(NoColourWire), "there are no {0} wires", false, 0,
		(colour, wires) => !wires.Contains(colour));
	private static ConditionType LastWireIsColour => new(nameof(LastWireIsColour), "the last wire is {0}", true, 1,
		(colour, wires) => wires[^1] == colour);

	public string Process(string text, XElement element, RequestProcess process) {
		var fields = text.Split((char[]?) null, StringSplitOptions.RemoveEmptyEntries);
		var ruleSeed = int.Parse(fields[0]);
		var rules = GetRules(ruleSeed);

		if (fields.Length > 1 && fields[1].Equals("GetRule", StringComparison.InvariantCultureIgnoreCase)) {
			var numberOfWires = int.Parse(fields[3]);
			var ruleIndex = int.Parse(fields[4]) - 1;
			var ruleList = rules[numberOfWires - 3];
			if (fields[2].Equals("Condition", StringComparison.InvariantCultureIgnoreCase)) {
				var conditionIndex = int.Parse(fields[5]) - 1;
				if (ruleIndex >= ruleList.Length)
					return "nil";
				var queries = ruleList[ruleIndex].Queries;
				return conditionIndex >= queries.Length ? "nil" :
					queries[conditionIndex] is WireCondition ? $"Query {queries[conditionIndex].Code}" : $"EdgeworkQuery {queries[conditionIndex].Code}";
			}

			var solution = ruleList[ruleIndex].Solution;
			return solution.Colour != null ? $"{solution.Type.Key} {solution.Colour}" : solution.Type.Key;
		}

		var wires = (from s in fields.Skip(1) select (Colour) Enum.Parse(typeof(Colour), s, true)).ToArray();

		foreach (var rule in rules[wires.Length - 3]) {
			var conditionResult = ConditionResult.FromBool(true);
			conditionResult = rule.Queries.Aggregate(conditionResult, (current, condition) => current && condition.Query(process, wires));
			if (conditionResult) {
				var result = rule.Solution.Type.Delegate(process, rule.Solution.Colour ?? 0, wires);
				return result.ToString();
			}

			if (conditionResult.Code == ConditionResultCode.Unknown) {
				return conditionResult.Details!;
			}
		}

		throw new InvalidOperationException("No rules matched?!");
	}

	public static Rule[][] GetRules(int ruleSeed) {
		if (ruleSeed == 1) {
			return [
				// 3 wires
				[
					new([NoColourWire.GetCondition(Colour.Red)], new(cutWire2)),
					new([LastWireIsColour.GetCondition(Colour.White)], new(cutWireLast)),
					new([MoreThanOneColourWire.GetCondition(Colour.Blue)], new(cutWireColourLast, Colour.Blue)),
					new(new(cutWireLast))
				],
				// 4 wires
				[
					new([MoreThanOneColourWire.GetCondition(Colour.Red), Condition<Colour[]>.SerialNumberIsOdd], new(cutWireColourLast, Colour.Red)),
					new([LastWireIsColour.GetCondition(Colour.Yellow), NoColourWire.GetCondition(Colour.Red)], new(cutWire1)),
					new([ExactlyOneColourWire.GetCondition(Colour.Blue)], new(cutWire1)),
					new([MoreThanOneColourWire.GetCondition(Colour.Yellow)], new(cutWireLast)),
					new(new(cutWire2))
				],
				// 5 wires
				[
					new([LastWireIsColour.GetCondition(Colour.Black), Condition<Colour[]>.SerialNumberIsOdd], new(cutWire4)),
					new([ExactlyOneColourWire.GetCondition(Colour.Red), MoreThanOneColourWire.GetCondition(Colour.Yellow)], new(cutWire1)),
					new([NoColourWire.GetCondition(Colour.Black)], new(cutWire2)),
					new(new(cutWire1))
				],
				// 6 wires
				[
					new([NoColourWire.GetCondition(Colour.Yellow), Condition<Colour[]>.SerialNumberIsOdd], new(cutWire3)),
					new([ExactlyOneColourWire.GetCondition(Colour.Yellow), MoreThanOneColourWire.GetCondition(Colour.White)], new(cutWire4)),
					new([NoColourWire.GetCondition(Colour.Red)], new(cutWireLast)),
					new(new(cutWire4))
				]
			];
		}

		var random = new MonoRandom(ruleSeed);
		var ruleSets = new Rule[4][];
		for (var i = 3; i <= 6; ++i) {
			var conditionWeights = new WeightMap<ConditionType, string>(c => c.Key);
			var instructionWeights = new WeightMap<InstructionType, string>(s => s.Key);
			var list2 = new List<Rule>();
			ConditionType[][] list = [
				[new(Condition<Colour[]>.SerialNumberStartsWithLetter), new(Condition<Colour[]>.SerialNumberIsOdd)],
				[ExactlyOneColourWire, NoColourWire, LastWireIsColour, MoreThanOneColourWire],
				[
					new(new PortCondition<Colour[]>(PortType.DviD, true)),
					new(new PortCondition<Colour[]>(PortType.PS2, true)),
					new(new PortCondition<Colour[]>(PortType.RJ45, true)),
					new(new PortCondition<Colour[]>(PortType.StereoRca, true)),
					new(new PortCondition<Colour[]>(PortType.Parallel, true)),
					new(new PortCondition<Colour[]>(PortType.Serial, true)),
					new(Condition<Colour[]>.EmptyPortPlate)
				]
			];

			var rules = new Rule[random.NextDouble() < 0.6 ? 3 : 4];
			ruleSets[i - 3] = rules;
			for (var j = 0; j < rules.Length; ++j) {
				var colours1 = new List<Colour>(colours);
				var list3 = new List<Colour>();

				var conditions = new Condition<Colour[]>[random.NextDouble() < 0.6 ? 1 : 2];
				var num = i - 1;
				for (var k = 0; k < conditions.Length; ++k) {
					var possibleQueryableProperties = GetPossibleConditions(list, num, k > 0);
#if TRACE
					if (Debugger.IsAttached) {
						Trace.WriteLine("queryWeights:");
						foreach (var entry in conditionWeights)
							Trace.WriteLine($"  -- {entry.Key} = {entry.Value}");
					}
#endif
					var conditionType = conditionWeights.Roll(possibleQueryableProperties, random, 0.1f);
					if (conditionType.UsesColour) {
						num -= conditionType.WiresInvolved;

						var i4 = random.Next(0, colours1.Count);
						var colour = colours1[i4];
						colours1.RemoveAt(i4);
						if (conditionType.ColourAvailableForSolution)
							list3.Add(colour);

						conditions[k] = conditionType.GetCondition(colour);
					} else
						conditions[k] = conditionType.GetCondition(0);
				}

				var instructions = GetPossibleInstructions(i, conditions);
				var instructionType = instructionWeights.Roll(instructions, random);
				var solution = list3.Count > 0 ? new(instructionType, list3[random.Next(0, list3.Count)]) : new Instruction(instructionType);

				var rule = new Rule(conditions, solution);
				if (rule.IsValid)
					list2.Add(rule);
				else
					--j;
			}

			Utils.StableSort(list2, r => -r.Queries.Length);

			var list4 = GetPossibleInstructions(i, null);

			// Remove redundant rules.
			var forbiddenId = list2[^1].Solution.Type.Key;
			list4.RemoveAll(l => l.Key == forbiddenId);

			list2.Add(new(new(random.Pick(list4))));
			ruleSets[i - 3] = [.. list2];
		}
		return ruleSets;
	}

	private static List<ConditionType> GetPossibleConditions(ConditionType[][] lists, int wiresAvailableInQuery, bool edgeworkAllowed) {
		var list = new List<ConditionType>();
		foreach (var l in lists) {
			list.AddRange(l.Where(conditionType => (edgeworkAllowed || conditionType.UsesColour) && conditionType.WiresInvolved <= wiresAvailableInQuery));
		}

		return list;
	}

		private static List<InstructionType> GetPossibleInstructions(int wireCount, IEnumerable<Condition<Colour[]>>? conditions) {
		var list = new List<InstructionType> { cutWire1, cutWire2, cutWireLast };
		if (wireCount >= 4) list.Add(cutWire3);
		if (wireCount >= 5) list.Add(cutWire4);
		if (wireCount >= 6) list.Add(cutWire5);

		if (conditions is null) return list;
		foreach (var condition in conditions) {
			if (condition is WireCondition wireCondition)
				list.AddRange(wireCondition.Type.AdditionalSolutions);
		}

		return list;
	}

	public void GenerateAiml(string path, int ruleSeed) {
		var rules = GetRules(ruleSeed);
		var allStars = new StringBuilder("<star/> <star index='2'/>");
		var allWires = new StringBuilder();
		var builder = new StringBuilder();
		builder.Append("<?xml version='1.0' encoding='UTF-8'?>\n<aiml>\n");

		for (var i = 0; i < 4; ++i) {
			var wireCount = i + 3;
			allStars.Append($" <star index='{wireCount}'/>");
			allWires.Append(i switch {
				0 => "CutFirstWire CutSecondWire",
				1 => " CutThirdWire",
				2 => " CutFourthWire",
				3 => " CutFifthWire",
				_ => ""
			});

			builder.Append($"<category><pattern>SolverFallback Wires {ruleSeed}");
			for (var j = 0; j < wireCount; ++j)
				builder.Append(" <set>BombColours</set>");
			builder.AppendLine($"</pattern><!-- {wireCount} wires -->\n<template>\n<think>\n<set var='result'>unknown</set>");

			var otherwise = false;
			foreach (var rule in rules[i]) {
				var closeTags = new Stack<string>();
				builder.AppendLine($"<!-- {(otherwise ? "Otherwise, " : "")}{rule} -->\n<condition var='result' value='unknown'>");
				otherwise = true;
				foreach (var condition in rule.Queries) {
					switch (condition) {
						case WireCondition wireCondition:
							switch (wireCondition.Key) {
								case nameof(ExactlyOneColourWire):
									builder.AppendLine($"<set var='temp'><srai>XCountMatch {wireCondition.Colour} XS {allStars}</srai></set>\n<condition var='temp' value='1'>");
									break;
								case nameof(MoreThanOneColourWire):
									builder.AppendLine($"<set var='temp'><srai>XCompareDigits <srai>XCountMatch {wireCondition.Colour} XS {allStars}</srai> XS 1</srai></set>\n<condition var='temp' value='1'>");
									break;
								case nameof(NoColourWire):
									builder.AppendLine($"<set var='temp'><srai>XContains {wireCondition.Colour} XS {allStars}</srai></set>\n<condition var='temp' value='false'>");
									break;
								case nameof(LastWireIsColour):
									builder.AppendLine($"<set var='temp'><star index='{wireCount}'/></set>\n<condition var='temp' value='{wireCondition.Colour}'>");
									break;
								default:
									throw new InvalidOperationException("Unknown wire condition");
							}
							closeTags.Push("</condition>");
							break;
						case SerialNumberStartsWithLetterCondition<Colour[]>:
							builder.AppendLine("<condition name='SerialNumberStartsWithLetter'>\n<li value='true'>");
							closeTags.Push("</li>\n<li value='unknown'><set var='result'>NeedEdgework SerialNumberStartsWithLetter</set></li>\n</condition>");
							break;
						case SerialNumberParityCondition<Colour[]>:
							builder.AppendLine("<condition name='SerialNumberIsOdd'>\n<li value='true'>");
							closeTags.Push("</li>\n<li value='unknown'><set var='result'>NeedEdgework SerialNumberIsOdd</set></li>\n</condition>");
							break;
						case PortCondition<Colour[]> portCondition:
							builder.AppendLine($"<condition name='Port{portCondition.PortType}'>\n<li value='true'>");
							closeTags.Push($"</li>\n<li value='unknown'><set var='result'>NeedEdgework Port {portCondition.PortType}</set></li>\n</condition>");
							break;
						case EmptyPortPlateCondition<Colour[]>:
							builder.AppendLine("<condition name='EmptyPortPlate'>\n<li value='true'>");
							closeTags.Push("</li>\n<li value='unknown'><set var='result'>NeedEdgework EmptyPortPlate</set></li>\n</condition>");
							break;
						default:
							throw new InvalidOperationException("Unknown condition");
					}
				}

				switch (rule.Solution.Type.Key) {
					case nameof(cutWire1):
						builder.AppendLine("<set var='result'>CutFirstWire</set>");
						break;
					case nameof(cutWire2):
						builder.AppendLine("<set var='result'>CutSecondWire</set>");
						break;
					case nameof(cutWire3):
						builder.AppendLine("<set var='result'>CutThirdWire</set>");
						break;
					case nameof(cutWire4):
						builder.AppendLine("<set var='result'>CutFourthWire</set>");
						break;
					case nameof(cutWire5):
						builder.AppendLine("<set var='result'>CutFifthWire</set>");
						break;
					case nameof(cutWireLast):
						builder.AppendLine("<set var='result'>CutLastWire</set>");
						break;
					case nameof(cutWireColourSingle):
						case nameof(cutWireColourFirst):
						builder.AppendLine($"<set var='result'><srai>XItem <srai>XIndex {rule.Solution.Colour} XS {allStars}</srai> XS {allWires} CutLastWire</srai></set>");
						break;
						case nameof(cutWireColourLast):
							builder.AppendLine($"<set var='result'><srai>XItem <srai>XLastIndex {rule.Solution.Colour} XS {allStars}</srai> XS {allWires} CutLastWire</srai></set>");
							break;
						default:
							throw new InvalidOperationException("Unknown solution");
					}

				while (closeTags.Count > 0)
					builder.AppendLine(closeTags.Pop());
				builder.AppendLine("</condition>");
			}

			builder.AppendLine("</think>\n<get var='result'/>\n</template>\n</category>");
		}
		builder.AppendLine("</aiml>");

			File.WriteAllText(Path.Combine(path, "aiml", $"wires{ruleSeed}.aiml"), builder.ToString());
	}

	public enum Result {
		CutFirstWire,
		CutSecondWire,
		CutThirdWire,
		CutFourthWire,
		CutFifthWire,
		CutLastWire
	}

	public delegate Result SolutionDelegate(RequestProcess process, Colour conditionColour, Colour[] wireColours);

	public record InstructionType(string Key, string Text, SolutionDelegate Delegate) {
		public override string ToString() => $"{{{Key}}}";
	}

	/// <summary>Represents a class of conditions, which can be used to construct conditions by providing a wire colour.</summary>
	private class ConditionType {
		public string Key { get; }
		public bool UsesColour { get; }
		public bool ColourAvailableForSolution { get; }
		public int WiresInvolved { get; }
		public InstructionType[] AdditionalSolutions { get; }
		private Func<Colour, Condition<Colour[]>> Delegate { get; }

		/// <summary>Initialises a colour-dependent <see cref="ConditionType"/> with the specified properties.</summary>
		/// <param name="key">The condition identifier.</param>
		/// <param name="text">The manual description of the solution. <c>{0}</c> is replaced with the colour.</param>
		/// <param name="colourAvailableForSolution">If true, the provided colour may be used in the instruction.</param>
		/// <param name="wiresInvolved">Always true.</param>
		/// <param name="func">A delegate that creates the condition from the colour.</param>
		/// <param name="additionalSolutions">A list of instructions that may be used in a rule with a condition of this type.</param>
		public ConditionType(string key, string text, bool colourAvailableForSolution, int wiresInvolved, Func<Colour, Colour[], bool> func, params InstructionType[] additionalSolutions) {
			Key = key;
			UsesColour = true;
			ColourAvailableForSolution = colourAvailableForSolution;
			WiresInvolved = wiresInvolved;
			AdditionalSolutions = additionalSolutions;
			Delegate = c => new WireCondition(this, string.Format(text, c.ToString().ToLower()), c, (_, w) => ConditionResult.FromBool(func(c, w)));
		}
		/// <summary>Initialises a <see cref="ConditionType"/> that produces the specified condition independent of colour.</summary>
		public ConditionType(Condition<Colour[]> condition) {
			Key = condition.Key;
			Delegate = _ => condition;
			AdditionalSolutions = [];
		}

		public Condition<Colour[]> GetCondition(Colour colour) => Delegate(colour);

		public override string ToString() => $"{{{Key}}}";
	}

	private class WireCondition(ConditionType type, string text, Colour colour, Condition<Colour[]>.ConditionDelegate conditionDelegate) : Condition<Colour[]>(type.Key, $"{type.Key} {colour}", text) {
		public Colour Colour { get; } = colour;
		private ConditionDelegate Delegate { get; } = conditionDelegate;
		public ConditionType Type { get; } = type;

		public override ConditionResult Query(RequestProcess process, Colour[] data) => Delegate.Invoke(process, data);
	}

	public readonly record struct Instruction(InstructionType Type, Colour? Colour = null) {
		public override string ToString() => string.Format(Type.Text, Colour);
	}

	public record Rule(Condition<Colour[]>[] Queries, Instruction Solution) {
		public Rule(Instruction solution) : this([], solution) { }

		private bool Has(string conditionKey, Colour colour)
			=> Queries.Any(c => c is WireCondition c2 && c2.Key == conditionKey && c2.Colour == colour);

		public bool IsValid {
			get {
				if (Queries.Length == 1) return true;

				for (var i = 0; i < 4 /* sic */; ++i) {
					var colour = colours[i];
					var count = 0;
					// There may not be more than one 'colour' query for the same colour.
					foreach (var query in Queries) {
						if ((query.Key == nameof(ExactlyOneColourWire) || query.Key == nameof(NoColourWire) || query.Key == nameof(MoreThanOneColourWire))
							&& ((WireCondition) query).Colour == colour)
							++count;
					}
					if (count >= 2) return false;

					if (Has(nameof(LastWireIsColour), colour)) {
						// There may not be a 'last wire is <colour>' and 'there are no <colour> wires' clauses for the same colour.
						if (Has(nameof(NoColourWire), colour)) return false;
						// There may not be more than one 'last wire is <colour>' clause for different colours.
						for (var j = i + 1; j < 5; ++j) {
							if (Has(nameof(LastWireIsColour), colours[j]))
								return false;
						}
					}
				}

				return true;
			}
		}

		public override string ToString()
			=> (Queries.Length > 0 ? "if " + string.Join<Condition<Colour[]>>(" and ", Queries) + ", " : "") + Solution;
	}
}
