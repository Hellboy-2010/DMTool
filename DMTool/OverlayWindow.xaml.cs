// OverlayWindow.xaml.cs
using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Forms;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using DMTool;
using System.Text;
using System.Linq;
using WinForms = System.Windows.Forms;
using WPF = System.Windows;

namespace DMTool
{
    public partial class OverlayWindow : Window
    {
        private bool _isErasing = false;
        private Point _lastPoint;
        private PathGeometry _combinedGeometry;
        private Geometry _fullScreenRect;

        public OverlayWindow()
        {
            InitializeComponent();

            // ItemsSource direkt setzen
            ImageItemsControl.ItemsSource = App.Images;

            // Beobachter für die Images-Collection hinzufügen
            ((System.Collections.Specialized.INotifyCollectionChanged)App.Images).CollectionChanged += (s, e) =>
                UpdateDebugInfoIfEnabled();

            // Beobachter für ShowDebugInfo-Änderungen
            App.AppSettings.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == nameof(Settings.ShowDebugInfo))
                    UpdateDebugInfoIfEnabled();
                else if (e.PropertyName == nameof(Settings.EnableFogOfWar))
                {
                    // Sicherstellen, dass Fog-Visibilität korrekt aktualisiert wird
                    if (FogRect != null)
                    {
                        FogRect.Visibility = App.AppSettings.EnableFogOfWar ?
                            Visibility.Visible : Visibility.Collapsed;
                    }
                    UpdateDebugInfoIfEnabled();
                }
            };

            // Overlay-Fenster auf zweiten Bildschirm positionieren
            PositionWindowOnSecondaryScreen();

            // Initial Debug-Info aktualisieren
            UpdateDebugInfoIfEnabled();

            // Fog initialisieren
            InitializeFogOfWar();
        }

        private void InitializeFogOfWar()
        {
            // Rechteck für den gesamten Bildschirm erstellen
            _fullScreenRect = new RectangleGeometry(new Rect(0, 0, Width, Height));

            // PathGeometry erstellen
            _combinedGeometry = new PathGeometry();

            // Mit dem Bildschirmrechteck starten
            _combinedGeometry.AddGeometry(_fullScreenRect);

            // Clip-Property zurücksetzen
            FogRect.Clip = null;
        }

        private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (App.AppSettings.EnableFogOfWar)
            {
                // Position des Klicks ermitteln
                _lastPoint = e.GetPosition(this);
                _isErasing = true;

                // Loch erstellen an der Mausposition
                RemoveCircleFromFog(_lastPoint);
            }
        }

        private void Window_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            _isErasing = false;
        }

        private void Window_MouseMove(object sender, System.Windows.Input.MouseEventArgs e)
        {
            if (App.AppSettings.EnableFogOfWar && _isErasing && e.LeftButton == MouseButtonState.Pressed)
            {
                Point currentPoint = e.GetPosition(this);

                // Genug Distanz zum letzten Punkt?
                double distance = Math.Sqrt(
                    Math.Pow(currentPoint.X - _lastPoint.X, 2) +
                    Math.Pow(currentPoint.Y - _lastPoint.Y, 2));

                // Nur neue Löcher hinzufügen, wenn wir uns genug bewegt haben
                double minDistance = App.AppSettings.FogRevealSize / 4;
                if (distance >= minDistance)
                {
                    // Mehrere Punkte zwischen Start und Ende für eine flüssige Linie
                    int steps = Math.Max(1, (int)(distance / minDistance));
                    for (int i = 0; i <= steps; i++)
                    {
                        double t = (double)i / steps;
                        double x = _lastPoint.X + t * (currentPoint.X - _lastPoint.X);
                        double y = _lastPoint.Y + t * (currentPoint.Y - _lastPoint.Y);
                        RemoveCircleFromFog(new Point(x, y));
                    }

                    _lastPoint = currentPoint;
                }
            }
        }

        // Entfernt einen Kreis vom Fog an der angegebenen Position
        private void RemoveCircleFromFog(Point position)
        {
            try
            {
                // Größe des "Radiergummis" aus den Einstellungen
                double size = App.AppSettings.FogRevealSize;

                // Kreis erstellen
                EllipseGeometry circleToRemove = new EllipseGeometry(position, size / 2, size / 2);

                // Einfacherer Ansatz: Direkt einen Kreis zur Geometrie hinzufügen
                CombinedGeometry combined = new CombinedGeometry(
                    GeometryCombineMode.Exclude, // Wichtig: Exclude entfernt die zweite Geometrie von der ersten
                    _fullScreenRect,
                    circleToRemove);

                // Kombinierte Geometrie als Clip setzen
                FogRect.Clip = combined;

                // Neue kombinierte Geometrie für den nächsten Durchgang speichern
                _fullScreenRect = combined.GetFlattenedPathGeometry();
            }
            catch (Exception ex)
            {
                if (App.AppSettings.ShowDebugInfo)
                {
                    DebugText.Text = $"Fehler bei Transparenz: {ex.Message}";
                }
            }
        }

        // Button zum Zurücksetzen des Fog of War
        public void ResetFogOfWar()
        {
            try
            {
                // Fog zurücksetzen
                InitializeFogOfWar();
            }
            catch (Exception ex)
            {
                if (App.AppSettings.ShowDebugInfo)
                {
                    DebugText.Text = $"Fehler beim Zurücksetzen: {ex.Message}";
                }
            }
        }

        private void UpdateDebugInfoIfEnabled()
        {
            if (App.AppSettings.ShowDebugInfo)
            {
                StringBuilder sb = new StringBuilder();
                sb.AppendLine($"Fenster: {Width} x {Height}");
                sb.AppendLine($"Fog of War: {(App.AppSettings.EnableFogOfWar ? "Ein" : "Aus")}");

                if (App.Images.Count == 0)
                {
                    sb.AppendLine("Keine Bilder geladen");
                }
                else
                {
                    sb.AppendLine($"Bilder: {App.Images.Count}");
                    foreach (var img in App.Images.Take(3)) // Zeige nur die ersten 3 zur Übersichtlichkeit
                    {
                        sb.AppendLine($"{img.FileName}: X={img.PosX}, Y={img.PosY}, Sichtbar={img.IsVisible}");
                    }
                }

                DebugText.Text = sb.ToString();
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