using System;
using System.ComponentModel;
using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using DMTool;
using WinForms = System.Windows.Forms;
using WPF = System.Windows;

namespace DMTool
{
    public class ImageItem : INotifyPropertyChanged
    {
        private string _path;
        private BitmapImage _image;
        private bool _isVisible = true;
        private double _rotation;
        private double _posX;
        private double _posY;
        private double _scale;

        public string Path
        {
            get { return _path; }
            set
            {
                _path = value;
                OnPropertyChanged(nameof(Path));
                OnPropertyChanged(nameof(FileName));
            }
        }

        public string FileName => System.IO.Path.GetFileName(Path);

        public BitmapImage Image
        {
            get { return _image; }
            set
            {
                _image = value;
                OnPropertyChanged(nameof(Image));
            }
        }

        public bool IsVisible
        {
            get { return _isVisible; }
            set
            {
                _isVisible = value;
                OnPropertyChanged(nameof(IsVisible));
            }
        }

        public double Rotation
        {
            get { return _rotation; }
            set
            {
                _rotation = value;
                OnPropertyChanged(nameof(Rotation));
            }
        }

        public double PosX
        {
            get { return _posX; }
            set
            {
                _posX = value;
                OnPropertyChanged(nameof(PosX));
            }
        }

        public double PosY
        {
            get { return _posY; }
            set
            {
                _posY = value;
                OnPropertyChanged(nameof(PosY));
            }
        }

        public double Scale
        {
            get { return _scale; }
            set
            {
                _scale = value;
                OnPropertyChanged(nameof(Scale));
            }
        }

        public void LoadImage()
        {
            if (File.Exists(Path))
            {
                try
                {
                    var image = new BitmapImage();
                    image.BeginInit();
                    image.CacheOption = BitmapCacheOption.OnLoad;
                    image.UriSource = new Uri(Path);
                    image.EndInit();
                    image.Freeze();
                    Image = image;
                }
                catch (Exception)
                {
                    // Fehlerbehandlung bei Bildladefehlern
                }
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}