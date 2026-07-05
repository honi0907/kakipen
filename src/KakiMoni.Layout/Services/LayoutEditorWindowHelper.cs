using KakiMoni_Layout.Layout;

namespace KakiMoni_Layout.Services;

public static class LayoutEditorWindowHelper
{
    private static LayoutEditorWindow? _window;

    public static void OpenOrActivate(string targetSlot)
    {
        if (_window is not null)
        {
            try
            {
                _window.Activate();
                return;
            }
            catch
            {
                _window = null;
            }
        }

        _window = new LayoutEditorWindow(targetSlot);
        _window.Closed += (_, _) => _window = null;
        _window.Activate();
    }
}
