using System;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using AngelAiml;
using static KtaneExpert.Solvers.ComplicatedWiresSolver.Flags;

namespace KtaneExpert.Solvers;
public class ComplicatedWiresSolver : IModuleSolver {
	private static readonly Instruction[] instructions = [Instruction.Cut, Instruction.DoNotCut, Instruction.CutIfSerialNumberEven, Instruction.CutIfParallelPort, Instruction.CutIfTwoOrMoreBatteries];

	public static Instruction[] GetRules(int seed) {
		var array = new Instruction[16];
		var weights = new WeightMap<Instruction, Instruction>(i => i);
		var random = new MonoRandom(seed);

		for (var i = 1; i < 16; ++i) {
			array[i] = weights.Roll(instructions, random, 0.1f);
		}
		if (seed != 1 && array.Count(i => i == Instruction.Cut) >= 2)
			array[0] = weights.Roll(instructions, random, 0.1f);
		return array;
	}

	public void GenerateAiml(string path, int ruleSeed) {
		var rules = GetRules(ruleSeed);

		using var writer = new StreamWriter(Path.Combine(path, "maps", $"ComplicatedWires{ruleSeed}.txt"));
		for (Flags flags = 0; flags < (Flags) 16; ++flags) {
			if (flags == 0)
				writer.Write("nil ");
			else {
				if (flags.HasFlag(Red)) writer.Write(nameof(Red) + " ");
				if (flags.HasFlag(Blue)) writer.Write(nameof(Blue) + " ");
				if (flags.HasFlag(Star)) writer.Write(nameof(Star) + " ");
				if (flags.HasFlag(Light)) writer.Write(nameof(Light) + " ");
			}
			writer.WriteLine(":" + rules[(int) flags] switch {
				Instruction.Cut => 'C',
				Instruction.DoNotCut => 'D',
				Instruction.CutIfSerialNumberEven => 'S',
				Instruction.CutIfParallelPort => 'P',
				Instruction.CutIfTwoOrMoreBatteries => 'B',
				_ => '?'
			});
		}
	}

	public string Process(string text, XElement element, RequestProcess process) {
		var inputWords = text.Split((char[]?) null, StringSplitOptions.RemoveEmptyEntries);

		var flags = text != "" && !text.Equals("nil", StringComparison.InvariantCultureIgnoreCase)
			? inputWords.Skip(1).Aggregate((Flags) 0, (current, word) => current | (Flags) Enum.Parse(typeof(Flags), word, true))
			: 0;

		var rules = GetRules(int.Parse(inputWords[0]));

		var instruction = rules[(int) flags];
		bool cut;
		switch (instruction) {
			case Instruction.Cut: cut = true; break;
			case Instruction.DoNotCut: cut = false; break;
			case Instruction.CutIfSerialNumberEven:
				var predicate = process.User.GetPredicate("SerialNumberIsOdd");
				if (predicate.Equals("unknown", StringComparison.CurrentCultureIgnoreCase)) return "NeedEdgework SerialNumberIsOdd";
				cut = !bool.Parse(predicate);
				break;
			case Instruction.CutIfParallelPort:
				predicate = process.User.GetPredicate("PortParallel");
				if (predicate.Equals("unknown", StringComparison.CurrentCultureIgnoreCase)) return "NeedEdgework Port Parallel";
				cut = bool.Parse(predicate);
				break;
			case Instruction.CutIfTwoOrMoreBatteries:
				predicate = process.User.GetPredicate("BatteryCount");
				if (predicate.Equals("unknown", StringComparison.CurrentCultureIgnoreCase)) return "NeedEdgework BatteryCount";
				cut = int.Parse(predicate) >= 2;
				break;
			default:
				throw new InvalidOperationException("Unknown instruction");
		}

		return cut.ToString();
	}

	[Flags]
	public enum Flags {
		Light = 1,
		Star = 2,
		Blue = 4,
		Red = 8
	}

	public enum Instruction {
		Cut,
		DoNotCut,
		CutIfSerialNumberEven,
		CutIfParallelPort,
		CutIfTwoOrMoreBatteries
	}
}
