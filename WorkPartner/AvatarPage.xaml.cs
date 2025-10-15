using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace WorkPartner
{
    public partial class AvatarPage : Page
    {
        // 저장된 캐릭터 외형 정보
        private CharacterAppearance savedAppearance;
        // 현재 꾸미고 있는 (미리보기) 외형 정보
        private CharacterAppearance previewAppearance;

        // 상점의 모든 아이템 리스트
        private List<ShopItem> allItems;

        public AvatarPage()
        {
            InitializeComponent();
            this.Loaded += AvatarPage_Loaded;
        }

        private void AvatarPage_Loaded(object sender, RoutedEventArgs e)
        {
            // 1. 데이터 초기화
            savedAppearance = DataManager.Instance.Settings.Appearance;
            previewAppearance = savedAppearance.Clone(); // 깊은 복사로 미리보기 객체 생성

            // 2. 상점 아이템 로드 및 UI 생성
            LoadShopItems();

            // 3. 초기 캐릭터 모습 업데이트
            characterDisplay.UpdateCharacter(previewAppearance);

            // 4. 코인 정보 업데이트
            UpdateCoinDisplay();
        }

        private void UpdateCoinDisplay()
        {
            CoinText.Text = $"{DataManager.Instance.Settings.Coins} C";
        }

        private void LoadShopItems()
        {
            allItems = DataManager.Instance.GetAllShopItems();

            // 카테고리별로 그룹화
            var groupedItems = allItems.GroupBy(item => item.Category).ToList();

            // 각 카테고리에 맞는 ItemsControl에 아이템 목록을 바인딩
            // XAML에 각 ItemsControl이 정의되어 있다고 가정합니다. (예: HairFrontItemsControl)
            BindItemsToCategory("hairFront", HairFrontItemsControl);
            BindItemsToCategory("hairBack", HairBackItemsControl);
            BindItemsToCategory("eye", EyeItemsControl);
            BindItemsToCategory("mouth", MouthItemsControl);
            BindItemsToCategory("clothes", ClothesItemsControl);
            BindItemsToCategory("cushion", CushionItemsControl);
            BindItemsToCategory("accessories", AccessoriesItemsControl);
            BindItemsToCategory("background", BackgroundsItemsControl);
        }

        private void BindItemsToCategory(string category, ItemsControl itemsControl)
        {
            if (itemsControl != null)
            {
                itemsControl.ItemsSource = allItems.Where(item => item.Category == category).ToList();
            }
        }

        // 상점 아이템 버튼 클릭 이벤트
        private void ItemButton_Click(object sender, RoutedEventArgs e)
        {
            if (!(sender is Button button) || !(button.DataContext is ShopItem selectedItem)) return;

            // 선택된 아이템의 카테고리에 따라 previewAppearance 객체를 업데이트합니다.
            switch (selectedItem.Category)
            {
                case "hairFront": previewAppearance.HairFront = selectedItem.Id; break;
                case "hairBack": previewAppearance.HairBack = selectedItem.Id; break;
                case "eye": previewAppearance.Eye = selectedItem.Id; break;
                case "mouth": previewAppearance.Mouth = selectedItem.Id; break;
                case "clothes": previewAppearance.Clothes = selectedItem.Id; break;
                case "cushion": previewAppearance.Cushion = selectedItem.Id; break;
                case "background": previewAppearance.Background = selectedItem.Id; break;
                case "accessories": UpdatePreviewAccessories(selectedItem); break;
            }

            // 변경된 previewAppearance로 캐릭터 모습을 즉시 업데이트합니다.
            characterDisplay.UpdateCharacter(previewAppearance);
        }

        // 장신구 미리보기 업데이트 로직
        private void UpdatePreviewAccessories(ShopItem accessoryItem)
        {
            int existingIndex = previewAppearance.Accessories.IndexOf(accessoryItem.Id);

            if (existingIndex != -1) // 이미 착용중이면 해제
            {
                previewAppearance.Accessories.RemoveAt(existingIndex);
                previewAppearance.AccessoryColors.RemoveAt(existingIndex);
            }
            else if (previewAppearance.Accessories.Count < 3) // 3개 미만일 경우에만 추가
            {
                previewAppearance.Accessories.Add(accessoryItem.Id);
                previewAppearance.AccessoryColors.Add("#FFFFFF"); // 기본 색상(흰색)으로 추가
            }
            else
            {
                MessageBox.Show("장신구는 최대 3개까지 착용할 수 있습니다.", "알림", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        // 되돌리기 버튼 클릭
        private void RevertButton_Click(object sender, RoutedEventArgs e)
        {
            previewAppearance = savedAppearance.Clone();
            characterDisplay.UpdateCharacter(previewAppearance);
        }

        // 저장하기 버튼 클릭
        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            // TODO: 다음 단계에서 아이템 구매 로직을 여기에 구현합니다.

            // 현재는 구매 로직 없이 바로 저장합니다.
            savedAppearance = previewAppearance.Clone();
            DataManager.Instance.Settings.Appearance = savedAppearance;
            DataManager.Instance.SaveSettings();

            MessageBox.Show("캐릭터 외형이 저장되었습니다.", "저장 완료", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void ColorButton_Click(object sender, RoutedEventArgs e)
        {
            // TODO: 다음 단계에서 색상 변경 로직을 여기에 구현합니다.
        }
    }
}
