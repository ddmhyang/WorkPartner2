// 파일: MemoWindow.xaml.cs (전체 교체)
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace WorkPartner
{
    public partial class MemoWindow : Window
    {
        // 1. '두뇌' ViewModel을 저장할 변수
        private readonly ViewModels.DashboardViewModel _viewModel;

        // 2. [수정] 생성자가 '두뇌' ViewModel을 전달받도록 변경
        public MemoWindow(ViewModels.DashboardViewModel viewModel)
        {
            InitializeComponent();

            _viewModel = viewModel; // 전달받은 '두뇌' 저장

            // 3. [수정] '두뇌'의 AllMemos 컬렉션을 ListBox에 바로 연결
            MemoListBox.ItemsSource = _viewModel.AllMemos;

            // 4. [삭제] LoadMemos()는 '두뇌'가 이미 처리했으므로 삭제
            //    Loaded += (s, e) => LoadMemos();

            // 5. [수정] 텍스트가 바뀔 때마다 저장하는 대신, 창이 닫힐 때만 저장
            this.Closing += Window_Closing;
        }

        // 8. [수정] NewMemoButton_Click (사용자님이 알려주신 정확한 이름)
        private void NewMemoButton_Click(object sender, RoutedEventArgs e)
        {
            var newMemo = new MemoItem();

            // '두뇌'의 리스트에 추가
            _viewModel.AllMemos.Add(newMemo);

            MemoListBox.SelectedItem = newMemo;
            MemoContentTextBox.Focus(); // (이름 수정)
        }

        // 9. [수정] DeleteMemoButton_Click
        private void DeleteMemoButton_Click(object sender, RoutedEventArgs e)
        {
            if (MemoListBox.SelectedItem is MemoItem memo)
            {
                // '두뇌'의 리스트에서 삭제
                _viewModel.AllMemos.Remove(memo);
            }
        }

        // 10. [수정] MemoContentTextBox_TextChanged (사용자님이 알려주신 정확한 이름)
        private void MemoContentTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            // 텍스트가 변경될 때마다 저장하던 로직을 삭제 (성능 향상)
            // SaveMemos(); // 👈 [삭제]
        }

        // 11. [수정] 핀 고정 로직
        private void PinCheckBox_Checked(object sender, RoutedEventArgs e)
        {
            if (MemoListBox.SelectedItem is MemoItem selectedMemo)
            {
                // '두뇌'의 다른 모든 메모를 핀 해제합니다.
                foreach (var memo in _viewModel.AllMemos.Where(m => m != selectedMemo))
                {
                    memo.IsPinned = false;
                }
            }
        }

        // 12. [수정] 닫힐 때 '두뇌'를 통해 저장
        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            // '두뇌'에게 메모 저장을 요청
            _viewModel?.SaveMemos();
        }
        // 파일: MemoWindow.xaml.cs

        // ▼▼▼ [CS0103 오류 해결] 이 메서드 전체를 교체하세요 ▼▼▼
        private void MemoListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (MemoListBox.SelectedItem is MemoItem memo)
            {
                // 1. [수정] MemoContentGrid -> EditorPanel (XAML 이름)
                EditorPanel.DataContext = memo;
                // 2. [추가] 메모가 선택되면 에디터 패널을 활성화
                EditorPanel.IsEnabled = true;
            }
            else
            {
                // 1. [수정] MemoContentGrid -> EditorPanel (XAML 이름)
                EditorPanel.DataContext = null;
                // 2. [추가] 선택된 메모가 없으면 비활성화
                EditorPanel.IsEnabled = false;
            }
        }
    }
}