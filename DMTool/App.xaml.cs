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

            // Zufällige Rotation zwischen ±30° oder 180±30°
            if (random.Next(2) == 0)
            {
                imageItem.Rotation = random.NextDouble() * 60 - 30; // -30 bis +30 Grad
            }
            else
            {
                imageItem.Rotation = 180 + random.NextDouble() * 60 - 30; // 150 bis 210 Grad
            }

            // Zielbildschirm bestimmen
            WinForms.Screen targetScreen;
            if (WinForms.Screen.AllScreens.Length > 1)
            {
                targetScreen = WinForms.Screen.AllScreens[1];
            }
            else
            {
                targetScreen = WinForms.Screen.PrimaryScreen;
            }

            // Canvas-Dimensionen
            int canvasWidth = targetScreen.WorkingArea.Width;
            int canvasHeight = targetScreen.WorkingArea.Height;

            // Bildgröße nach Skalierung berechnen
            int scaledWidth = (int)(imageItem.Image.PixelWidth * imageItem.Scale);
            int scaledHeight = (int)(imageItem.Image.PixelHeight * imageItem.Scale);

            // Berechne den maximal möglichen Radius eines umschreibenden Kreises
            // Bei einer Rotation um 45° ist der Radius am größten, entspricht der halben Diagonale
            int diagonalRadius = (int)(Math.Sqrt(scaledWidth * scaledWidth + scaledHeight * scaledHeight) / 2.0);

            // Zusätzlicher Sicherheitsrand (20% mehr)
            int safetyMargin = (int)(diagonalRadius * 0.2);
            int totalMargin = diagonalRadius + safetyMargin;

            // Sichere Positionierungsgrenzen
            int minX = totalMargin;
            int maxX = canvasWidth - totalMargin;
            int minY = totalMargin;
            int maxY = canvasHeight - totalMargin;

            // Prüfen, ob der sichere Bereich groß genug ist
            if (maxX <= minX || maxY <= minY)
            {
                // Sehr großes Bild - zentrieren und stark verkleinern
                imageItem.PosX = canvasWidth / 2;
                imageItem.PosY = canvasHeight / 2;

                // Berechne ein neues Scale, das garantiert passt (mit 70% der Bildschirmgröße)
                double maxDimension = Math.Max(scaledWidth, scaledHeight);
                double availableSpace = Math.Min(canvasWidth, canvasHeight) * 0.7;
                double newScale = imageItem.Scale * (availableSpace / maxDimension);

                imageItem.Scale = newScale;
            }
            else
            {
                // Position zufällig wählen, aber mit mehr Abstand zum Rand
                // Wir verwenden als Bildmittelpunkt den Bereich zwischen totalMargin und canvasWidth/Height - totalMargin
                imageItem.PosX = minX + (int)(random.NextDouble() * (maxX - minX));
                imageItem.PosY = minY + (int)(random.NextDouble() * (maxY - minY));
            }

            // Debug-Ausgabe in die Konsole
            System.Diagnostics.Debug.WriteLine($"Bild: {imageItem.FileName}, Pos: ({imageItem.PosX}, {imageItem.PosY}), " +
                                               $"Scale: {imageItem.Scale}, Rotation: {imageItem.Rotation}°, " +
                                               $"Dimensions: {scaledWidth}x{scaledHeight}, Margin: {totalMargin}");
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

    public class Settings
    {
        private double _maxImageSize = 400.0;
        private bool _installContextMenu = true;
        private bool _showDebugInfo = false;
        private bool _enableFogOfWar = false;
        private double _fogRevealSize = 50.0;

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
            catch (Exception)
            {
                // Fehlerbehandlung
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
                    }
                }
            }
            catch (Exception)
            {
                // Bei Fehler: Standardeinstellungen verwenden
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