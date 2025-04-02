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

            // Beobachter für die Images-Collection hinzufügen
            ((System.Collections.Specialized.INotifyCollectionChanged)App.Images).CollectionChanged += (s, e) => UpdateDebugInfoIfEnabled();

            // Beobachter für ShowDebugInfo-Änderungen
            App.AppSettings.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == nameof(Settings.ShowDebugInfo))
                    UpdateDebugInfoIfEnabled();
            };

            // Overlay-Fenster auf zweiten Bildschirm positionieren
            PositionWindowOnSecondaryScreen();

            // Initial Debug-Info aktualisieren
            UpdateDebugInfoIfEnabled();
        }

        private void UpdateDebugInfoIfEnabled()
        {
            if (App.AppSettings.ShowDebugInfo)
            {
                UpdateDebugInfo();
            }
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

        private void UpdateDebugInfo()
        {
            if (App.Images.Count == 0)
            {
                DebugText.Text = "Keine Bilder geladen";
                return;
            }

            var sb = new System.Text.StringBuilder();
            sb.AppendLine($"Bilder: {App.Images.Count}");

            foreach (var img in App.Images.Take(3)) // Zeige nur die ersten 3 zur Übersichtlichkeit
            {
                sb.AppendLine($"{img.FileName}: X={img.PosX}, Y={img.PosY}, Sichtbar={img.IsVisible}");
            }

            if (App.Images.Count > 3)
            {
                sb.AppendLine("...");
            }

            DebugText.Text = sb.ToString();
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