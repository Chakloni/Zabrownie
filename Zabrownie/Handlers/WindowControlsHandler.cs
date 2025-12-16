using Zabrownie.Services;
using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;

namespace Zabrownie.Handlers
{
    public class WindowControlsHandler
    {
        private readonly Window _window;

        public WindowControlsHandler(Window window)
        {
            _window = window;
        }

        public void Minimize()
        {
            _window.WindowState = WindowState.Minimized;
        }

        public void Maximize()
        {
            if (_window.WindowState == WindowState.Maximized)
            {
                _window.WindowState = WindowState.Normal;
            }
            else
            {
                var workArea = SystemParameters.WorkArea;
                _window.WindowState = WindowState.Maximized;
                _window.MaxHeight = workArea.Height + 8;
                _window.MaxWidth = workArea.Width;
            }
        }

        public void OnTitleBarMouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ClickCount == 2)
            {
                Maximize();
            }
            else
            {
                try
                {
                    _window.DragMove();
                }
                catch (Exception ex)
                {
                    LoggingService.LogError("Error during window drag", ex);
                }
            }
        }

        public void OnMoveMouseDown(object sender, MouseButtonEventArgs e)
        {
            try
            {
                _window.DragMove();
            }
            catch (Exception ex)
            {
                LoggingService.LogError("Error during window drag", ex);
            }
        }

        public void OnResizeGripMouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed)
            {
                DragResize(WindowResizeEdge.BottomRight);
            }
        }

        public void OnSourceInitialized(EventArgs e)
        {
            HwndSource source = HwndSource.FromHwnd(new WindowInteropHelper(_window).Handle);
            source.AddHook(WndProc);
        }

        public void OnStateChanged(EventArgs e)
        {
            if (_window.WindowState == WindowState.Maximized)
            {
                var workArea = SystemParameters.WorkArea;
                _window.Top = workArea.Top;
                _window.Left = workArea.Left;
                _window.Width = workArea.Width;
                _window.Height = workArea.Height;
            }
        }

        private void DragResize(WindowResizeEdge edge)
        {
            SendMessage(
                new WindowInteropHelper(_window).Handle,
                0x112,
                (IntPtr)(0xF000 + edge),
                IntPtr.Zero);
        }

        private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            const int WM_NCHITTEST = 0x0084;

            if (msg == WM_NCHITTEST)
            {
                Point point = _window.PointFromScreen(new Point(
                    (short)(lParam.ToInt32() & 0xFFFF),
                    (short)((lParam.ToInt32() >> 16) & 0xFFFF)));

                ResizeDirection direction = GetResizeDirection(point);

                if (direction != ResizeDirection.None)
                {
                    handled = true;
                    return (IntPtr)GetHitTestValue(direction);
                }
            }

            return IntPtr.Zero;
        }

        private ResizeDirection GetResizeDirection(Point point)
        {
            const double edgeThickness = 10;

            bool isLeft = point.X <= edgeThickness;
            bool isRight = point.X >= _window.ActualWidth - edgeThickness;
            bool isTop = point.Y <= edgeThickness;
            bool isBottom = point.Y >= _window.ActualHeight - edgeThickness;

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
                ResizeDirection.Left => 10,
                ResizeDirection.Right => 11,
                ResizeDirection.Top => 12,
                ResizeDirection.Bottom => 15,
                ResizeDirection.TopLeft => 13,
                ResizeDirection.TopRight => 14,
                ResizeDirection.BottomLeft => 16,
                ResizeDirection.BottomRight => 17,
                _ => 1
            };
        }

        [DllImport("user32.dll")]
        private static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);

        private enum WindowResizeEdge
        {
            BottomRight = 8
        }

        private enum ResizeDirection
        {
            None, Left, Right, Top, Bottom, TopLeft, TopRight, BottomLeft, BottomRight
        }
    }
}