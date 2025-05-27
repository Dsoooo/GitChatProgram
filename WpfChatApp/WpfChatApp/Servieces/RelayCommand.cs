using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;

namespace WpfChatApp
{
    /// <summary>
    /// Model과 ViewModel을 이어주는 역할
    /// MVVM 패넡에서 UI를 함수로 바인딩 하기 위해 필수
    /// </summary>
    public class RelayCommand : ICommand
    {
        private readonly Action _execute;
        private readonly Func<bool> _canExecute;

        /// <summary>
        /// RelayCommand 생성자
        /// </summary>
        /// <param name="execute"></param>
        /// <param name="canExecute"></param>
        public RelayCommand(Action execute, Func<bool> canExecute = null)
        {
            _execute = execute;
            _canExecute = canExecute;
        }

        /// <summary>
        /// 명령이 실행 가능한지를 반환
        /// </summary>
        /// <param name="parameter"></param>
        /// <returns></returns>
        public bool CanExecute(object parameter) => _canExecute == null || _canExecute();

        /// <summary>
        /// 버튼 클릭시 실제 명령 실행
        /// </summary>
        /// <param name="parameter"></param>
        public void Execute(object parameter) => _execute();

        /// <summary>
        /// WPF가 자동으로 버튼 상태를 갱신
        /// </summary>
        public event EventHandler CanExecuteChanged
        {
            add => CommandManager.RequerySuggested += value;
            remove => CommandManager.RequerySuggested -= value;
        }
    }
}
