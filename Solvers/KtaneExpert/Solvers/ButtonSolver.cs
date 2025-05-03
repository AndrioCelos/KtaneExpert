using AngelAiml;
using KtaneExpert.Conditions;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;

namespace KtaneExpert.Solvers;
public class ButtonSolver : IModuleSolver {
	private const int MaxInitialRules = 6;
	private const int MaxHeldRules = 3;

	private class ButtonColourCondition(Colours colour) : Condition<ButtonData>(nameof(ButtonColourCondition), $"ButtonColour {colour}", $"the button is {colour.ToString().ToLower()}") {
		public Colours Colour { get; } = colour;
		public override ConditionResult Query(RequestProcess process, ButtonData data) => data.Colour == Colour;
	}

	private class ButtonLabelCondition(Labels label) : Condition<ButtonData>(nameof(ButtonLabelCondition), $"ButtonLabel {label}", $"the button says '{label}'") {
		public Labels Label { get; } = label;
		public override ConditionResult Query(RequestProcess process, ButtonData data) => data.Label == Label;
	}

	public static RuleSet GetRules(int ruleSeed) {
		if (ruleSeed == 1) return new(
			[
				new([new ButtonColourCondition(Colours.Blue), new ButtonLabelCondition(Labels.Abort)], InitialSolution.Hold),
				new([new BatteriesCondition<ButtonData>(Operations.MoreThan, 1), new ButtonLabelCondition(Labels.Detonate)], InitialSolution.Tap),
				new([new ButtonColourCondition(Colours.White), IndicatorCondition<ButtonData>.Lit("CAR")], InitialSolution.Hold),
				new([new BatteriesCondition<ButtonData>(Operations.MoreThan, 2), IndicatorCondition<ButtonData>.Lit("FRK")], InitialSolution.Tap),
				new([new ButtonColourCondition(Colours.Yellow)], InitialSolution.Hold),
				new([new ButtonColourCondition(Colours.Red), new ButtonLabelCondition(Labels.Hold)], InitialSolution.Tap),
				new(InitialSolution.Hold)
			],
			[
				new(Colours.Blue, new(HeldSolutionType.ReleaseOnTimerDigit, 4)),
				new(Colours.White, new(HeldSolutionType.ReleaseOnTimerDigit, 1)),
				new(Colours.Yellow, new(HeldSolutionType.ReleaseOnTimerDigit, 5)),
				new(null, new(HeldSolutionType.ReleaseOnTimerDigit, 1)),
			]
		);

		var random = new MonoRandom(ruleSeed);
		var initialInstructionWeights = new WeightMap<InitialSolution, InitialSolution>(s => s);
		initialInstructionWeights.SetWeight(InitialSolution.Tap, 0.1f);
		initialInstructionWeights.SetWeight(InitialSolution.TapWhenSecondsMatch, 0.05f);
		var heldInstructionWeights = new WeightMap<HeldSolution, string>(s => s.Key);

		// Build condition lists.
		var primaryConditions = new List<Condition<ButtonData>> {
				new ButtonColourCondition(Colours.Red), new ButtonColourCondition(Colours.Blue), new ButtonColourCondition(Colours.Yellow), new ButtonColourCondition(Colours.White),
				new BatteriesCondition<ButtonData>(Operations.MoreThan, 1), new BatteriesCondition<ButtonData>(Operations.MoreThan, 2)
			};
		var indicatorColorConditions = new List<Colours?> { Colours.Red, Colours.Blue, Colours.Yellow, Colours.White };
		var secondaryConditions = new List<Condition<ButtonData>> { new ButtonLabelCondition(Labels.Press), new ButtonLabelCondition(Labels.Hold), new ButtonLabelCondition(Labels.Abort), new ButtonLabelCondition(Labels.Detonate) };

		for (var i = 0; i < 3; ++i)
			secondaryConditions.Add(IndicatorCondition<ButtonData>.Lit(random.Pick(Utils.IndicatorLabels)));

		var ports = new List<PortType> { PortType.DviD, PortType.PS2, PortType.RJ45, PortType.StereoRca, PortType.Parallel, PortType.Serial };
		for (var i = 0; i < 3; ++i) {
			var port = Utils.RemoveRandom(ports, random);
			secondaryConditions.Add(new PortCondition<ButtonData>(port));
		}

		primaryConditions.AddRange(ports.Select(port => new PortCondition<ButtonData>(port)));
		primaryConditions.Add(Condition<ButtonData>.EmptyPortPlate);
		secondaryConditions.Add(Condition<ButtonData>.SerialNumberStartsWithLetter);
		secondaryConditions.Add(Condition<ButtonData>.SerialNumberIsOdd);

		// Generate initial rules.
		var initialRules = new List<InitialRule>();
		while (initialRules.Count < MaxInitialRules && primaryConditions.Count > 0) {
			var query = Utils.RemoveRandom(primaryConditions, random);
			initialRules.Add(random.Next(2) == 0
				? CreateInitialRule([query], initialInstructionWeights, random)
				: CreateInitialRule([query, Utils.RemoveRandom(secondaryConditions, random)], initialInstructionWeights, random));
		}
		initialRules.Add(new(InitialSolution.Hold));

		// Generate held rules.
		var heldRules = new List<HeldRule>();
		while (heldRules.Count < MaxHeldRules && indicatorColorConditions.Count > 0) {
			var condition = Utils.RemoveRandom(indicatorColorConditions, random);
			heldRules.Add(CreateHeldRule(condition, heldInstructionWeights, random));
		}
		heldRules.Add(CreateHeldRule(null, heldInstructionWeights, random));

		RemoveRedundantRules(initialRules, initialInstructionWeights, random, primaryConditions, secondaryConditions);

		return new([.. initialRules], [.. heldRules]);
	}

	private static void RemoveRedundantRules(List<InitialRule> rules, WeightMap<InitialSolution, InitialSolution> weights, Random random, List<Condition<ButtonData>> primaryQueryList, List<Condition<ButtonData>> secondaryQueryList) {
		// Since the last rule always has the instruction 'hold the button',
		// force the second-last one to have a different instruction.
		if (rules[^2].Solution == InitialSolution.Hold)
			rules[^2] = new(rules[^2].Conditions, weights.Roll(GetInitialSolutions(false), random));

		var twobattery = -1;
		var onebattery = -1;
		InitialSolution? solution = null;
		var samesolution = true;

		for (var i = rules.Count - 1; i >= 0; i--) {
			var condition = rules[i].Conditions.OfType<BatteriesCondition<ButtonData>>().FirstOrDefault();
			if (condition != null) {
				switch (condition.Number) {
					case 1:
						onebattery = i;
						if (twobattery > -1) {
							samesolution &= rules[i].Solution == solution;
							break;
						}
						solution = rules[i].Solution;
						break;
					case 2:
						twobattery = i;
						if (onebattery > -1) {
							samesolution &= rules[i].Solution == solution;
							break;
						}
						solution = rules[i].Solution;
						break;
				}
			}
			if (solution.HasValue)
				samesolution &= rules[i].Solution == solution;
		}
		if (onebattery < 0 || twobattery < 0) {
			return;
		}

		if (onebattery < twobattery && rules[onebattery].Conditions.Count == 1) {
			// We have a 'if there is more than 1 battery' rule above a 'if there are more than 2 batteries' rule.
			if (random.Next(2) == 0) {
				rules[onebattery].Conditions.Add(Utils.RemoveRandom(secondaryQueryList, random));
			} else {
				rules[onebattery].Conditions[0] = Utils.RemoveRandom(primaryQueryList, random);
			}
		} else if (rules[onebattery].Conditions.Count == 1 && rules[twobattery].Conditions.Count == 1 && samesolution) {
			// We have a 'if there is more than 1 battery' rule below a 'if there are more than 2 batteries' rule,
			// and every rule in between has the same instruction.
			switch (random.Next(7)) {
				// Add a secondary query to one or both of the battery rules.
				case 0:
					rules[onebattery].Conditions.Add(Utils.RemoveRandom(secondaryQueryList, random));
					break;
				case 1:
					rules[twobattery].Conditions.Add(Utils.RemoveRandom(secondaryQueryList, random));
					break;
				case 2:
					rules[twobattery].Conditions.Add(Utils.RemoveRandom(secondaryQueryList, random));
					goto case 0;

				// Replace one or both of the battery rules with a new primary query.
				case 3:
					rules[onebattery].Conditions[0] = Utils.RemoveRandom(primaryQueryList, random);
					break;
				case 4:
					rules[twobattery].Conditions[0] = Utils.RemoveRandom(primaryQueryList, random);
					break;
				case 5:
					rules[twobattery].Conditions[0] = Utils.RemoveRandom(primaryQueryList, random);
					goto case 3;

				// Replace one of the solutions in between the minimum and maximum battery.
				default:
					var replacementsolution = rules[onebattery].Solution == InitialSolution.Tap ? InitialSolution.Hold : InitialSolution.Tap;
					if (Math.Abs(onebattery - twobattery) == 1)
						rules[Math.Min(onebattery, twobattery)].Solution = replacementsolution;
					else
						rules[random.Next(Math.Min(onebattery, twobattery), Math.Max(onebattery, twobattery))].Solution = replacementsolution;
					break;
			}
		}
	}

	private static InitialRule CreateInitialRule(List<Condition<ButtonData>> conditions, WeightMap<InitialSolution, InitialSolution> weights, Random random)
		=> new(conditions, weights.Roll(GetInitialSolutions(true), random));

	private static HeldRule CreateHeldRule(Colours? colour, WeightMap<HeldSolution, string> weights, Random random)
		=> new(colour, weights.Roll(GetHeldSolutions(), random));

	private static List<InitialSolution> GetInitialSolutions(bool holdAllowed) {
		var solutions = new List<InitialSolution>();
		if (holdAllowed) solutions.Add(InitialSolution.Hold);
		solutions.Add(InitialSolution.Tap);
		solutions.Add(InitialSolution.TapWhenSecondsMatch);
		return solutions;
	}
	private static List<HeldSolution> GetHeldSolutions() {
		var list = new List<HeldSolution> { new(HeldSolutionType.ReleaseOnTimerDigit, 5) };
		for (var i = 1; i <= 4; ++i)
			list.Add(new(HeldSolutionType.ReleaseOnTimerDigit, i));
		for (var i = 6; i <= 9; ++i)
			list.Add(new(HeldSolutionType.ReleaseOnTimerDigit, i));
		list.Add(new(HeldSolutionType.ReleaseOnTimerDigit));
		for (var i = 0; i <= 9; ++i)
			list.Add(new(HeldSolutionType.ReleaseOnSecondsOnesDigit, i));
		list.Add(new(HeldSolutionType.ReleaseAnyTime));
		list.Add(new(HeldSolutionType.ReleaseOnSecondsMultipleOfFour));
		list.Add(new(HeldSolutionType.ReleaseOnSecondsDigitSum, 5));
		list.Add(new(HeldSolutionType.ReleaseOnSecondsDigitSum, 7));
		list.Add(new(HeldSolutionType.ReleaseOnSecondsDigitSum, 3));
		list.Add(new(HeldSolutionType.ReleaseOnSecondsPrimeOrZero));

		return list;
	}

	public string Process(string text, XElement element, RequestProcess process) {
		// Usage: <rule seed> <colour> [label]

		var fields = text.Split((char[]?) null, StringSplitOptions.RemoveEmptyEntries);
		var colour = (Colours) Enum.Parse(typeof(Colours), fields[1], true);
		var rules = GetRules(int.Parse(fields[0]));

		if (fields.Length > 2) {
			// Initial stage
			var label = (Labels) Enum.Parse(typeof(Labels), fields[2], true);
			InitialSolution? instruction = null;

			foreach (var rule in rules.InitialRules) {
				var result = ConditionResult.FromBool(true);
				foreach (var condition in rule.Conditions)
					result = result && condition.Query(process, new(colour, label));

				if (result.Code == ConditionResultCode.True) {
					instruction = rule.Solution;
					break;
				} else if (result.Code == ConditionResultCode.Unknown) return result.Details!;
			}
			return instruction != null ? instruction.Value.ToString() : throw new InvalidOperationException("No rules matched?!");
		} else {
			// Held stage
			var rule = rules.HeldRules.FirstOrDefault(r => r.Colour == colour) ?? rules.HeldRules.Single(r => r.Colour == null);
			return $"{rule.Solution.Type} {rule.Solution.Digit}";
		}
	}

	public void GenerateAiml(string path, int ruleSeed) {
		var rules = GetRules(ruleSeed);
		using var writer = new StreamWriter(Path.Combine(path, "aiml", $"button{ruleSeed}.aiml"));
		writer.Write("<?xml version='1.0' encoding='UTF-8'?>\n" +
			"<aiml version='2.0'>\n" +
			$"<category><pattern>SolverFallback Button {ruleSeed} <set>BombColours</set> <set>ButtonLabels</set></pattern>\n" +
			"<template>\n" +
			"<think>\n" +
			"<set var='colour'><star/></set>\n" +
			"<set var='label'><star index='2'/></set>\n");

		for (var i = 0; i < rules.InitialRules.Length; ++i) {
			var rule = rules.InitialRules[i];

			writer.WriteLine();
			writer.WriteLine($"<!-- {i + 1}. {rule} -->\n<condition var='result' value='unknown'>");

			// Check button conditions before edgework ones.
			static bool IsButtonCondition(Condition<ButtonData> condition) => condition is ButtonColourCondition or ButtonLabelCondition;
			var conditions = rule.Conditions;
			if (conditions.Count == 2 && IsButtonCondition(conditions[1]) && !IsButtonCondition(conditions[0]))
				conditions = [conditions[1], conditions[0]];

			var closeTags = new Stack<string>();
			foreach (var condition in conditions) {
				switch (condition) {
					case ButtonColourCondition buttonColourCondition:
						writer.WriteLine($"<condition var='colour' value='{buttonColourCondition.Colour}'>");
						closeTags.Push("</condition>");
						break;
					case ButtonLabelCondition buttonLabelCondition:
						writer.WriteLine($"<condition var='label' value='{buttonLabelCondition.Label}'>");
						closeTags.Push("</condition>");
						break;
					case BatteriesCondition<ButtonData> { Operation: Operations.MoreThan } batteriesCondition:
						writer.WriteLine("<condition name='BatteryCount'>\n<li value='unknown'><set var='result'>NeedEdgework BatteryCount</set></li>");
						for (var j = 0; j <= batteriesCondition.Number; ++j)
							writer.WriteLine($"<li value='{j}'></li>");
						writer.WriteLine("<li>");
						closeTags.Push("</li>\n</condition>");
						break;
					case IndicatorCondition<ButtonData> indicatorCondition:
						writer.WriteLine($"<condition name='{indicatorCondition.Predicate}'>\n" +
							$"<li value='unknown'><set var='result'>NeedEdgework {indicatorCondition.EdgeworkQuery}</set></li>\n" +
							"<li value='true'>");
						closeTags.Push("</li>\n</condition>");
						break;
					case PortCondition<ButtonData> portCondition:
						writer.WriteLine($"<condition name='Port{portCondition.PortType}'>\n<li value='true'>");
						closeTags.Push($"</li>\n<li value='unknown'><set var='result'>NeedEdgework Port {portCondition.PortType}</set></li>\n</condition>");
						break;
					case EmptyPortPlateCondition<ButtonData>:
						writer.WriteLine("<condition name='EmptyPortPlate'>\n<li value='true'>");
						closeTags.Push("</li>\n<li value='unknown'><set var='result'>NeedEdgework EmptyPortPlate</set></li>\n</condition>");
						break;
					case SerialNumberStartsWithLetterCondition<ButtonData>:
						writer.WriteLine("<condition name='SerialNumberStartsWithLetter'>\n<li value='true'>");
						closeTags.Push("</li>\n<li value='unknown'><set var='result'>NeedEdgework SerialNumberStartsWithLetter</set></li>\n</condition>");
						break;
					case SerialNumberParityCondition<ButtonData> serialNumberParityCondition:
						writer.WriteLine($"<condition name='SerialNumberIsOdd'>\n<li value='{serialNumberParityCondition.Odd}'>");
						closeTags.Push("</li>\n<li value='unknown'><set var='result'>NeedEdgework SerialNumberIsOdd</set></li>\n</condition>");
						break;
					default:
						throw new InvalidOperationException("Unknown condition");
				}
			}

			writer.WriteLine($"<set var='result'>{rule.Solution}</set>");
			while (closeTags.Count > 0)
				writer.WriteLine(closeTags.Pop());
			writer.WriteLine("</condition>");
		}
		writer.WriteLine("</think>");
		writer.WriteLine("<get var='result'/>");
		writer.WriteLine("</template>");
		writer.WriteLine("</category>");

		writer.WriteLine($"<category><pattern>SolverFallback Button {ruleSeed} <set>BombColours</set></pattern>");
		writer.WriteLine("<template>");
		writer.WriteLine("<think><set var='colour'><star/></set></think>");
		writer.WriteLine("<condition var='colour'>");

		foreach (var rule in rules.HeldRules) {
			writer.Write(rule.Colour.HasValue ? $"<li value='{rule.Colour}'>" : "<li>");
			writer.WriteLine($"{rule.Solution.Type} {rule.Solution.Digit}</li>");
		}

		writer.WriteLine("</condition>");
		writer.WriteLine("</template>");
		writer.WriteLine("</category>");
		writer.WriteLine("</aiml>");
	}

	public enum Colours {
		Red,
		Yellow,
		Blue,
		White
	}

	public enum Labels {
		Press,
		Abort,
		Detonate,
		Hold
	}

	public enum InitialSolution {
		Hold,
		Tap,
		TapWhenSecondsMatch
	}

	public enum HeldSolutionType {
		ReleaseOnTimerDigit,
		ReleaseOnSecondsDigitSum,
		ReleaseOnSecondsPrimeOrZero,
		ReleaseOnSecondsMultipleOfFour,
		ReleaseOnSecondsOnesDigit,
		ReleaseAnyTime
	}

	public class HeldSolution(HeldSolutionType type, int digit = 0) {
		public string Key { get; } = type.ToString() + digit;
		public HeldSolutionType Type { get; } = type;
		public int Digit { get; } = digit;

		public override string ToString() => Type switch {
			HeldSolutionType.ReleaseOnTimerDigit => $"release when the countdown timer has a {Digit} in any position",
			HeldSolutionType.ReleaseOnSecondsDigitSum => $"release when the two seconds digits add up to {Digit}{(Digit <= 14 ? " or 1" + Digit : "")}",
			HeldSolutionType.ReleaseOnSecondsPrimeOrZero => "release when the number of seconds displayed is either prime or 0",
			HeldSolutionType.ReleaseOnSecondsMultipleOfFour => "release when the two seconds digits add up to a multiple of 4",
			HeldSolutionType.ReleaseOnSecondsOnesDigit => $"release when right most seconds digit is {Digit}",
			HeldSolutionType.ReleaseAnyTime => "release at any time",
			_ => throw new InvalidOperationException("Unknown solution type."),
		};
	}

	public class InitialRule(List<Condition<ButtonData>> conditions, InitialSolution solution) {
		public List<Condition<ButtonData>> Conditions { get; } = conditions;
		public InitialSolution Solution { get; internal set; } = solution;

		public InitialRule(InitialSolution solution) : this([], solution) { }

		public override string ToString()
			=> "If " + (Conditions.Count == 0 ? "none of the above apply" : string.Join(" and ", Conditions)) + ", " +
				Solution switch {
					InitialSolution.Hold => "hold the button and refer to “Releasing a Held Button”",
					InitialSolution.Tap => "press and immediately release the button",
					InitialSolution.TapWhenSecondsMatch => "press and immediately release when the two seconds digits on the timer match",
					_ => ""
				} + ".";
	}

	public record HeldRule(Colours? Colour, HeldSolution Solution) {
		public override string ToString()
			=> (Colour != null ? Colour + " strip: " : "Any other color strip: ") +
			Solution;
	}

	public record RuleSet(InitialRule[] InitialRules, HeldRule[] HeldRules);

	public record struct ButtonData(Colours Colour, Labels Label);
}
