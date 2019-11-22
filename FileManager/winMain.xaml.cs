using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Forms;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using settings = FileManager.Properties.Settings;

namespace FileManager
{
  public class NodeData
  {
    public string Filespec { get; set; }
    public int TotalDirectories { get; set; }
    public int TotalFiles { get; set; }
    public long TotalData { get; set; }
    public DateTime Created { get; set; }
    public DateTime LastModified { get; set; }
  }
  public class NodeModel
  {
    public NodeData data { get; set; }
    public NodeModel Parent { get; set; }
    public Collection<NodeModel> Items { get; set; }
    bool IsExpanded { get; set; }
    bool IsSelected { get; set; }
    BitmapImage Image { get; set; }/*=new BitmapImage(new Uri(@"pack://application:,,,/include/Continuous.png", UriKind.Absolute));*/
    public NodeModel() : this(new NodeData(), null) { }
    public NodeModel(NodeData node) : this(node, null) { }
    public NodeModel(NodeData node, NodeModel parent)
    {
      data = node;
      Parent = parent;
      Items = new Collection<NodeModel>();
    }
  }
  /// <summary>
  /// Interaction logic for MainWindow.xaml
  /// </summary>
  public partial class winMain : Window, INotifyPropertyChanged
  {
    System.Threading.Timer clrHeader;
    static Thread processingThread = null;
    public List<NodeModel> SourceNodes;
    CancellationTokenSource cts;
    public event PropertyChangedEventHandler PropertyChanged;
    protected void OnPropertyChanged([CallerMemberName] string propertyName = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    protected bool SetField<T>(ref T field, T value, [CallerMemberName] string propertyName = null)
    {
      if (EqualityComparer<T>.Default.Equals(field, value)) return false;
      field = value;
      OnPropertyChanged(propertyName);
      return true;
    }
    private int _totalFolders;
    public int TotalFolders { get => _totalFolders; set => SetField(ref _totalFolders, value); }
    private int _totalFiles;
    public int TotalFiles { get => _totalFiles; set => SetField(ref _totalFiles, value); }
    private long _totalSize;
    public long TotalSize { get => _totalSize; set => SetField(ref _totalSize, value); }
    private string _status;
    public string Status { get => _status; set => SetField(ref _status, value); }
    private string _filesOfFiles;
    public string FilesOfFiles { get => _filesOfFiles; set => SetField(ref _filesOfFiles, value); }
    private byte _progress;
    public byte Progress { get => _progress; set => SetField(ref _progress, value); }
    //private bool _recursive;
    //public bool Recursive { get => _recursive; set => SetField(ref _recursive, value); }
    //private bool _deleteOriginal;
    //public bool DeleteOriginal { get => _deleteOriginal; set => SetField(ref _deleteOriginal, value); }
    //private bool _archive;
    //public bool Archive { get => _archive; set => SetField(ref _archive, value); }
    //private int _archiveDays;
    //public int ArchiveDays { get => _archiveDays; set => SetField(ref _archiveDays, value); }
    public bool Recursive { get { return settings.Default.LastRecursive; } }
    private bool _processCancelled;
    public bool ProcessCancelled { get => _processCancelled; set => SetField(ref _processCancelled, value); }

    public winMain()
    {
      try
      {
        InitializeComponent();
        this.DataContext = this;
      }
      catch (Exception ex)
      {
        System.Windows.MessageBox.Show(ex.Message, "Fatal Error", MessageBoxButton.OK, MessageBoxImage.Error);
        throw;
      }
    }
    private void BtnClose_Click(object sender, RoutedEventArgs e)
    {
      this.Close();
    }
    private void BtnMaximize_Click(object sender, RoutedEventArgs e)
    {
      if (WindowState == WindowState.Maximized)
      {
        WindowState = WindowState.Normal;
      }
      else
      {
        WindowState = WindowState.Maximized;
      }
    }
    private void BtnMinimize_Click(object sender, RoutedEventArgs e)
    {
      this.WindowState = WindowState.Minimized;
    }
    private void BtnApp_Click(object sender, RoutedEventArgs e)
    {

    }
    private void ClearStatus()
    {
      Dispatcher.Invoke(() =>
      {
        txtStatus.Text = string.Empty;
        txtStatus.Background = new SolidColorBrush(Colors.Transparent);
        txtStatus.Foreground = new SolidColorBrush(Colors.White);
      });
    }
    private void UpdateStatus(object o)
    {
      UpdateStatus((o ?? string.Empty).ToString(), new SolidColorBrush(Colors.Transparent), new SolidColorBrush(Colors.White));
    }
    private void UpdateStatus(string message, Brush background, Brush foreground)
    {
      if (string.IsNullOrEmpty(message)) { ClearStatus(); return; }
      Dispatcher.Invoke(() =>
      {
        txtStatus.Text = message;
        txtStatus.Background = background ?? txtStatus.Background;
        txtStatus.Foreground = foreground ?? txtStatus.Foreground;
      });
      if (clrHeader == null)
      {
        clrHeader = new System.Threading.Timer(new System.Threading.TimerCallback(UpdateStatus), null, 30000, System.Threading.Timeout.Infinite);
      }
      else
      {
        clrHeader.Change(30000, System.Threading.Timeout.Infinite);
      }
    }
    private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
    {
      SaveSettings();
    }
    private void Window_Loaded(object sender, RoutedEventArgs e)
    {

    }
    private void ApplySettings()
    {
      if (settings.Default.UpgradeRequired)
      {
        settings.Default.Upgrade();
        settings.Default.UpgradeRequired = false;
        settings.Default.Save();
      }
      WindowState = settings.Default.LastWindowState;
      Width = settings.Default.LastWindowRect.Width;
      Height = settings.Default.LastWindowRect.Height;
      Top = settings.Default.LastWindowRect.Top;
      Left = settings.Default.LastWindowRect.Left;
      //txtSource.Text = settings.Default.LastSource;
      //txtDestination.Text = settings.Default.LastDestination;
      //txtLog.Text = settings.Default.LastLogFile;
      //ArchiveDays = settings.Default.LastArchiveDays;
    }
    private void SaveSettings()
    {
      settings.Default.LastWindowState = this.WindowState;
      settings.Default.LastWindowRect = this.RestoreBounds;
      settings.Default.LastSource = txtSource.Text;
      settings.Default.LastDestination = txtDestination.Text;
      settings.Default.LastLogFile = txtLog.Text;
      settings.Default.LastRecursive = chkRecursive.IsChecked ?? false;
      settings.Default.LastDeleteOriginal = chkDeleteOriginal.IsChecked ?? false;
      settings.Default.LastArchive = chkArchive.IsChecked ?? false;
      settings.Default.LastArchiveDays = int.TryParse(txtArchiveDays.Text, out int i) ? i : settings.Default.LastArchiveDays;
      settings.Default.Save();
    }
    private void Window_Initialized(object sender, EventArgs e)
    {
      txtVersion.Text = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version.ToString();
      progressbar.Value = 0;
      progressbar.Visibility = Visibility.Hidden; txtProgress.Visibility = Visibility.Hidden;
      ClearStatus();
      ApplySettings();
    }
    private void BrowseSource_Click(object sender, RoutedEventArgs e)
    {
      FolderBrowserDialog d = new FolderBrowserDialog() { SelectedPath = txtSource.Text };
      DialogResult result = d.ShowDialog();
      if (result == System.Windows.Forms.DialogResult.OK)
      {
        txtSource.Text = d.SelectedPath;
      }
    }
    private void BrowseDestination_Click(object sender, RoutedEventArgs e)
    {
      FolderBrowserDialog d = new FolderBrowserDialog() { SelectedPath = txtDestination.Text };
      DialogResult result = d.ShowDialog();
      if (result == System.Windows.Forms.DialogResult.OK)
      {
        txtDestination.Text = d.SelectedPath;
      }
    }
    private void BrowseLog_Click(object sender, RoutedEventArgs e)
    {
      var f = new SaveFileDialog() { InitialDirectory = txtLog.Text, DefaultExt = " txt", Filter = "Text files (*.txt)|*.txt|All files (*.*)|*.*" };
      f.ShowDialog();
      txtLog.Text = f.FileName;
    }
    private void _MouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
      if (e.ChangedButton == MouseButton.Left) { this.DragMove(); }
      if (e.ChangedButton == MouseButton.Left && e.ClickCount == 2)
      {
        this.WindowState = this.WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
      }
    }
    private void BtnMore_Click(object sender, RoutedEventArgs e)
    {
      TotalFolders++;
    }
    private void BtnPreview_Click(object sender, RoutedEventArgs e)
    {
      _ = PreviewAsync();
    }
    private void BtnStart_Click(object sender, RoutedEventArgs e)
    {
      TotalSize++;
    }
    private bool ToggleProcessing()
    {
      if (cts == null)
      {
        ProcessCancelled = false;
        TotalFolders = 0;
        TotalFiles = 0;
        TotalSize = 0;
        btnPreview.Content = "cancel";
        return true;
      }
      else
      {
        btnPreview.Content = "preview";
        cts.Cancel();
        return false;
      }
    }
    private void CompleteProcessing()
    {
      btnPreview.Content = "preview";
    }
    private void AbortProcessing()
    {
      ProcessCancelled = true;
      btnPreview.Content = "preview";
    }
    private async Task PreviewAsync()
    {
      if (!ToggleProcessing()) { return; }
      if (Directory.Exists(txtSource.Text))
      {
        cts = new CancellationTokenSource();
        CancellationToken token = cts.Token;
        _ = PreviewDirectory(txtSource.Text, token);
        await Task.Factory.StartNew(() =>
        {
          while (processingThread != null)
          {
            if (token.IsCancellationRequested)
            {
              processingThread.Abort();
              processingThread = null;
              cts = null;
              AbortProcessing();
              return;
            }
            Thread.Sleep(5);
          }
        });
        CompleteProcessing();
      }
    }





    async Task<string> PreviewDirectory(string dir, CancellationToken ct)
    {
      var tcs = new TaskCompletionSource<string>();
      SourceNodes = new List<NodeModel>();
      NodeModel curNode = null;
      await Task.Factory.StartNew(() =>
      {
        processingThread = Thread.CurrentThread;
        try
        {
          ProcessDirectory(dir, curNode, ct);
        }
        catch (Exception)
        {
        }
      });
      processingThread = null;
      cts = null;
      tcs.SetResult("Complete");
      return tcs.Task.Result;
    }
    private void ProcessDirectory(string dir, NodeModel curNode, CancellationToken ct)
    {
      if (Recursive)
      {
        foreach (var f in Directory.GetDirectories(dir))
        {
          if (ct.IsCancellationRequested)
          {
            return;
          }
          SourceNodes.Add(new NodeModel() { Parent = curNode });
          TotalFolders++;
          ProcessDirectory(f, curNode, ct);
        }
      }
      foreach (var f in Directory.GetFiles(dir))
      {
        if (ct.IsCancellationRequested)
        {
          return;
        }
        TotalFiles++;
        SourceNodes.Add(new NodeModel() { Parent = curNode });
        var fi = new FileInfo(f);
        TotalSize += fi.Length;
        //Thread.Sleep(100);
      }

    }
  }
}
