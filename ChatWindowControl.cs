using Microsoft.VisualStudio.Shell;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;

namespace VibeCodingExtensionG1
{
    public class ChatWindowControl : UserControl
    {
        private StackPanel filesPanel;
        public static ChatWindowControl Instance { get; private set; }
        private string lastGeneratedCode = string.Empty;

        //private TextBox responseBox;
        private RichTextBox responseBox;
        private TextBox inputBox;

        private System.Windows.Threading.DispatcherTimer longPressTimer;
        private int pressDuration = 0;
        private const int LongPressThreshold = 10; // 1 секунда (10 тиков по 100мс)
        private Button btnClear;

        private TextBox zoomInput;
        private double currentZoom = 1.0; // 1.0 = 100% (12px)

        public ChatWindowControl()
        {

            Instance = this;
            var grid = new Grid { Margin = new Thickness(10) };

            // 0: Список файлов (Авто-высота)
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            // 1: Поле ответа (Занимает всё свободное место)
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            // 2: Сплиттер (Тонкая полоска)
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(5) });
            // 3: Поле ввода (Фиксированная высота)
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(100) });
            // 4: Кнопки (Авто-высота)
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            // --- 0: ПАНЕЛЬ ФАЙЛОВ ---
            filesPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                // Обернем в ScrollViewer на случай, если файлов будет много
                Margin = new Thickness(0, 0, 0, 10),
                MinHeight = 25
            };
            var fileScroll = new ScrollViewer
            {
                HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
                Content = filesPanel,
                VerticalScrollBarVisibility = ScrollBarVisibility.Disabled
            };
            Grid.SetRow(fileScroll, 0);
            grid.Children.Add(fileScroll);

            // --- 1: ПОЛЕ ОТВЕТА ---
            // Вместо TextBox используем RichTextBox
            responseBox = new RichTextBox
            {
                IsReadOnly = true,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                Background = new SolidColorBrush(Color.FromRgb(30, 30, 30)), // Темный фон как в VS
                Foreground = Brushes.LightGray,
                FontFamily = new FontFamily("Consolas"), // Моноширинный шрифт для кода
                Document = new FlowDocument()
            };
            //responseBox = new TextBox
            //{
            //    IsReadOnly = true,
            //    TextWrapping = TextWrapping.Wrap,
            //    AcceptsReturn = true,
            //    VerticalScrollBarVisibility = ScrollBarVisibility.Auto
            //};
            Grid.SetRow(responseBox, 1); // ТУТ ВАЖНО: индекс 1
            grid.Children.Add(responseBox);

            // --- 2: СПЛИТТЕР ---
            var splitter = new GridSplitter
            {
                HorizontalAlignment = HorizontalAlignment.Stretch,
                Background = Brushes.Transparent,
                Height = 5
            };
            Grid.SetRow(splitter, 2);
            grid.Children.Add(splitter);

            // --- 3: ПОЛЕ ВВОДА ---
            inputBox = new TextBox
            {
                TextWrapping = TextWrapping.Wrap,
                AcceptsReturn = true,
                Margin = new Thickness(0, 5, 0, 0)
            };
            Grid.SetRow(inputBox, 3); // ТУТ ВАЖНО: индекс 3
            grid.Children.Add(inputBox);

            // В конструкторе для inputBox:
            inputBox.PreviewKeyDown += (s, e) =>
            {
                if (e.Key == System.Windows.Input.Key.Enter &&
                    System.Windows.Input.Keyboard.Modifiers == System.Windows.Input.ModifierKeys.Control)
                {
                    SendToAi();
                    e.Handled = true; // Чтобы не добавлялся лишний перенос строки
                }
            };
            // --- 4: КНОПКИ ---

            var btnDock = new DockPanel
            {
                Margin = new Thickness(0, 5, 0, 0),
                LastChildFill = false // Чтобы кнопки не растягивались на всё пространство
            };


            btnClear = new Button
            {
                Content = "🗑️ Clear Context",
                Padding = new Thickness(5, 3, 5, 3),
                //Margin = new Thickness(0, 5, 5, 0),
                MinWidth = 80,
                //Opacity = 0.7 // Немного приглушим, чтобы не отвлекала
                HorizontalContentAlignment = HorizontalAlignment.Center,
                VerticalContentAlignment = VerticalAlignment.Center,
            };
            // События нажатия
            btnClear.PreviewMouseDown += (s, e) => StartLongPress();
            btnClear.PreviewMouseUp += (s, e) => StopLongPress();
            btnClear.MouseLeave += (s, e) => StopLongPress();

            // Таймер для отсчета
            longPressTimer = new System.Windows.Threading.DispatcherTimer();
            longPressTimer.Interval = TimeSpan.FromMilliseconds(100);
            longPressTimer.Tick += LongPressTimer_Tick;

            DockPanel.SetDock(btnClear, Dock.Left); // Прижимаем к левому краю

            var btnSend = new Button 
            { 
                Content = "🚀 Send to AI",
                Padding = new Thickness(5, 3, 5, 3), 
                HorizontalContentAlignment = HorizontalAlignment.Center,
                VerticalContentAlignment = VerticalAlignment.Center,
                MinWidth = 100
            };
            btnSend.Click += (s, e) => SendToAi();

            DockPanel.SetDock(btnSend, Dock.Right); // Прижимаем к правому краю



            var btnCopy = new Button
            {
                Content = "📋 Copy Code",
                Padding = new Thickness(5, 5,5,5),
                Margin = new Thickness(5, 0, 5, 0),
                MinWidth = 90,
                HorizontalContentAlignment = HorizontalAlignment.Center
            };
            btnCopy.Click += (s, e) =>
            {
                if (!string.IsNullOrEmpty(lastGeneratedCode))
                {
                    System.Windows.Clipboard.SetText(lastGeneratedCode);

                    // Визуальный фидбек
                    var originalContent = btnCopy.Content;
                    btnCopy.Content = "✔️ Done!";
                    btnCopy.Background = Brushes.DarkGreen;

                    var timer = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
                    timer.Tick += (sender, args) => {
                        btnCopy.Content = originalContent;
                        btnCopy.ClearValue(Button.BackgroundProperty);
                        timer.Stop();
                    };
                    timer.Start();
                }
            };


            // В конструкторе, там где создаем кнопки:
            zoomInput = new TextBox
            {
                Text = "100%",
                Width = 50,
                Margin = new Thickness(10, 0, 10, 0),
                VerticalContentAlignment = VerticalAlignment.Center,
                HorizontalContentAlignment = HorizontalAlignment.Center
            };

            // Привязываем событие изменения текста
            zoomInput.TextChanged += (s, e) => {
                string val = zoomInput.Text.Replace("%", "");
                if (double.TryParse(val, out double percent))
                {
                    ApplyZoom(percent / 100.0);
                }
            };

            // Добавляем в Dock (между кнопками)
            DockPanel.SetDock(zoomInput, Dock.Left);


            // Добавляем в панель (после зума)
            DockPanel.SetDock(btnCopy, Dock.Right);



            // Добавляем в панель
            btnDock.Children.Add(btnClear);
            btnDock.Children.Add(zoomInput);
            btnDock.Children.Add(btnSend);
            btnDock.Children.Add(btnCopy);

            Grid.SetRow(btnDock, 4);
            grid.Children.Add(btnDock);

            this.Content = grid;
            RefreshFilesList();


            // ... твой код создания Grid и TextBox ...

            // Привязываем цвета к теме Visual Studio
            this.SetResourceReference(Control.BackgroundProperty, VsBrushes.WindowKey);
            this.SetResourceReference(Control.ForegroundProperty, VsBrushes.WindowTextKey);

            responseBox.SetResourceReference(Control.BackgroundProperty, VsBrushes.ToolWindowBackgroundKey);
            responseBox.SetResourceReference(Control.ForegroundProperty, VsBrushes.WindowTextKey);
            responseBox.SetResourceReference(Control.FontFamilyProperty, VsFonts.CaptionFontFamilyKey);

            inputBox.SetResourceReference(Control.BackgroundProperty, VsBrushes.WindowKey);
            inputBox.SetResourceReference(Control.ForegroundProperty, VsBrushes.WindowTextKey);

            //btnSend.SetResourceReference(Control.BackgroundProperty, VsBrushes.CaptionBackgroundKey);
            //btnSend.SetResourceReference(Control.ForegroundProperty, VsBrushes.ButtonTextKey);
            //btnSend.SetResourceReference(Control.BorderBrushProperty, VsBrushes.DockTargetButtonBorderKey);

            //btnSend.SetResourceReference(Control.BackgroundProperty, VsBrushes.ButtonBackgroundKey);
            //btnSend.SetResourceReference(Control.ForegroundProperty, VsBrushes.ButtonTextKey);
            //btnSend.SetResourceReference(Control.BorderBrushProperty, VsBrushes.ButtonBorderKey);

            // В конструкторе добавь:
            this.PreviewMouseWheel += ChatWindowControl_PreviewMouseWheel;
        }

        public void RefreshFilesList()
        {
            if (filesPanel == null) return;
            filesPanel.Children.Clear();

            foreach (var fileName in LMCommand.ContextFiles.Keys)
            {
                var border = new Border
                {
                    Background = new SolidColorBrush(Color.FromRgb(45, 45, 48)), // Цвет VS Header
                    BorderBrush = new SolidColorBrush(Color.FromRgb(0, 122, 204)), // Синий акцент VS
                    BorderThickness = new Thickness(1),
                    CornerRadius = new CornerRadius(2),
                    Margin = new Thickness(0, 0, 5, 0),
                    Padding = new Thickness(8, 4, 8, 4),
                    Cursor = System.Windows.Input.Cursors.Hand,
                    ToolTip = "Нажмите, чтобы удалить из контекста"
                };

                var txt = new TextBlock
                {
                    Text = fileName,
                    FontSize = 11,
                    Foreground = Brushes.White
                };
                border.Child = txt;

                border.MouseDown += (s, e) => {
                    LMCommand.ContextFiles.Remove(fileName);
                    RefreshFilesList();
                };

                filesPanel.Children.Add(border);
            }
        }

        private void ApplyZoom(double zoom)
        {
            if (zoom < 0.2) zoom = 0.2; // Минимум 20%
            if (zoom > 5.0) zoom = 5.0; // Максимум 500%

            currentZoom = zoom;
            double newSize = 12 * zoom;

            responseBox.FontSize = newSize;
            inputBox.FontSize = newSize;

            // Применяем ко всему содержимому истории чата
            var range = new TextRange(responseBox.Document.ContentStart, responseBox.Document.ContentEnd);
            range.ApplyPropertyValue(TextElement.FontSizeProperty, newSize);

            // Обновляем текст в поле, если он не в фокусе (чтобы не мешать вводу)
            if (!zoomInput.IsFocused)
            {
                zoomInput.Text = $"{(int)(zoom * 100)}%";
            }
        }

        // Сам метод:
        private void ChatWindowControl_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            if (Keyboard.Modifiers == ModifierKeys.Control)
            {
                e.Handled = true; // Отменяем стандартный скролл

                if (e.Delta > 0)
                    ApplyZoom(currentZoom + 0.1); // Крупнее
                else
                    ApplyZoom(currentZoom - 0.1); // Мельче

                zoomInput.Text = $"{(int)(currentZoom * 100)}%";
            }
        }

        public void SendExternalQuery(string codeContext)
        {
            // Очищаем поле ввода и вставляем туда запрос (или добавляем к существующему)
            inputBox.Text = $"{codeContext}";

            // Вызываем уже существующую логику отправки
            SendToAi();
        }

        private void SendToAi()
        {
            string text = inputBox.Text.Trim();
            if (string.IsNullOrWhiteSpace(text)) return;

            // Текстовая команда очистки
            if (text.ToLower() == "/clear")
            {
                ClearAllContext();
                inputBox.Clear();
                return;
            }

            if (string.IsNullOrWhiteSpace(text)) return;

            AppendFormattedText($"{text}\n", true);
            inputBox.Clear();

            // Вызов вашей логики
            Microsoft.VisualStudio.Shell.ThreadHelper.JoinableTaskFactory.RunAsync(async delegate
            {
                string reply = await LMCommand.Instance.CallAiFromChatAsync(text);
                await Microsoft.VisualStudio.Shell.ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
                AppendFormattedText($"{reply}\n", false);
                responseBox.ScrollToEnd();
            });
        }

        private void AppendFormattedText(string text, bool isUser)
        {
            var paragraph = new Paragraph { Margin = new Thickness(0, 5, 0, 10) };

            if (isUser && !text.Contains("```"))
            {
                // Считаем признаки кода: точки с запятой, фигурные скобки, лямбды, типичные операторы
                int codeSignals = 0;
                if (text.Contains("{") && text.Contains("}")) codeSignals += 2;
                if (text.Contains(";")) codeSignals += 1;
                if (text.Contains("=>")) codeSignals += 1;
                if (text.Contains("public ") || text.Contains("private ")) codeSignals += 1;
                if (text.Contains("var ") && text.Contains("=")) codeSignals += 1;

                // Если признаков много или текст длинный и структурный — это код
                if (codeSignals >= 2 || (text.Length > 50 && text.Contains("\n") && codeSignals >= 1))
                {
                    AddHighlightCode(paragraph, text.Trim());
                    responseBox.Document.Blocks.Add(paragraph);
                    responseBox.ScrollToEnd();
                    return;
                }
            }

            // Заголовок (тот же)
            paragraph.Inlines.Add(new Bold(new Run(isUser ? "👤 User: " : "🤖 AI: "))
            { Foreground = isUser ? Brushes.SkyBlue : Brushes.LightGreen });
            paragraph.Inlines.Add(new LineBreak());

            // 1. Сначала ищем блоки кода (они в приоритете)
            var codeRegex = new System.Text.RegularExpressions.Regex(@"```(?:\w+)?\r?\n?(.*?)\r?\n?```",
                System.Text.RegularExpressions.RegexOptions.Singleline);

            int lastIndex = 0;
            var matches = codeRegex.Matches(text);

            foreach (System.Text.RegularExpressions.Match match in matches)
            {
                // Обрабатываем обычный текст ДО кода (в нем может быть жирный шрифт)
                ProcessMarkdownText(paragraph, text.Substring(lastIndex, match.Index - lastIndex));

                // Обрабатываем КОД
                string codeContent = match.Groups[1].Value.Trim('\r', '\n');
                paragraph.Inlines.Add(new LineBreak());
                AddHighlightCode(paragraph, codeContent);
                paragraph.Inlines.Add(new LineBreak());

                lastIndex = match.Index + match.Length;
            }

            // Обрабатываем остаток текста
            ProcessMarkdownText(paragraph, text.Substring(lastIndex));

            responseBox.Document.Blocks.Add(paragraph);
            responseBox.ScrollToEnd();
        }

        private void AddHighlightCode(Paragraph paragraph, string code)
        {
            lastGeneratedCode = code;

            var keywords = new HashSet<string> {
                // Доступ и модификаторы
                "public", "private", "protected", "internal", "static", "readonly", "sealed",
                "abstract", "virtual", "override", "partial", "volatile", "const", "extern",
                
                // Типы и структуры
                "class", "struct", "interface", "enum", "delegate", "record", "event", "namespace",
                
                // Базовые типы
                "void", "string", "int", "bool", "double", "float", "decimal", "long", "short",
                "char", "byte", "object", "dynamic", "var", "null", "true", "false",
                
                // Управление потоком
                "if", "else", "switch", "case", "default", "while", "do", "for", "foreach",
                "break", "continue", "return", "yield", "goto",
                
                // Исключения
                "try", "catch", "finally", "throw", "when",
                
                // Асинхронность и LINQ
                "async", "await", "task", "from", "where", "select", "group", "into",
                "orderby", "join", "let", "in", "on", "ascending", "descending",
                
                // Операторы и прочее
                "new", "using", "is", "as", "out", "ref", "params", "this", "base",
                "lock", "typeof", "sizeof", "checked", "unchecked", "get", "set", "init", "value"
            };

            // Регулярка для деления на: комментарии (//...), строки ("..."), слова (\w+) или прочее (\W)
            //var tokens = System.Text.RegularExpressions.Regex.Matches(code, @"(//.*?$)|("".*?"")|(\w+)|(\W)",
            //    System.Text.RegularExpressions.RegexOptions.Multiline);

            // ОБНОВЛЕННАЯ РЕГУЛЯРКА: 
            // Добавляем (@"".*?"") для поддержки verbatim-строк C# и (```) как отдельный токен
            var tokens = System.Text.RegularExpressions.Regex.Matches(code,
                @"(@"".*?"")|(""[^""]*?"")|(//.*?$)|(```)|(\w+)|(\W)",
                System.Text.RegularExpressions.RegexOptions.Multiline);

            foreach (System.Text.RegularExpressions.Match match in tokens)
            {
                string token = match.Value;
                var run = new Run(token) { FontFamily = new FontFamily("Consolas") };
                run.Background = new SolidColorBrush(Color.FromRgb(40, 40, 40));

                if (token.StartsWith("//"))
                {
                    run.Foreground = new SolidColorBrush(Color.FromRgb(87, 166, 74));
                }
                // Обработка обычных и @-строк (теперь они не будут рваться на ```)
                else if (token.StartsWith("\"") || token.StartsWith("@\""))
                {
                    run.Foreground = new SolidColorBrush(Color.FromRgb(214, 157, 133));
                }
                else if (token == "```") // Если кавычки встретились ВНЕ строки
                {
                    run.Foreground = Brushes.Tomato; // Выделим их как спецсимвол
                }
                else if (keywords.Contains(token))
                {
                    run.Foreground = new SolidColorBrush(Color.FromRgb(86, 156, 214));
                }

                // Подсвечиваем фон блока кода легким серым цветом, чтобы отделить от текста
                run.Background = new SolidColorBrush(Color.FromRgb(40, 40, 40));

                paragraph.Inlines.Add(run);
            }
        }
        private void ProcessMarkdownText(Paragraph paragraph, string text)
        {
            if (string.IsNullOrEmpty(text)) return;

            // Разбиваем на строки, чтобы обработать списки
            string[] lines = text.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);

            foreach (var line in lines)
            {
                if (string.IsNullOrWhiteSpace(line)) continue;

                string trimmedLine = line.TrimStart();

                // Проверка на список (начинается с -, * или 1.)
                bool isBullet = trimmedLine.StartsWith("- ") || trimmedLine.StartsWith("* ");
                bool isNumeric = System.Text.RegularExpressions.Regex.IsMatch(trimmedLine, @"^\d+\. ");

                if (isBullet || isNumeric)
                {
                    paragraph.Inlines.Add(new LineBreak());
                    // Добавляем отступ для списка
                    paragraph.Inlines.Add(new Run("  • ") { Foreground = Brushes.Gray });

                    // Убираем маркер из текста строки
                    string content = isBullet ? trimmedLine.Substring(2) : trimmedLine.Substring(trimmedLine.IndexOf('.') + 2);
                    ParseInlineMarkdown(paragraph, content);
                }
                else
                {
                    ParseInlineMarkdown(paragraph, line);
                }

                paragraph.Inlines.Add(new LineBreak());
            }
        }

        // Выносим обработку **жирного** в отдельный метод, чтобы применять его везде
        private void ParseInlineMarkdown(Paragraph paragraph, string text)
        {
            var boldRegex = new System.Text.RegularExpressions.Regex(@"(\*\*.*?\*\*)", System.Text.RegularExpressions.RegexOptions.None);
            string[] parts = boldRegex.Split(text);

            foreach (var part in parts)
            {
                if (part.StartsWith("**") && part.EndsWith("**"))
                {
                    var content = part.Substring(2, part.Length - 4);
                    paragraph.Inlines.Add(new Bold(new Run(content)) { Foreground = Brushes.White });
                }
                else
                {
                    paragraph.Inlines.Add(new Run(part));
                }
            }
        }

        private void ClearAllContext()
        {
            LMCommand.ContextFiles.Clear();
            RefreshFilesList();
            responseBox.AppendText("\n[Система]: Весь контекст очищен.\n");
        }

        private void StartLongPress()
        {
            pressDuration = 0;
            longPressTimer.Start();
            btnClear.Content = "⏳ Hold... [0%]";
        }

        private void StopLongPress()
        {
            longPressTimer.Stop();
            pressDuration = 0;
            btnClear.Content = "🗑️ Clear Context";
            //btnClear.Opacity = 0.7;
        }

        private void LongPressTimer_Tick(object sender, EventArgs e)
        {
            pressDuration++;
            int progress = pressDuration * 10; // Процент

            if (progress <= 100)
            {
                btnClear.Content = $"⏳ Hold... [{progress}%]";
                //btnClear.Opacity = 0.7 + (progress / 333.0); // Постепенно становится ярче
            }

            if (pressDuration >= LongPressThreshold)
            {
                StopLongPress();
                ClearAllContext();
                // Визуальный эффект успешной очистки
                btnClear.Content = "✅ CLEARED!";
            }
        }




    }
}