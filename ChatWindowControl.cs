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

            // --- 4: КНОПКИ ---

            var btnDock = new DockPanel
            {
                Margin = new Thickness(0, 5, 0, 0),
                LastChildFill = false // Чтобы кнопки не растягивались на всё пространство
            };


            btnClear = new Button
            {
                Content = "Clear Context",
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
                Content = "Send to AI", 
                Padding = new Thickness(5, 3, 5, 3), 
                HorizontalContentAlignment = HorizontalAlignment.Center,
                VerticalContentAlignment = VerticalAlignment.Center,
                MinWidth = 100
            };
            btnSend.Click += (s, e) => SendToAi();

            DockPanel.SetDock(btnSend, Dock.Right); // Прижимаем к правому краю







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
            btnDock.Children.Add(zoomInput);






            // Добавляем в панель
            btnDock.Children.Add(btnClear);
            btnDock.Children.Add(btnSend);

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
        //private void ApplyZoom(double zoom)
        //{
        //    // ... расчет newSize ...

        //    inputBox.FontSize = newSize;

        //    // Применяем ко всему содержимому истории чата
        //    var range = new TextRange(responseBox.Document.ContentStart, responseBox.Document.ContentEnd);
        //    range.ApplyPropertyValue(TextElement.FontSizeProperty, newSize);

        //    // И устанавливаем дефолт для будущих сообщений
        //    responseBox.FontSize = newSize;
        //}
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


            //string text = inputBox.Text;
            if (string.IsNullOrWhiteSpace(text)) return;

            AppendFormattedText($"{text}\n", true);
            //responseBox.AppendText($"\nUser: {text}\n");
            inputBox.Clear();

            // Вызов вашей логики
            Microsoft.VisualStudio.Shell.ThreadHelper.JoinableTaskFactory.RunAsync(async delegate
            {
                string reply = await LMCommand.Instance.CallAiFromChatAsync(text);
                await Microsoft.VisualStudio.Shell.ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
                //responseBox.AppendText($"\nAI: {reply}\n");
                AppendFormattedText($"{reply}\n", false);
                responseBox.ScrollToEnd();
            });
        }

        private void AppendFormattedText(string text, bool isUser)
        {
            var paragraph = new Paragraph { Margin = new Thickness(0, 5, 0, 5) };

            if (isUser)
            {
                paragraph.Inlines.Add(new Bold(new Run("User: ")) { Foreground = Brushes.SkyBlue });
                paragraph.Inlines.Add(new Run(text));
            }
            else
            {
                paragraph.Inlines.Add(new Bold(new Run("AI: ")) { Foreground = Brushes.LightGreen });
                paragraph.Inlines.Add(new LineBreak());

                string[] parts = text.Split(new[] { "```" }, StringSplitOptions.None);
                for (int i = 0; i < parts.Length; i++)
                {
                    if (i % 2 == 1) // БЛОК КОДА
                    {
                        // Очищаем от названия языка (например, ```csharp)
                        string code = parts[i];
                        if (code.Contains("\n")) code = code.Substring(code.IndexOf('\n')).Trim();

                        // Добавляем пустую строку для визуального отступа
                        paragraph.Inlines.Add(new LineBreak());

                        // Красим код
                        AddHighlightCode(paragraph, code);

                        paragraph.Inlines.Add(new LineBreak());
                    }
                    else // ОБЫЧНЫЙ ТЕКСТ
                    {
                        paragraph.Inlines.Add(new Run(parts[i]));
                    }
                }
            }

            responseBox.Document.Blocks.Add(paragraph);
            responseBox.ScrollToEnd();
        }

        private void AddHighlightCode(Paragraph paragraph, string code)
        {
            var keywords = new HashSet<string> {
                "public", "private", "protected", "static", "class", "struct", "void",
                "string", "int", "bool", "var", "async", "await", "return", "new", "using", "if", "else", "foreach", "for"
            };

            // Регулярка для деления на: комментарии (//...), строки ("..."), слова (\w+) или прочее (\W)
            var tokens = System.Text.RegularExpressions.Regex.Matches(code, @"(//.*?$)|("".*?"")|(\w+)|(\W)",
                System.Text.RegularExpressions.RegexOptions.Multiline);

            foreach (System.Text.RegularExpressions.Match match in tokens)
            {
                string token = match.Value;
                var run = new Run(token) { FontFamily = new FontFamily("Consolas") };

                if (token.StartsWith("//")) // КОММЕНТАРИИ
                {
                    run.Foreground = new SolidColorBrush(Color.FromRgb(87, 166, 74)); // Зеленый VS
                }
                else if (token.StartsWith("\"") && token.EndsWith("\"")) // СТРОКИ
                {
                    run.Foreground = new SolidColorBrush(Color.FromRgb(214, 157, 133)); // Рыжий VS
                }
                else if (keywords.Contains(token)) // КЛЮЧЕВЫЕ СЛОВА
                {
                    run.Foreground = new SolidColorBrush(Color.FromRgb(86, 156, 214)); // Синий VS
                }
                else if (System.Text.RegularExpressions.Regex.IsMatch(token, @"^\d+$")) // ЧИСЛА
                {
                    run.Foreground = new SolidColorBrush(Color.FromRgb(181, 206, 168));
                }
                else
                {
                    run.Foreground = Brushes.Gainsboro;
                }

                // Подсвечиваем фон блока кода легким серым цветом, чтобы отделить от текста
                run.Background = new SolidColorBrush(Color.FromRgb(40, 40, 40));

                paragraph.Inlines.Add(run);
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
            btnClear.Content = "Hold... [0%]";
        }

        private void StopLongPress()
        {
            longPressTimer.Stop();
            pressDuration = 0;
            btnClear.Content = "Clear Context";
            //btnClear.Opacity = 0.7;
        }

        private void LongPressTimer_Tick(object sender, EventArgs e)
        {
            pressDuration++;
            int progress = pressDuration * 10; // Процент

            if (progress <= 100)
            {
                btnClear.Content = $"Hold... [{progress}%]";
                //btnClear.Opacity = 0.7 + (progress / 333.0); // Постепенно становится ярче
            }

            if (pressDuration >= LongPressThreshold)
            {
                StopLongPress();
                ClearAllContext();
                // Визуальный эффект успешной очистки
                btnClear.Content = "CLEARED!";
            }
        }




    }
}