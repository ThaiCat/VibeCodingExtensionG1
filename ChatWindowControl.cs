using Microsoft.VisualStudio.Shell;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace VibeCodingExtensionG1
{
    public class ChatWindowControl : UserControl
    {
        private StackPanel filesPanel;
        public static ChatWindowControl Instance { get; private set; }

        private TextBox responseBox;
        private TextBox inputBox;

        public ChatWindowControl()
        {
            Instance = this;
            // Создаем интерфейс программно
            var grid = new Grid { Margin = new Thickness(10) };

            // Добавляем еще одну строку сверху для списка файлов
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // 0: Список файлов
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) }); // 1: Ответ
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // 2: Сплиттер
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(100) }); // 3: Ввод
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // 4: Кнопки


            //grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            //grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            //grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(100) });
            //grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            // Панель для файлов (будет выглядеть как набор "тегов")
            filesPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Margin = new Thickness(0, 0, 0, 5)
            };
            Grid.SetRow(filesPanel, 0);
            grid.Children.Add(filesPanel);


            responseBox = new TextBox
            {
                IsReadOnly = true,
                TextWrapping = TextWrapping.Wrap,
                AcceptsReturn = true,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto
            };
            Grid.SetRow(responseBox, 0);

            inputBox = new TextBox
            {
                TextWrapping = TextWrapping.Wrap,
                AcceptsReturn = true,
                Margin = new Thickness(0, 5, 0, 0)
            };
            Grid.SetRow(inputBox, 2);

            var btnSend = new Button
            {
                Content = "Send to AI",
                Padding = new Thickness(10, 5, 0,0),
                Margin = new Thickness(0, 5, 0, 0),
                HorizontalAlignment = HorizontalAlignment.Right
            };
            btnSend.Click += (s, e) => SendToAi();
            Grid.SetRow(btnSend, 3);

            grid.Children.Add(responseBox);
            grid.Children.Add(inputBox);
            grid.Children.Add(btnSend);

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
        }

        public void RefreshFilesList()
        {
            filesPanel.Children.Clear();
            foreach (var fileName in LMCommand.ContextFiles.Keys)
            {
                var border = new Border
                {
                    Background = new SolidColorBrush(Color.FromRgb(60, 60, 60)),
                    CornerRadius = new CornerRadius(3),
                    Margin = new Thickness(0, 0, 5, 0),
                    Padding = new Thickness(5, 2, 5, 2)
                };

                var txt = new TextBlock
                {
                    Text = fileName,
                    FontSize = 10,
                    Foreground = Brushes.LightGray
                };
                border.Child = txt;

                // Кнопка удаления файла из контекста при клике
                border.MouseDown += (s, e) => {
                    LMCommand.ContextFiles.Remove(fileName);
                    RefreshFilesList();
                };

                filesPanel.Children.Add(border);
            }
        }

        private void SendToAi()
        {
            string text = inputBox.Text;
            if (string.IsNullOrWhiteSpace(text)) return;

            responseBox.AppendText($"\nUser: {text}\n");
            inputBox.Clear();

            // Вызов вашей логики
            Microsoft.VisualStudio.Shell.ThreadHelper.JoinableTaskFactory.RunAsync(async delegate
            {
                string reply = await LMCommand.Instance.CallAiFromChatAsync(text);
                await Microsoft.VisualStudio.Shell.ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
                responseBox.AppendText($"\nAI: {reply}\n");
                responseBox.ScrollToEnd();
            });
        }
    }
}