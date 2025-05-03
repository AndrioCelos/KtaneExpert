using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml.Linq;
using AngelAiml;
using KtaneExpert.Solvers;
using static KtaneExpert.Solvers.MazeSolver.MazeFlags;

namespace KtaneExpert.Tester;

internal class Program {
	private static void Main(string[] args) {
		Console.OutputEncoding = Encoding.UTF8;

		var solvers = new Dictionary<string, IModuleSolver>();
		foreach (var type in typeof(ButtonSolver).Assembly.GetExportedTypes().Where(t => !t.IsInterface && typeof(IModuleSolver).IsAssignableFrom(t))) {
			var solver = (IModuleSolver?) Activator.CreateInstance(type);
			if (solver != null) solvers.Add(type.Name, solver);
		}

		switch (args) {
			case [ "/?" or "-?" or "-h" or "--help", ..]:
				Console.WriteLine("Usage: KtaneExpert.Tester [options]");
				Console.WriteLine("Without options, enters interactive tester mode.");
				Console.WriteLine("Options:");
				Console.WriteLine("  -g <rule seed> [out directory], --generate <rule seed> [out directory] : Generates AIML files for the specified rule seed.");
				return;
			case ["-g" or "--generate", ..]: {
				var ruleSeed = int.Parse(args[1]);
				var path = args.Length > 2 ? args[2] : "aiml" + ruleSeed;
				GenerateAiml(solvers, ruleSeed, path);
				return;
			}
		}

		var user = new User("User", new());
		var dummyElement = XElement.Parse("<dummy/>");

		string Evaluate(string serviceName, string text)
			=> solvers[serviceName].Process(text, dummyElement, new(new(new(text, user, user.Bot), text), 0, false));

		while (true) {
			Console.Write("> ");
			var s = Console.ReadLine();
			if (s == null) return;
			var fields = s.Split((char[]?) null, StringSplitOptions.RemoveEmptyEntries);

			try {
				var text = string.Join(" ", fields.Skip(1));
				switch (fields[0].ToLower()) {
					case "reset":
						user = new("User", new());
						break;
					case "set":
						user.Predicates[fields[1]] = fields[2];
						break;
					case "generate":
						var ruleSeed = int.Parse(fields[1]);
						var path = Path.Combine(Path.GetTempPath(), "aiml" + ruleSeed);
						GenerateAiml(solvers, ruleSeed, path);

						Console.WriteLine("Bot files written to " + path);
						break;
					case "button":
						if (fields[1].Equals("rules", StringComparison.CurrentCultureIgnoreCase)) {
							var rules = ButtonSolver.GetRules(int.Parse(fields[2]));

							for (var i = 0; i < rules.InitialRules.Length; ++i)
								Console.WriteLine($"{i + 1}. {rules.InitialRules[i]}");

							Console.WriteLine();
							Console.ForegroundColor = ConsoleColor.White;
							Console.WriteLine("Releasing a Held Button");
							Console.ResetColor();

							foreach (var t in rules.HeldRules)
								Console.WriteLine(t);

							Console.WriteLine();
						} else {
							Console.WriteLine(Evaluate(nameof(ButtonSolver), text));
						}
						break;
					case "complicatedwires":
						if (fields[1].Equals("rules", StringComparison.CurrentCultureIgnoreCase)) {
							var rules = ComplicatedWiresSolver.GetRules(int.Parse(fields[2]));

							for (var i = 0; i < rules.Length; ++i) {
								Console.WriteLine($"{(ComplicatedWiresSolver.Flags) i}: {rules[i]}");
							}
							Console.WriteLine();
						} else {
							Console.WriteLine(Evaluate(nameof(ComplicatedWiresSolver), text));
						}
						break;
					case "hexamaze":
						if (fields[1].Equals("rules", StringComparison.CurrentCultureIgnoreCase)) {
							var maze = HexamazeSolver.GetMaze(int.Parse(fields[2]));
							Console.WriteLine(maze);
						} else {
							Console.WriteLine(Evaluate(nameof(HexamazeSolver), text));
						}
						break;
					case "keypad":
						if (fields[1].Equals("rules", StringComparison.CurrentCultureIgnoreCase)) {
							var columns = KeypadSolver.GetColumns(int.Parse(fields[2]));

							foreach (var t in columns)
								Console.WriteLine(string.Join("", t.Select(g => g.ToString().PadRight(14))));

							Console.WriteLine();
						} else {
							Console.WriteLine(Evaluate(nameof(KeypadSolver), text));
						}
						break;
					case "knob":
						if (fields[1].Equals("rules", StringComparison.CurrentCultureIgnoreCase)) {
							var rules = KnobSolver.GetRules(int.Parse(fields[2]));

							for (var i = 0; i < 4; ++i) {
								Console.ForegroundColor = ConsoleColor.White;
								Console.WriteLine($"{(KnobSolver.Position) i} Position:");
								Console.ResetColor();

								var rule0 = rules[i * 2];
								var rule1 = rules[i * 2 + 1];

								for (var j = 0; j < 6; ++j) {
									Console.Write(rule0.Lights[j] ? 'X' : '.');
									Console.Write(' ');
								}
								Console.Write("    ");
								for (var j = 0; j < 6; ++j) {
									Console.Write(rule1.Lights[j] ? 'X' : '.');
									Console.Write(' ');
								}
								Console.WriteLine();
								for (var j = 6; j < 12; ++j) {
									Console.Write(rule0.Lights[j] ? 'X' : '.');
									Console.Write(' ');
								}
								Console.Write("    ");
								for (var j = 6; j < 12; ++j) {
									Console.Write(rule1.Lights[j] ? 'X' : '.');
									Console.Write(' ');
								}
								Console.WriteLine();
								Console.WriteLine();
							}
						} else {
							Console.WriteLine(Evaluate(nameof(KnobSolver), text));
						}
						break;
					case "maze":
						char BoxDrawingChar(MazeSolver.MazeFlags flags) => flags switch {
							0 => ' ',
							North => '╵',
							West => '╴',
							West | North => '┘',
							East => '╶',
							East | North => '└',
							East | West => '─',
							East | West | North => '┴',
							South => '╷',
							South | North => '│',
							South | West => '┐',
							South | West | North => '┤',
							South | East => '┌',
							South | East | North => '├',
							South | East | West => '┬',
							South | East | West | North => '┼',
							_ => '?',
						};

						if (fields[1].Equals("rules", StringComparison.CurrentCultureIgnoreCase)) {
							var mazes = MazeSolver.GetMazes(int.Parse(fields[2]));

							for (var i = 0; i < mazes.Length; ++i) {
								var maze = mazes[i];
								var maxx = maze.Cells.GetUpperBound(0);
								var maxy = maze.Cells.GetUpperBound(1);

								Console.ForegroundColor = ConsoleColor.White;
								Console.WriteLine($"Maze {i + 1}");
								Console.ResetColor();

								for (var y = 0; ; ++y) {
									for (var x = 0; ; ++x) {
										// Corner tile
										MazeSolver.MazeFlags flags = 0;
										if (y > 0 && (x <= 0 || !maze.Cells[x - 1, y - 1].HasFlag(East))) flags |= North;
										if (x > 0 && (y <= 0 || !maze.Cells[x - 1, y - 1].HasFlag(South))) flags |= West;
										if (x <= maxx && (y <= 0 || !maze.Cells[x, y - 1].HasFlag(South))) flags |= East;
										if (y <= maxy && (x <= 0 || !maze.Cells[x - 1, y].HasFlag(East))) flags |= South;
										Console.Write(BoxDrawingChar(flags));

										if (x > maxx) break;
										// Horizontal border tile
										flags = 0;
										if (y <= 0 || !maze.Cells[x, y - 1].HasFlag(South)) flags |= West | East;
										Console.Write(BoxDrawingChar(flags));
									}
									Console.WriteLine();

									if (y > maxy) break;
									for (var x = 0; ; ++x) {
										// Vertical border tile
										MazeSolver.MazeFlags flags = 0;
										if (x <= 0 || !maze.Cells[x - 1, y].HasFlag(East)) flags |= North | South;
										Console.Write(BoxDrawingChar(flags));

										if (x > maxx) break;
										// Space tile
										if (maze.Indicators.Contains(new(x, y))) {
											Console.ForegroundColor = ConsoleColor.Green;
											Console.Write($"O");
											Console.ResetColor();
										} else
											Console.Write('·');
									}
									Console.WriteLine();
								}

								Console.WriteLine();
							}
						} else {
							Console.WriteLine(Evaluate(nameof(MazeSolver), text));
						}
						break;
					case "memory":
						if (fields[1].Equals("rules", StringComparison.CurrentCultureIgnoreCase)) {
							var rules = MemorySolver.GetRules(int.Parse(fields[2]));

							for (var i = 0; i <= rules.GetUpperBound(0); ++i) {
								Console.ForegroundColor = ConsoleColor.White;
								Console.WriteLine($"Stage {i + 1}:");
								Console.ResetColor();

								for (var j = 0; j < 4; ++j) {
									Console.WriteLine($"If the display is {j + 1}, {rules[i, j]}.");
								}
								Console.WriteLine();
							}
						} else {
							Console.WriteLine(Evaluate(nameof(MemorySolver), text));
						}
						break;
					case "morsecode":
						if (fields[1].Equals("rules", StringComparison.CurrentCultureIgnoreCase)) {
							var entries = MorseCodeSolver.GetRules(int.Parse(fields[2])).OrderBy(e => e.Value);

							Console.ForegroundColor = ConsoleColor.White;
							Console.WriteLine($"If the word is:  Respond at frequency:");
							Console.ResetColor();
							foreach (var entry in entries) {
								Console.WriteLine($"{entry.Key,-15}  {entry.Value} MHz");
							}
							Console.WriteLine();
						} else {
							Console.WriteLine(Evaluate(nameof(MorseCodeSolver), text));
						}
						break;
					case "password":
						if (fields[1].Equals("rules", StringComparison.CurrentCultureIgnoreCase)) {
							var words = PasswordSolver.GetWords(int.Parse(fields[2]));

							for (var i = 0; i < words.Length; ++i) {
								Console.Write(words[i]);
								if (i % 5 == 4) Console.WriteLine();
								else Console.Write("  ");
							}
							Console.WriteLine();
						} else {
							Console.WriteLine(Evaluate(nameof(PasswordSolver), text));
						}
						break;
					case "simonsays":
						if (fields[1].Equals("rules", StringComparison.CurrentCultureIgnoreCase)) {
							var colours = SimonSaysSolver.GetRules(int.Parse(fields[2]));

							for (var i = 1; i >= 0; --i) {
								Console.ForegroundColor = ConsoleColor.White;
								Console.WriteLine($"If the serial number {(i > 0 ? "contains" : "does not contain")} a vowel:");
								Console.ResetColor();
								Console.WriteLine($"            Red flash     Blue flash    Green flash   Yellow flash");
								for (var j = 0; j < 3; ++j) {
									Console.Write((j == 0 ? "No strikes" : j == 1 ? "1 strike" : "2+ strikes").PadRight(12));
									for (var k = 0; k < 4; ++k) {
										Console.Write(colours[i, j, k].ToString().PadRight(14));
									}
									Console.WriteLine();
								}
								Console.WriteLine();
							}
						} else {
							Console.WriteLine(Evaluate(nameof(SimonSaysSolver), text));
						}
						break;
					case "whosonfirst":
						if (fields[1].Equals("rules", StringComparison.CurrentCultureIgnoreCase)) {
							var ruleSet = WhosOnFirstSolver.GetRules(int.Parse(fields[2]));

							Console.ForegroundColor = ConsoleColor.White;
							Console.WriteLine($"Step 1:");
							Console.ResetColor();

							var count = 0;
							StringBuilder builder1 = new(), builder2 = new(), builder3 = new(), builder4 = new();
							foreach (var entry in ruleSet.DisplayRules) {
								builder1.Append(entry.Key.PadRight(10));
								builder2.Append($" {(entry.Value == 0 ? 'O' : '.')}   {(entry.Value == 1 ? 'O' : '.')}    ");
								builder3.Append($" {(entry.Value == 2 ? 'O' : '.')}   {(entry.Value == 3 ? 'O' : '.')}    ");
								builder4.Append($" {(entry.Value == 4 ? 'O' : '.')}   {(entry.Value == 5 ? 'O' : '.')}    ");

								++count;
								if (count % 6 == 0) {
									Console.WriteLine(builder1.ToString());
									Console.WriteLine(builder2.ToString());
									Console.WriteLine(builder3.ToString());
									Console.WriteLine(builder4.ToString());
									Console.WriteLine();
									builder1.Clear();
									builder2.Clear();
									builder3.Clear();
									builder4.Clear();
								}
							}
							if (count % 6 > 0) {
								Console.WriteLine(builder1.ToString());
								Console.WriteLine(builder2.ToString());
								Console.WriteLine(builder3.ToString());
								Console.WriteLine(builder4.ToString());
								Console.WriteLine();
							}

							Console.ForegroundColor = ConsoleColor.White;
							Console.WriteLine($"Step 2:");
							Console.ResetColor();

							foreach (var entry in ruleSet.ButtonRules)
								Console.WriteLine($"{$"\"{entry.Key}\":",-12}{string.Join(", ", entry.Value)}");

							Console.WriteLine();
						} else {
							Console.WriteLine(Evaluate(nameof(WhosOnFirstSolver), text));
						}
						break;
					case "wires":
						if (fields[1].Equals("rules", StringComparison.CurrentCultureIgnoreCase)) {
							var rules = WiresSolver.GetRules(int.Parse(fields[2]));

							for (var i = 0; i < rules.Length; ++i) {
								Console.ForegroundColor = ConsoleColor.White;
								Console.WriteLine($"{i + 3} wires");
								Console.ResetColor();

								var otherwise = false;
								foreach (var rule in rules[i]) {
									if (otherwise) Console.Write("Otherwise, ");
									otherwise = true;
									Console.WriteLine(rule.ToString());
								}

								Console.WriteLine();
							}
						} else {
							Console.WriteLine(Evaluate(nameof(WiresSolver), text));
						}
						break;
					case "wiresequence":
						if (fields[1].Equals("rules", StringComparison.CurrentCultureIgnoreCase)) {
							string DescribeInstruction(WireSequenceSolver.Instruction instruction) => (int) instruction switch {
								0 => "never",
								1 => "A",
								2 => "B",
								3 => "A or B",
								4 => "C",
								5 => "A or C",
								6 => "B or C",
								7 => "A, B or C",
								_ => instruction.ToString(),
							};

							var rules = WireSequenceSolver.GetRules(int.Parse(fields[2]));

							Console.ForegroundColor = ConsoleColor.White;
							Console.WriteLine("         Red        Blue       Black");
							Console.ResetColor();
							for (var i = 0; i < 9; ++i) {
								var ordinal = i switch {
									0 => "first",
									1 => "second",
									2 => "third",
									3 => "fourth",
									4 => "fifth",
									5 => "sixth",
									6 => "seventh",
									7 => "eighth",
									_ => "ninth",
								};
								Console.WriteLine($"{ordinal,-7}  {DescribeInstruction(rules.RedRules[i]),-9}  {DescribeInstruction(rules.BlueRules[i]),-9}  {DescribeInstruction(rules.BlackRules[i])}");
							}
							Console.WriteLine();
						} else {
							Console.WriteLine(Evaluate(nameof(WireSequenceSolver), text));
						}
						break;
					case "q":
						return;
				}
			} catch (Exception ex) {
				Console.Error.WriteLine(ex);
			}
		}
	}

	private static void GenerateAiml(Dictionary<string, IModuleSolver> solvers, int ruleSeed, string path) {
		Directory.CreateDirectory(Path.Combine(path, "aiml"));
		Directory.CreateDirectory(Path.Combine(path, "sets"));
		Directory.CreateDirectory(Path.Combine(path, "maps"));

		using (var writer = new StreamWriter(Path.Combine(path, "aiml", $"bomb{ruleSeed}.aiml"))) {
			writer.WriteLine("<?xml version='1.0' encoding='UTF-8'?>");
			writer.WriteLine("<aiml version='2.0'>");
			writer.WriteLine("<category>");
			writer.WriteLine($"<pattern>OKRuleSeed {ruleSeed}</pattern>");
			writer.WriteLine("<template>true</template>");
			writer.WriteLine("</category>");
			writer.WriteLine("</aiml>");
		}

		foreach (var solver in solvers.Values) {
			solver.GenerateAiml(path, ruleSeed);
		}
	}
}
