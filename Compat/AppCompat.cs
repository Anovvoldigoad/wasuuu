using System;
using System.Windows.Input;

namespace NSC_ModManager
{
    public static class App
    {
        public static System.Windows.Application Current => System.Windows.Application.Current;
    }

    public sealed class RelayCommand : ICommand
    {
        private readonly Action<object?> _execute;
        private readonly Func<object?, bool>? _canExecute;
        public RelayCommand(Action<object?> execute, Func<object?, bool>? canExecute = null)
        {
            _execute = execute ?? throw new ArgumentNullException(nameof(execute));
            _canExecute = canExecute;
        }
        public bool CanExecute(object? parameter) => _canExecute?.Invoke(parameter) ?? true;
        public void Execute(object? parameter) => _execute(parameter);
        public event EventHandler? CanExecuteChanged;
        public void RaiseCanExecuteChanged() => CanExecuteChanged?.Invoke(this, EventArgs.Empty);
    }
}

namespace NSC_ModManager.View
{
    public sealed class CharacterRosterEditorView
    {
        public CharacterRosterEditorView(object viewModel) { }
        public void Show() => NSC_ModManager.Compat.UiBridge.Log("Character Roster Editor UI is not ported in the Winlator Lite build yet.");
    }

    public sealed class CharacterRosterEditorNS4View
    {
        public CharacterRosterEditorNS4View(object viewModel) { }
        public void Show() => NSC_ModManager.Compat.UiBridge.Log("Storm 4 Character Roster Editor UI is not ported in the Winlator Lite build yet.");
    }
}
