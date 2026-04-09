using diplomnarabotki.Models.Enums;
using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace diplomnarabotki.ViewModels
{
    public class NotificationViewModel : INotifyPropertyChanged
    {
        private DateTime _reminderTime;
        private ReminderRepeatType _repeatType;
        private NotificationSound _sound;
        private bool _isEnabled;
        private DateTime? _lastNotified;

        public DateTime ReminderTime
        {
            get => _reminderTime;
            set { _reminderTime = value; OnPropertyChanged(); }
        }

        public ReminderRepeatType RepeatType
        {
            get => _repeatType;
            set { _repeatType = value; OnPropertyChanged(); }
        }

        public NotificationSound Sound
        {
            get => _sound;
            set { _sound = value; OnPropertyChanged(); }
        }

        public bool IsEnabled
        {
            get => _isEnabled;
            set { _isEnabled = value; OnPropertyChanged(); }
        }

        public DateTime? LastNotified
        {
            get => _lastNotified;
            set { _lastNotified = value; OnPropertyChanged(); }
        }

        public NotificationViewModel()
        {
            ReminderTime = DateTime.Now.AddHours(1);
            RepeatType = ReminderRepeatType.None;
            Sound = NotificationSound.Default;
            IsEnabled = false;
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}