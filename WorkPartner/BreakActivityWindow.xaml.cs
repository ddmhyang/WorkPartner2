using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;

namespace WorkPartner
{
    public partial class BreakActivityWindow : Window
    {
        public List<string> SelectedActivities { get; private set; }

        public BreakActivityWindow()
        {
            InitializeComponent();
            SelectedActivities = new List<string>();
        }

        private void ConfirmButton_Click(object sender, RoutedEventArgs e)
        {
            foreach (var child in ActivityStackPanel.Children)
            {
                if (child is CheckBox chk && chk.IsChecked == true)
                {
                    SelectedActivities.Add(chk.Content.ToString());
                }
            }
            this.DialogResult = true;
            this.Close();
        }
    }
}