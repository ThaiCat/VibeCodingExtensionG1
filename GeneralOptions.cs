using Microsoft.VisualStudio.Shell;
using System.ComponentModel;

namespace VibeCodingExtensionG1
{
    public class GeneralOptions : DialogPage
    {
        // --- Секция подключения ---
        [Category("Connection")]
        [DisplayName("LM Studio API URL")]
        [Description("Endpoint для API (например, http://localhost:1234/v1/chat/completions)")]
        public string ApiUrl { get; set; } = "http://localhost:1234/v1/chat/completions";

        [Category("Connection")]
        [DisplayName("Model Name")]
        [Description("Идентификатор модели в LM Studio (обычно 'local-model')")]
        public string ModelName { get; set; } = "local-model";

        // --- Секция параметров генерации ---
        [Category("AI Parameters")]
        [DisplayName("Temperature")]
        [Description("Степень случайности (0.0 — точный ответ, 1.0 — творческий). Рекомендуется 0.7")]
        public double Temperature { get; set; } = 0.1;

        [Category("AI Parameters")]
        [DisplayName("Max Tokens")]
        [Description("Максимальное количество генерируемых токенов (-1 для бесконечности)")]
        public int MaxTokens { get; set; } = -1;

        // --- Секция промптов ---
        [Category("AI Prompts")]
        [DisplayName("Explain Prompt")]
        public string ExplainPrompt { get; set; } = "Объясни, как работает этот код:";

        [Category("AI Prompts")]
        [DisplayName("Fix Bugs Prompt")]
        public string FixBugsPrompt { get; set; } = "Найди ошибки и предложи исправления в этом коде:";

        [Category("AI Prompts")]
        [DisplayName("Optimize Prompt")]
        public string OptimizePrompt { get; set; } = "Оптимизируй этот код для лучшей читаемости и производительности:";
    }
}