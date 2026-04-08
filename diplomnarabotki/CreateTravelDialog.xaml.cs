using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace diplomnarabotki
{
    public partial class CreateTravelDialog : Window
    {
        public string TravelName { get; private set; }
        public string TravelRoute { get; private set; }
        public bool IsCreated { get; private set; }

        public CreateTravelDialog()
        {
            InitializeComponent();
            TxtTravelName.Focus();
        }

        private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ButtonState == MouseButtonState.Pressed)
            {
                this.DragMove();
            }
        }

        private void BtnClose_Click(object sender, RoutedEventArgs e)
        {
            IsCreated = false;
            DialogResult = false;
            Close();
        }

        private void TxtTravelName_TextChanged(object sender, TextChangedEventArgs e)
        {
            BtnCreate.IsEnabled = !string.IsNullOrWhiteSpace(TxtTravelName.Text);
        }

        private void BtnCreate_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(TxtTravelName.Text))
            {
                MessageBox.Show("Введите название путешествия!", "Внимание",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            TravelName = TxtTravelName.Text.Trim();
            TravelRoute = TxtRoute.Text?.Trim() ?? "";
            IsCreated = true;

            DialogResult = true;
            Close();
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            IsCreated = false;
            DialogResult = false;
            Close();
        }
    }
}