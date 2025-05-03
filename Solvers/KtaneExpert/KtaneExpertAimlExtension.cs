using System;
using System.Linq;
using AngelAiml;

namespace KtaneExpert;
public class KtaneExpertAimlExtension : IAimlExtension {
	public void Initialise() {
		foreach (var type in typeof(KtaneExpertAimlExtension).Assembly.GetExportedTypes().Where(t => !t.IsInterface && typeof(IModuleSolver).IsAssignableFrom(t))) {
			AimlLoader.AddCustomSraiXService((IModuleSolver) Activator.CreateInstance(type)!);
		}
	}
}
