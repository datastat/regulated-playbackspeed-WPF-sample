using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows.Input;

namespace SampleFilePlaybackWPF.ViewModel
{
    class RelayCommand : ICommand
    {
        public RelayCommand(Action<object> action)
        {
            ExecuteDelegate = action;
        }

        public Predicate<object> CanExecuteDelegate { get; set; }

        public Action<object> ExecuteDelegate { get; set; }

        public bool CanExecute(object parameter)
        {
            return CanExecuteDelegate == null || CanExecuteDelegate(parameter);
        }

        public void Execute(object parameter)
        {
            if (ExecuteDelegate != null)
            {
                ExecuteDelegate(parameter);
            }
        }

        public event EventHandler CanExecuteChanged
        {
            add { CommandManager.RequerySuggested += value; }
            remove { CommandManager.RequerySuggested -= value; }
        }
    }
}
