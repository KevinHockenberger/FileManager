using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
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
    public static BitmapImage File = new BitmapImage(new Uri(@"pack://application:,,,/include/document16.png", UriKind.Absolute));
    public static BitmapImage Nope = new BitmapImage(new Uri(@"pack://application:,,,/include/nope16.png", UriKind.Absolute));
    public static BitmapImage Right = new BitmapImage(new Uri(@"pack://application:,,,/include/right16.png", UriKind.Absolute));
    public static BitmapImage Left = new BitmapImage(new Uri(@"pack://application:,,,/include/left16.png", UriKind.Absolute));
    public static BitmapImage UpToDate = new BitmapImage(new Uri(@"pack://application:,,,/include/check16.png", UriKind.Absolute));
    public static BitmapImage Archive = new BitmapImage(new Uri(@"pack://application:,,,/include/a16.png", UriKind.Absolute));
  }
  public enum AvailableUpdateMethods
  {
    None = 0,
    UpToDate = 1,
    ToDestination = 2,
    ToSource = 3,
    ToArchive = 4,
    Disregard = 5
  }
  /// <summary>
  /// https://thomaslevesque.com/2009/04/17/wpf-binding-to-an-asynchronous-collection/
  /// </summary>
  /// <typeparam name="T"></typeparam>
  //public class AsyncObservableCollection<T> : ObservableCollection<T>
  //{
  //  private SynchronizationContext _synchronizationContext = SynchronizationContext.Current;

  //  public AsyncObservableCollection() { }

  //  public AsyncObservableCollection(IEnumerable<T> list) : base(list) { }

  //  protected override void OnCollectionChanged(NotifyCollectionChangedEventArgs e)
  //  {
  //    if (SynchronizationContext.Current == _synchronizationContext)
  //    {
  //      // Execute the CollectionChanged event on the current thread
  //      RaiseCollectionChanged(e);
  //    }
  //    else
  //    {
  //      // Raises the CollectionChanged event on the creator thread
  //      _synchronizationContext.Send(RaiseCollectionChanged, e);
  //    }
  //  }

  //  private void RaiseCollectionChanged(object param)
  //  {
  //    // We are in the creator thread, call the base implementation directly
  //    base.OnCollectionChanged((NotifyCollectionChangedEventArgs)param);
  //  }

  //  protected override void OnPropertyChanged(PropertyChangedEventArgs e)
  //  {
  //    if (SynchronizationContext.Current == _synchronizationContext)
  //    {
  //      // Execute the PropertyChanged event on the current thread
  //      RaisePropertyChanged(e);
  //    }
  //    else
  //    {
  //      // Raises the PropertyChanged event on the creator thread
  //      _synchronizationContext.Send(RaisePropertyChanged, e);
  //    }
  //  }

  //  private void RaisePropertyChanged(object param)
  //  {
  //    // We are in the creator thread, call the base implementation directly
  //    base.OnPropertyChanged((PropertyChangedEventArgs)param);
  //  }
  //}
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
  public class NodeModel : INotifyPropertyChanged
  {
    public event PropertyChangedEventHandler PropertyChanged;
    protected void OnPropertyChanged([CallerMemberName] string propertyName = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    protected bool SetField<T>(ref T field, T value, [CallerMemberName] string propertyName = null)
    {
      if (EqualityComparer<T>.Default.Equals(field, value)) return false;
      field = value;
      OnPropertyChanged(propertyName);
      return true;
    }
    public NodeData data { get; set; }
    public NodeModel Parent { get; set; }
    public NodeModel LinkedNode { get; set; }
    private bool _isFolder;
    public bool IsFolder
    {
      get { return _isFolder; }
      set
      {
        if (value != _isFolder)
        {
          _isFolder = value;
          if (value) { Image = NodeIcons.Folder; } else { Image = NodeIcons.File; }
        }
      }
    }
    private AvailableUpdateMethods _updateMethod;
    public AvailableUpdateMethods UpdateMethod
    {
      get { return _updateMethod; }
      set
      {
        if (value != _updateMethod)
        {
          _updateMethod = value;
          switch (value)
          {
            case AvailableUpdateMethods.None:
              ImageOverlay = null;
              break;
            case AvailableUpdateMethods.UpToDate:
              ImageOverlay = NodeIcons.UpToDate;
              break;
            case AvailableUpdateMethods.ToDestination:
              ImageOverlay = NodeIcons.Right;
              break;
            case AvailableUpdateMethods.ToSource:
              ImageOverlay = NodeIcons.Left;
              break;
            case AvailableUpdateMethods.ToArchive:
              ImageOverlay = NodeIcons.Archive;
              break;
            case AvailableUpdateMethods.Disregard:
              ImageOverlay = NodeIcons.Nope;
              break;
            default:
              break;
          }
        }
      }
    }
    public ObservableCollection<NodeModel> Items { get; set; }
    private bool _isExpanded = true;
    public bool IsExpanded { get => _isExpanded; set => SetField(ref _isExpanded, value); }
    private bool _isSelected = true;
    public bool IsSelected { get => _isSelected; set => SetField(ref _isSelected, value); }
    public string Name { get; set; }
    public BitmapImage Image { get; private set; } = NodeIcons.File;/**/
    public BitmapImage ImageOverlay { get; private set; }/**/
    //public NodeModel() : this(new NodeData(), null) { }
    public NodeModel(NodeData node) : this(node, null, false) { }
    public NodeModel(NodeData node, NodeModel parent, bool isFolder)
    {
      data = node;
      Name = node.Name;
      Parent = parent;
      Items = new ObservableCollection<NodeModel>();
      IsExpanded = !(parent != null);
      IsFolder = isFolder;
    }
  }
  public class EvalData
  {
    public ObservableCollection<NodeModel> SourceNodes { get; } = new ObservableCollection<NodeModel>();
    public ObservableCollection<NodeModel> DestNodes { get; } = new ObservableCollection<NodeModel>();
    public void Clear()
    {
      SourceNodes.Clear();
      DestNodes.Clear();
    }
    public void Add(string SourcePath, string DestPath)
    {
      throw new NotImplementedException();
      //SourceNodes.Add();
      //DestNodes.Add();
    }
  }
  public enum AvailableNodeImages
  {
    none = 0,
    folder = 1,
    file = 2,
    folderNope = 3,
    //public static BitmapImage Folder = new BitmapImage(new Uri(@"pack://application:,,,/include/folderEmpty.ico", UriKind.Absolute));
  }
  /// <summary>
  /// Interaction logic for MainWindow.xaml
  /// </summary>
  public partial class winMain : Window, INotifyPropertyChanged
  {
    System.Threading.Timer clrHeader;
    static Thread processingThread = null;
    public ObservableCollection<NodeModel> SourceNodes { get; } = new ObservableCollection<NodeModel>();
    public EvalData Nodes { get; } = new EvalData();
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
          _ = Preview(txtSource.Text, txtDestination.Text, Nodes, token);
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





    //async Task<string> PreviewDirectory(string dir, ObservableCollection<NodeModel> nodebase, CancellationToken ct)
    async Task<string> Preview(string sourceDirectory, string destDirectory, EvalData nodebase, CancellationToken ct)
    {
      var tcs = new TaskCompletionSource<string>();
      if (!string.IsNullOrEmpty(sourceDirectory) && !string.IsNullOrEmpty(destDirectory) && nodebase != null)
      {
        sourceDirectory = sourceDirectory.TrimEnd('\\') + "\\";
        destDirectory = destDirectory.TrimEnd('\\') + "\\";
        try
        {
          Dispatcher.Invoke(() =>
          {
            nodebase.Clear();
          });
          NodeModel sourceNode = new NodeModel(new NodeData(sourceDirectory), null, true);
          NodeModel destNode = new NodeModel(new NodeData(destDirectory), null, true) { LinkedNode = sourceNode };
          sourceNode.LinkedNode = destNode;
          await Task.Factory.StartNew(() =>
          {
            processingThread = Thread.CurrentThread;
            try
            {
              Dispatcher.BeginInvoke((Action)(() =>
              {
                nodebase.SourceNodes.Add(sourceNode);
                nodebase.DestNodes.Add(destNode);
              }));
              ProcessDirectory(sourceNode, destNode, ct);
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
    private void ProcessDirectory(NodeModel sourceNode, NodeModel destNode, CancellationToken ct)
    {
      if (sourceNode == null) { return; }
      if (Recursive)
      {
        string filespec = sourceNode.data.Filespec;
        int lenSourceBase = sourceNode.data.Filespec.Length;
        int lenDestBase = destNode.data.Filespec.Length;
        try
        {
          foreach (var f in Directory.GetDirectories(sourceNode.data.Filespec))
          {
            filespec = f;
            if (ct.IsCancellationRequested)
            {
              return;
            }
            var subDir = f.Substring(lenSourceBase);
            var subSourceNode = new NodeModel(new NodeData(f), sourceNode, true);
            var subDestNode = new NodeModel(new NodeData(destNode.data.Filespec + subDir), destNode, true) { LinkedNode = subSourceNode };
            subSourceNode.LinkedNode = subDestNode;
            ProcessDirectory(subSourceNode, subDestNode, ct);
            Dispatcher.BeginInvoke((Action)(() =>
            {
              sourceNode.Items.Add(subSourceNode);
              destNode.Items.Add(subDestNode);
            }));
            TotalFolders++;
          }
          foreach (var f in Directory.GetFiles(sourceNode.data.Filespec))
          {
            filespec = f;
            if (ct.IsCancellationRequested)
            {
              return;
            }
            TotalFiles++;

            var subDir = f.Substring(lenSourceBase);
            var sourcefileinfo = new FileInfo(f);
            var destfileinfo = new FileInfo(destNode.data.Filespec + subDir);
            var subSourceNode = new NodeModel(new NodeData(sourcefileinfo.Name), sourceNode, false);
            var subDestNode = new NodeModel(new NodeData(destfileinfo.Name), destNode, false) { LinkedNode = sourceNode };
            subSourceNode.LinkedNode = subDestNode;
            if (sourcefileinfo.LastWriteTimeUtc == destfileinfo.LastWriteTimeUtc)
            {
              subDestNode.UpdateMethod = subSourceNode.UpdateMethod = AvailableUpdateMethods.UpToDate;
            }
            else if (sourcefileinfo.LastWriteTimeUtc > destfileinfo.LastWriteTimeUtc)
            {
              subDestNode.UpdateMethod = subSourceNode.UpdateMethod = AvailableUpdateMethods.ToDestination;
            }
            else if (sourcefileinfo.LastWriteTimeUtc < destfileinfo.LastWriteTimeUtc)
            {
              subDestNode.UpdateMethod = subSourceNode.UpdateMethod = AvailableUpdateMethods.ToSource;
            }
            Dispatcher.BeginInvoke((Action)(() =>
            {
              sourceNode.Items.Add(subSourceNode);
              destNode.Items.Add(subDestNode);
            }));
            TotalSize += sourcefileinfo.Length;
          }
        }
        catch (UnauthorizedAccessException)
        {
          if (filespec == sourceNode.data.Filespec)
          {
            Dispatcher.BeginInvoke((Action)(() =>
            {
              destNode.UpdateMethod = sourceNode.UpdateMethod = AvailableUpdateMethods.Disregard;
            }));
          }
          else
          {
            // need to test this
          }
          TotalFolders++;
        }
        catch (Exception)
        {
          throw;
        }
      }
    }

    private void tree_ScrollChanged(object sender, ScrollChangedEventArgs e)
    {
      //if (treeSource.Template.FindName("_tv_scrollviewer_", treeSource) == (ScrollViewer)sender)
      //{
      //  ((ScrollViewer)treeDest.Template.FindName("_tv_scrollviewer_", treeDest));
      //}
      //else
      //{
      //  ((ScrollViewer)treeSource.Template.FindName("_tv_scrollviewer_", treeSource)).ScrollToVerticalOffset(e.VerticalOffset);
      //}
      if (sender as System.Windows.Controls.TreeView == treeSource)
      {
        ((ScrollViewer)VisualTreeHelper.GetChild(VisualTreeHelper.GetChild(treeDest, 0), 0)).ScrollToVerticalOffset(e.VerticalOffset);
      }
      else
      {
        ((ScrollViewer)VisualTreeHelper.GetChild(VisualTreeHelper.GetChild(treeSource, 0), 0)).ScrollToVerticalOffset(e.VerticalOffset);
      }
    }

    private void TreeViewItem_Expanded(object sender, RoutedEventArgs e)
    {
      (((NodeModel)(((TreeViewItem)e.OriginalSource).DataContext)).LinkedNode).IsExpanded = true;
    }

    private void TreeViewItem_Collapsed(object sender, RoutedEventArgs e)
    {
      (((NodeModel)(((TreeViewItem)e.OriginalSource).DataContext)).LinkedNode).IsExpanded = false;
    }

    private void treeViewItem_Selected(object sender, RoutedEventArgs e)
    {
      (((NodeModel)(((TreeViewItem)e.OriginalSource).DataContext)).LinkedNode).IsSelected = true;
    }
  }
}
