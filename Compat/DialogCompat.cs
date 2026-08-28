using System.Windows.Forms;

namespace Microsoft.Win32
{
    public sealed class OpenFileDialog
    {
        private readonly System.Windows.Forms.OpenFileDialog _dialog = new();
        public string Filter { get => _dialog.Filter; set => _dialog.Filter = value; }
        public bool CheckFileExists { get => _dialog.CheckFileExists; set => _dialog.CheckFileExists = value; }
        public bool Multiselect { get => _dialog.Multiselect; set => _dialog.Multiselect = value; }
        public string FileName { get => _dialog.FileName; set => _dialog.FileName = value; }
        public string[] FileNames => _dialog.FileNames;
        public string InitialDirectory { get => _dialog.InitialDirectory; set => _dialog.InitialDirectory = value; }
        public bool? ShowDialog() => _dialog.ShowDialog() == DialogResult.OK;
    }

    public sealed class SaveFileDialog
    {
        private readonly System.Windows.Forms.SaveFileDialog _dialog = new();
        public string Filter { get => _dialog.Filter; set => _dialog.Filter = value; }
        public string FileName { get => _dialog.FileName; set => _dialog.FileName = value; }
        public string DefaultExt { get => _dialog.DefaultExt; set => _dialog.DefaultExt = value; }
        public string InitialDirectory { get => _dialog.InitialDirectory; set => _dialog.InitialDirectory = value; }
        public bool? ShowDialog() => _dialog.ShowDialog() == DialogResult.OK;
    }
}

namespace Microsoft.WindowsAPICodePack.Dialogs
{
    public enum CommonFileDialogResult { None, Ok, Cancel }

    public sealed class CommonOpenFileDialog : IDisposable
    {
        public bool IsFolderPicker { get; set; }
        public string Title { get; set; } = string.Empty;
        public string FileName { get; private set; } = string.Empty;

        public CommonFileDialogResult ShowDialog()
        {
            if (IsFolderPicker)
            {
                using var dialog = new FolderBrowserDialog { Description = Title, ShowNewFolderButton = true };
                var result = dialog.ShowDialog();
                if (result == DialogResult.OK)
                {
                    FileName = dialog.SelectedPath;
                    return CommonFileDialogResult.Ok;
                }
                return CommonFileDialogResult.Cancel;
            }

            using var file = new System.Windows.Forms.OpenFileDialog { Title = Title };
            var fileResult = file.ShowDialog();
            if (fileResult == DialogResult.OK)
            {
                FileName = file.FileName;
                return CommonFileDialogResult.Ok;
            }
            return CommonFileDialogResult.Cancel;
        }

        public void Dispose() { }
    }
}
