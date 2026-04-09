using diplomnarabotki.Models.Enums;

namespace diplomnarabotki.ViewModels.NoteViewModels
{
    public class TextNoteViewModel : NoteBaseViewModel
    {
        private string _content = string.Empty;

        public string Content
        {
            get => _content;
            set { _content = value; OnPropertyChanged(); }
        }

        public TextNoteViewModel()
        {
            NoteType = NoteType.Text;
        }
    }
}