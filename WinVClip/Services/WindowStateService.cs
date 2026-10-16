using System;

namespace WinVClip.Services
{
    public class WindowStateService
    {
        private bool _isPinned;
        private readonly object _lock = new object();

        public bool IsPinned
        {
            get
            {
                lock (_lock)
                {
                    return _isPinned;
                }
            }
        }

        public event Action<bool>? PinStateChanged;

        public void SetPinned(bool pinned)
        {
            bool changed;
            lock (_lock)
            {
                changed = _isPinned != pinned;
                _isPinned = pinned;
            }

            if (changed)
            {
                PinStateChanged?.Invoke(pinned);
            }
        }
    }
}
