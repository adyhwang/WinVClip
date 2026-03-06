using System;

namespace WinVClip.Services
{
    public enum WindowState
    {
        Hidden,
        Visible,
        Minimized
    }

    public enum SnapEdge
    {
        None,
        Left,
        Right,
        Top,
        Bottom
    }

    public class WindowStateService
    {
        private WindowState _state = WindowState.Hidden;
        private bool _isPinned;
        private bool _isSnapped;
        private bool _isHidden;
        private bool _isDragging;
        private SnapEdge _snapEdge = SnapEdge.None;
        private Tuple<int, int>? _snapPosition;
        private readonly object _lock = new object();

        public WindowState State
        {
            get
            {
                lock (_lock)
                {
                    return _state;
                }
            }
        }

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

        public bool IsSnapped
        {
            get
            {
                lock (_lock)
                {
                    return _isSnapped;
                }
            }
        }

        public bool IsHidden
        {
            get
            {
                lock (_lock)
                {
                    return _isHidden;
                }
            }
        }

        public bool IsDragging
        {
            get
            {
                lock (_lock)
                {
                    return _isDragging;
                }
            }
        }

        public SnapEdge CurrentSnapEdge
        {
            get
            {
                lock (_lock)
                {
                    return _snapEdge;
                }
            }
        }

        public Tuple<int, int>? SnapPosition
        {
            get
            {
                lock (_lock)
                {
                    return _snapPosition;
                }
            }
        }

        public event Action<WindowState>? StateChanged;
        public event Action<bool>? PinStateChanged;
        public event Action<bool>? SnapStateChanged;
        public event Action<bool>? HiddenStateChanged;

        public void SetVisible()
        {
            lock (_lock)
            {
                if (_state == WindowState.Visible) return;
                _state = WindowState.Visible;
                _isHidden = false;
            }
            StateChanged?.Invoke(WindowState.Visible);
        }

        public void SetHidden()
        {
            lock (_lock)
            {
                if (_state == WindowState.Hidden) return;
                _state = WindowState.Hidden;
                _isHidden = true;
            }
            StateChanged?.Invoke(WindowState.Hidden);
        }

        public void SetMinimized()
        {
            lock (_lock)
            {
                if (_state == WindowState.Minimized) return;
                _state = WindowState.Minimized;
            }
            StateChanged?.Invoke(WindowState.Minimized);
        }

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

        public void TogglePin()
        {
            bool newPinned;
            lock (_lock)
            {
                _isPinned = !_isPinned;
                newPinned = _isPinned;
            }
            PinStateChanged?.Invoke(newPinned);
        }

        public void SetSnapped(SnapEdge edge, Tuple<int, int>? position = null)
        {
            lock (_lock)
            {
                _isSnapped = edge != SnapEdge.None;
                _snapEdge = edge;
                _snapPosition = position;
            }
            SnapStateChanged?.Invoke(_isSnapped);
        }

        public void ClearSnap()
        {
            lock (_lock)
            {
                _isSnapped = false;
                _snapEdge = SnapEdge.None;
                _snapPosition = null;
                _isHidden = false;
            }
            SnapStateChanged?.Invoke(false);
        }

        public void SetHidden(bool hidden)
        {
            bool changed;
            lock (_lock)
            {
                changed = _isHidden != hidden;
                _isHidden = hidden;
            }

            if (changed)
            {
                HiddenStateChanged?.Invoke(hidden);
            }
        }

        public void SetDragging(bool dragging)
        {
            lock (_lock)
            {
                _isDragging = dragging;
            }
        }

        public bool ShouldShow()
        {
            lock (_lock)
            {
                return (_isSnapped && _isHidden) || _state != WindowState.Visible;
            }
        }

        public bool ShouldHide()
        {
            lock (_lock)
            {
                return _state == WindowState.Visible && !_isHidden && !_isPinned;
            }
        }

        public void Reset()
        {
            lock (_lock)
            {
                _state = WindowState.Hidden;
                _isPinned = false;
                _isSnapped = false;
                _isHidden = false;
                _isDragging = false;
                _snapEdge = SnapEdge.None;
                _snapPosition = null;
            }
        }

        public WindowStateSnapshot GetSnapshot()
        {
            lock (_lock)
            {
                return new WindowStateSnapshot
                {
                    State = _state,
                    IsPinned = _isPinned,
                    IsSnapped = _isSnapped,
                    IsHidden = _isHidden,
                    IsDragging = _isDragging,
                    SnapEdge = _snapEdge,
                    SnapPosition = _snapPosition
                };
            }
        }
    }

    public class WindowStateSnapshot
    {
        public WindowState State { get; set; }
        public bool IsPinned { get; set; }
        public bool IsSnapped { get; set; }
        public bool IsHidden { get; set; }
        public bool IsDragging { get; set; }
        public SnapEdge SnapEdge { get; set; }
        public Tuple<int, int>? SnapPosition { get; set; }
    }
}
