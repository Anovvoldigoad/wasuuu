using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using WinForms = System.Windows.Forms;

namespace NSC_ModManager.Compat
{
    public static class UiBridge
    {
        public static event Action<string>? Message;
        public static void Log(string? text)
        {
            if (string.IsNullOrWhiteSpace(text)) return;
            try { Message?.Invoke(text); } catch { }
            try { System.Diagnostics.Debug.WriteLine(text); } catch { }
            try { NSC_ModManager.WinlatorEntry.Trace("UiBridge: " + text); } catch { }
        }
    }
}

namespace System.Windows
{
    public enum Visibility { Visible, Hidden, Collapsed }
    public enum MessageBoxButton { OK, OKCancel, YesNo, YesNoCancel }
    public enum MessageBoxImage { None, Error, Hand, Stop, Question, Exclamation, Warning, Information, Asterisk }
    public enum MessageBoxResult { None, OK, Cancel, Yes, No }

    public sealed class CompatDispatcher
    {
        // IMPORTANT FOR WINE/WINLATOR:
        // Do not create WindowsFormsSynchronizationContext here. Its constructor
        // creates a hidden marshaling control/window, which crashes some ARM64EC
        // Wine builds before the main form is even created.
        private WinForms.Control? _uiControl;
        private int _threadId;

        public void BindToCurrentThread()
        {
            _threadId = Environment.CurrentManagedThreadId;
        }

        public void BindToControl(WinForms.Control control)
        {
            _uiControl = control ?? throw new ArgumentNullException(nameof(control));
            _threadId = Environment.CurrentManagedThreadId;
        }

        public bool CheckAccess()
        {
            if (_uiControl is { IsDisposed: false, IsHandleCreated: true })
                return !_uiControl.InvokeRequired;
            return _threadId == 0 || Environment.CurrentManagedThreadId == _threadId;
        }

        public void Invoke(Action action)
        {
            if (action is null) return;

            var control = _uiControl;
            if (control is { IsDisposed: false, IsHandleCreated: true })
            {
                if (control.InvokeRequired)
                    control.Invoke(action);
                else
                    action();
                return;
            }

            // Before the main form has a handle there is nothing safe to marshal
            // through. Startup work runs on the UI thread, so direct invocation is
            // correct here and avoids creating any hidden Wine window.
            action();
        }

        public T Invoke<T>(Func<T> action)
        {
            if (action is null) throw new ArgumentNullException(nameof(action));

            var control = _uiControl;
            if (control is { IsDisposed: false, IsHandleCreated: true })
            {
                if (control.InvokeRequired)
                    return (T)control.Invoke(action)!;
                return action();
            }

            return action();
        }
    }

    public sealed class CompatResourceDictionary
    {
        private readonly Dictionary<string, object> _items = new(StringComparer.OrdinalIgnoreCase);
        public object this[object key]
        {
            get
            {
                string k = key?.ToString() ?? string.Empty;
                return _items.TryGetValue(k, out var value) ? value : k;
            }
            set => _items[key?.ToString() ?? string.Empty] = value;
        }
    }

    public class Application
    {
        private static readonly Application _current = new();
        public static Application Current => _current;
        public CompatDispatcher Dispatcher { get; } = new();
        public CompatResourceDictionary Resources { get; } = new();
    }

    public static class MessageBox
    {
        public static MessageBoxResult Show(string messageBoxText) => Show(messageBoxText, "NSC Mod Manager", MessageBoxButton.OK, MessageBoxImage.None);
        public static MessageBoxResult Show(string messageBoxText, string caption) => Show(messageBoxText, caption, MessageBoxButton.OK, MessageBoxImage.None);
        public static MessageBoxResult Show(string messageBoxText, string caption, MessageBoxButton button) => Show(messageBoxText, caption, button, MessageBoxImage.None);
        public static MessageBoxResult Show(string messageBoxText, string caption, MessageBoxButton button, MessageBoxImage icon)
            => NSC_ModManager.Compat.DialogMapper.Show(messageBoxText, caption, button, icon);
    }

    public static class Clipboard
    {
        public static string GetText()
        {
            try { return WinForms.Clipboard.ContainsText() ? WinForms.Clipboard.GetText() : string.Empty; }
            catch { return string.Empty; }
        }
        public static void SetText(string text)
        {
            try { WinForms.Clipboard.SetText(text ?? string.Empty); } catch { }
        }
        public static bool ContainsText()
        {
            try { return WinForms.Clipboard.ContainsText(); } catch { return false; }
        }
    }
}

namespace NSC_ModManager.Compat
{
    internal static class DialogMapper
    {
        public static System.Windows.MessageBoxResult Show(string text, string caption, System.Windows.MessageBoxButton button, System.Windows.MessageBoxImage icon)
        {
            UiBridge.Log(text);

            WinForms.MessageBoxButtons wfButtons = button switch
            {
                System.Windows.MessageBoxButton.OKCancel => WinForms.MessageBoxButtons.OKCancel,
                System.Windows.MessageBoxButton.YesNo => WinForms.MessageBoxButtons.YesNo,
                System.Windows.MessageBoxButton.YesNoCancel => WinForms.MessageBoxButtons.YesNoCancel,
                _ => WinForms.MessageBoxButtons.OK
            };
            WinForms.MessageBoxIcon wfIcon = icon switch
            {
                System.Windows.MessageBoxImage.Error or System.Windows.MessageBoxImage.Hand or System.Windows.MessageBoxImage.Stop => WinForms.MessageBoxIcon.Error,
                System.Windows.MessageBoxImage.Question => WinForms.MessageBoxIcon.Question,
                System.Windows.MessageBoxImage.Exclamation or System.Windows.MessageBoxImage.Warning => WinForms.MessageBoxIcon.Warning,
                System.Windows.MessageBoxImage.Information or System.Windows.MessageBoxImage.Asterisk => WinForms.MessageBoxIcon.Information,
                _ => WinForms.MessageBoxIcon.None
            };

            // Info-only notifications are logged instead of creating dozens of popups during compilation.
            bool mustPrompt = button != System.Windows.MessageBoxButton.OK || wfIcon is WinForms.MessageBoxIcon.Error or WinForms.MessageBoxIcon.Warning;
            if (!mustPrompt) return System.Windows.MessageBoxResult.OK;

            try
            {
                return WinForms.MessageBox.Show(text ?? string.Empty, caption ?? "NSC Mod Manager", wfButtons, wfIcon) switch
                {
                    WinForms.DialogResult.OK => System.Windows.MessageBoxResult.OK,
                    WinForms.DialogResult.Cancel => System.Windows.MessageBoxResult.Cancel,
                    WinForms.DialogResult.Yes => System.Windows.MessageBoxResult.Yes,
                    WinForms.DialogResult.No => System.Windows.MessageBoxResult.No,
                    _ => System.Windows.MessageBoxResult.None
                };
            }
            catch
            {
                return button == System.Windows.MessageBoxButton.YesNo ? System.Windows.MessageBoxResult.No : System.Windows.MessageBoxResult.OK;
            }
        }
    }
}

namespace ModernWpf
{
    public static class MessageBox
    {
        public static System.Windows.MessageBoxResult Show(string messageBoxText)
            => NSC_ModManager.Compat.DialogMapper.Show(messageBoxText, "NSC Mod Manager", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.None);
        public static System.Windows.MessageBoxResult Show(string messageBoxText, string caption)
            => NSC_ModManager.Compat.DialogMapper.Show(messageBoxText, caption, System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.None);
        public static System.Windows.MessageBoxResult Show(string messageBoxText, string caption, System.Windows.MessageBoxButton button)
            => NSC_ModManager.Compat.DialogMapper.Show(messageBoxText, caption, button, System.Windows.MessageBoxImage.None);
        public static System.Windows.MessageBoxResult Show(string messageBoxText, string caption, System.Windows.MessageBoxButton button, System.Windows.MessageBoxImage icon)
            => NSC_ModManager.Compat.DialogMapper.Show(messageBoxText, caption, button, icon);
    }
}

namespace System.Windows.Data
{
    public sealed class SimpleCollectionView
    {
        public object Source { get; }
        internal SimpleCollectionView(object source) => Source = source;
        public bool MoveCurrentTo(object? item) => true;
        public void Refresh() { }
    }

    public static class CollectionViewSource
    {
        public static SimpleCollectionView GetDefaultView(object source) => new(source);
    }
}

namespace System.Windows.Media.Animation
{
    public readonly struct RepeatBehavior
    {
        public double? Count { get; }
        public bool IsForever { get; }
        public RepeatBehavior(double count) { Count = count; IsForever = false; }
        private RepeatBehavior(bool forever) { Count = null; IsForever = forever; }
        public static RepeatBehavior Forever => new(true);
    }
}

namespace System.Windows.Media.Imaging
{
    public enum BitmapCacheOption { Default, OnDemand, OnLoad, None }

    public class BitmapSource
    {
        public Image? NativeImage { get; protected set; }
    }

    public class BitmapImage : BitmapSource
    {
        public Uri? UriSource { get; set; }
        public Stream? StreamSource { get; set; }
        public BitmapCacheOption CacheOption { get; set; }
        public void BeginInit() { }
        public void EndInit()
        {
            try
            {
                if (StreamSource != null)
                {
                    long old = StreamSource.CanSeek ? StreamSource.Position : 0;
                    using var tmp = Image.FromStream(StreamSource, true, true);
                    NativeImage = new Bitmap(tmp);
                    if (StreamSource.CanSeek) StreamSource.Position = old;
                }
                else if (UriSource?.IsFile == true && File.Exists(UriSource.LocalPath))
                {
                    using var tmp = Image.FromFile(UriSource.LocalPath);
                    NativeImage = new Bitmap(tmp);
                }
            }
            catch { NativeImage = null; }
        }
        public void Freeze() { }
    }

    public sealed class BitmapFrame : BitmapSource
    {
        private BitmapFrame() { }
        public static BitmapFrame Create(Stream stream)
        {
            var frame = new BitmapFrame();
            try
            {
                using var tmp = Image.FromStream(stream, true, true);
                frame.NativeImage = new Bitmap(tmp);
            }
            catch { }
            return frame;
        }
    }
}
