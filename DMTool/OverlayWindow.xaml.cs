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
    // Value Converter für die Kalibrierungsanzeige
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

    public class DivideByTwoConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            if (value is double number)
            {
                return number / 2;
            }
            return 0;
        }

        public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    public class SubtractValueConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            if (value is double number && parameter is string paramStr && double.TryParse(paramStr, out double paramValue))
            {
                return number - paramValue;
            }
            return 0;
        }

        public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    public class CenterRectangleConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            if (value is double dimension && parameter is string paramStr && double.TryParse(paramStr, out double objectSize))
            {
                // Berechnet die Position, um das Objekt zu zentrieren
                return (dimension / 2) - (objectSize / 2);
            }
            return 0;
        }

        public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
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
                {
                    UpdateDebugInfoIfEnabled();

                    // Bei Änderung des Debug-Status Kalibrierungsmarker aktualisieren
                    if (App.AppSettings.ShowDebugInfo)
                    {
                        CreateCalibrationMarkers();
                    }
                }
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
                sb.AppendLine($"Position-Offsets: X={App.AppSettings.PositionOffsetX}, Y={App.AppSettings.PositionOffsetY}");

                // Bildschirminformationen hinzufügen
                sb.AppendLine("\nBildschirme:");
                for (int i = 0; i < Screen.AllScreens.Length; i++)
                {
                    var screen = Screen.AllScreens[i];
                    double scaleFactor = DpiUtils.GetDpiScaleFactor(screen);
                    sb.AppendLine($"  {i}: {screen.DeviceName} {(screen.Primary ? "(Primär)" : "")}");
                    sb.AppendLine($"     Bounds (Pixel): {screen.Bounds.Width}x{screen.Bounds.Height} bei ({screen.Bounds.X}, {screen.Bounds.Y})");
                    sb.AppendLine($"     DPI-Skalierung: {scaleFactor * 100:F0}%");
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

        public void PositionWindowOnSecondaryScreen()
        {
            // Alle verfügbaren Bildschirme abrufen
            var screens = Screen.AllScreens;
            Screen targetScreen = null;

            int targetIndex = App.AppSettings.TargetScreenIndex;

            // 1. Priorität: Benutzereinstellung
            if (targetIndex >= 0 && targetIndex < screens.Length)
            {
                targetScreen = screens[targetIndex];
                System.Diagnostics.Debug.WriteLine($"Verwende ausgewählten Bildschirm {targetIndex}: {targetScreen.DeviceName}");
            }
            // 2. Priorität: Automatische Erkennung (Sekundärer)
            else if (screens.Length > 1)
            {
                // Finde den Nicht-Primär-Bildschirm
                targetScreen = screens.FirstOrDefault(s => !s.Primary);
                
                // Fallback
                if (targetScreen == null)
                {
                    targetScreen = screens.First(s => s != Screen.PrimaryScreen);
                }
                System.Diagnostics.Debug.WriteLine($"Automatische Wahl (Sekundär): {targetScreen.DeviceName}");
            }
            // 3. Priorität: Primär (Notfall)
            else
            {
                targetScreen = Screen.PrimaryScreen;
                System.Diagnostics.Debug.WriteLine($"Keine Auswahl/Sekundär gefunden. Verwende Primär: {targetScreen.DeviceName}");
            }

            var workingArea = targetScreen.WorkingArea;
            
            // Setze die Fensterposition und -größe mit der physischen Methode
            int x = workingArea.Left + App.AppSettings.PositionOffsetX;
            int y = workingArea.Top + App.AppSettings.PositionOffsetY;
            
            System.Diagnostics.Debug.WriteLine($"Setze Fensterposition auf Screen '{targetScreen.DeviceName}': ({x}, {y}), {workingArea.Width}x{workingArea.Height}");
            DpiUtils.SetWindowPosition(this, x, y, workingArea.Width, workingArea.Height);

            // Aktualisiere den Fog of War
            InitializeFogOfWar();
            
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
                CreateCalibrationMarkers();
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
                CreateCalibrationMarkers();
            };

            // Bei Änderung der Offsets oder des Zielbildschirms die Fensterposition aktualisieren
            App.AppSettings.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == "PositionOffsetX" || e.PropertyName == "PositionOffsetY" || e.PropertyName == "TargetScreenIndex")
                {
                    // Fensterposition aktualisieren
                    // Wir nutzen die neue Methode, die jetzt public ist und Index beachtet
                    PositionWindowOnSecondaryScreen();
                }
            };
        }

        // Veraltet, aber wir lassen sie leer um Konflikte zu vermeiden, PositionWindowOnSecondaryScreen übernimmt alles
        private void UpdateWindowPositionForOffsetChange()
        {
        }



        private void CreateCalibrationMarkers()
        {
            try
            {
                // Referenzen zu den Canvas-Elementen im XAML
                var calibrationFrame = FindName("CalibrationFrame") as Canvas;
                if (calibrationFrame == null)
                {
                    System.Diagnostics.Debug.WriteLine("Fehler: CalibrationFrame konnte nicht gefunden werden.");
                    return;
                }

                var borders = calibrationFrame.Children.OfType<Border>().ToList();
                if (borders.Count < 4)
                {
                    System.Diagnostics.Debug.WriteLine($"Fehler: Nicht genügend Border-Elemente gefunden: {borders.Count}");
                    return;
                }

                var topCanvas = borders[0].Child as Canvas;
                var bottomCanvas = borders[1].Child as Canvas;
                var leftCanvas = borders[2].Child as Canvas;
                var rightCanvas = borders[3].Child as Canvas;

                if (topCanvas == null || bottomCanvas == null ||
                    leftCanvas == null || rightCanvas == null)
                {
                    System.Diagnostics.Debug.WriteLine("Fehler: Canvas-Elemente konnten nicht gefunden werden.");
                    return;
                }

                // Löschen Sie vorhandene Markierungen
                topCanvas.Children.Clear();
                bottomCanvas.Children.Clear();
                leftCanvas.Children.Clear();
                rightCanvas.Children.Clear();

                // Entferne alte Info-Labels
                foreach (var child in calibrationFrame.Children.OfType<TextBlock>().ToList())
                {
                    calibrationFrame.Children.Remove(child);
                }

                // Erstellen der horizontalen Markierungen (oben und unten)
                double width = this.ActualWidth;
                for (int x = 0; x < width; x += 100) // Große Markierungen alle 100 Pixel
                {
                    // Oben
                    var lineTop = new Line
                    {
                        X1 = x,
                        Y1 = 0,
                        X2 = x,
                        Y2 = 15,
                        Stroke = Brushes.Yellow,
                        StrokeThickness = 1
                    };
                    topCanvas.Children.Add(lineTop);

                    var textTop = new TextBlock
                    {
                        Text = x.ToString(),
                        Foreground = Brushes.Yellow,
                        FontSize = 9
                    };
                    Canvas.SetLeft(textTop, x + 2);
                    Canvas.SetTop(textTop, 16);
                    topCanvas.Children.Add(textTop);

                    // Unten
                    var lineBottom = new Line
                    {
                        X1 = x,
                        Y1 = 15,
                        X2 = x,
                        Y2 = 30,
                        Stroke = Brushes.Yellow,
                        StrokeThickness = 1
                    };
                    bottomCanvas.Children.Add(lineBottom);

                    var textBottom = new TextBlock
                    {
                        Text = x.ToString(),
                        Foreground = Brushes.Yellow,
                        FontSize = 9
                    };
                    Canvas.SetLeft(textBottom, x + 2);
                    Canvas.SetTop(textBottom, 0);
                    bottomCanvas.Children.Add(textBottom);
                }

                // Kleinere Markierungen alle 50 Pixel
                for (int x = 50; x < width; x += 100)
                {
                    // Oben
                    var lineTop = new Line
                    {
                        X1 = x,
                        Y1 = 0,
                        X2 = x,
                        Y2 = 10,
                        Stroke = Brushes.Yellow,
                        StrokeThickness = 0.5
                    };
                    topCanvas.Children.Add(lineTop);

                    // Unten
                    var lineBottom = new Line
                    {
                        X1 = x,
                        Y1 = 20,
                        X2 = x,
                        Y2 = 30,
                        Stroke = Brushes.Yellow,
                        StrokeThickness = 0.5
                    };
                    bottomCanvas.Children.Add(lineBottom);
                }

                // Erstellen der vertikalen Markierungen (links und rechts)
                double height = this.ActualHeight;
                for (int y = 0; y < height; y += 100) // Große Markierungen alle 100 Pixel
                {
                    // Links
                    var lineLeft = new Line
                    {
                        X1 = 0,
                        Y1 = y,
                        X2 = 15,
                        Y2 = y,
                        Stroke = Brushes.Yellow,
                        StrokeThickness = 1
                    };
                    leftCanvas.Children.Add(lineLeft);

                    var textLeft = new TextBlock
                    {
                        Text = y.ToString(),
                        Foreground = Brushes.Yellow,
                        FontSize = 9
                    };
                    Canvas.SetLeft(textLeft, 16);
                    Canvas.SetTop(textLeft, y);
                    leftCanvas.Children.Add(textLeft);

                    // Rechts
                    var lineRight = new Line
                    {
                        X1 = 15,
                        Y1 = y,
                        X2 = 30,
                        Y2 = y,
                        Stroke = Brushes.Yellow,
                        StrokeThickness = 1
                    };
                    rightCanvas.Children.Add(lineRight);

                    var textRight = new TextBlock
                    {
                        Text = y.ToString(),
                        Foreground = Brushes.Yellow,
                        FontSize = 9
                    };
                    Canvas.SetLeft(textRight, 0);
                    Canvas.SetTop(textRight, y);
                    rightCanvas.Children.Add(textRight);
                }

                // Kleinere Markierungen alle 50 Pixel
                for (int y = 50; y < height; y += 100)
                {
                    // Links
                    var lineLeft = new Line
                    {
                        X1 = 0,
                        Y1 = y,
                        X2 = 10,
                        Y2 = y,
                        Stroke = Brushes.Yellow,
                        StrokeThickness = 0.5
                    };
                    leftCanvas.Children.Add(lineLeft);

                    // Rechts
                    var lineRight = new Line
                    {
                        X1 = 20,
                        Y1 = y,
                        X2 = 30,
                        Y2 = y,
                        Stroke = Brushes.Yellow,
                        StrokeThickness = 0.5
                    };
                    rightCanvas.Children.Add(lineRight);
                }

                // Zentrumsmarkierung
                var centerX = width / 2;
                var centerY = height / 2;

                // Erstelle ein Label für die Anzeige der Bildschirmgröße
                TextBlock sizeInfo = new TextBlock
                {
                    Text = $"Bildschirm: {width}x{height} px",
                    Foreground = Brushes.White,
                    Background = new SolidColorBrush(Color.FromArgb(128, 0, 0, 0)),
                    FontSize = 12,
                    Padding = new Thickness(5)
                };
                Canvas.SetLeft(sizeInfo, 100);
                Canvas.SetTop(sizeInfo, 100);
                calibrationFrame.Children.Add(sizeInfo);

                // Erstelle ein Label für die aktuelle Offset-Einstellung
                TextBlock offsetInfo = new TextBlock
                {
                    Text = $"Aktueller Offset: X={App.AppSettings.PositionOffsetX}px, Y={App.AppSettings.PositionOffsetY}px",
                    Foreground = Brushes.White,
                    Background = new SolidColorBrush(Color.FromArgb(128, 0, 0, 0)),
                    FontSize = 12,
                    Padding = new Thickness(5)
                };
                Canvas.SetLeft(offsetInfo, 100);
                Canvas.SetTop(offsetInfo, 130);
                calibrationFrame.Children.Add(offsetInfo);

                System.Diagnostics.Debug.WriteLine($"Kalibrierungsmarkierungen erstellt. Bildschirmgröße: {width}x{height}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Fehler beim Erstellen der Kalibrierungsmarkierungen: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"Stack Trace: {ex.StackTrace}");
            }
        }
    }

    
}