using Microsoft.VisualStudio.Shell;
using System.ComponentModel;

namespace VibeCodingExtensionG1
{
    public class OptionPageGrid : DialogPage
    {
        [Category("LM Studio Settings")]
        [DisplayName("API URL")]
        [Description("Endpoint для LM Studio (по умолчанию http://127.0.0.1:1234/v1/chat/completions)")]
        public string ApiUrl { get; set; } = "http://127.0.0.1:1234/v1/chat/completions";

        [Category("Last Response")]
        [DisplayName("Full AI Response")]
        [Description("Здесь сохраняется последний полученный ответ целиком")]
        // Это поле позволит вам зайти в настройки и скопировать текст, если что-то пошло не так
        public string LastResponse { get; set; } = "";

        private void InitializeComponent()
        {

        }
    }
}