using System.Windows.Controls;

namespace XingYueCore.Wpf.Extensions;
public static class WpfExtensions {

    public static bool Any(this UIElementCollection? arr) 
        => arr?.Count > 0;

}
