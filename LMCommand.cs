using System;
using System.ComponentModel.Design;
using System.Globalization;
using System.IO;
using System.Net.Http;
using System.Runtime.Serialization.Json;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Web; // Требует ссылки на System.Web
using EnvDTE;
using EnvDTE80;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using System.Text.Json;

namespace VibeCodingExtensionG1
{
    internal sealed class LMCommand
    {
        public const int CommandId = 0x0100; 
        public const int InsertCommandId = 0x0101; // ID должен совпадать с VSCT
        public static readonly Guid CommandSet = new Guid("7A94A48F-9C2B-42E9-8179-ED0C72668AF5");

        private readonly AsyncPackage package;
        private static string lastAiResponse = string.Empty;
        // Статичный клиент с преднастроенным таймаутом
        //private static readonly HttpClient client = new HttpClient (){ Timeout = TimeSpan.FromSeconds(1200) }; 
        
        private static readonly HttpClient client = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(1200),
            MaxResponseContentBufferSize = 1024 * 1024 // 1 МБ лимит на ответ
        };


        private LMCommand(AsyncPackage package, OleMenuCommandService commandService)
        {
            this.package = package ?? throw new ArgumentNullException(nameof(package));

            // Команда 1: Запрос
            var menuCommandID1 = new CommandID(CommandSet, CommandId);
            var menuItem1 = new MenuCommand(this.ExecuteRequest, menuCommandID1);
            commandService.AddCommand(menuItem1);

            // Команда 2: Вставка
            var menuCommandID2 = new CommandID(CommandSet, InsertCommandId);
            var menuItem2 = new MenuCommand(this.ExecuteInsert, menuCommandID2);
            commandService.AddCommand(menuItem2);
        }

        public static LMCommand Instance { get; private set; }

        public static async Task InitializeAsync(AsyncPackage package)
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync(package.DisposalToken);
            OleMenuCommandService commandService = await package.GetServiceAsync(typeof(IMenuCommandService)) as OleMenuCommandService;
            Instance = new LMCommand(package, commandService);
        }

        private void ExecuteRequest(object sender, EventArgs e)
        {
            ThreadHelper.JoinableTaskFactory.RunAsync(async delegate
            {
                await ProcessRequestAsync();
            });
        }


        private async Task ProcessRequestAsync()
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync(package.DisposalToken);
            var dte = await package.GetServiceAsync(typeof(SDTE)) as DTE2;

            // Получаем URL из настроек
            var options = (OptionPageGrid)package.GetDialogPage(typeof(OptionPageGrid));
            string url = options.ApiUrl;

            // ... (код получения selectedText и fullCode) ...

            if (!(dte?.ActiveDocument?.Selection is TextSelection selection) || string.IsNullOrEmpty(selection.Text))
            {
                dte.StatusBar.Text = "Выделите код для запроса!";
                return;
            }

            string selectedText = selection.Text;
            dte.StatusBar.Text = "LM Studio: Генерирую...";

            try
            {
                // Отправляем запрос в фоне
                string result = await Task.Run(async () => await CallLMStudioAsync(selectedText, ""));

                await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
                if (!string.IsNullOrEmpty(result))
                {
                    // 1. Сохраняем в переменную для кнопки вставки
                    lastAiResponse = result;

                    // 2. Сохраняем в настройки (можно посмотреть через Tools -> Options)
                    options.LastResponse = result;
                    options.SaveSettingsToStorage();

                    // 3. Дублируем в Output Window (Ctrl+Alt+O)
                    LogToOutputWindow("--- NEW AI RESPONSE ---\n" + result + "\n--- END ---");

                    dte.StatusBar.Text = "Ответ получен и сохранен в памяти и в Output Window.";
                }
                else
                {
                    dte.StatusBar.Text = result ?? "Ошибка связи";
                }
            }
            catch (Exception ex)
            {
                await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
                dte.StatusBar.Text = "Error: " + ex.Message;
            }
        }
        private void ExecuteInsert(object sender, EventArgs e)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            var dte = Package.GetGlobalService(typeof(SDTE)) as DTE2;
            if (string.IsNullOrEmpty(lastAiResponse))
            {
                dte.StatusBar.Text = "Сначала получите ответ (кнопка 1).";
                return;
            }

            if (dte?.ActiveDocument?.Selection is TextSelection selection)
            {
                dte.UndoContext.Open("AI_Insert");
                try
                {
                    selection.Insert(lastAiResponse);
                    dte.StatusBar.Text = "Код вставлен.";
                }
                catch (Exception ex)
                {
                    LogToOutputWindow("Insert Error: " + ex.Message);
                }
                finally
                {
                    dte.UndoContext.Close();
                }
            }
        }

        private async Task<string> CallLMStudioAsync(string selectedText, string context)
        {
            try
            {
                // 1. Упрощаем промпт для теста, чтобы нейросеть ответила мгновенно
                string prompt = "Fix this C# code: " + selectedText;
                string escapedPrompt = HttpUtility.JavaScriptStringEncode(prompt);

                string json = "{\"model\":\"local-model\",\"messages\":[{\"role\":\"user\",\"content\":\"" + escapedPrompt + "\"}],\"temperature\":0.3}";

                //using (var localClient = new HttpClient())
                using (var content = new StringContent(json, Encoding.UTF8, "application/json"))
                {
                    // 2. Увеличиваем таймаут до 2 минут (120 секунд)
                    //client.Timeout = TimeSpan.FromSeconds(120);

                    // 3. Проверьте адрес! В некоторых версиях LM Studio адрес может быть http://127.0.0.1:1234/v1/...
                    var response = await client.PostAsync("http://localhost:1234/v1/chat/completions", content);

                    if (!response.IsSuccessStatusCode)
                        return "Ошибка сервера: " + response.StatusCode;

                    string respString = await response.Content.ReadAsStringAsync();
                    LogToOutputWindow("RAW JSON FROM SERVER:\n" + respString); // Добавьте это временно для отладки
                    return ParseJsonContent(respString);
                }
            
            }
            catch (TaskCanceledException)
            {
                return "Ошибка: Нейросеть отвечала слишком долго (Таймаут).";
            }
            catch (Exception ex)
            {
                return "Ошибка сети: " + ex.Message;
            }
        }

        private string ParseJsonContent(string json)
        {
            try
            {
                using (JsonDocument doc = JsonDocument.Parse(json))
                {
                    // Идем по пути choices[0].message.content
                    JsonElement root = doc.RootElement;
                    if (root.TryGetProperty("choices", out JsonElement choices) && choices.GetArrayLength() > 0)
                    {
                        JsonElement firstChoice = choices[0];
                        if (firstChoice.TryGetProperty("message", out JsonElement message))
                        {
                            if (message.TryGetProperty("content", out JsonElement content))
                            {
                                return content.GetString();
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                return "Ошибка парсинга JSON: " + ex.Message + "\nСырой ответ: " +
                       (json.Length > 100 ? json.Substring(0, 100) + "..." : json);
            }
            return "Ошибка: Не удалось найти поле content в ответе.";
        }


        private void LogToOutputWindow(string message)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            IVsOutputWindow outWindow = Package.GetGlobalService(typeof(SVsOutputWindow)) as IVsOutputWindow;
            Guid paneGuid = new Guid("B532450C-8D63-42D0-9118-208151D36980"); // Произвольный GUID
            outWindow.CreatePane(ref paneGuid, "Vibe AI", 1, 1);
            outWindow.GetPane(ref paneGuid, out IVsOutputWindowPane pane);
            pane?.OutputStringThreadSafe(message + "\n");
            pane?.Activate(); // Сфокусироваться на окне
        }
    }
}