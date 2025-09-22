using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Threading;

namespace WorkPartner
{
    public partial class MemoWindow : Window
    {
        private readonly string _memosFilePath = DataManager.MemosFilePath;
        public ObservableCollection<MemoItem> AllMemos { get; set; }
        public ObservableCollection<MemoItem> MemosForSelectedDate { get; set; }
        private MemoItem _currentMemo;
        private bool _isSaving = false;

        public MemoWindow()
        {
            InitializeComponent();
            AllMemos = new ObservableCollection<MemoItem>();
            MemosForSelectedDate = new ObservableCollection<MemoItem>();
            MemoListBox.ItemsSource = MemosForSelectedDate;
        }

        #region Window Events
        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            LoadMemos();
            MemoCalendar.SelectedDate = DateTime.Today;
            FilterMemos(DateTime.Today);
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            // Force update of the current binding target before saving
            if (_currentMemo != null)
            {
                _currentMemo.Title = MemoTitleTextBox.Text;
                _currentMemo.Content = MemoContentTextBox.Text;
            }
            SaveMemosToFile();
        }
        #endregion

        #region Data Handling
        private void LoadMemos()
        {
            if (File.Exists(_memosFilePath))
            {
                try
                {
                    var json = File.ReadAllText(_memosFilePath);
                    if (string.IsNullOrWhiteSpace(json))
                    {
                        AllMemos = new ObservableCollection<MemoItem>();
                        return;
                    }
                    var loadedMemos = JsonSerializer.Deserialize<ObservableCollection<MemoItem>>(json);
                    if (loadedMemos != null) AllMemos = loadedMemos;
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"메모를 불러오는 중 오류 발생: {ex.Message}");
                    AllMemos = new ObservableCollection<MemoItem>();
                }
            }
        }

        private void SaveChanges()
        {
            if (_currentMemo != null)
            {
                // No need to check for empty text here, let the user save empty notes if they want
                _currentMemo.Title = MemoTitleTextBox.Text;
                _currentMemo.Content = MemoContentTextBox.Text;
            }
        }

        private void SaveMemosToFile()
        {
            if (_isSaving) return;
            _isSaving = true;
            try
            {
                SaveChanges(); // Make sure current changes are captured
                var options = new JsonSerializerOptions { WriteIndented = true, Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping };
                var json = JsonSerializer.Serialize(AllMemos, options);
                File.WriteAllText(_memosFilePath, json);
            }
            finally
            {
                _isSaving = false;
            }
        }
        #endregion

        #region UI Event Handlers
        private void FilterMemos(DateTime date)
        {
            MemosForSelectedDate.Clear();
            var filtered = AllMemos.Where(m => m.Timestamp.Date == date.Date).OrderByDescending(m => m.Timestamp);
            foreach (var memo in filtered)
            {
                MemosForSelectedDate.Add(memo);
            }
            if (MemosForSelectedDate.Any())
            {
                MemoListBox.SelectedItem = MemosForSelectedDate.First();
            }
            else
            {
                ClearEditor();
            }
        }

        private void DisplayMemo(MemoItem memo)
        {
            _currentMemo = memo;
            if (memo != null)
            {
                MemoTitleTextBox.Text = memo.Title;
                MemoContentTextBox.Text = memo.Content;
                MemoTitleTextBox.IsEnabled = true;
                MemoContentTextBox.IsEnabled = true;
            }
            else
            {
                ClearEditor();
            }
        }

        private void ClearEditor()
        {
            _currentMemo = null;
            MemoTitleTextBox.Text = "";
            MemoContentTextBox.Text = "";
            MemoTitleTextBox.IsEnabled = false;
            MemoContentTextBox.IsEnabled = false;
        }

        private void MemoCalendar_SelectedDatesChanged(object sender, SelectionChangedEventArgs e)
        {
            if (MemoCalendar.SelectedDate.HasValue)
            {
                SaveChanges();
                SaveMemosToFile(); // Save changes before switching date
                FilterMemos(MemoCalendar.SelectedDate.Value);
            }
        }

        private void MemoListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_isSaving) return;

            // Save the currently displayed memo before switching
            SaveChanges();

            if (e.AddedItems.Count > 0 && e.AddedItems[0] is MemoItem selectedMemo)
            {
                DisplayMemo(selectedMemo);
            }
            else if (e.RemovedItems.Count > 0 && MemosForSelectedDate.Count == 0)
            {
                ClearEditor();
            }
        }


        private void MemoTitleTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_currentMemo != null)
            {
                _currentMemo.Title = MemoTitleTextBox.Text;
                // Refresh the listbox item to show the new title
                ICollectionView view = CollectionViewSource.GetDefaultView(MemoListBox.ItemsSource);
                view.Refresh();
            }
        }

        private void MemoContentTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_currentMemo != null)
            {
                _currentMemo.Content = MemoContentTextBox.Text;
            }
        }

        private void NewMemoButton_Click(object sender, RoutedEventArgs e)
        {
            SaveChanges(); // Save any pending changes to the current memo
            DateTime selectedDate = MemoCalendar.SelectedDate ?? DateTime.Now;
            var newMemo = new MemoItem { Title = "새 메모", Content = "", Timestamp = selectedDate };
            AllMemos.Add(newMemo);
            FilterMemos(selectedDate);
            MemoListBox.SelectedItem = newMemo;

            // UI가 업데이트된 후 포커스를 설정하고 텍스트를 선택하도록 Dispatcher를 사용합니다.
            Dispatcher.BeginInvoke(new Action(() =>
            {
                MemoTitleTextBox.Focus();
                MemoTitleTextBox.SelectAll();
            }), DispatcherPriority.Input);
        }

        private void DeleteMemoButton_Click(object sender, RoutedEventArgs e)
        {
            if (MemoListBox.SelectedItem is MemoItem selectedMemo)
            {
                if (MessageBox.Show("정말로 이 메모를 삭제하시겠습니까?", "삭제 확인", MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes)
                {
                    AllMemos.Remove(selectedMemo);
                    FilterMemos(selectedMemo.Timestamp.Date); // Re-filter for the same date
                    SaveMemosToFile(); // Save changes after deletion
                }
            }
        }
        #endregion

        #region Date Navigation
        private void PrevDayButton_Click(object sender, RoutedEventArgs e)
        {
            if (MemoCalendar.SelectedDate.HasValue)
                MemoCalendar.SelectedDate = MemoCalendar.SelectedDate.Value.AddDays(-1);
        }

        private void TodayButton_Click(object sender, RoutedEventArgs e)
        {
            MemoCalendar.SelectedDate = DateTime.Today;
        }

        private void NextDayButton_Click(object sender, RoutedEventArgs e)
        {
            if (MemoCalendar.SelectedDate.HasValue)
                MemoCalendar.SelectedDate = MemoCalendar.SelectedDate.Value.AddDays(1);
        }
        #endregion

        #region Window Chrome
        private void TitleBar_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left)
                this.DragMove();
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }
        #endregion
    }
}

