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
        // Вместо static string contextCode...
        public static Dictionary<string, string> ContextFiles = new Dictionary<string, string>();

        private readonly AsyncPackage package;

        // Статичный клиент: один на всё расширение, чтобы не перегружать сеть
        private static readonly HttpClient client = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(1500), // Увеличили для больших ответов
            MaxResponseContentBufferSize = 10 * 1024 * 1024 // Разрешаем до 10МБ данных
        };

        private LMCommand(AsyncPackage package, OleMenuCommandService commandService)
        {
            this.package = package ?? throw new ArgumentNullException(nameof(package));

            if (commandService != null)
            {
                Guid CommandSet = new Guid(VibeCodingExtensionG1Package.guidCmdSet);

                // Идентификаторы команд должны совпадать с файлом magic.vsct
                commandService.AddCommand(new MenuCommand(ExecuteShowChat, new CommandID(CommandSet, 0x0100)));
                commandService.AddCommand(new MenuCommand(ExecuteAddFile, new CommandID(CommandSet, 0x0101)));

                // Внутри конструктора LMCommand
                foreach (int id in new[] { 0x0100, 0x0101, 0x0102, 0x0103, 0x0104 })
                {
                    // Для кнопок Explain, Fix, Optimize используем ExecuteAsync
                    // Для ShowChat и AddContext оставляем их старые методы
                    if (id >= 0x0102)
                    {
                        commandService.AddCommand(new OleMenuCommand(this.ExecuteAsk, new CommandID(CommandSet, id)));
                    }
                }
            }
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

        private void ExecuteShowChat(object sender, EventArgs e)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            // FindToolWindow ищет существующее окно или создает новое
            ToolWindowPane window = this.package.FindToolWindow(typeof(ChatWindow), 0, true);
            if ((null == window) || (null == window.Frame))
            {
                throw new NotSupportedException("Не удалось создать окно чата");
            }

            // Показываем окно
            IVsWindowFrame windowFrame = (IVsWindowFrame)window.Frame;
            Microsoft.VisualStudio.ErrorHandler.ThrowOnFailure(windowFrame.Show());
        }

        public async Task<string> CallAiFromChatAsync(string prompt)
        {
            // Здесь логика почти такая же, как в CallLMStudioAsync, 
            // только вместо 'selectedCode' мы используем 'prompt' из окна чата
            return await CallLMStudioAsync(prompt);
        }

        // --- ЛОГИКА ПЕРВОЙ КНОПКИ (ASK AI) ---
        private void ExecuteAsk(object sender, EventArgs e)
        {
            // ТОЧКА ВХОДА: UI Поток (пользователь нажал на меню)

            // Запускаем асинхронную цепочку через фабрику задач VS
            ThreadHelper.JoinableTaskFactory.RunAsync(async delegate
            {
                //await ProcessAskInternalAsync();
                await ExecuteAsync(sender, e);
            });
        }

        private void ExecuteAddFile(object sender, EventArgs e)
        {
            // ТОЧКА ВХОДА: UI Поток (пользователь нажал на меню)

            // Запускаем асинхронную цепочку через фабрику задач VS
            ThreadHelper.JoinableTaskFactory.RunAsync(async delegate
            {
                await ProcessAddContextAsync();
            });
        }

        // Обновим метод сохранения контекста
        private async Task ProcessAddContextAsync()
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
            var dte = await package.GetServiceAsync(typeof(SDTE)) as DTE2;

            if (dte?.ActiveDocument != null)
            {
                var textDoc = dte.ActiveDocument.Object("TextDocument") as TextDocument;
                var editPoint = textDoc.StartPoint.CreateEditPoint();
                string content = editPoint.GetText(textDoc.EndPoint);
                string fileName = dte.ActiveDocument.Name;

                if (!ContextFiles.ContainsKey(fileName))
                {
                    ContextFiles.Add(fileName, content);
                    // Сообщаем окну чата (если оно открыто), что список обновился
                    ChatWindowControl.Instance?.RefreshFilesList();
                }

                dte.StatusBar.Text = $"Файл '{fileName}' добавлен в контекст чата.";
            }
        }

        private async Task ExecuteAsync(object sender, EventArgs e)
        {
            var menuCommand = sender as OleMenuCommand;
            if (menuCommand == null) return;

            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

            // Получаем настройки
            var options = (GeneralOptions)package.GetDialogPage(typeof(GeneralOptions));

            // Получаем код
            var dte = await package.GetServiceAsync(typeof(SDTE)) as DTE2;
            var selection = dte?.ActiveDocument?.Selection as TextSelection;
            string selectedCode = selection?.Text;

            if (string.IsNullOrWhiteSpace(selectedCode)) return;

            // Выбираем промпт
            string prompt = "";
            switch (menuCommand.CommandID.ID)
            {
                case 0x0102: prompt = options.ExplainPrompt; break;
                case 0x0103: prompt = options.FixBugsPrompt; break;
                case 0x0104: prompt = options.OptimizePrompt; break;
            }

            // Отправляем в чат
            ToolWindowPane window = this.package.FindToolWindow(typeof(ChatWindow), 0, true);
            if (window?.Frame != null)
            {
                ((IVsWindowFrame)window.Frame).Show();
                var control = window.Content as ChatWindowControl;
                control?.SendExternalQuery($"{prompt}\n\n```csharp\n{selectedCode}\n```");
            }
        }

        private async Task<string> CallLMStudioAsync(string selectedCode)
        {
            // Получаем доступ к странице настроек
            var options = (GeneralOptions)package.GetDialogPage(typeof(GeneralOptions));
            string url = options.ApiUrl; // Используем значение из настроек!

            try
            {
                var messages = new System.Collections.Generic.List<object>();

                // Добавляем системную роль из настроек
                messages.Add(new { role = "system", content = options.SystemPrompt });


                // 2. Справочный контекст (если есть файлы)
                if (ContextFiles.Count > 0)
                {
                    StringBuilder sb = new StringBuilder("Используй эти файлы ТОЛЬКО как справочную информацию:\n\n");
                    foreach (var file in ContextFiles)
                    {
                        sb.AppendLine($"Файл: {file.Key}\n{file.Value}\n---");
                    }
                    messages.Add(new { role = "system", content = sb.ToString() });
                }

                // Если мы ранее "запомнили" файл, добавляем его первым
                //if (ContextFiles.Count > 0)
                //{
                //    StringBuilder fullContext = new StringBuilder("Используй следующий контекст из нескольких файлов:\n\n");
                //    foreach (var file in ContextFiles)
                //    {
                //        fullContext.AppendLine($"--- FILE: {file.Key} ---");
                //        fullContext.AppendLine(file.Value);
                //        fullContext.AppendLine("-------------------\n");
                //    }

                //    messages.Add(new
                //    {
                //        role = "system",
                //        content = fullContext.ToString()
                //    });
                //}

                //Добавляем само выделение и вопрос
                messages.Add(new { role = "user", content = selectedCode });
                //messages.Add(new
                //{
                //    role = "user",
                //    content = $"Ниже приведен фрагмент кода. Проанализируй его, учитывая контекст выше (если есть):\n\n{selectedCode}"
                //});

                var payload = new
                {
                    model = options.ModelName, // Из настроек
                    messages = messages,
                    temperature = options.Temperature, // Из настроек
                    max_tokens = options.MaxTokens // Из настроек
                };

                var serializer = new JavaScriptSerializer();
                string jsonPayload = serializer.Serialize(payload);

                using (var content = new StringContent(jsonPayload, Encoding.UTF8, "application/json"))
                {
                    var response = await client.PostAsync(url, content);
                    // ... далее ваш парсинг без изменений ...

                    if (!response.IsSuccessStatusCode)
                        return "Ошибка сети: " + response.StatusCode;

                    string jsonResponse = await response.Content.ReadAsStringAsync();
                    await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
                    LogToOutputWindow("RAW JSON: " + jsonResponse);

                    // Парсим результат через System.Text.Json (надежно для больших текстов)
                    return ParseJsonContent(jsonResponse);
                }
            }
            catch (Exception ex) { return "Ошибка: " + ex.Message; }
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