// MainWindow.xaml.cs
using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.IO;
using System.Collections.Generic;
using DMTool;
using WinForms = System.Windows.Forms;
using WPF = System.Windows;

namespace DMTool
{
    public partial class MainWindow : Window
    {
        public static bool IsShuttingDown { get; private set; } = false;
        public MainWindow()
        {
            InitializeComponent();
            ImagesListView.ItemsSource = App.Images;

            // Dynamische Anpassung des Sliders für die maximale Bildgröße
            UpdateImageSizeSliderRange();
            
            // Bildschirm-Auswahl initialisieren
            InitializeScreenSelector();
            
            // Event für Bildschirmänderungen registrieren
            Microsoft.Win32.SystemEvents.DisplaySettingsChanged += (s, e) => {
                 Dispatcher.Invoke(InitializeScreenSelector);
            };
        }

        public class ScreenInfo
        {
            public int Index { get; set; }
            public string Name { get; set; }
            public bool IsPrimary { get; set; }
        }

        private void InitializeScreenSelector()
        {
            try 
            {
                var screens = WinForms.Screen.AllScreens;
                var screenList = new List<ScreenInfo>();

                // Warnung prüfen (Benutzeranforderung)
                if (screens.Length <= 1) 
                {
                   // Wir zeigen die Warnung nur einmalig beim Start an, wenn noch nicht gewarnt wurde
                   if (ScreenSelector.Items.Count == 0) // Nur beim ersten Laden
                   {
                         MessageBox.Show("Es wurde nur ein Bildschirm gefunden. Der 'Fog of War' wird auf dem Hauptbildschirm angezeigt.", 
                                         "Nur ein Bildschirm", MessageBoxButton.OK, MessageBoxImage.Warning);
                   }
                }

                for (int i = 0; i < screens.Length; i++)
                {
                    var s = screens[i];
                    screenList.Add(new ScreenInfo 
                    { 
                        Index = i, 
                        Name = $"Bildschirm {i + 1}: {s.Bounds.Width}x{s.Bounds.Height} {(s.Primary ? "(Primär)" : "")}",
                        IsPrimary = s.Primary
                    });
                }

                ScreenSelector.ItemsSource = screenList;

                // Aktuelle Auswahl wiederherstellen
                int current = App.AppSettings.TargetScreenIndex;
                if (current >= 0 && current < screens.Length)
                {
                    ScreenSelector.SelectedValue = current;
                }
                else
                {
                     // Standard: Automatisch (wir haben keinen Auto-Eintrag, also wählen wir den Sekundären oder Primären)
                     // Wir fügen einen "Automatisch" Eintrag hinzu? 
                     // Plan: Explizite Auswahl.
                     // Falls nichts gesetzt (-1), versuchen wir den Sekundären zu wählen
                     var secondary = screenList.FirstOrDefault(s => !s.IsPrimary);
                     if (secondary != null)
                        ScreenSelector.SelectedValue = secondary.Index;
                     else
                        ScreenSelector.SelectedValue = 0; // Primärer
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Fehler beim Laden der Bildschirme: {ex.Message}");
            }
        }
        
        private void ScreenSelector_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ScreenSelector.SelectedValue is int index)
            {
                App.AppSettings.TargetScreenIndex = index;
            }
        }

        private void UpdateImageSizeSliderRange()
        {
            try
            {
                // Den größten verfügbaren Bildschirm ermitteln
                int maxScreenDimension = 0;
                foreach (var screen in WinForms.Screen.AllScreens)
                {
                    maxScreenDimension = Math.Max(maxScreenDimension,
                        Math.Max(screen.WorkingArea.Width, screen.WorkingArea.Height));
                }

                // Wenn kein Bildschirm gefunden wurde oder ein Fehler auftritt, Standardwerte verwenden
                if (maxScreenDimension <= 0)
                {
                    maxScreenDimension = 1920; // Typische Full-HD-Auflösung als Fallback
                }

                // Slider-Grenzen anpassen
                ImageSizeSlider.Minimum = 100; // Minimale Größe bleibt gleich
                ImageSizeSlider.Maximum = maxScreenDimension; // Maximale Größe ist die größte Bildschirmdimension

                // Slider-Schrittweite anpassen (je nach Bildschirmgröße)
                ImageSizeSlider.SmallChange = Math.Max(10, maxScreenDimension / 100);
                ImageSizeSlider.LargeChange = Math.Max(50, maxScreenDimension / 20);

                // Falls der aktuelle Wert außerhalb der neuen Grenzen liegt, anpassen
                if (ImageSizeSlider.Value > ImageSizeSlider.Maximum)
                {
                    App.AppSettings.MaxImageSize = ImageSizeSlider.Maximum;
                }
                else if (ImageSizeSlider.Value < ImageSizeSlider.Minimum)
                {
                    App.AppSettings.MaxImageSize = ImageSizeSlider.Minimum;
                }

                StatusText.Text = $"Slider angepasst: 100-{maxScreenDimension}px";
            }
            catch (Exception ex)
            {
                // Im Fehlerfall Standardwerte verwenden
                System.Diagnostics.Debug.WriteLine($"Fehler bei der Slider-Anpassung: {ex.Message}");
                ImageSizeSlider.Minimum = 100;
                ImageSizeSlider.Maximum = 1000;
            }
        }
        public void RefreshImageSizeSlider()
        {
            UpdateImageSizeSliderRange();
        }

        private void Window_DragEnter(object sender, DragEventArgs e)
        {
            // Prüfen, ob die gezogenen Dateien Bilder sind
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                string[] files = (string[])e.Data.GetData(DataFormats.FileDrop);
                if (files != null && files.Any(IsImageFile))
                {
                    e.Effects = DragDropEffects.Copy;
                    DropIndicator.Visibility = Visibility.Visible;
                    DropIndicator.Opacity = 0.7;
                }
                else
                {
                    e.Effects = DragDropEffects.None;
                }
            }
            else
            {
                e.Effects = DragDropEffects.None;
            }

            e.Handled = true;
        }

        private void Window_DragOver(object sender, DragEventArgs e)
        {
            // Gleiche Logik wie bei DragEnter
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                string[] files = (string[])e.Data.GetData(DataFormats.FileDrop);
                if (files != null && files.Any(IsImageFile))
                {
                    e.Effects = DragDropEffects.Copy;
                }
                else
                {
                    e.Effects = DragDropEffects.None;
                }
            }
            else
            {
                e.Effects = DragDropEffects.None;
            }

            e.Handled = true;
        }

        private void Window_Drop(object sender, DragEventArgs e)
        {
            // Drag-Indikator ausblenden
            DropIndicator.Visibility = Visibility.Collapsed;

            // Prüfen ob Dateien gedroppt wurden
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                string[] files = (string[])e.Data.GetData(DataFormats.FileDrop);

                if (files != null)
                {
                    // Nur Bilddateien filtern und hinzufügen
                    var imageFiles = files.Where(IsImageFile).ToList();

                    foreach (string file in imageFiles)
                    {
                        App.AddImage(file);
                    }

                    if (imageFiles.Count > 0)
                    {
                        StatusText.Text = $"{imageFiles.Count} Bild(er) hinzugefügt";
                    }
                }
            }

            e.Handled = true;
        }

        // Hilfsmethode zum Überprüfen, ob eine Datei ein Bild ist
        private bool IsImageFile(string filePath)
        {
            if (!File.Exists(filePath))
                return false;

            string extension = Path.GetExtension(filePath).ToLower();
            return extension == ".jpg" || extension == ".jpeg" ||
                   extension == ".png" || extension == ".bmp" ||
                   extension == ".gif";
        }
        private void AddImageButton_Click(object sender, RoutedEventArgs e)
        {
            var openFileDialog = new Microsoft.Win32.OpenFileDialog
            {
                Filter = "Bilddateien|*.jpg;*.jpeg;*.png;*.bmp;*.gif",
                Multiselect = true
            };

            if (openFileDialog.ShowDialog() == true)
            {
                foreach (var filename in openFileDialog.FileNames)
                {
                    App.AddImage(filename);
                }
            }
        }

        private void RemoveImageButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag is ImageItem imageItem)
            {
                App.Images.Remove(imageItem);
            }
        }
        private void RepositionImageButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag is ImageItem imageItem)
            {
                App.RepositionImage(imageItem);
                StatusText.Text = $"Bild '{imageItem.FileName}' neu positioniert";
            }
        }
        private void HideAllButton_Click(object sender, RoutedEventArgs e)
        {
            foreach (var image in App.Images)
            {
                image.IsVisible = false;
            }
        }

        private void ShowAllButton_Click(object sender, RoutedEventArgs e)
        {
            foreach (var image in App.Images)
            {
                image.IsVisible = true;
            }
        }

        private void RemoveAllButton_Click(object sender, RoutedEventArgs e)
        {
            if (WPF.MessageBox.Show("Möchten Sie wirklich alle Bilder entfernen?", "Bestätigen",
                                MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
            {
                App.Images.Clear();
            }
        }
        private void ExitButton_Click(object sender, RoutedEventArgs e)
        {
            App.ShutdownApplication();
        }
        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            // Prüfen, ob die Anwendung bewusst geschlossen wird
            if (App.IsShuttingDown)
            {
                // Normal fortfahren (nicht abbrechen)
                return;
            }

            // Statt Beenden nur minimieren und in System-Tray ablegen
            e.Cancel = true;
            WindowState = WindowState.Minimized;
            Hide();
        }

        private void ContextMenu_CheckedChanged(object sender, RoutedEventArgs e)
        {
            if (App.AppSettings.InstallContextMenu)
            {
                App.InstallContextMenu();
            }
            else
            {
                App.UninstallContextMenu();
            }
        }

        private void ResetFogButton_Click(object sender, RoutedEventArgs e)
        {
            if (App.OverlayWindow != null)
            {
                App.OverlayWindow.ResetFogOfWar();
                StatusText.Text = "Fog of War zurückgesetzt";
            }
        }

    }
}