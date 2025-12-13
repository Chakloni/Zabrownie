using System;
using System.Windows;
using System.Windows.Media;

namespace Zabrownie.Core
{
    public class ThemeManager
    {
        public static readonly string[] PresetColors = new[]
        {
            "#8B5CF6", // Morado
            "#FF006B", // Rosa/Magenta
            "#00D9FF", // Azul Cyan
            "#00FF88"  // Verde Neón
        };

        public static void ApplyAccentColor(string hexColor)
        {
            try
            {
                var color = (Color)ColorConverter.ConvertFromString(hexColor);
                var accentBrush = new SolidColorBrush(color);
                
                // Crear color hover (más claro)
                var hoverColor = Color.FromArgb(255,
                    (byte)Math.Min(color.R + 20, 255),
                    (byte)Math.Min(color.G + 20, 255),
                    (byte)Math.Min(color.B + 20, 255));
                var hoverBrush = new SolidColorBrush(hoverColor);

                // Aplicar a los recursos de la aplicación
                Application.Current.Resources["AccentColor"] = accentBrush;
                Application.Current.Resources["AccentHover"] = hoverBrush;
            }
            catch (Exception ex)
            {
                Services.LoggingService.LogError("Failed to apply accent color", ex);
            }
        }

        public static bool IsValidHexColor(string hex)
        {
            if (string.IsNullOrWhiteSpace(hex))
                return false;

            if (!hex.StartsWith("#"))
                hex = "#" + hex;

            try
            {
                ColorConverter.ConvertFromString(hex);
                return true;
            }
            catch
            {
                return false;
            }
        }
    }
}