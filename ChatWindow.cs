using Microsoft.VisualStudio.Shell;
using System.Runtime.InteropServices;
using VibeCodingExtensionG1;

[Guid("E1B2C3D4-5678-90AB-CDEF-1234567890AB")]
public class ChatWindow : ToolWindowPane
{
    public ChatWindow() : base(null)
    {
        this.Caption = "AI Chat Context";
        this.Content = new ChatWindowControl();
    }
}