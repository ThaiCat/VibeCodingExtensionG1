using Microsoft.VisualStudio.Shell;
using System.ComponentModel;

namespace VibeCodingExtensionG1
{
    public class GeneralOptions : DialogPage
    {
        [Category("AI Prompts")]
        [DisplayName("Explain Prompt")]
        [Description("Текст перед кодом для команды Explain")]
        public string ExplainPrompt { get; set; } = "Объясни, как работает этот код:";

        [Category("AI Prompts")]
        [DisplayName("Fix Bugs Prompt")]
        [Description("Текст перед кодом для команды Fix Bugs")]
        public string FixBugsPrompt { get; set; } = "Найди ошибки и предложи исправления:";

        [Category("AI Prompts")]
        [DisplayName("Optimize Prompt")]
        [Description("Текст перед кодом для команды Optimize")]
        public string OptimizePrompt { get; set; } = "Оптимизируй этот код:";
    }
}