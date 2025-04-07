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

            // Setup für Fog of War mit verbesserter Bildschirmerkennung
            SetupFogOfWarWithScreenDetection();
        }

        private void InitializeFogOfWar()
        {
            // Rechteck für den gesamten Bildschirm erstellen mit aktuellen Fenstermaßen
            _fullScreenRect = new RectangleGeometry(new Rect(0, 0, Width, Height));

            // PathGeometry erstellen
            _combinedGeometry = new PathGeometry();

            // Mit dem Bildschirmrechteck starten
            _combinedGeometry.AddGeometry(_fullScreenRect);

            // Clip-Property zurücksetzen
            FogRect.Clip = null;

            // Debug-Informationen
            System.Diagnostics.Debug.WriteLine($"Fog of War initialisiert mit Größe: {Width}x{Height}");
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
                // Verbesserte Fehlerprotokollierung
                System.Diagnostics.Debug.WriteLine($"Fehler beim Entfernen des Fog: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"Stack Trace: {ex.StackTrace}");

                if (App.AppSettings.ShowDebugInfo)
                {
                    DebugText.Text = $"Fehler bei Transparenz: {ex.Message}\nPosition: ({position.X}, {position.Y})";
                }

                // In schwerwiegenden Fällen versuchen, den Fog zurückzusetzen
                try
                {
                    InitializeFogOfWar();
                }
                catch
                {
                    // Ignorieren, falls auch das Zurücksetzen fehlschlägt
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
                sb.AppendLine($"Fenster: {Width:F0} x {Height:F0}");
                sb.AppendLine($"Fensterposition: ({Left:F0}, {Top:F0})");
                sb.AppendLine($"Fog of War: {(App.AppSettings.EnableFogOfWar ? "Ein" : "Aus")}");

                // Bildschirminformationen hinzufügen
                sb.AppendLine("\nBildschirme:");
                for (int i = 0; i < Screen.AllScreens.Length; i++)
                {
                    var screen = Screen.AllScreens[i];
                    sb.AppendLine($"  {i}: {screen.DeviceName} {(screen.Primary ? "(Primär)" : "")}");
                    sb.AppendLine($"     Bounds: {screen.Bounds.Width}x{screen.Bounds.Height} bei ({screen.Bounds.X}, {screen.Bounds.Y})");
                }

                if (App.Images.Count == 0)
                {
                    sb.AppendLine("\nKeine Bilder geladen");
                }
                else
                {
                    sb.AppendLine($"\nBilder: {App.Images.Count}");
                    foreach (var img in App.Images.Take(3)) // Zeige nur die ersten 3 zur Übersichtlichkeit
                    {
                        sb.AppendLine($"  {img.FileName}:");
                        sb.AppendLine($"     Pos: ({img.PosX:F0}, {img.PosY:F0}), Rotation: {img.Rotation:F1}°");
                        sb.AppendLine($"     Scale: {img.Scale:F2}, Sichtbar: {img.IsVisible}");
                        if (img.Image != null)
                        {
                            sb.AppendLine($"     Größe: {img.Image.PixelWidth}x{img.Image.PixelHeight}");
                            sb.AppendLine($"     Skalierte Größe: {img.Image.PixelWidth * img.Scale:F0}x{img.Image.PixelHeight * img.Scale:F0}");
                        }
                    }
                }

                DebugText.Text = sb.ToString();
            }
        }

        private void PositionWindowOnSecondaryScreen()
        {
            // Alle verfügbaren Bildschirme abrufen
            var screens = Screen.AllScreens;

            if (screens.Length > 1)
            {
                // Finde den Nicht-Primär-Bildschirm (falls mehrere, nimm den ersten gefundenen)
                var secondaryScreen = screens.FirstOrDefault(s => !s.Primary);

                // Falls kein eindeutiger Nicht-Primär-Bildschirm gefunden wurde, nehme einfach einen anderen als den Primären
                if (secondaryScreen == null)
                {
                    secondaryScreen = screens.First(s => s != Screen.PrimaryScreen);
                }

                var workingArea = secondaryScreen.WorkingArea;

                // Protokolliere für Debugging-Zwecke
                System.Diagnostics.Debug.WriteLine($"Sekundärer Bildschirm gefunden: {secondaryScreen.DeviceName}");
                System.Diagnostics.Debug.WriteLine($"Position: ({workingArea.Left}, {workingArea.Top}), Größe: {workingArea.Width}x{workingArea.Height}");

                // Setze die Fensterposition und -größe
                Left = workingArea.Left;
                Top = workingArea.Top;
                Width = workingArea.Width;
                Height = workingArea.Height;

                // Aktualisiere den Fog of War, um sicherzustellen, dass er die gesamte Bildschirmfläche abdeckt
                InitializeFogOfWar();
            }
            else
            {
                // Fallback auf den Primärbildschirm
                var primaryScreen = Screen.PrimaryScreen;
                var workingArea = primaryScreen.WorkingArea;

                System.Diagnostics.Debug.WriteLine("Nur ein Bildschirm gefunden. Verwende Primärbildschirm.");

                Left = workingArea.Left;
                Top = workingArea.Top;
                Width = workingArea.Width;
                Height = workingArea.Height;

                // Aktualisiere den Fog of War
                InitializeFogOfWar();
            }

            // Debuginformationen aktualisieren
            UpdateDebugInfoIfEnabled();
        }

        // Diese Methode sollte nach OnSourceInitialized oder in Loaded-Ereignis aufgerufen werden
        private void EnsureFogCoversScreen()
        {
            // Beim Laden des Fensters muss sichergestellt werden, dass der Fog wirklich den ganzen Bildschirm abdeckt
            WinForms.Screen currentScreen = WinForms.Screen.FromHandle(new System.Windows.Interop.WindowInteropHelper(this).Handle);

            System.Diagnostics.Debug.WriteLine($"Bildschirm für Fog of War: {currentScreen.DeviceName}");
            System.Diagnostics.Debug.WriteLine($"Bildschirmgröße: {currentScreen.Bounds.Width}x{currentScreen.Bounds.Height}");
            System.Diagnostics.Debug.WriteLine($"Aktuelle Fenstergröße: {Width}x{Height}");

            // Sicherstellen, dass der Fog wirklich den kompletten Bildschirm abdeckt
            if (Width < currentScreen.Bounds.Width || Height < currentScreen.Bounds.Height)
            {
                System.Diagnostics.Debug.WriteLine("Passe Fenstergröße an Bildschirmgröße an");
                Width = currentScreen.Bounds.Width;
                Height = currentScreen.Bounds.Height;

                // Fog neu initialisieren mit korrekter Größe
                InitializeFogOfWar();
            }
        }

        // Diese Methode wird am Ende des Konstruktors aufgerufen
        private void SetupFogOfWarWithScreenDetection()
        {
            // Fenster geladen Event hinzufügen
            this.Loaded += (s, e) =>
            {
                System.Diagnostics.Debug.WriteLine("Overlay-Fenster geladen, überprüfe Fog of War-Abdeckung");
                EnsureFogCoversScreen();
            };

            // SourceInitialized nutzen, um sicherzustellen, dass wir den korrekten Handle haben
            this.SourceInitialized += (s, e) =>
            {
                System.Diagnostics.Debug.WriteLine("Overlay-Fenster SourceInitialized, überprüfe Fog of War-Abdeckung");
                EnsureFogCoversScreen();
            };

            // Bei Größenänderung des Fensters
            this.SizeChanged += (s, e) =>
            {
                System.Diagnostics.Debug.WriteLine($"Fenstergröße geändert auf {Width}x{Height}, aktualisiere Fog of War");
                InitializeFogOfWar();
            };
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