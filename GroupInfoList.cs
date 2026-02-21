using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace levyke
{
    /// <summary>
    /// Класс для группировки элементов в ListView (как в Groove Music)
    /// </summary>
    public class GroupInfoList<TKey> : ObservableCollection<object>
    {
        public TKey Key { get; set; }
    }
}