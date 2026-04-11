using System;
using System.Windows.Input;

namespace DXTnavis.Helpers
{
    /// <summary>
    /// MVVM 패턴에서 사용할 수 있는 범용 ICommand 구현체
    /// </summary>
    public class RelayCommand : ICommand
    {
        private readonly Action<object> _execute;
        private readonly Func<object, bool> _canExecute;

        /// <summary>
        /// RelayCommand 생성자
        /// </summary>
        /// <param name="execute">명령이 실행될 때 호출될 액션</param>
        /// <param name="canExecute">명령 실행 가능 여부를 결정하는 함수 (옵션)</param>
        public RelayCommand(Action<object> execute, Func<object, bool> canExecute = null)
        {
            _execute = execute ?? throw new ArgumentNullException(nameof(execute));
            _canExecute = canExecute;
        }

        /// <summary>
        /// 파라미터 없는 RelayCommand 생성자
        /// </summary>
        /// <param name="execute">명령이 실행될 때 호출될 액션</param>
        /// <param name="canExecute">명령 실행 가능 여부를 결정하는 함수 (옵션)</param>
        public RelayCommand(Action execute, Func<bool> canExecute = null)
            : this(_ => execute(), canExecute == null ? null : new Func<object, bool>(_ => canExecute()))
        {
        }

        /// <summary>
        /// 명령 실행 가능 여부가 변경되었음을 알리는 이벤트
        /// </summary>
        public event EventHandler CanExecuteChanged
        {
            add { CommandManager.RequerySuggested += value; }
            remove { CommandManager.RequerySuggested -= value; }
        }

        /// <summary>
        /// 명령 실행 가능 여부 확인
        /// </summary>
        public bool CanExecute(object parameter)
        {
            return _canExecute == null || _canExecute(parameter);
        }

        /// <summary>
        /// 명령 실행
        /// </summary>
        public void Execute(object parameter)
        {
            _execute(parameter);
        }

        /// <summary>
        /// CanExecute 상태가 변경되었음을 알리고 UI가 다시 평가하도록 함
        /// PRD v6: 체크박스 선택 시 버튼 활성화를 위해 추가
        /// </summary>
        public void RaiseCanExecuteChanged()
        {
            CommandManager.InvalidateRequerySuggested();
        }
    }

    /// <summary>
    /// 비동기 명령을 위한 ICommand 구현체
    /// </summary>
    public class AsyncRelayCommand : ICommand
    {
        private readonly Func<object, System.Threading.Tasks.Task> _execute;
        private readonly Func<object, bool> _canExecute;
        private bool _isExecuting;

        public AsyncRelayCommand(Func<object, System.Threading.Tasks.Task> execute, Func<object, bool> canExecute = null)
        {
            _execute = execute ?? throw new ArgumentNullException(nameof(execute));
            _canExecute = canExecute;
        }

        public event EventHandler CanExecuteChanged;

        public bool CanExecute(object parameter)
        {
            return !_isExecuting && (_canExecute == null || _canExecute(parameter));
        }

        public async void Execute(object parameter)
        {
            _isExecuting = true;
            RaiseCanExecuteChanged();

            try
            {
                await _execute(parameter);
            }
            finally
            {
                _isExecuting = false;
                RaiseCanExecuteChanged();
            }
        }

        public void RaiseCanExecuteChanged()
        {
            CanExecuteChanged?.Invoke(this, EventArgs.Empty);
        }
    }
}
