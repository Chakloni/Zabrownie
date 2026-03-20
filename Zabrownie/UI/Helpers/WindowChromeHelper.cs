using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;

namespace Zabrownie.UI.Helpers
{
    public static class WindowChromeHelper
    {
        public static void Attach(Window window)
        {
            window.SourceInitialized += (s, e) =>
            {
                var source = HwndSource.FromHwnd(new WindowInteropHelper(window).Handle);
                source?.AddHook(new HwndSourceHook((IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled) => 
                    WndProc(window, hwnd, msg, wParam, lParam, ref handled)));
            };
        }

        public static void DragResize(Window window)
        {
            SendMessage(
                new WindowInteropHelper(window).Handle,
                0x112,
                (IntPtr)(0xF000 + 8), // 8 = BottomRight
                IntPtr.Zero);
        }

        [DllImport("user32.dll")]
        private static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);

        private static IntPtr WndProc(Window window, IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            const int WM_NCHITTEST = 0x0084;

            if (msg == WM_NCHITTEST)
            {
                Point point = window.PointFromScreen(new Point(
                    (short)(lParam.ToInt32() & 0xFFFF),
                    (short)((lParam.ToInt32() >> 16) & 0xFFFF)));

                ResizeDirection direction = GetResizeDirection(window, point);

                if (direction != ResizeDirection.None)
                {
                    handled = true;
                    return (IntPtr)GetHitTestValue(direction);
                }
                handled = false;
            }

            return IntPtr.Zero;
        }

        private enum ResizeDirection
        {
            None,
            Left,
            Right,
            Top,
            Bottom,
            TopLeft,
            TopRight,
            BottomLeft,
            BottomRight
        }

        private static ResizeDirection GetResizeDirection(Window window, Point point)
        {
            const double edgeThickness = 10;

            bool isLeft = point.X <= edgeThickness;
            bool isRight = point.X >= window.ActualWidth - edgeThickness;
            bool isTop = point.Y <= edgeThickness;
            bool isBottom = point.Y >= window.ActualHeight - edgeThickness;

            if (isTop && isLeft) return ResizeDirection.TopLeft;
            if (isTop && isRight) return ResizeDirection.TopRight;
            if (isBottom && isLeft) return ResizeDirection.BottomLeft;
            if (isBottom && isRight) return ResizeDirection.BottomRight;
            if (isLeft) return ResizeDirection.Left;
            if (isRight) return ResizeDirection.Right;
            if (isTop) return ResizeDirection.Top;
            if (isBottom) return ResizeDirection.Bottom;

            return ResizeDirection.None;
        }

        private static int GetHitTestValue(ResizeDirection direction)
        {
            return direction switch
            {
                ResizeDirection.Left => 10,      // HTLEFT
                ResizeDirection.Right => 11,     // HTRIGHT
                ResizeDirection.Top => 12,       // HTTOP
                ResizeDirection.Bottom => 15,    // HTBOTTOM
                ResizeDirection.TopLeft => 13,   // HTTOPLEFT
                ResizeDirection.TopRight => 14,  // HTTOPRIGHT
                ResizeDirection.BottomLeft => 16,// HTBOTTOMLEFT
                ResizeDirection.BottomRight => 17,// HTBOTTOMRIGHT
                _ => 1 // HTCLIENT
            };
        }
    }
}
