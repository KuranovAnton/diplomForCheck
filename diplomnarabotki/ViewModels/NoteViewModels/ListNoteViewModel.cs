using diplomnarabotki.Models;
using diplomnarabotki.Models.Enums;
using System.Collections.ObjectModel;

namespace diplomnarabotki.ViewModels.NoteViewModels
{
    public class ListNoteViewModel : NoteBaseViewModel
    {
        private ObservableCollection<ListItemModel> _items = new();

        public ObservableCollection<ListItemModel> Items
        {
            get => _items;
            set { _items = value; OnPropertyChanged(); }
        }

        public ListNoteViewModel()
        {
            NoteType = NoteType.List;
            Items = new ObservableCollection<ListItemModel>();
        }
    }
}