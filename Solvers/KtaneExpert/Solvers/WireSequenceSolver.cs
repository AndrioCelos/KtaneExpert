using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using AngelAiml;
using static KtaneExpert.Solvers.WireSequenceSolver.Instruction;

namespace KtaneExpert.Solvers;
public class WireSequenceSolver : IModuleSolver {
	private const int NumPages = 4;
	private const int NumPerPage = 3;
	private const int BlankPageCount = 1;
	private const int NumWiresPerColour = NumPerPage * (NumPages - BlankPageCount);

	public static RuleSet GetRules(int ruleSeed) {
		if (ruleSeed == 1) return new(
			[CutC, CutB, CutA, CutA | CutC, CutB, CutA | CutC, CutA | CutB | CutC, CutA | CutB, CutB],
			[CutB, CutA | CutC, CutB, CutA, CutB, CutB | CutC, CutC, CutA | CutC, CutA],
			[CutA | CutB | CutC, CutA | CutC, CutB, CutA | CutC, CutB, CutB | CutC, CutA | CutB, CutC, CutC]
		);

		var random = new MonoRandom(ruleSeed);
		return new(
			GetInstructions(random),
			GetInstructions(random),
			GetInstructions(random)
		);
	}

	private static Instruction[] GetInstructions(Random random) {
		var instructions = new Instruction[NumWiresPerColour];
		for (var i = 0; i < NumWiresPerColour; ++i) {
			var flags = Never;
			for (var j = 0; j < 3; ++j) {
				if (random.NextDouble() > 0.55) flags |= (Instruction) (1 << j);
			}
			instructions[i] = flags;
		}
		return instructions;
	}

	public void GenerateAiml(string path, int ruleSeed) {
		var rules = GetRules(ruleSeed);

		using var writer = new StreamWriter(Path.Combine(path, "maps", $"WireSequence{ruleSeed}.txt"));
		void Write(string colour, Instruction[] instructions) {
			for (var i = 0; i < instructions.Length; ++i) {
				var instruction = instructions[i];
				writer.Write($"{colour} {i + 1}:");
				if (instruction == 0) writer.WriteLine("nil");
				else {
					if (instruction.HasFlag(CutA)) writer.Write("A ");
					if (instruction.HasFlag(CutB)) writer.Write("B ");
					if (instruction.HasFlag(CutC)) writer.Write("C ");
					writer.WriteLine();
				}
			}
		}
		Write("red", rules.RedRules);
		Write("blue", rules.BlueRules);
		Write("black", rules.BlackRules);
	}

	public string Process(string text, XElement element, RequestProcess process) {
		// Usage: <rule seed> GetRule <colour> <total> | <rule seed> <red total> <blue total> <black total> <colour> <letter>
		
		var words = text.Split((char[]?) null, StringSplitOptions.RemoveEmptyEntries);
		var rules = GetRules(int.Parse(words[0]));

		if (words[1].Equals("GetRule", StringComparison.InvariantCultureIgnoreCase)) {
			var colour = (Colour) Enum.Parse(typeof(Colour), words[2], true);
			var total = int.Parse(words[3]);

			var ruleSet = colour == Colour.Red ? rules.RedRules : colour == Colour.Blue ? rules.BlueRules : rules.BlackRules;
			var cut = ruleSet[total - 1];

			var result = string.Join(' ', Enumerable.Range(0, 3).Where(i => cut.HasFlag((Instruction) (1 << i))).Select(i => (char) ('A' + i)));
			return result != "" ? result : "nil";
		} else {
			var redWireCount = int.Parse(words[1]);
			var blueWireCount = int.Parse(words[2]);
			var blackWireCount = int.Parse(words[3]);

			var colour = (Colour) Enum.Parse(typeof(Colour), words[4], true);
			var letter = char.ToUpperInvariant(words[5][0]);

			var ruleSet = colour == Colour.Red ? rules.RedRules : colour == Colour.Blue ? rules.BlueRules : rules.BlackRules;
			var total = colour == Colour.Red ? redWireCount : colour == Colour.Blue ? blueWireCount : blackWireCount;
			var cut = ruleSet[total - 1].HasFlag(letter == 'A' ? CutA : letter == 'B' ? CutB : CutC);
			return cut ? "true" : "false";
		}
	}

	[Flags]
	public enum Instruction {
		Never,
		CutA = 1,
		CutB = 2,
		CutC = 4
	}

	public record RuleSet(Instruction[] RedRules, Instruction[] BlueRules, Instruction[] BlackRules) {
		public Instruction[] this[Colour colour] => colour switch {
			Colour.Red => RedRules,
			Colour.Blue => BlueRules,
			Colour.Black => BlackRules,
			_ => throw new KeyNotFoundException(),
		};
	}
}
