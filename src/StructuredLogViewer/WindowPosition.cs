using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace StructuredLogViewer
{
    public static class WindowPosition
    {
        public static string GetWindowPosition(Window window)
        {
            var hwnd = new WindowInteropHelper(window).Handle;
            var windowPlacement = new WindowPlacement();
            GetWindowPlacement(hwnd, windowPlacement);
            return windowPlacement.ToString();
        }

        public static void RestoreWindowPosition(Window window, string position)
        {
            if (!TryRestoreWindowPosition(window, position))
            {
                window.WindowStartupLocation = WindowStartupLocation.CenterScreen;
                window.WindowState = WindowState.Maximized;
            }
        }

        public static bool TryRestoreWindowPosition(Window window, string position)
        {
            if (string.IsNullOrWhiteSpace(position))
            {
                return false;
            }

            var placement = WindowPlacement.Parse(position);
            if (placement == null)
            {
                return false;
            }

            var hWnd = new WindowInteropHelper(window).Handle;
            bool result = SetWindowPlacement(hWnd, placement);

            return result;
        }

        [DllImport("user32.dll")]
        private static extern bool SetWindowPlacement(IntPtr hWnd, WindowPlacement lpwndpl);

        [DllImport("user32.dll")]
        private static extern bool GetWindowPlacement(IntPtr hWnd, WindowPlacement lpwndpl);

        [StructLayout(LayoutKind.Sequential)]
        public class WindowPlacement
        {
            public int length = Marshal.SizeOf(typeof(WindowPlacement));
            public int flags;
            public int showCmd;
            public POINT minPosition;
            public POINT maxPosition;
            public RECT normalPosition;

            public static WindowPlacement Parse(string text)
            {
                if (string.IsNullOrEmpty(text))
                {
                    return null;
                }

                var parts = text.Split(',');
                if (parts.Length != 10)
                {
                    return null;
                }

                var result = new WindowPlacement();
                if (!int.TryParse(parts[0], out int flags) ||
                    !int.TryParse(parts[1], out int showCmd) ||
                    !int.TryParse(parts[2], out int minX) ||
                    !int.TryParse(parts[3], out int minY) ||
                    !int.TryParse(parts[4], out int maxX) ||
                    !int.TryParse(parts[5], out int maxY) ||
                    !int.TryParse(parts[6], out int left) ||
                    !int.TryParse(parts[7], out int top) ||
                    !int.TryParse(parts[8], out int width) ||
                    !int.TryParse(parts[9], out int height))
                {
                    return null;
                }

                result.flags = flags;
                result.showCmd = showCmd;
                result.minPosition = new POINT { x = minX, y = minY };
                result.maxPosition = new POINT { x = maxX, y = maxY };
                result.normalPosition = new RECT(left, top, left + width, top + height);

                return result;
            }

            public override string ToString()
            {
                return $"{flags},{showCmd},{minPosition.x},{minPosition.y},{maxPosition.x},{maxPosition.y},{normalPosition.Left},{normalPosition.Top},{normalPosition.Width},{normalPosition.Height}";
            }
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct POINT
        {
            public int x;
            public int y;

            public static POINT FromPoint(Point pt)
            {
                return new POINT()
                {
                    x = (int)pt.X,
                    y = (int)pt.Y,
                };
            }
        }

        [Serializable, StructLayout(LayoutKind.Sequential)]
        public struct RECT
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;

            public RECT(int left, int top, int right, int bottom)
            {
                Left = left;
                Top = top;
                Right = right;
                Bottom = bottom;
            }

            public RECT(Rect rect)
            {
                Left = (int)rect.Left;
                Top = (int)rect.Top;
                Right = (int)rect.Right;
                Bottom = (int)rect.Bottom;
            }

            public void Offset(int dx, int dy)
            {
                Left += dx;
                Right += dx;
                Top += dy;
                Bottom += dy;
            }

            public Point Position => new Point(Left, Top);

            public Size Size => new Size(Width, Height);

            public int Height
            {
                get => Bottom - Top;
                set => Bottom = Top + value;
            }

            public int Width
            {
                get => Right - Left;
                set => Right = Left + value;
            }

            public Int32Rect ToInt32Rect() => new Int32Rect(Left, Top, Width, Height);

            public Rect ToRect() => new Rect(Left, Top, Width, Height);

            public static RECT FromInt32Rect(Int32Rect rect)
            {
                return new RECT(
                    rect.X,
                    rect.Y,
                    rect.X + rect.Width,
                    rect.Y + rect.Height);
            }
        }
    }
}
