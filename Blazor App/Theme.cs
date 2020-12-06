using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace StructuredLogViewerWASM
{
    public sealed class Theme
    {
        public bool DarkMode { get; private set; }

        public void FlipTheme()
        {
            DarkMode = !DarkMode;
            Changed?.Invoke();
        }

        public string CssStyleString => DarkMode
            ? "background-color: #202020; color: #E0E0E0;"
            : "background-color: white; color: black;";

        public string CssClass => DarkMode ? "dark-theme" : "light-theme";

        public string ThemeName => DarkMode ? "Dark Mode" : "Light Mode";

        public string AlternateThemeName => DarkMode ? "Light Mode" : "Dark Mode";

        public string EditorTheme => DarkMode ? "vs-dark" : "vs";

        public event Action Changed;
    }
}
