using diplomnarabotki.Models;
using diplomnarabotki.Models.Enums;
using System.Collections.ObjectModel;

namespace diplomnarabotki.ViewModels.NoteViewModels
{
    public class ChecklistNoteViewModel : NoteBaseViewModel
    {
        private ObservableCollection<ChecklistItemModel> _items = new();

        public ObservableCollection<ChecklistItemModel> Items
        {
            get => _items;
            set { _items = value; OnPropertyChanged(); }
        }

        public ChecklistNoteViewModel()
        {
            NoteType = NoteType.Checklist;
            Items = new ObservableCollection<ChecklistItemModel>();
        }
    }
}