using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace VibeCodingExtensionG1
{
    public class ChatWindowControl : UserControl
    {
        private TextBox responseBox;
        private TextBox inputBox;

        public ChatWindowControl()
        {
            // Создаем интерфейс программно
            var grid = new Grid { Margin = new Thickness(10) };
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(100) });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

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