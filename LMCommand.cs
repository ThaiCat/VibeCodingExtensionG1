using EnvDTE;
using EnvDTE80;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel.Design;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using System.Web;
using System.Web.Script.Serialization; // Добавь этот using

namespace VibeCodingExtensionG1
{
    internal sealed class LMCommand
    {
        // Идентификаторы команд должны совпадать с файлом magic.vsct
        public const int AskCommandId = 0x0100;
        public const int InsertCommandId = 0x0101;
        public static readonly Guid CommandSet = new Guid("7A94A48F-9C2B-42E9-8179-ED0C72668AF5");

        private readonly AsyncPackage package;

        // Статичное хранилище: живет всё время, пока открыта Visual Studio
        private static string lastAiResponse = string.Empty;

        // Статичный клиент: один на всё расширение, чтобы не перегружать сеть
        private static readonly HttpClient client = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(1500), // Увеличили для больших ответов
            MaxResponseContentBufferSize = 10 * 1024 * 1024 // Разрешаем до 10МБ данных
        };

        private LMCommand(AsyncPackage package, OleMenuCommandService commandService)
        {
            this.package = package;

            // Регистрируем кнопку 1 (Запрос)
            var askID = new CommandID(CommandSet, AskCommandId);
            var askItem = new MenuCommand(this.ExecuteAsk, askID);
            commandService.AddCommand(askItem);

            // Регистрируем кнопку 2 (Вставка)
            var insertID = new CommandID(CommandSet, InsertCommandId);
            var insertItem = new MenuCommand(this.ExecuteInsert, insertID);
            commandService.AddCommand(insertItem);
        }

        public static LMCommand Instance { get; private set; }

        public static async Task InitializeAsync(AsyncPackage package)
        {
            // Мы уже в UI-потоке (так как переключились в Package), 
            // но на всякий случай подтверждаем это:
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync(package.DisposalToken);

            // Пытаемся получить сервис команд
            OleMenuCommandService commandService = await package.GetServiceAsync(typeof(IMenuCommandService)) as OleMenuCommandService;

            // КРИТИЧЕСКИЙ МОМЕНТ: 
            // Если commandService == null, значит Студия еще не готова. 
            // В норме на UI потоке внутри InitializeAsync пакета он уже должен быть доступен.
            if (commandService != null)
            {
                // Только когда сервис ТОЧНО получен, создаем инстанс и вешаем его в статику
                Instance = new LMCommand(package, commandService);
            }
        }

        // --- ЛОГИКА ПЕРВОЙ КНОПКИ (ASK AI) ---
        private void ExecuteAsk(object sender, EventArgs e)
        {
            // ТОЧКА ВХОДА: UI Поток (пользователь нажал на меню)

            // Запускаем асинхронную цепочку через фабрику задач VS
            ThreadHelper.JoinableTaskFactory.RunAsync(async delegate
            {
                await ProcessAskInternalAsync();
            });
        }

        private async Task ProcessAskInternalAsync()
        {
            // МЫ НАХОДИТЕСЬ В: UI Потоке.
            // Нужно переключиться на него явно на случай, если RunAsync начал выполнение иначе.
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

            var dte = await package.GetServiceAsync(typeof(SDTE)) as DTE2;
            if (dte?.ActiveDocument == null) return;

            // Читаем выделение (ТОЛЬКО в UI потоке!)
            TextSelection selection = dte.ActiveDocument.Selection as TextSelection;
            if (selection == null || string.IsNullOrEmpty(selection.Text))
            {
                dte.StatusBar.Text = "Сначала выделите код!";
                return;
            }

            string codeToProcess = selection.Text;
            dte.StatusBar.Text = "LM Studio: Отправка запроса...";

            try
            {
                // ПЕРЕКЛЮЧЕНИЕ: Уходим в Фоновый Поток (Background Thread)
                // Используем Task.Run, чтобы сетевое ожидание не "фризило" интерфейс Visual Studio.
                string aiResult = await Task.Run(async () =>
                {
                    // ТУТ МЫ В ФОНЕ: Ждем ответа от сервера 30-120 секунд. 
                    // VS в это время отзывчива, пользователь может работать.
                    return await CallLMStudioAsync(codeToProcess);
                });

                // ПЕРЕКЛЮЧЕНИЕ: Возвращаемся в UI Поток
                await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

                if (!string.IsNullOrEmpty(aiResult))
                {
                    lastAiResponse = aiResult; // Сохраняем "в карман"
                                                                  
                    // Сохраняем в настройки, чтобы увидеть "Full AI Response"
                    var options = (OptionPageGrid)package.GetDialogPage(typeof(OptionPageGrid));
                    options.LastResponse = aiResult;
                    options.SaveSettingsToStorage();

                    // Также выводим в окно вывода, чтобы ответ можно было увидеть "как есть"
                    LogToOutputWindow(aiResult); 
                    dte.StatusBar.Text = "Ответ получен! Можно вставлять.";
                }
                else
                {
                    dte.StatusBar.Text = aiResult ?? "Неизвестная ошибка";
                }
            }
            catch (Exception ex)
            {
                await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
                dte.StatusBar.Text = "Критический сбой: " + ex.Message;
            }
        }

        // --- ЛОГИКА ВТОРОЙ КНОПКИ (INSERT) ---
        private void ExecuteInsert(object sender, EventArgs e)
        {
            // МЫ В: UI Потоке. 
            // Вставка текста в редактор — это работа с UI, фоновые потоки тут запрещены.
            ThreadHelper.ThrowIfNotOnUIThread();

            if (string.IsNullOrEmpty(lastAiResponse))
            {
                return;
            }

            var dte = Package.GetGlobalService(typeof(SDTE)) as DTE2;
            if (dte?.ActiveDocument?.Selection is TextSelection selection)
            {
                // Открываем транзакцию отмены (чтобы Ctrl+Z работал корректно)
                dte.UndoContext.Open("AI Code Insertion");
                try
                {
                    selection.Insert(lastAiResponse);
                }
                finally
                {
                    dte.UndoContext.Close();
                }
            }
        }

        private async Task<string> CallLMStudioAsync(string prompt)
        {
            // МЫ В: Фоновом потоке.
            try
            {
                // Экранируем текст для JSON
                string escaped = HttpUtility.JavaScriptStringEncode(prompt);
                string jsonPayload = "{\"model\":\"local-model\",\"messages\":[{\"role\":\"user\",\"content\":\"" + escaped + "\"}],\"temperature\":0.7}";

                using (var content = new StringContent(jsonPayload, Encoding.UTF8, "application/json"))
                {
                    // Выполняем HTTP POST запрос
                    var response = await client.PostAsync("http://127.0.0.1:1234/v1/chat/completions", content);

                    if (!response.IsSuccessStatusCode)
                        return "Ошибка сети: " + response.StatusCode;

                    string jsonResponse = await response.Content.ReadAsStringAsync(); 
                    await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
                    LogToOutputWindow("RAW JSON: " + jsonResponse);

                    // Парсим результат через System.Text.Json (надежно для больших текстов)
                    return ParseJsonContent(jsonResponse);
                }
            }
            catch (Exception ex)
            {
                return "Ошибка: " + ex.Message;
            }
        }

        private string ParseJsonContent(string json)
        {
            try
            {
                var serializer = new JavaScriptSerializer();
                // Ограничиваем длину текста, чтобы сериализатор не захлебнулся
                serializer.MaxJsonLength = int.MaxValue;

                var root = serializer.Deserialize<Dictionary<string, object>>(json);

                if (root != null && root.ContainsKey("choices"))
                {
                    var choices = root["choices"] as IEnumerable;
                    foreach (var choice in choices)
                    {
                        var choiceDict = choice as Dictionary<string, object>;
                        if (choiceDict != null && choiceDict.ContainsKey("message"))
                        {
                            var messageDict = choiceDict["message"] as Dictionary<string, object>;
                            if (messageDict != null && messageDict.ContainsKey("content"))
                            {
                                string content = messageDict["content"]?.ToString();
                                if (!string.IsNullOrEmpty(content))
                                {
                                    return content;
                                }
                            }
                        }
                    }
                }
                return "Ошибка: Структура JSON распознана, но поле 'content' пустое.";
            }
            catch (Exception ex)
            {
                return "Ошибка парсинга: " + ex.Message;
            }
        }

        private void LogToOutputWindow(string message)
        {
            // МЫ В: UI Потоке (требуется для доступа к окнам VS)
            ThreadHelper.ThrowIfNotOnUIThread();
            IVsOutputWindow outWindow = Package.GetGlobalService(typeof(SVsOutputWindow)) as IVsOutputWindow;
            Guid paneGuid = new Guid("A1B2C3D4-E5F6-4A5B-8C9D-E0F1A2B3C4D5");
            outWindow.CreatePane(ref paneGuid, "AI Response Log", 1, 1);
            outWindow.GetPane(ref paneGuid, out IVsOutputWindowPane pane);
            pane?.OutputStringThreadSafe("\n--- NEW RESPONSE ---\n" + message + "\n");
            pane?.Activate();
        }
    }
}