// OverlayWindow.xaml.cs
using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Forms;
using DMTool;
using WinForms = System.Windows.Forms;
using WPF = System.Windows;


namespace DMTool
{
    public partial class OverlayWindow : Window
    {
        public OverlayWindow()
        {
            InitializeComponent();

            // ItemsSource direkt setzen
            ImageItemsControl.ItemsSource = App.Images;

            // Overlay-Fenster auf zweiten Bildschirm positionieren
            PositionWindowOnSecondaryScreen();
        }

        private void PositionWindowOnSecondaryScreen()
        {
            if (Screen.AllScreens.Length > 1)
            {
                var secondaryScreen = Screen.AllScreens[1];
                var workingArea = secondaryScreen.WorkingArea;

                Left = workingArea.Left;
                Top = workingArea.Top;
                Width = workingArea.Width;
                Height = workingArea.Height;
            }
            else
            {
                var primaryScreen = Screen.PrimaryScreen;
                var workingArea = primaryScreen.WorkingArea;

                Left = workingArea.Left;
                Top = workingArea.Top;
                Width = workingArea.Width;
                Height = workingArea.Height;
            }
        }
    }

    public class ScaleHalfConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            if (value is double size && parameter is double scale)
            {
                return size * scale / 2;
            }
            return 0;
        }

        public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}