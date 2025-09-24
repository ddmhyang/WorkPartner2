using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;

namespace WorkPartner
{
    // 아이템 목록에 표시될 데이터를 담는 클래스
    public class DisplayShopItem : INotifyPropertyChanged
    {
        public ShopItem OriginalItem { get; }
        public string Name => OriginalItem.Name;
        public int Price => OriginalItem.Price;
        public string ImagePath => OriginalItem.ImagePath;
        public bool IsOwned { get; set; }
        public bool IsEquipped { get; set; }
        public bool IsFree => Price == 0;
        public Brush BorderColor => IsEquipped ? Brushes.Gold : (IsOwned ? Brushes.SkyBlue : Brushes.LightGray);

        public event PropertyChangedEventHandler PropertyChanged;

        public DisplayShopItem(ShopItem originalItem, List<Guid> ownedIds, Dictionary<ItemType, Guid> equippedIds)
        {
            OriginalItem = originalItem;
            IsOwned = ownedIds.Contains(originalItem.Id) || originalItem.Price == 0;
            IsEquipped = equippedIds.TryGetValue(originalItem.Type, out Guid equippedId) && equippedId == originalItem.Id;
        }

        public void UpdateStatus(List<Guid> ownedIds, Dictionary<ItemType, Guid> equippedIds)
        {
            IsOwned = ownedIds.Contains(OriginalItem.Id) || OriginalItem.Price == 0;
            IsEquipped = equippedIds.TryGetValue(OriginalItem.Type, out Guid equippedId) && equippedId == OriginalItem.Id;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsOwned)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsEquipped)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(BorderColor)));
        }
    }

    // Boolean 값을 Visibility 값으로 변환해주는 컨버터
    public class BooleanToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            bool boolValue = (bool)value;
            if (parameter != null && parameter.ToString().ToLower() == "invert")
            {
                boolValue = !boolValue;
            }
            return boolValue ? Visibility.Visible : Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}

