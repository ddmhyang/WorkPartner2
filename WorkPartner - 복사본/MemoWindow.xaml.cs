using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;

namespace WorkPartner
{
    public partial class MemoWindow : Window
    {
        private readonly string _memosFilePath = DataManager.MemosFilePath;
        public ObservableCollection<MemoItem> AllMemos { get; set; }

        public MemoWindow()
        {
            InitializeComponent();
            LoadMemos();
            MemoListBox.ItemsSource = AllMemos;
        }

        private void LoadMemos()
        {
            if (File.Exists(_memosFilePath))
            {
                var json = File.ReadAllText(_memosFilePath);
                AllMemos = JsonSerializer.Deserialize<ObservableCollection<MemoItem>>(json) ?? new ObservableCollection<MemoItem>();
            }
            else
            {
                AllMemos = new ObservableCollection<MemoItem>();
            }
        }

        private void SaveMemos()
        {
            var options = new JsonSerializerOptions { WriteIndented = true, Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping };
            var json = JsonSerializer.Serialize(AllMemos, options);
            File.WriteAllText(_memosFilePath, json);
        }

        private void MemoListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (MemoListBox.SelectedItem is MemoItem selectedMemo)
            {
                EditorPanel.IsEnabled = true;
                MemoContentTextBox.Text = selectedMemo.Content;
                PinCheckBox.IsChecked = selectedMemo.IsPinned;
            }
            else
            {
                EditorPanel.IsEnabled = false;
                MemoContentTextBox.Text = "";
                PinCheckBox.IsChecked = false;
            }
        }

        private void MemoContentTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (MemoListBox.SelectedItem is MemoItem selectedMemo)
            {
                selectedMemo.Content = MemoContentTextBox.Text;
            }
        }

        private void PinCheckBox_Click(object sender, RoutedEventArgs e)
        {
            if (MemoListBox.SelectedItem is MemoItem selectedMemo)
            {
                bool isNowPinned = PinCheckBox.IsChecked ?? false;

                // 모든 메모의 고정 상태를 해제
                foreach (var memo in AllMemos)
                {
                    memo.IsPinned = false;
                }

                // 현재 선택된 메모만 고정 (체크된 경우)
                selectedMemo.IsPinned = isNowPinned;

                // UI 즉시 새로고침
                MemoListBox.Items.Refresh();
            }
        }

        private void NewMemoButton_Click(object sender, RoutedEventArgs e)
        {
            var newMemo = new MemoItem { Content = "새 메모" };
            AllMemos.Insert(0, newMemo);
            MemoListBox.SelectedItem = newMemo;
            MemoContentTextBox.Focus();
        }

        private void DeleteMemoButton_Click(object sender, RoutedEventArgs e)
        {
            if (MemoListBox.SelectedItem is MemoItem selectedMemo)
            {
                if (MessageBox.Show("메모를 삭제하시겠습니까?", "확인", MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes)
                {
                    AllMemos.Remove(selectedMemo);
                }
            }
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            SaveMemos();
        }
    }
}