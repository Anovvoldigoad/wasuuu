using System.Collections;
using System.Collections.ObjectModel;

namespace System.Windows
{
    public enum Visibility { Visible, Hidden, Collapsed }
    public enum MessageBoxButton { OK, OKCancel, YesNo, YesNoCancel }
    public enum MessageBoxImage { None, Error, Warning, Information, Question }
    public enum MessageBoxResult { None, OK, Cancel, Yes, No }

    public sealed class ResourceBag
    {
        public object this[object key] => key?.ToString() ?? string.Empty;
    }

    public class Application
    {
        private static readonly Application _current = new();
        public static Application Current => _current;
        public ResourceBag Resources { get; } = new();
    }

    public static class Clipboard
    {
        private static string _text = string.Empty;
        public static string GetText() => _text;
        public static void SetText(string text) => _text = text ?? string.Empty;
    }

    public static class MessageBox
    {
        public static MessageBoxResult Show(string message) => MessageBoxResult.OK;
        public static MessageBoxResult Show(string message, string caption) => MessageBoxResult.OK;
        public static MessageBoxResult Show(string message, string caption, MessageBoxButton button) => MessageBoxResult.OK;
        public static MessageBoxResult Show(string message, string caption, MessageBoxButton button, MessageBoxImage image) => MessageBoxResult.OK;
    }
}

namespace ModernWpf
{
    public static class MessageBox
    {
        public static System.Windows.MessageBoxResult Show(string message) => System.Windows.MessageBoxResult.OK;
        public static System.Windows.MessageBoxResult Show(string message, string caption) => System.Windows.MessageBoxResult.OK;
        public static System.Windows.MessageBoxResult Show(string message, string caption, System.Windows.MessageBoxButton button) => System.Windows.MessageBoxResult.OK;
        public static System.Windows.MessageBoxResult Show(string message, string caption, System.Windows.MessageBoxButton button, System.Windows.MessageBoxImage image) => System.Windows.MessageBoxResult.OK;
    }
}

namespace System.Windows.Data
{
    public sealed class PortableCollectionView
    {
        public bool MoveCurrentTo(object? item) => true;
    }
    public static class CollectionViewSource
    {
        public static PortableCollectionView GetDefaultView(object source) => new();
    }
}

namespace Microsoft.Win32
{
    public sealed class OpenFileDialog
    {
        public string Filter { get; set; } = "";
        public bool CheckFileExists { get; set; }
        public bool Multiselect { get; set; }
        public string FileName { get; set; } = "";
        public string[] FileNames { get; set; } = Array.Empty<string>();
        public bool? ShowDialog() => false;
    }
    public sealed class SaveFileDialog
    {
        public string DefaultExt { get; set; } = "";
        public string Filter { get; set; } = "";
        public string FileName { get; set; } = "";
        public bool? ShowDialog() => false;
    }
}

namespace System.Windows.Forms
{
    public enum DialogResult { None, OK, Cancel }
    public sealed class OpenFileDialog
    {
        public string Filter { get; set; } = "";
        public bool CheckFileExists { get; set; }
        public bool Multiselect { get; set; }
        public string FileName { get; set; } = "";
        public DialogResult ShowDialog() => DialogResult.Cancel;
    }
    public sealed class SaveFileDialog
    {
        public string DefaultExt { get; set; } = "";
        public string Filter { get; set; } = "";
        public string FileName { get; set; } = "";
        public DialogResult ShowDialog() => DialogResult.Cancel;
    }
}

namespace DynamicData
{
    public static class PortableObservableCollectionExtensions
    {
        public static void AddRange<T>(this ObservableCollection<T> collection, IEnumerable<T> items)
        {
            foreach (var item in items) collection.Add(item);
        }
    }
}

namespace System.Windows.Media.Imaging
{
    public enum BitmapCacheOption { Default, OnLoad }
    public sealed class BitmapImage
    {
        public Uri? UriSource { get; set; }
        public BitmapCacheOption CacheOption { get; set; }
        public void BeginInit() { }
        public void EndInit() { }
    }
}

namespace NSC_ModManager
{
    public sealed class PortableDispatcher
    {
        public void Invoke(Action action) => action();
    }
    public sealed class App
    {
        private static readonly App _current = new();
        public static App Current => _current;
        public PortableDispatcher Dispatcher { get; } = new();
    }

    public sealed class RelayCommand : System.Windows.Input.ICommand
    {
        private readonly Action<object?> _execute;
        private readonly Func<object?, bool>? _canExecute;
        public RelayCommand(Action<object?> execute, Func<object?, bool>? canExecute = null)
        {
            _execute = execute; _canExecute = canExecute;
        }
        public bool CanExecute(object? parameter) => _canExecute?.Invoke(parameter) ?? true;
        public void Execute(object? parameter) => _execute(parameter);
        public event EventHandler? CanExecuteChanged;
        public void RaiseCanExecuteChanged() => CanExecuteChanged?.Invoke(this, EventArgs.Empty);
    }
}

namespace NSC_ModManager.Properties
{
    public sealed class Settings
    {
        private static readonly Settings _default = new();
        public static Settings Default => _default;
    }
}

namespace DynamicData.Binding { }
