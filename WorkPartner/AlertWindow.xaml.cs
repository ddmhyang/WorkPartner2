using System.Windows;
using System.Windows.Input;

namespace WorkPartner
{
    public partial class AlertWindow : Window
    {
        public AlertWindow(string message, string title)
        {
            InitializeComponent();
            MessageTextBlock.Text = message;
            Title = title;
        }

        private void OkButton_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }

        private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ButtonState == MouseButtonState.Pressed)
            {
                this.DragMove();
            }
        }
    }
}
