using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace diplomnarabotki.ViewModels
{
    public class TravelStringViewModel : INotifyPropertyChanged
    {
        private int _id;
        private int _from;
        private int _to;
        private string _description = "";
        private string _color = "#ed8936";
        private double _width = 2;

        public int Id
        {
            get => _id;
            set { _id = value; OnPropertyChanged(); }
        }

        public int From
        {
            get => _from;
            set { _from = value; OnPropertyChanged(); }
        }

        public int To
        {
            get => _to;
            set { _to = value; OnPropertyChanged(); }
        }

        public string Description
        {
            get => _description;
            set { _description = value; OnPropertyChanged(); }
        }

        public string Color
        {
            get => _color;
            set { _color = value; OnPropertyChanged(); }
        }

        public double Width
        {
            get => _width;
            set { _width = value; OnPropertyChanged(); }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}