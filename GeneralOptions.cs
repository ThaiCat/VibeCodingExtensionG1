using Microsoft.VisualStudio.Shell;
using System.ComponentModel;

namespace VibeCodingExtensionG1
{
    public class GeneralOptions : DialogPage
    {
        // --- Секция подключения ---
        [Category("Connection")]
        [DisplayName("LM Studio API URL")]
        [Description("Endpoint для LM Studio (например, http://localhost:1234/v1/chat/completions)")]
        public string ApiUrl { get; set; } = "http://localhost:1234/v1/chat/completions";

        // --- Секция промптов ---
        [Category("AI Prompts")]
        [DisplayName("Explain Prompt")]
        [Description("Инструкция для команды Explain Code")]
        public string ExplainPrompt { get; set; } = "Объясни, как работает этот код:";

        [Category("AI Prompts")]
        [DisplayName("Fix Bugs Prompt")]
        [Description("Инструкция для команды Fix Bugs")]
        public string FixBugsPrompt { get; set; } = "Найди ошибки и предложи исправления в этом коде:";

        [Category("AI Prompts")]
        [DisplayName("Optimize Prompt")]
        [Description("Инструкция для команды Optimize Code")]
        public string OptimizePrompt { get; set; } = "Оптимизируй этот код для лучшей читаемости и производительности:";
    }
}