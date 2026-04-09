using diplomnarabotki.ViewModels.NoteViewModels;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace diplomnarabotki.ViewModels
{
    public class TravelViewModel : INotifyPropertyChanged
    {
        private int _id;
        private string _name = string.Empty;
        private string _route = string.Empty;
        private ObservableCollection<NoteBaseViewModel> _notes = new();
        private ObservableCollection<NoteBaseViewModel> _pinnedNotes = new();
        private ObservableCollection<RoutePointViewModel> _routePoints = new();
        private ObservableCollection<TravelStringViewModel> _travelStrings = new();

        public int Id
        {
            get => _id;
            set { _id = value; OnPropertyChanged(); }
        }

        public string Name
        {
            get => _name;
            set { _name = value; OnPropertyChanged(); }
        }

        public string Route
        {
            get => _route;
            set { _route = value; OnPropertyChanged(); }
        }

        public ObservableCollection<NoteBaseViewModel> Notes
        {
            get => _notes;
            set { _notes = value; OnPropertyChanged(); }
        }

        public ObservableCollection<NoteBaseViewModel> PinnedNotes
        {
            get => _pinnedNotes;
            set { _pinnedNotes = value; OnPropertyChanged(); }
        }

        public ObservableCollection<RoutePointViewModel> RoutePoints
        {
            get => _routePoints;
            set { _routePoints = value; OnPropertyChanged(); }
        }

        public ObservableCollection<TravelStringViewModel> TravelStrings
        {
            get => _travelStrings;
            set { _travelStrings = value; OnPropertyChanged(); }
        }

        public TravelViewModel()
        {
            Notes = new ObservableCollection<NoteBaseViewModel>();
            PinnedNotes = new ObservableCollection<NoteBaseViewModel>();
            RoutePoints = new ObservableCollection<RoutePointViewModel>();
            TravelStrings = new ObservableCollection<TravelStringViewModel>();
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}