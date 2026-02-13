using Microsoft.VisualStudio.Shell;
using System.ComponentModel;

namespace VibeCodingExtensionG1
{
    public class GeneralOptions : DialogPage
    {
        // --- Подключение ---
        [Category("1. Connection")]
        [DisplayName("Model Name")]
        public string ModelName { get; set; } = "openai/gpt-oss-20b";

        [Category("1. Connection")]
        [DisplayName("Models List URL")]
        [Description("Эндпоинт для получения списка активных моделей")]
        public string ModelsUrl { get; set; } = "http://localhost:1234/v1/models";

        [Category("1. Connection")]
        [DisplayName("API URL")]
        [Description("Endpoint для API LM Studio")]
        public string ApiUrl { get; set; } = "http://localhost:1234/v1/chat/completions";
        
        [Category("1. Connection")]
        [DisplayName("Unload Model URL")]
        [Description("Эндпоинт для выгрузки модели из памяти")]
        public string UnloadUrl { get; set; } = "http://localhost:1234/api/v1/models/unload";

        [Category("1. Connection")]
        [DisplayName("Load Model URL")]
        [Description("Эндпоинт для загрузки модели в память")]
        public string LoadUrl { get; set; } = "http://localhost:1234/api/v1/models/load";

        [Category("2. AI Parameters")]
        [DisplayName("Context Length")]
        [Description("Размер контекстного окна при перезагрузке (токены)")]
        public int ContextLength { get; set; } = 32000;

        // --- Параметры нейросети ---
        [Category("2. AI Parameters")]
        [DisplayName("Temperature")]
        [Description("0.0 - точный ответ, 1.0 - творческий. Рекомендуется 0.7")]
        public double Temperature { get; set; } = 0.1;

        [Category("2. AI Parameters")]
        [DisplayName("Max Tokens")]
        [Description("Лимит длины ответа (-1 — без ограничений)")]
        public int MaxTokens { get; set; } = -1;

        [Category("2. AI Parameters")]
        [DisplayName("System Prompt")]
        [Description("Инструкция, определяющая роль ИИ")]
        public string SystemPrompt { get; set; } = "Ты опытный C# разработчик. Отвечай кратко, профессионально и приводи примеры кода в блоках ```csharp";

        // --- Шаблоны команд ---
        [Category("3. AI Prompts")]
        [DisplayName("Explain Prompt")]
        public string ExplainPrompt { get; set; } = "Объясни следующий фрагмент кода, опираясь на контекст файлов выше:";

        [Category("3. AI Prompts")]
        [DisplayName("Fix Bugs Prompt")]
        public string FixBugsPrompt { get; set; } = "Найди ошибки в этом фрагменте кода и исправь их. Учти связи с другими файлами из контекста:";

        [Category("3. AI Prompts")]
        [DisplayName("Optimize Prompt")]
        public string OptimizePrompt { get; set; } = "Оптимизируй этот фрагмент кода:";
    }
}