using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Data;
using System.Windows.Forms;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using settings = FileManager.Properties.Settings;
namespace FileManager
{
  public static class NodeIcons
  {
    public static BitmapImage Folder = new BitmapImage(new Uri(@"pack://application:,,,/include/folderEmpty16.png", UriKind.Absolute));
    public static BitmapImage File = new BitmapImage(new Uri(@"pack://application:,,,/include/documentSend16.png", UriKind.Absolute));
  }
  /// <summary>
  /// https://thomaslevesque.com/2009/04/17/wpf-binding-to-an-asynchronous-collection/
  /// </summary>
  /// <typeparam name="T"></typeparam>
  public class AsyncObservableCollection<T> : ObservableCollection<T>
  {
    private SynchronizationContext _synchronizationContext = SynchronizationContext.Current;

    public AsyncObservableCollection() { }

    public AsyncObservableCollection(IEnumerable<T> list) : base(list) { }

    protected override void OnCollectionChanged(NotifyCollectionChangedEventArgs e)
    {
      if (SynchronizationContext.Current == _synchronizationContext)
      {
        // Execute the CollectionChanged event on the current thread
        RaiseCollectionChanged(e);
      }
      else
      {
        // Raises the CollectionChanged event on the creator thread
        _synchronizationContext.Send(RaiseCollectionChanged, e);
      }
    }

    private void RaiseCollectionChanged(object param)
    {
      // We are in the creator thread, call the base implementation directly
      base.OnCollectionChanged((NotifyCollectionChangedEventArgs)param);
    }

    protected override void OnPropertyChanged(PropertyChangedEventArgs e)
    {
      if (SynchronizationContext.Current == _synchronizationContext)
      {
        // Execute the PropertyChanged event on the current thread
        RaisePropertyChanged(e);
      }
      else
      {
        // Raises the PropertyChanged event on the creator thread
        _synchronizationContext.Send(RaisePropertyChanged, e);
      }
    }

    private void RaisePropertyChanged(object param)
    {
      // We are in the creator thread, call the base implementation directly
      base.OnPropertyChanged((PropertyChangedEventArgs)param);
    }
  }
  public class DirInfo
  {
    DirectoryInfo info { get; set; }
    public DirInfo(DirectoryInfo info)
    {
      this.info = info;
    }
    public DirInfo() { }
    public DirInfo(string path)
    {
      info = new DirectoryInfo(path);
    }
    public string Name { get { return info.Name; } }
    public IList Items
    {
      get
      {
        var children = new CompositeCollection();
        var subDirItems = new List<DirInfo>();
        foreach (var item in info.GetDirectories())
        {
          subDirItems.Add(new DirInfo(item));
        }
        children.Add(new CollectionContainer() { Collection = subDirItems });
        children.Add(new CollectionContainer() { Collection = info.GetFiles() });
        return children;
      }
    }
    public NodeData Parent { get; set; }
  }
  public class NodeData
  {
    public string Filespec { get; set; }
    public string Name { get; set; }
    public int TotalDirectories { get; set; }
    public int TotalFiles { get; set; }
    public long TotalData { get; set; }
    public DateTime Created { get; set; }
    public DateTime LastModified { get; set; }
    public Collection<NodeData> Items { get; set; }
    public NodeData(string filespec)
    {
      Filespec = filespec;
      Name = Path.GetFileName(filespec);
      if (string.IsNullOrEmpty(Name)) { Name = filespec; }
    }

  }
  public class NodeModel
  {
    public NodeData data { get; set; }
    public NodeModel Parent { get; set; }
    public AsyncObservableCollection<NodeModel> Items { get; set; }
    public bool IsExpanded { get; set; } = true;
    public bool IsSelected { get; set; }
    public string Name { get; set; }

    public BitmapImage Image { get; }/**/
    //public NodeModel() : this(new NodeData(), null) { }
    public NodeModel(NodeData node) : this(node, null, AvailableNodeImages.none) { }
    public NodeModel(NodeData node, NodeModel parent, AvailableNodeImages image)
    {
      data = node;
      Name = node.Name;
      Parent = parent;
      Items = new AsyncObservableCollection<NodeModel>();
      IsExpanded = !(parent != null);
      switch (image)
      {
        case AvailableNodeImages.none:
          break;
        case AvailableNodeImages.folder:
          Image = NodeIcons.Folder;
          break;
        case AvailableNodeImages.file:
          Image = NodeIcons.File;
          break;
        default:
          break;
      }
    }
  }
  public enum AvailableNodeImages
  {
    none = 0,
    folder = 1,
    file = 2
    //public static BitmapImage Folder = new BitmapImage(new Uri(@"pack://application:,,,/include/folderEmpty.ico", UriKind.Absolute));
  }
  /// <summary>
  /// Interaction logic for MainWindow.xaml
  /// </summary>
  public partial class winMain : Window, INotifyPropertyChanged
  {
    System.Threading.Timer clrHeader;
    static Thread processingThread = null;
    public AsyncObservableCollection<NodeModel> SourceNodes { get; } = new AsyncObservableCollection<NodeModel>();
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
        Dispatcher.Invoke(() => { btnPreview.Content = "cancel"; });
        return true;
      }
      else
      {
        Dispatcher.Invoke(() => { btnPreview.Content = "preview"; });
        cts.Cancel();
        return false;
      }
    }
    private void CompleteProcessing()
    {
      Dispatcher.Invoke(() => { btnPreview.Content = "preview"; });
    }
    private void AbortProcessing()
    {
      ProcessCancelled = true;
      Dispatcher.Invoke(() => { btnPreview.Content = "preview"; });
    }
    private async Task PreviewAsync()
    {
      try
      {
        if (!ToggleProcessing()) { return; }
        if (Directory.Exists(txtSource.Text))
        {
          cts = new CancellationTokenSource();
          CancellationToken token = cts.Token;
          _ = PreviewDirectory(txtSource.Text, SourceNodes, token);
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
      catch (Exception ex)
      {
        Status = ex.Message;
      }
    }





    async Task<string> PreviewDirectory(string dir, AsyncObservableCollection<NodeModel> nodebase, CancellationToken ct)
    {
      var tcs = new TaskCompletionSource<string>();
      if (!string.IsNullOrEmpty(dir) && nodebase != null)
      {
        try
        {
          //Dispatcher.Invoke(() => {
          nodebase.Clear();
          //});
          NodeModel curNode = new NodeModel(new NodeData(dir), null, AvailableNodeImages.folder);
          await Task.Factory.StartNew(() =>
          {
            processingThread = Thread.CurrentThread;
            try
            {

              //Dispatcher.BeginInvoke((Action)(() => {
              nodebase.Add(curNode);
              //}));
              ProcessDirectory(curNode, ct);
            }
            catch (Exception)
            {
              throw;
            }
          });
        }
        catch (Exception ex)
        {
          processingThread = null;
          cts = null;
          tcs.SetResult("Error : " + ex.Message);
          return tcs.Task.Result;
        }
      }
      processingThread = null;
      cts = null;
      tcs.SetResult("Complete");
      return tcs.Task.Result;
    }
    private void ProcessDirectory(NodeModel curNode, CancellationToken ct)
    {
      if (curNode == null) { return; }
      try
      {
        if (Recursive)
        {
          try
          {
            foreach (var f in Directory.GetDirectories(curNode.data.Filespec))
            {
              if (ct.IsCancellationRequested)
              {
                return;
              }
              var subNode = new NodeModel(new NodeData(f), curNode, AvailableNodeImages.folder);
              ProcessDirectory(subNode, ct);
              //Dispatcher.BeginInvoke((Action)(() =>
              //{
              curNode.Items.Add(subNode);
              //}));
              TotalFolders++;
              //Thread.Sleep(5);
            }

          }
          catch (UnauthorizedAccessException)
          {
          }
        }
        foreach (var f in Directory.GetFiles(curNode.data.Filespec))
        {
          if (ct.IsCancellationRequested)
          {
            return;
          }
          TotalFiles++;
          //Dispatcher.BeginInvoke((Action)(() =>
          //{
          curNode.Items.Add(new NodeModel(new NodeData(f), curNode, AvailableNodeImages.file));
          //}));
          var fi = new FileInfo(f);
          TotalSize += fi.Length;
        }
      }
      catch (Exception)
      {
        throw;
      }
      //Thread.Sleep(1);
    }
  }
}
