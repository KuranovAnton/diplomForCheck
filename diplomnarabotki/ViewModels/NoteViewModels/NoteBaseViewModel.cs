using diplomnarabotki.Models.Enums;
using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace diplomnarabotki.ViewModels.NoteViewModels
{
    public abstract class NoteBaseViewModel : INotifyPropertyChanged
    {
        private string _title = string.Empty;
        private DateTime _createdDate;
        private NoteType _noteType;
        private NotificationViewModel? _notification;

        public string Title
        {
            get => _title;
            set { _title = value; OnPropertyChanged(); }
        }

        public DateTime CreatedDate
        {
            get => _createdDate;
            set { _createdDate = value; OnPropertyChanged(); }
        }

        public NoteType NoteType
        {
            get => _noteType;
            set { _noteType = value; OnPropertyChanged(); }
        }

        public NotificationViewModel? Notification
        {
            get => _notification;
            set { _notification = value; OnPropertyChanged(); }
        }

        public NoteBaseViewModel()
        {
            CreatedDate = DateTime.Now;
            Notification = new NotificationViewModel();
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}