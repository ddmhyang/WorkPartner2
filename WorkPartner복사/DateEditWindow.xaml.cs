using System;
using System.Windows;

namespace WorkPartner
{
    public partial class DateEditWindow : Window
    {
        public DateTime SelectedDate { get; private set; }

        public DateEditWindow(DateTime currentDate)
        {
            InitializeComponent();
            DatePicker.SelectedDate = currentDate;
            DatePicker.DisplayDate = currentDate;
        }

        private void OkButton_Click(object sender, RoutedEventArgs e)
        {
            if (DatePicker.SelectedDate.HasValue)
            {
                SelectedDate = DatePicker.SelectedDate.Value;
                this.DialogResult = true;
            }
        }
    }
}
