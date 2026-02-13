using Microsoft.VisualStudio.Shell;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
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
            // НОРМАЛИЗАЦИЯ: Превращаем HTML-переносы в реальные до начала парсинга
            text = text.Replace("<br>", "\n").Replace("<br/>", "\n");

            var paragraph = new Paragraph { Margin = new Thickness(0, 5, 0, 10) };
            paragraph.Inlines.Add(new Bold(new Run(isUser ? "👤 User: " : "🤖 AI: "))
            { Foreground = isUser ? Brushes.SkyBlue : Brushes.LightGreen });
            paragraph.Inlines.Add(new LineBreak());

            // --- ВОТ ОН, ВОЗВРАТ АВТО-ДЕТЕКТА ---
            // Если это пользователь, и он НЕ использовал кавычки, и это похоже на код
            if (isUser && /*!text.Contains("```") &&*/ LooksLikeCode(text))
            {
                AddHighlightCode(paragraph, text.Trim());
                responseBox.Document.Blocks.Add(paragraph);
                responseBox.ScrollToEnd();
                return; // Сразу выходим, не запуская Markdown-парсер
            }
            // ------------------------------------

            // Разделяем на строки для потокового анализа
            string[] lines = text.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);

            bool isInCodeBlock = false;
            List<string> currentCodeLines = new List<string>();
            StringBuilder currentTextBlock = new StringBuilder();

            foreach (var line in lines)
            {
                // Проверяем, является ли строка маркером начала или конца блока кода
                // Маркером считаем только строку, которая НАЧИНАЕТСЯ с ```
                bool isCodeMarker = line.TrimStart().StartsWith("```");

                if (isCodeMarker)
                {
                    if (!isInCodeBlock)
                    {
                        // Входим в блок кода: сбрасываем накопленный текст
                        if (currentTextBlock.Length > 0)
                        {
                            ProcessMarkdownText(paragraph, currentTextBlock.ToString());
                            currentTextBlock.Clear();
                        }
                        isInCodeBlock = true;
                    }
                    else
                    {
                        // Выходим из блока кода: отрисовываем накопленный код
                        paragraph.Inlines.Add(new LineBreak());
                        AddHighlightCode(paragraph, string.Join("\n", currentCodeLines));
                        paragraph.Inlines.Add(new LineBreak());

                        currentCodeLines.Clear();
                        isInCodeBlock = false;
                    }
                    continue;
                }

                if (isInCodeBlock)
                {
                    currentCodeLines.Add(line);
                }
                else
                {
                    currentTextBlock.AppendLine(line);
                }
            }

            // Дорисовываем остатки
            if (isInCodeBlock && currentCodeLines.Count > 0)
            {
                AddHighlightCode(paragraph, string.Join("\n", currentCodeLines));
            }
            else if (currentTextBlock.Length > 0)
            {
                ProcessMarkdownText(paragraph, currentTextBlock.ToString());
            }

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

            // Нормализация: убираем лишние \r для кроссплатформенности
            string normalized = text.Replace("\r", "");
            string[] lines = normalized.Split('\n');

            foreach (var line in lines)
            {
                string trimmed = line.Trim();
                if (string.IsNullOrEmpty(trimmed)) continue;

                // 1. Пропускаем разделители таблиц |---|---|
                if (System.Text.RegularExpressions.Regex.IsMatch(trimmed, @"^\|?[\s-:\\|]+$")) continue;

                // 2. ТАБЛИЦЫ
                if (trimmed.StartsWith("|"))
                {
                    var cells = trimmed.Trim('|').Split('|');
                    paragraph.Inlines.Add(new Run("  "));

                    for (int i = 0; i < cells.Length; i++)
                    {
                        string cellContent = cells[i].Replace("<br/>", "\n").Replace("<br>", "\n").Trim();
                        ParseInlineMarkdown(paragraph, cellContent);

                        if (i < cells.Length - 1)
                            paragraph.Inlines.Add(new Run("  │  ") { Foreground = Brushes.DarkGray, FontWeight = FontWeights.Bold });
                    }
                }
                // 3. ЗАГОЛОВКИ
                else if (trimmed.StartsWith("###"))
                {
                    string content = trimmed.TrimStart('#').Trim();
                    if (string.IsNullOrEmpty(content)) continue;

                    var headerSpan = new Span { FontSize = responseBox.FontSize + 2, FontWeight = FontWeights.Bold };
                    paragraph.Inlines.Add(headerSpan);
                    ParseInlineMarkdown(headerSpan, content, Brushes.SkyBlue);
                }
                // 4. СПИСКИ
                else if (trimmed.StartsWith("- ") || trimmed.StartsWith("* ") || trimmed.StartsWith("• ") ||
                         System.Text.RegularExpressions.Regex.IsMatch(trimmed, @"^\d+\. "))
                {
                    paragraph.Inlines.Add(new Run("    • ") { Foreground = Brushes.Gray });
                    string content = System.Text.RegularExpressions.Regex.Replace(trimmed, @"^([-*•]|\d+\.)\s+", "");
                    ParseInlineMarkdown(paragraph, content);
                }
                // 5. ОБЫЧНЫЙ ТЕКСТ
                else
                {
                    ParseInlineMarkdown(paragraph, line);
                }

                // Добавляем перенос только если это не последняя строка (чтобы не было пустых хвостов)
                paragraph.Inlines.Add(new LineBreak());
            }
        }

        private void FormatInlineCode(Paragraph paragraph, string text)
        {
            var parts = text.Split('`');
            for (int i = 0; i < parts.Length; i++)
            {
                if (i % 2 == 1) // Текст внутри кавычек
                {
                    paragraph.Inlines.Add(new Run(parts[i])
                    {
                        FontFamily = new FontFamily("Consolas"),
                        Foreground = Brushes.SandyBrown,
                        Background = new SolidColorBrush(Color.FromRgb(50, 50, 50))
                    });
                }
                else
                {
                    paragraph.Inlines.Add(new Run(parts[i]));
                }
            }
        }

        // Выносим обработку **жирного** в отдельный метод, чтобы применять его везде
        // Обрати внимание: теперь принимает InlineCollection (чтобы работать и с Paragraph, и со Span)
        private void ParseInlineMarkdown(TextElement parent, string text, Brush defaultColor = null)
        {
            if (string.IsNullOrEmpty(text)) return;
            InlineCollection inlines = (parent is Paragraph p) ? p.Inlines : ((Span)parent).Inlines;
            if (defaultColor == null) defaultColor = Brushes.Gainsboro;

            // Регулярка для захвата блоков кода и жирного текста
            // ВАЖНО: Добавлен RegexOptions.Singleline, чтобы .* захватывал переносы строк внутри ячейки
            var inlineRegex = new System.Text.RegularExpressions.Regex(@"(```(?:\w+)?.*?```)|(`.*?`)|(\*\*.*?\*\*)",
                System.Text.RegularExpressions.RegexOptions.Singleline);

            var parts = inlineRegex.Split(text);

            foreach (var part in parts)
            {
                if (string.IsNullOrEmpty(part)) continue;

                if (part.StartsWith("```") || part.StartsWith("`"))
                {
                    // Чистим контент
                    string content = part;
                    if (content.StartsWith("```"))
                    {
                        // Убираем маркеры ``` и название языка (csharp/cpp и т.д.)
                        content = System.Text.RegularExpressions.Regex.Replace(content, @"^```(\w+)?\s*", "");
                        content = content.TrimEnd('`').TrimEnd();
                    }
                    else
                    {
                        content = content.Trim('`');
                    }

                    inlines.Add(new Run(content)
                    {
                        FontFamily = new FontFamily("Consolas"),
                        Foreground = Brushes.SandyBrown,
                        Background = new SolidColorBrush(Color.FromRgb(50, 50, 50))
                    });
                }
                else if (part.StartsWith("**") && part.EndsWith("**"))
                {
                    var content = part.Substring(2, part.Length - 4);
                    var boldSpan = new Bold();
                    inlines.Add(boldSpan);
                    ParseInlineMarkdown(boldSpan, content, Brushes.White);
                }
                else
                {
                    // Если внутри обычного текста остались одиночные \n от <br>, превращаем их в LineBreak
                    string[] textLines = part.Split('\n');
                    for (int k = 0; k < textLines.Length; k++)
                    {
                        if (!string.IsNullOrEmpty(textLines[k]))
                            inlines.Add(new Run(textLines[k]) { Foreground = defaultColor });

                        if (k < textLines.Length - 1)
                            inlines.Add(new LineBreak());
                    }
                }
            }
        }


        private bool LooksLikeCode(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return false;

            // Если в тексте есть явные признаки блочного Markdown, это НЕ ленивый код
            if (text.Contains("```")) return false;

            int signals = 0;
            string trimmed = text.Trim();

            // Сильные сигналы (характерны почти только для кода)
            if (trimmed.EndsWith(";") && trimmed.Contains(" ")) signals += 2;
            if (text.Contains("public ") || text.Contains("private ") || text.Contains("static ")) signals += 2;
            if (text.Contains("{") && text.Contains("}")) signals += 2;
            if (text.Contains("=>")) signals += 1;
            if (text.Contains("using System")) signals += 3;
            if (System.Text.RegularExpressions.Regex.IsMatch(text, @"\b(var|int|string|bool)\s+\w+\s*=")) signals += 2;
            if (text.Contains("namespace ")) signals += 3;

            // Проверка на многострочность (код редко бывает в одну короткую строку без пробелов)
            bool isMultiline = text.Contains("\n");

            // Если это одна строка, она должна быть ОЧЕНЬ похожа на код (например, вызов метода с ;)
            if (!isMultiline) return signals >= 3;

            // Для многострочного текста порог ниже
            return signals >= 2;
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