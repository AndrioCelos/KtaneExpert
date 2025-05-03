using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using AngelAiml;
using static KtaneExpert.Colour;

namespace KtaneExpert.Solvers;
public class SimonSaysSolver : IModuleSolver {
	public static Colour[,,] GetRules(int ruleSeed) {
		if (ruleSeed == 1) return new[,,] {
			// If the serial number does not contain a vowel
			{
				{ Blue, Yellow, Green, Red },
				{ Red, Blue, Yellow, Green },
				{ Yellow, Green, Blue, Red }
			},
			// If the serial number contains a vowel
			{
				{ Blue, Red, Yellow, Green },
				{ Yellow, Green, Blue, Red },
				{ Green, Red, Yellow, Blue }
			}
		};

		var random = new MonoRandom(ruleSeed);
		var colours = new Colour[2, 3, 4];
		for (var i = 1; i >= 0; --i) {
			for (var j = 0; j < 3; ++j) {
				for (var k = 0; k < 4; ++k)
					colours[i, j, k] = (Colour) random.Next(4);
				// Make sure they're not all the same.
				while (colours[i, j, 1] == colours[i, j, 0] &&
					colours[i, j, 2] == colours[i, j, 0] &&
					colours[i, j, 3] == colours[i, j, 0])
					colours[i, j, random.Next(4)] = (Colour) random.Next(4);
			}
		}

		return colours;
	}

	public string Process(string text, XElement element, RequestProcess process) {
		// Usage: <rule seed> <vowel> <strikes> <colour>+

		var words = text.Split((char[]?) null, StringSplitOptions.RemoveEmptyEntries);
		var ruleSeed = int.Parse(words[0]);
		var vowel = bool.Parse(words[1]);
		var strikes = int.Parse(words[2]);
		var colours = from s in words.Skip(3) select (Colour) Enum.Parse(typeof(Colour), s, true);
		return string.Join(" ", Solve(ruleSeed, vowel, strikes, colours.ToList()));
	}

	private static Colour[] Solve(int ruleSeed, bool vowel, int strikes, IList<Colour> colours) {
		if (strikes > 2) strikes = 2;
		var rules = GetRules(ruleSeed);

		var result = new Colour[colours.Count];
		for (var i = 0; i < colours.Count; ++i) {
			result[i] = rules[vowel ? 1 : 0, strikes, (int) colours[i]];
		}
		return result;
	}

	public void GenerateAiml(string path, int ruleSeed) {
		var rules = GetRules(ruleSeed);
		using var writer = new StreamWriter(Path.Combine(path, "maps", $"SimonSays{ruleSeed}.txt"));
		for (var i = 0; i < 2; ++i) {
			for (var j = 0; j < 3; ++j) {
				for (var k = 0; k < 4; ++k) {
					writer.WriteLine($"{i == 1} {j} {k switch { 0 => "red", 1 => "blue", 2 => "green", 3 => "yellow", _ => "unknown" }}:{rules[i, j, k]}");
				}
			}
		}
	}
}
