using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Forms;
using Microsoft.Win32;
using WinForms = System.Windows.Forms; // Alias für Windows.Forms
using WPF = System.Windows; // Alias für System.Windows

namespace DMTool
{
    public partial class App : WPF.Application
    {
        public static ObservableCollection<ImageItem> Images { get; } = new ObservableCollection<ImageItem>();
        public static Settings AppSettings { get; } = new Settings();
        public static bool IsShuttingDown { get; private set; } = false;

        private int _positionOffsetX = 0;
        private int _positionOffsetY = 0;
        private WinForms.NotifyIcon _notifyIcon;
        private MainWindow _mainWindow;
        private static OverlayWindow _overlayWindow;
        public static OverlayWindow OverlayWindow => _overlayWindow;
        public static void ShutdownApplication()
        {
            IsShuttingDown = true;

            // Speichern der Einstellungen
            AppSettings.Save();

            // Korrektes Beenden der Anwendung
            Current.Shutdown();
        }

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            // Laden der Einstellungen
            AppSettings.Load();

            // Registrierung des Kontextmenüs falls nötig
            if (AppSettings.InstallContextMenu)
            {
                InstallContextMenu();
            }

            // Starten mit Bild-Parameter
            if (e.Args.Length > 0)
            {
                foreach (var arg in e.Args)
                {
                    if (System.IO.File.Exists(arg))
                    {
                        AddImage(arg);
                    }
                }
            }

            // System-Tray-Icon erstellen
            InitializeNotifyIcon();

            // Hauptfenster erstellen und anzeigen
            _mainWindow = new MainWindow();
            _mainWindow.Show();

            // Overlay-Fenster erstellen und anzeigen
            if (System.Windows.Forms.Screen.AllScreens.Length > 1)
            {
                _overlayWindow = new OverlayWindow();
                _overlayWindow.Show();
            }
            else
            {
                WPF.MessageBox.Show("Es wurde kein zweiter Bildschirm gefunden. Das Overlay wird auf dem Hauptbildschirm angezeigt.");
                _overlayWindow = new OverlayWindow();
                _overlayWindow.Show();
            }
        }

        private void InitializeNotifyIcon()
        {
            _notifyIcon = new System.Windows.Forms.NotifyIcon
            {
                Icon = new System.Drawing.Icon(GetResourceStream(new Uri("pack://application:,,,/DMTool;component/Resources/app_icon.ico")).Stream),
                Visible = true,
                Text = "DM Tool"
            };

            var contextMenu = new WinForms.ContextMenuStrip();

            // Hauptfenster öffnen
            var showItem = new WinForms.ToolStripMenuItem("Hauptfenster öffnen");
            showItem.Click += (s, e) => {
                _mainWindow.Show();
                _mainWindow.WindowState = WindowState.Normal;
                _mainWindow.Activate();
            };

            // Kontextmenü installieren
            var installContextMenuItem = new WinForms.ToolStripMenuItem("Explorer-Kontextmenü installieren");
            installContextMenuItem.Click += (s, e) => {
                AppSettings.InstallContextMenu = true;
                InstallContextMenu();
                _notifyIcon.ShowBalloonTip(3000, "DM Tool", "Explorer-Kontextmenü wurde installiert.", WinForms.ToolTipIcon.Info);
            };

            // Kontextmenü entfernen
            var uninstallContextMenuItem = new WinForms.ToolStripMenuItem("Explorer-Kontextmenü entfernen");
            uninstallContextMenuItem.Click += (s, e) => {
                AppSettings.InstallContextMenu = false;
                UninstallContextMenu();
                _notifyIcon.ShowBalloonTip(3000, "DM Tool", "Explorer-Kontextmenü wurde entfernt.", WinForms.ToolTipIcon.Info);
            };

            // Trennlinie
            var separator = new WinForms.ToolStripSeparator();

            // Beenden
            var closeItem = new WinForms.ToolStripMenuItem("Beenden");
            closeItem.Click += (s, e) => ShutdownApplication();

            // Menüeinträge hinzufügen
            contextMenu.Items.Add(showItem);
            contextMenu.Items.Add(new WinForms.ToolStripSeparator());
            contextMenu.Items.Add(installContextMenuItem);
            contextMenu.Items.Add(uninstallContextMenuItem);
            contextMenu.Items.Add(separator);
            contextMenu.Items.Add(closeItem);

            // Menüeinträge aktivieren/deaktivieren basierend auf aktuellem Status
            contextMenu.Opening += (s, e) => {
                installContextMenuItem.Enabled = !AppSettings.InstallContextMenu;
                uninstallContextMenuItem.Enabled = AppSettings.InstallContextMenu;
            };

            _notifyIcon.ContextMenuStrip = contextMenu;
            _notifyIcon.DoubleClick += (s, e) => {
                _mainWindow.Show();
                _mainWindow.WindowState = WindowState.Normal;
                _mainWindow.Activate();
            };
        }

        protected override void OnExit(ExitEventArgs e)
        {
            if (_notifyIcon != null)
            {
                _notifyIcon.Visible = false;
                _notifyIcon.Dispose();
            }

            AppSettings.Save();

            base.OnExit(e);
        }
        public static void SetRandomPositionAndRotation(ImageItem imageItem)
        {
            if (imageItem == null || imageItem.Image == null)
                return;

            var random = new Random();

            // Zielbildschirm bestimmen
            WinForms.Screen targetScreen;
            if (WinForms.Screen.AllScreens.Length > 1)
            {
                var secondaryScreen = WinForms.Screen.AllScreens.FirstOrDefault(s => !s.Primary);
                if (secondaryScreen == null)
                {
                    secondaryScreen = WinForms.Screen.AllScreens.First(s => s != WinForms.Screen.PrimaryScreen);
                }
                targetScreen = secondaryScreen;
            }
            else
            {
                targetScreen = WinForms.Screen.PrimaryScreen;
            }

            // Bildschirmabmessungen
            int screenWidth = targetScreen.WorkingArea.Width;
            int screenHeight = targetScreen.WorkingArea.Height;

            System.Diagnostics.Debug.WriteLine($"POSITIONIERUNG MIT DYNAMISCHER GRÖSSENANPASSUNG");
            System.Diagnostics.Debug.WriteLine($"Bildschirm: {screenWidth}x{screenHeight}");

            // Originale Bilddimensionen
            double originalWidth = imageItem.Image.PixelWidth;
            double originalHeight = imageItem.Image.PixelHeight;

            System.Diagnostics.Debug.WriteLine($"Originales Bild: {originalWidth}x{originalHeight}");

            // Maximale Skalierungsfaktoren berechnen:
            // 1. Basierend auf AppSettings.MaxImageSize
            // 2. Basierend auf Bildschirmbreite (100%)
            // 3. Basierend auf Bildschirmhöhe (100%)
            double maxSettingScale = AppSettings.MaxImageSize / Math.Max(originalWidth, originalHeight);
            double maxWidthScale = screenWidth / originalWidth;
            double maxHeightScale = screenHeight / originalHeight;

            // Der kleinste dieser Faktoren ist der limitierende Faktor
            double scale = Math.Min(maxSettingScale, Math.Min(maxWidthScale, maxHeightScale));

            // Sicherheitsreduktion um 5%, damit ein Mindestrand bleibt
            scale *= 0.95;

            imageItem.Scale = scale;

            System.Diagnostics.Debug.WriteLine($"Skalierungsfaktoren - Einstellung: {maxSettingScale:F3}, Breite: {maxWidthScale:F3}, Höhe: {maxHeightScale:F3}");
            System.Diagnostics.Debug.WriteLine($"Finaler Skalierungsfaktor: {scale:F3}");

            // Skalierte Dimensionen berechnen
            double scaledWidth = originalWidth * scale;
            double scaledHeight = originalHeight * scale;

            System.Diagnostics.Debug.WriteLine($"Skaliertes Bild: {scaledWidth:F1}x{scaledHeight:F1}px");

            // Berechnen des Verhältnisses der Bildgröße zur Bildschirmgröße
            double widthRatio = scaledWidth / screenWidth;
            double heightRatio = scaledHeight / screenHeight;
            double sizeRatio = Math.Max(widthRatio, heightRatio);

            System.Diagnostics.Debug.WriteLine($"Größenverhältnis zum Bildschirm: {sizeRatio:P1}");

            // Zufällige Rotation bestimmen - keine Rotation für große Bilder
            bool allowRotation = sizeRatio <= 0.8; // Keine Rotation, wenn das Bild >80% des Bildschirms einnimmt

            if (allowRotation)
            {
                bool isAround0 = random.Next(2) == 0;
                double baseRotation = isAround0 ? 0 : 180;
                double variation = (random.NextDouble() * 20) - 10; // -10° bis +10°
                imageItem.Rotation = baseRotation + variation;
                System.Diagnostics.Debug.WriteLine($"Rotation erlaubt, Wert: {imageItem.Rotation:F1}°");
            }
            else
            {
                imageItem.Rotation = 0; // Bei großen Bildern keine Rotation
                System.Diagnostics.Debug.WriteLine("Bild zu groß für Rotation, setze Rotation auf 0°");
            }

            // Berechnung des Sicherheitsrands
            int baseMargin = 80; // Basis-Sicherheitsrand
            int totalMargin = baseMargin;

            // Zusätzlicher Rand für Rotation, falls Rotation verwendet wird
            if (allowRotation && Math.Abs(imageItem.Rotation) > 0.1)
            {
                double diagonalRadius = Math.Sqrt(scaledWidth * scaledWidth + scaledHeight * scaledHeight) / 2;
                double straightRadius = Math.Max(scaledWidth, scaledHeight) / 2;
                double rotationRadians = Math.Abs(imageItem.Rotation % 180) * Math.PI / 180;
                double extraMarginForRotation = (diagonalRadius - straightRadius) * Math.Sin(rotationRadians);

                totalMargin += (int)Math.Ceiling(extraMarginForRotation);
                System.Diagnostics.Debug.WriteLine($"Zusätzlicher Rand für Rotation: {(int)Math.Ceiling(extraMarginForRotation)}px");
            }

            // Bei sehr großen Bildern den Rand reduzieren, damit das Bild überhaupt passt
            if (sizeRatio > 0.8)
            {
                //totalMargin = Math.Max(20, totalMargin / 2); // Mindestens 20px, aber Reduktion um 50%
                totalMargin = 0;
                System.Diagnostics.Debug.WriteLine($"Rand reduziert wegen Bildgröße auf {totalMargin}px");
            }

            // Berechnung der sicheren Grenzen für die Positionierung
            int minX, maxX, minY, maxY;

            if (imageItem.Rotation < 90 || imageItem.Rotation > 270) // Nahe 0° Rotation
            {
                // Bei 0° Rotation: Normaler Fall mit Sicherheitsrand
                minX = totalMargin;
                maxX = (int)(screenWidth - scaledWidth - totalMargin);
                minY = totalMargin;
                maxY = (int)(screenHeight - scaledHeight - totalMargin);
            }
            else // Nahe 180° Rotation
            {
                // Bei 180° Rotation brauchen wir zusätzlichen Platz auf der linken und oberen Seite
                minX = (int)(scaledWidth + totalMargin);
                maxX = (int)(screenWidth - totalMargin);
                minY = (int)(scaledHeight + totalMargin);
                maxY = (int)(screenHeight - totalMargin);
            }

            // Sicherstellen, dass die Grenzen sinnvoll sind
            if (maxX < minX)
            {
                System.Diagnostics.Debug.WriteLine("WARNUNG: Bild ist zu breit für sichere Positionierung");
                // Zentrieren
                minX = maxX = screenWidth / 2;
            }
            if (maxY < minY)
            {
                System.Diagnostics.Debug.WriteLine("WARNUNG: Bild ist zu hoch für sichere Positionierung");
                // Zentrieren
                minY = maxY = screenHeight / 2;
            }

            System.Diagnostics.Debug.WriteLine($"Sichere Positionierungsgrenzen: X({minX}-{maxX}), Y({minY}-{maxY})");

            // Zufällige Position innerhalb der sicheren Grenzen wählen
            if (maxX > minX)
            {
                imageItem.PosX = minX + random.Next(maxX - minX + 1);
            }
            else
            {
                imageItem.PosX = minX;
            }

            if (maxY > minY)
            {
                imageItem.PosY = minY + random.Next(maxY - minY + 1);
            }
            else
            {
                imageItem.PosY = minY;
            }

            // Finale Koordinaten
            System.Diagnostics.Debug.WriteLine($"Finale Position: X={imageItem.PosX}, Y={imageItem.PosY}");

            // Zusammenfassung
            System.Diagnostics.Debug.WriteLine("Zusammenfassung:");
            System.Diagnostics.Debug.WriteLine($"- Bild: {imageItem.FileName}");
            System.Diagnostics.Debug.WriteLine($"- Größe: {scaledWidth:F1}x{scaledHeight:F1}px ({sizeRatio:P1} des Bildschirms)");
            System.Diagnostics.Debug.WriteLine($"- Rotation: {imageItem.Rotation:F1}° (Erlaubt: {allowRotation})");
            System.Diagnostics.Debug.WriteLine($"- Position: ({imageItem.PosX}, {imageItem.PosY})");
            System.Diagnostics.Debug.WriteLine($"- Sicherheitsrand: {totalMargin}px");
        }


        public static void AddImage(string path)
        {
            var imageItem = new ImageItem
            {
                Path = path,
                Scale = 1.0
            };

            imageItem.LoadImage();

            if (imageItem.Image != null)
            {
                // Skalierung berechnen
                double imageWidth = imageItem.Image.PixelWidth;
                double imageHeight = imageItem.Image.PixelHeight;

                if (imageWidth >= imageHeight)
                {
                    imageItem.Scale = AppSettings.MaxImageSize / imageWidth;
                }
                else
                {
                    imageItem.Scale = AppSettings.MaxImageSize / imageHeight;
                }

                // Position und Rotation setzen
                SetRandomPositionAndRotation(imageItem);

                // Bild zur Collection hinzufügen
                
                WPF.Application.Current.Dispatcher.Invoke(() => Images.Add(imageItem));
            }
        }

        public static void InstallContextMenu()
        {
            try
            {
                // Den Pfad zur ausführbaren Datei korrekt ermitteln
                string executablePath = Environment.ProcessPath;

                // Wenn ProcessPath null ist, versuchen wir einen alternativen Ansatz
                if (string.IsNullOrEmpty(executablePath))
                {
                    executablePath = System.Diagnostics.Process.GetCurrentProcess().MainModule.FileName;
                }

                // Überprüfen, ob der Pfad auf .dll endet und korrigieren
                if (executablePath.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
                {
                    executablePath = executablePath.Substring(0, executablePath.Length - 4) + ".exe";
                }

                // Sicherstellen, dass die Datei existiert
                if (!System.IO.File.Exists(executablePath))
                {
                    string appDirectory = AppDomain.CurrentDomain.BaseDirectory;
                    string appName = System.Reflection.Assembly.GetExecutingAssembly().GetName().Name;
                    executablePath = System.IO.Path.Combine(appDirectory, appName + ".exe");

                    // Wenn wir immer noch keine EXE finden können
                    if (!System.IO.File.Exists(executablePath))
                    {
                        WPF.MessageBox.Show("Kann die ausführbare Datei nicht finden. Kontextmenü-Installation fehlgeschlagen.",
                                             "DM Tool", WPF.MessageBoxButton.OK, WPF.MessageBoxImage.Error);
                        return;
                    }
                }

                // JPEG
                using (var key = Registry.CurrentUser.CreateSubKey(@"Software\Classes\.jpg\shell\SendToDMTool"))
                {
                    key.SetValue("", "Sende an DM-Tool");
                }
                using (var key = Registry.CurrentUser.CreateSubKey(@"Software\Classes\.jpg\shell\SendToDMTool\command"))
                {
                    key.SetValue("", $"\"{executablePath}\" \"%1\"");
                }

            }
            catch (Exception ex)
            {
                WPF.MessageBox.Show($"Fehler beim Einrichten des Kontextmenüs: {ex.Message}",
                                     "DM Tool", WPF.MessageBoxButton.OK, WPF.MessageBoxImage.Error);
            }
        }
        public static void RepositionImage(ImageItem imageItem)
        {
            // Ruft einfach die gemeinsame Methode auf
            SetRandomPositionAndRotation(imageItem);
        }

        public static void UninstallContextMenu()
        {
            try
            {
                Registry.CurrentUser.DeleteSubKeyTree(@"Software\Classes\.jpg\shell\SendToDMTool", false);
                Registry.CurrentUser.DeleteSubKeyTree(@"Software\Classes\.jpeg\shell\SendToDMTool", false);
                Registry.CurrentUser.DeleteSubKeyTree(@"Software\Classes\.png\shell\SendToDMTool", false);
                Registry.CurrentUser.DeleteSubKeyTree(@"Software\Classes\.bmp\shell\SendToDMTool", false);
                Registry.CurrentUser.DeleteSubKeyTree(@"Software\Classes\.gif\shell\SendToDMTool", false);
            }
            catch (Exception ex)
            {
                WPF.MessageBox.Show($"Fehler beim Entfernen des Kontextmenüs: {ex.Message}", "DM Tool", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }

    public class Settings : INotifyPropertyChanged
    {
        private double _maxImageSize = 400.0;
        private bool _installContextMenu = true;
        private bool _showDebugInfo = false;
        private bool _enableFogOfWar = false;
        private double _fogRevealSize = 50.0;
        private int _positionOffsetX = 0;
        private int _positionOffsetY = 0;

        public int PositionOffsetX
        {
            get => _positionOffsetX;
            set
            {
                if (_positionOffsetX != value)
                {
                    _positionOffsetX = value;
                    OnPropertyChanged(nameof(PositionOffsetX));
                }
            }
        }

        public int PositionOffsetY
        {
            get => _positionOffsetY;
            set
            {
                if (_positionOffsetY != value)
                {
                    _positionOffsetY = value;
                    OnPropertyChanged(nameof(PositionOffsetY));
                }
            }
        }

        public bool EnableFogOfWar
        {
            get => _enableFogOfWar;
            set
            {
                if (_enableFogOfWar != value)
                {
                    _enableFogOfWar = value;
                    OnPropertyChanged(nameof(EnableFogOfWar));
                }
            }
        }

        public double FogRevealSize
        {
            get => _fogRevealSize;
            set
            {
                if (_fogRevealSize != value)
                {
                    _fogRevealSize = value;
                    OnPropertyChanged(nameof(FogRevealSize));
                }
            }
        }

        public double MaxImageSize
        {
            get => _maxImageSize;
            set
            {
                if (_maxImageSize != value)
                {
                    _maxImageSize = value;
                    OnPropertyChanged(nameof(MaxImageSize));
                }
            }
        }

        public bool InstallContextMenu
        {
            get => _installContextMenu;
            set
            {
                if (_installContextMenu != value)
                {
                    _installContextMenu = value;
                    OnPropertyChanged(nameof(InstallContextMenu));
                }
            }
        }

        public bool ShowDebugInfo
        {
            get => _showDebugInfo;
            set
            {
                if (_showDebugInfo != value)
                {
                    _showDebugInfo = value;
                    OnPropertyChanged(nameof(ShowDebugInfo));
                }
            }
        }

        // Rest der Klasse bleibt unverändert
        private readonly string _settingsFilePath = System.IO.Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "DMTool",
            "settings.xml");

        public void Save()
        {
            try
            {
                var directory = System.IO.Path.GetDirectoryName(_settingsFilePath);
                if (!System.IO.Directory.Exists(directory))
                {
                    System.IO.Directory.CreateDirectory(directory);
                }

                var serializer = new System.Xml.Serialization.XmlSerializer(typeof(Settings));
                using (var writer = new System.IO.StreamWriter(_settingsFilePath))
                {
                    serializer.Serialize(writer, this);
                }
            }
            catch (Exception ex)
            {
                // Verbesserte Fehlerbehandlung mit Logging
                System.Diagnostics.Debug.WriteLine($"Fehler beim Speichern der Einstellungen: {ex.Message}");
            }
        }

        public void Load()
        {
            try
            {
                if (System.IO.File.Exists(_settingsFilePath))
                {
                    var serializer = new System.Xml.Serialization.XmlSerializer(typeof(Settings));
                    using (var reader = new System.IO.StreamReader(_settingsFilePath))
                    {
                        var settings = (Settings)serializer.Deserialize(reader);
                        MaxImageSize = settings.MaxImageSize;
                        InstallContextMenu = settings.InstallContextMenu;
                        ShowDebugInfo = settings.ShowDebugInfo;
                        EnableFogOfWar = settings.EnableFogOfWar;
                        FogRevealSize = settings.FogRevealSize;
                        PositionOffsetX = settings.PositionOffsetX;
                        PositionOffsetY = settings.PositionOffsetY;
                    }
                }
            }
            catch (Exception ex)
            {
                // Bei Fehler: Standardeinstellungen verwenden und Fehler protokollieren
                System.Diagnostics.Debug.WriteLine($"Fehler beim Laden der Einstellungen: {ex.Message}");
            }
        }

        // PropertyChanged-Ereignis hinzufügen
        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}