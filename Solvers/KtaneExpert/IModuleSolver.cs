using AngelAiml;
using JetBrains.Annotations;

namespace KtaneExpert;
/// <summary>Represents a solver for a Keep Talking and Nobody Explodes module that can be called from an AIML script.</summary>
[UsedImplicitly(ImplicitUseTargetFlags.WithInheritors)]
public interface IModuleSolver : ISraixService {
	/// <summary>Writes AIML files for this module to the specified path.</summary>
	/// <remarks>
	///		The following directories should be available for writing files to before calling this method:
	///		<list type="bullet">
	///			<item><paramref name="path"/>/aiml</item>
	///			<item><paramref name="path"/>/maps</item>
	///			<item><paramref name="path"/>/sets</item>
	///		</list>
	/// </remarks>
	public void GenerateAiml(string path, int ruleSeed);
}
