using System;
using System.IO;
using System.Windows;
using System.Collections.ObjectModel;
using System.Text.Json;
using System.Linq;
using System.Windows.Controls;
using System.Collections.Generic;

namespace WorkPartner
{
    public partial class MemoWindow : Window
    {
        private readonly string _memoFilePath = DataManager.MemosFilePath;
        private ObservableCollection<MemoItem> _allMemos; // 모든 메모를 저장
        public ObservableCollection<MemoItem> VisibleMemos { get; set; } // 화면에 보여줄 메모만 저장
        private bool _isSaving = false;

        public MemoWindow()
        {
            InitializeComponent();
            _allMemos = new ObservableCollection<MemoItem>();
            VisibleMemos = new ObservableCollection<MemoItem>();
            MemoListBox.ItemsSource = VisibleMemos;
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            LoadMemos();
            MemoCalendar.SelectedDate = DateTime.Today; // 오늘 날짜를 기본으로 선택
            FilterMemosByDate(DateTime.Today);
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            SaveMemos();
        }

        private void LoadMemos()
        {
            if (File.Exists(_memoFilePath))
            {
                var json = File.ReadAllText(_memoFilePath);
                var loadedMemos = JsonSerializer.Deserialize<ObservableCollection<MemoItem>>(json);
                if (loadedMemos != null)
                {
                    _allMemos = loadedMemos;
                }
            }
        }

        private void SaveMemos()
        {
            var options = new JsonSerializerOptions { WriteIndented = true };
            var json = JsonSerializer.Serialize(_allMemos, options);
            File.WriteAllText(_memoFilePath, json);
        }

        // [메서드 추가] 날짜에 따라 메모를 필터링
        private void FilterMemosByDate(DateTime? selectedDate)
        {
            VisibleMemos.Clear();
            if (selectedDate == null) return;

            var filtered = _allMemos.Where(m => m.CreatedDate.Date == selectedDate.Value.Date).ToList();
            foreach (var memo in filtered)
            {
                VisibleMemos.Add(memo);
            }

            if (VisibleMemos.Any())
            {
                MemoListBox.SelectedIndex = 0;
            }
            else // 해당 날짜에 메모가 없으면 내용 초기화
            {
                MemoTitleTextBox.Text = "";
                MemoContentTextBox.Text = "";
            }
        }

        private void NewMemoButton_Click(object sender, RoutedEventArgs e)
        {
            var selectedDate = MemoCalendar.SelectedDate ?? DateTime.Today;
            var newMemo = new MemoItem { Title = "새 제목", CreatedDate = selectedDate };
            _allMemos.Add(newMemo);
            VisibleMemos.Add(newMemo); // 현재 보이는 목록에도 추가
            MemoListBox.SelectedItem = newMemo;
            MemoTitleTextBox.Focus();
        }

        private void DeleteMemoButton_Click(object sender, RoutedEventArgs e)
        {
            if (MemoListBox.SelectedItem is MemoItem selectedMemo)
            {
                if (MessageBox.Show($"'{selectedMemo.Title}' 메모를 삭제하시겠습니까?", "삭제 확인", MessageBoxButton.YesNo) == MessageBoxResult.Yes)
                {
                    _allMemos.Remove(selectedMemo);
                    VisibleMemos.Remove(selectedMemo); // 보이는 목록에서도 삭제
                }
            }
        }

        private void MemoListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!_isSaving && MemoListBox.SelectedItem is MemoItem selectedMemo)
            {
                MemoTitleTextBox.Text = selectedMemo.Title;
                MemoContentTextBox.Text = selectedMemo.Content;
            }
        }

        private void MemoContentTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (MemoListBox.SelectedItem is MemoItem selectedMemo)
            {
                _isSaving = true;
                selectedMemo.Content = MemoContentTextBox.Text;
                _isSaving = false;
            }
        }

        private void MemoTitleTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (MemoListBox.SelectedItem is MemoItem selectedMemo)
            {
                _isSaving = true;
                selectedMemo.Title = MemoTitleTextBox.Text;
                // [수정] 제목이 바뀌면 ListBox의 아이템도 실시간으로 새로고침
                var temp = MemoListBox.SelectedItem;
                MemoListBox.ItemsSource = null;
                MemoListBox.ItemsSource = VisibleMemos;
                MemoListBox.SelectedItem = temp;
                _isSaving = false;
            }
        }

        // [이벤트 핸들러 추가] 달력 날짜 선택 변경 시
        private void MemoCalendar_SelectedDatesChanged(object sender, SelectionChangedEventArgs e)
        {
            FilterMemosByDate(MemoCalendar.SelectedDate);
        }

        // MemoWindow.xaml.cs 파일

        // '어제' 버튼 클릭 시 실행될 메서드
        private void PrevDayButton_Click(object sender, RoutedEventArgs e)
        {
            if (MemoCalendar.SelectedDate.HasValue)
            {
                MemoCalendar.SelectedDate = MemoCalendar.SelectedDate.Value.AddDays(-1);
            }
        }

        // '오늘' 버튼 클릭 시 실행될 메서드
        private void TodayButton_Click(object sender, RoutedEventArgs e)
        {
            MemoCalendar.SelectedDate = DateTime.Today;
        }

        // '내일' 버튼 클릭 시 실행될 메서드
        private void NextDayButton_Click(object sender, RoutedEventArgs e)
        {
            if (MemoCalendar.SelectedDate.HasValue)
            {
                MemoCalendar.SelectedDate = MemoCalendar.SelectedDate.Value.AddDays(1);
            }
        }
    }
}