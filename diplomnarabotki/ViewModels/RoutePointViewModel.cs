using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace diplomnarabotki.ViewModels
{
    public class RoutePointViewModel : INotifyPropertyChanged
    {
        private int _id;
        private double _latitude;
        private double _longitude;
        private string _title = string.Empty;
        private int _order;
        private string _iconEmoji = "📍";
        private string _iconType = "default";
        private string _description = "";
        private string _iconColor = "#e2e8f0";
        private int _iconSize = 36;
        private string _status = "planned";
        private string _photoUrl = "";
        private string _visitDate = "";

        public int Id
        {
            get => _id;
            set { _id = value; OnPropertyChanged(); }
        }

        public double Latitude
        {
            get => _latitude;
            set { _latitude = value; OnPropertyChanged(); }
        }

        public double Longitude
        {
            get => _longitude;
            set { _longitude = value; OnPropertyChanged(); }
        }

        public string Title
        {
            get => _title;
            set { _title = value; OnPropertyChanged(); }
        }

        public int Order
        {
            get => _order;
            set { _order = value; OnPropertyChanged(); }
        }

        public string IconEmoji
        {
            get => _iconEmoji;
            set { _iconEmoji = value; OnPropertyChanged(); }
        }

        public string IconType
        {
            get => _iconType;
            set { _iconType = value; OnPropertyChanged(); }
        }

        public string Description
        {
            get => _description;
            set { _description = value; OnPropertyChanged(); }
        }

        public string IconColor
        {
            get => _iconColor;
            set { _iconColor = value; OnPropertyChanged(); }
        }

        public int IconSize
        {
            get => _iconSize;
            set { _iconSize = value; OnPropertyChanged(); }
        }

        public string Status
        {
            get => _status;
            set { _status = value; OnPropertyChanged(); }
        }

        public string PhotoUrl
        {
            get => _photoUrl;
            set { _photoUrl = value; OnPropertyChanged(); }
        }

        public string VisitDate
        {
            get => _visitDate;
            set { _visitDate = value; OnPropertyChanged(); }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}