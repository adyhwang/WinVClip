using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;

namespace WinVClip.Models
{
    public class RangeObservableCollection<T> : ObservableCollection<T>
    {
        private bool _suppressNotification = false;

        protected override void OnCollectionChanged(NotifyCollectionChangedEventArgs e)
        {
            if (!_suppressNotification)
                base.OnCollectionChanged(e);
        }

        protected override void OnPropertyChanged(PropertyChangedEventArgs e)
        {
            if (!_suppressNotification)
                base.OnPropertyChanged(e);
        }

        public void AddRange(IEnumerable<T> items)
        {
            if (items == null)
                throw new ArgumentNullException(nameof(items));

            var list = items as IList<T> ?? new List<T>(items);
            if (list.Count == 0) return;

            
            _suppressNotification = true;
            try
            {
                var startIndex = Items.Count;
                foreach (var item in list)
                    Items.Add(item);
            }
            finally
            {
                _suppressNotification = false;
            }

            
            
            
            
            for (int i = 0; i < list.Count; i++)
            {
                OnCollectionChanged(new NotifyCollectionChangedEventArgs(
                    NotifyCollectionChangedAction.Add, list[i], Count - list.Count + i));
            }
            OnPropertyChanged(new PropertyChangedEventArgs(nameof(Count)));
        }

        public void ReplaceAll(IEnumerable<T> items)
        {
            if (items == null)
                throw new ArgumentNullException(nameof(items));

            var list = items as IList<T> ?? new List<T>(items);

            _suppressNotification = true;
            try
            {
                Items.Clear();
                foreach (var item in list)
                    Items.Add(item);
            }
            finally
            {
                _suppressNotification = false;
                OnPropertyChanged(new PropertyChangedEventArgs(nameof(Count)));
                OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
            }
        }
    }
}
