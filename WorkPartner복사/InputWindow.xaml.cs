using System.Windows;
using System.Windows.Input;

namespace WorkPartner
{
    public partial class InputWindow : Window
    {
        public string ResponseText { get; private set; }

        public InputWindow(string prompt, string defaultText = "")
        {
            InitializeComponent();
            PromptText.Text = prompt;
            InputTextBox.Text = defaultText;
            InputTextBox.Focus();
            InputTextBox.SelectAll();
        }

        private void OkButton_Click(object sender, RoutedEventArgs e)
        {
            ResponseText = InputTextBox.Text;
            this.DialogResult = true;
            this.Close();
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = false;
            this.Close();
        }

        private void InputTextBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                OkButton_Click(sender, e);
            }
        }
    }
}
