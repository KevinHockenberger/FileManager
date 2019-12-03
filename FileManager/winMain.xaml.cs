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
    public static BitmapImage Disregard = new BitmapImage(new Uri(@"pack://application:,,,/include/cancel16.png", UriKind.Absolute));
    public static BitmapImage Delete = new BitmapImage(new Uri(@"pack://application:,,,/include/nope16.png", UriKind.Absolute));
    public static BitmapImage Right = new BitmapImage(new Uri(@"pack://application:,,,/include/right16.png", UriKind.Absolute));
    public static BitmapImage Left = new BitmapImage(new Uri(@"pack://application:,,,/include/left16.png", UriKind.Absolute));
    public static BitmapImage UpToDate = new BitmapImage(new Uri(@"pack://application:,,,/include/check16.png", UriKind.Absolute));
    public static BitmapImage Archive = new BitmapImage(new Uri(@"pack://application:,,,/include/a16.png", UriKind.Absolute));
    public static BitmapImage Question = new BitmapImage(new Uri(@"pack://application:,,,/include/question16.png", UriKind.Absolute));
    public static BitmapImage Add = new BitmapImage(new Uri(@"pack://application:,,,/include/add16.png", UriKind.Absolute));
  }
  public enum AvailableUpdateMethods
  {
    None = 0,
    UpToDate = 1,
    ToDestination = 2,
    ToSource = 3,
    ToArchive = 4,
    Disregard = 5,
    Delete = 6,
    Question = 7,
    Add = 8
  }

  // removed the following in favor of xaml in treeviews: VirtualizingStackPanel.IsVirtualizing="True" VirtualizingStackPanel.VirtualizationMode="Recycling"
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
              ImageOverlay = NodeIcons.Disregard;
              break;
            case AvailableUpdateMethods.Delete:
              ImageOverlay = NodeIcons.Delete;
              break;
            case AvailableUpdateMethods.Question:
              ImageOverlay = NodeIcons.Question;
              break;
            case AvailableUpdateMethods.Add:
              ImageOverlay = NodeIcons.Add;
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
  public partial class winMain : Window, INotifyPropertyChanged
  {
    System.Threading.Timer clrHeader;
    System.Threading.Timer SourceChanging;
    static Thread processingThread = null;
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
    private int _progress;
    public int Progress { get => _progress; set => SetField(ref _progress, value); }
    public bool Recursive { get { return settings.Default.LastRecursive; } }
    private bool _processCancelled;
    public bool ProcessCancelled { get => _processCancelled; set => SetField(ref _processCancelled, value); }
    private NodeModel _selectedSourceNode;
    public NodeModel SelectedSourceNode { get => _selectedSourceNode; set => SetField(ref _selectedSourceNode, value); }
    private NodeModel _selectedDestNode;
    public NodeModel SelectedDestNode { get => _selectedDestNode; set => SetField(ref _selectedDestNode, value); }
    private int _totalCount;
    public int TotalCount { get => _totalCount; set => SetField(ref _totalCount, value); }
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
        Status = string.Empty;
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
        Status = message;
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
      ClearStatus();
      ApplySettings();
      ResetForm();
      GetTotalCount(null);
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
      ResetFormForStart();
      _ = ProcessAsync(true);
    }
    private void BtnStart_Click(object sender, RoutedEventArgs e)
    {
      winConfirmStart d = new winConfirmStart() { Owner = this };
      d.ShowDialog();
      if (d.DialogResult == true)
      {
        ResetFormForStart();
        _ = ProcessAsync(false);
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
    private void GetTotalCount(object o)
    {
      Dispatcher.Invoke(() =>
      {
        string s = txtSource.Text;
        if (Directory.Exists(s))
        {
          try
          {
            _ = Task.Factory.StartNew(() =>
            {
              try
              {
                TotalCount = Directory.GetFiles(s, "*", SearchOption.AllDirectories).Length;
              }
              catch (Exception)
              {
                Dispatcher.BeginInvoke((Action)(() => { UpdateStatus("Cannot determine number of files.", new SolidColorBrush(Colors.Transparent), new SolidColorBrush(Colors.Red)); }));
                TotalCount = 0;
              }
            });
            SourceChanging.Dispose();
            SourceChanging = null;
          }
          catch (Exception)
          {
          }
        }
      });
    }
    private void TxtSource_TextChanged(object sender, TextChangedEventArgs e)
    {
      ResetForm();
      TotalCount = 0;
      if (SourceChanging == null)
      {
        SourceChanging = new System.Threading.Timer(new System.Threading.TimerCallback(GetTotalCount), null, 5000, System.Threading.Timeout.Infinite);
      }
      else
      {
        SourceChanging.Change(5000, System.Threading.Timeout.Infinite);
      }
    }
    private void TxtDestination_TextChanged(object sender, TextChangedEventArgs e)
    {
      ResetForm();
    }
    private void ResetForm()
    {
      Progress = 0;
      Nodes.Clear();
      progressbar.Visibility = Visibility.Hidden;
      txtProgress.Visibility = Visibility.Hidden;
      TotalFolders = 0;
      TotalFiles = 0;
      TotalSize = 0;
      //TotalCount = 0;
    }
    private void ResetFormForStart()
    {
      Progress = 0;
      Nodes.Clear();
      TotalFolders = 0;
      TotalFiles = 0;
      TotalSize = 0;
      //TotalCount = 0;
      progressbar.Visibility = Visibility.Visible;
      txtProgress.Visibility = Visibility.Visible;
    }
    private void TxtLog_TextChanged(object sender, TextChangedEventArgs e)
    {

    }
    private void BtnFlipDirectory_Click(object sender, RoutedEventArgs e)
    {
      var source = txtSource.Text;
      txtSource.Text = txtDestination.Text;
      txtDestination.Text = source;
    }
    private void TreeSource_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
    {
      SelectedSourceNode = e.NewValue as NodeModel;
    }
    private void TreeDest_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
    {
      SelectedDestNode = e.NewValue as NodeModel;
    }
    private bool ToggleProcessing()
    {
      Progress = 0;
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
      Progress = 100;
      Dispatcher.Invoke(() => { btnPreview.Content = "preview"; UpdateStatus("Processing complete.", null, null); });
    }
    private void AbortProcessing()
    {
      Progress = 0;
      ProcessCancelled = true;
      Dispatcher.Invoke(() => { btnPreview.Content = "preview"; UpdateStatus("Processing aborting.", null, null); });
    }
    //private async Task PreviewAsync()
    //{
    //  try
    //  {
    //    if (!ToggleProcessing()) { return; }
    //    string s = txtSource.Text;
    //    if (Directory.Exists(s))
    //    {
    //      cts = new CancellationTokenSource();
    //      CancellationToken token = cts.Token;
    //      _ = Preview(s, txtDestination.Text, Nodes, Recursive, chkArchive.IsChecked == true, int.TryParse(txtArchiveDays.Text, out int i) ? i : settings.Default.LastArchiveDays, token);
    //      await Task.Factory.StartNew(() =>
    //      {
    //        while (processingThread != null)
    //        {
    //          if (token.IsCancellationRequested)
    //          {
    //            processingThread.Abort();
    //            processingThread = null;
    //            cts = null;
    //            AbortProcessing();
    //            return;
    //          }
    //        }
    //      });
    //      CompleteProcessing();
    //    }
    //  }
    //  catch (Exception ex)
    //  {
    //    UpdateStatus(ex.Message);
    //  }
    //}
    //async Task<string> Preview(string sourceDirectory, string destDirectory, EvalData nodebase, bool recursive, bool archive, int daysToArchive, CancellationToken ct)
    //{
    //  var tcs = new TaskCompletionSource<string>();
    //  if (!string.IsNullOrEmpty(sourceDirectory) && !string.IsNullOrEmpty(destDirectory) && nodebase != null && !destDirectory.StartsWith(sourceDirectory) && !sourceDirectory.StartsWith(destDirectory))
    //  {
    //    sourceDirectory = sourceDirectory.TrimEnd('\\') + "\\";
    //    destDirectory = destDirectory.TrimEnd('\\') + "\\";
    //    try
    //    {
    //      Dispatcher.Invoke(() =>
    //      {
    //        nodebase.Clear();
    //      });
    //      NodeModel sourceNode = new NodeModel(new NodeData(sourceDirectory), null, true);
    //      NodeModel destNode = new NodeModel(new NodeData(destDirectory), null, true) { LinkedNode = sourceNode };
    //      sourceNode.LinkedNode = destNode;
    //      await Task.Factory.StartNew(() =>
    //      {
    //        processingThread = Thread.CurrentThread;
    //        try
    //        {
    //          Dispatcher.BeginInvoke((Action)(() =>
    //          {
    //            nodebase.SourceNodes.Add(sourceNode);
    //            nodebase.DestNodes.Add(destNode);
    //          }));
    //          PreviewDirectory(sourceNode, destNode, recursive, archive, daysToArchive, ct);

    //        }
    //        catch (Exception)
    //        {
    //          throw;
    //        }
    //      });
    //    }
    //    catch (Exception ex)
    //    {
    //      processingThread = null;
    //      cts = null;
    //      tcs.SetResult("Error : " + ex.Message);
    //      return tcs.Task.Result;
    //    }
    //  }
    //  else
    //  {
    //    UpdateStatus("Unable to process folders as source cannot contain destination and vice versa.");
    //  }
    //  processingThread = null;
    //  cts = null;
    //  tcs.SetResult("Complete");
    //  return tcs.Task.Result;
    //}
    //private void PreviewDirectory(NodeModel sourceNode, NodeModel destNode, bool recursive, bool archive, int daysToArchive, CancellationToken ct)
    //{
    //  if (processingThread == null || sourceNode == null) { return; }
    //  string filespec = sourceNode.data.Filespec;
    //  int lenSourceBase = sourceNode.data.Filespec.Length;
    //  int lenDestBase = destNode.data.Filespec.Length;
    //  if (recursive)
    //  {
    //    try
    //    {
    //      foreach (var f in Directory.GetDirectories(sourceNode.data.Filespec))
    //      {
    //        filespec = f;
    //        if (ct.IsCancellationRequested)
    //        {
    //          return;
    //        }
    //        var subDir = f.Substring(lenSourceBase);
    //        var subSourceNode = new NodeModel(new NodeData(f), sourceNode, true);
    //        var subDestNode = new NodeModel(new NodeData(destNode.data.Filespec + subDir), destNode, true) { LinkedNode = subSourceNode };
    //        subSourceNode.LinkedNode = subDestNode;
    //        PreviewDirectory(subSourceNode, subDestNode, recursive, archive, daysToArchive, ct);
    //        sourceNode.data.TotalDirectories++;
    //        Dispatcher.BeginInvoke((Action)(() =>
    //        {
    //          sourceNode.Items.Add(subSourceNode);
    //          destNode.Items.Add(subDestNode);
    //        }));
    //        TotalFolders++;
    //      }
    //    }
    //    catch (UnauthorizedAccessException)
    //    {
    //      Dispatcher.BeginInvoke((Action)(() =>
    //      {
    //        destNode.UpdateMethod = sourceNode.UpdateMethod = AvailableUpdateMethods.Disregard;
    //      }));
    //    }
    //    catch (Exception)
    //    {
    //      //throw;
    //    }
    //  }
    //  try
    //  {
    //    foreach (var f in Directory.GetFiles(sourceNode.data.Filespec))
    //    {
    //      try
    //      {
    //        filespec = f;
    //        if (ct.IsCancellationRequested)
    //        {
    //          return;
    //        }
    //        TotalFiles++;
    //        if (TotalCount > 0)
    //        {
    //          Progress = (int)(((decimal)TotalFiles / TotalCount) * 100);
    //        }
    //        var subDir = f.Substring(lenSourceBase);
    //        var sourcefileinfo = new FileInfo(f);
    //        var destFilespec = destNode.data.Filespec + subDir;
    //        var subSourceNode = new NodeModel(new NodeData(sourcefileinfo.Name), sourceNode, false);
    //        subSourceNode.data.Created = sourcefileinfo.CreationTime;
    //        subSourceNode.data.Filespec = sourcefileinfo.FullName;
    //        subSourceNode.data.LastModified = sourcefileinfo.LastWriteTime;
    //        subSourceNode.data.Name = sourcefileinfo.Name;
    //        subSourceNode.data.TotalData = sourcefileinfo.Length;
    //        sourceNode.data.TotalFiles++;
    //        sourceNode.data.TotalData += sourcefileinfo.Length;
    //        NodeModel subDestNode = null;
    //        if (File.Exists(destFilespec))
    //        {
    //          var destfileinfo = new FileInfo(destFilespec);
    //          subDestNode = new NodeModel(new NodeData(destfileinfo.Name), destNode, false) { LinkedNode = subSourceNode };
    //          subSourceNode.LinkedNode = subDestNode;
    //          subDestNode.data.Created = destfileinfo.CreationTime;
    //          subDestNode.data.Filespec = destfileinfo.FullName;
    //          subDestNode.data.LastModified = destfileinfo.LastWriteTime;
    //          subDestNode.data.Name = destfileinfo.Name;
    //          subDestNode.data.TotalData = destfileinfo.Length;
    //          if (archive && (DateTime.Now - sourcefileinfo.LastWriteTimeUtc).TotalDays > daysToArchive)
    //          { // archive copies to destination and removes the original (regardless of delete original option) if the last write time is older than the days specified
    //            subSourceNode.UpdateMethod = AvailableUpdateMethods.Delete;
    //            subDestNode.UpdateMethod = AvailableUpdateMethods.ToArchive;
    //          }
    //          else if (sourcefileinfo.LastWriteTimeUtc == destfileinfo.LastWriteTimeUtc)
    //          {
    //            subDestNode.UpdateMethod = subSourceNode.UpdateMethod = AvailableUpdateMethods.UpToDate;
    //          }
    //          else if (sourcefileinfo.LastWriteTimeUtc > destfileinfo.LastWriteTimeUtc)
    //          {
    //            subDestNode.UpdateMethod = subSourceNode.UpdateMethod = AvailableUpdateMethods.ToDestination;
    //          }
    //          else if (sourcefileinfo.LastWriteTimeUtc < destfileinfo.LastWriteTimeUtc)
    //          {
    //            subDestNode.UpdateMethod = subSourceNode.UpdateMethod = AvailableUpdateMethods.ToSource;
    //          }

    //        }
    //        else
    //        {
    //          // destination file does not exist
    //          subDestNode = new NodeModel(new NodeData(Path.GetFileName(destFilespec)), destNode, false) { LinkedNode = subSourceNode };
    //          subSourceNode.LinkedNode = subDestNode;
    //          subDestNode.data.Created = DateTime.Now;
    //          subDestNode.data.Filespec = destFilespec;
    //          subDestNode.data.LastModified = DateTime.Now;
    //          subDestNode.data.Name = Path.GetFileName(destFilespec);
    //          subSourceNode.UpdateMethod = AvailableUpdateMethods.ToDestination;
    //          subDestNode.UpdateMethod = AvailableUpdateMethods.Add;
    //        }
    //        Dispatcher.BeginInvoke((Action)(() =>
    //        {
    //          sourceNode.Items.Add(subSourceNode);
    //          destNode.Items.Add(subDestNode);
    //        }));
    //        TotalSize += sourcefileinfo.Length;

    //      }
    //      catch (UnauthorizedAccessException)
    //      {
    //        Dispatcher.BeginInvoke((Action)(() =>
    //        {
    //          destNode.UpdateMethod = sourceNode.UpdateMethod = AvailableUpdateMethods.Disregard;
    //        }));
    //      }
    //      catch (Exception)
    //      {
    //        //throw;
    //      }
    //    }
    //    if (Directory.Exists(destNode.data.Filespec))
    //    {
    //      // iterate through the destination folder to find existing files only in destination folder

    //      bool found;
    //      foreach (var destfile in Directory.GetFiles(destNode.data.Filespec))
    //      {
    //        found = false;
    //        var subDir = destfile.Substring(lenDestBase);
    //        foreach (var sourcefile in Directory.GetFiles(sourceNode.data.Filespec))
    //        {
    //          if (Path.GetFileName(sourcefile) == Path.GetFileName(destfile))
    //          {
    //            found = true;
    //            break;
    //          }
    //        }
    //        if (!found)
    //        {
    //          var subSourceNode = new NodeModel(new NodeData(sourceNode.data.Filespec + subDir), sourceNode, false) { UpdateMethod = AvailableUpdateMethods.Disregard };
    //          var destFileInfo = new FileInfo(destfile);
    //          var subDestNode = new NodeModel(new NodeData(destfile), destNode, false) { LinkedNode = subSourceNode, UpdateMethod = AvailableUpdateMethods.Question };
    //          subDestNode.data.TotalData = destFileInfo.Length;
    //          subDestNode.data.Created = destFileInfo.CreationTime;
    //          subDestNode.data.LastModified = destFileInfo.LastWriteTime;
    //          subSourceNode.LinkedNode = subDestNode;
    //          RecursiveSetParentIcon(subDestNode, AvailableUpdateMethods.Question);
    //          Dispatcher.BeginInvoke((Action)(() =>
    //          {
    //            sourceNode.Items.Add(subSourceNode);
    //            destNode.Items.Add(subDestNode);
    //          }));
    //        }
    //      }
    //    }
    //  }
    //  catch (UnauthorizedAccessException)
    //  {
    //  }
    //  catch (Exception)
    //  {
    //    throw;
    //  }
    //}
    private void ProcessDirectory(bool preview, NodeModel sourceNode, NodeModel destNode, bool recursive, bool archive, int daysToArchive, CancellationToken ct)
    {
      if (processingThread == null || sourceNode == null) { return; }
      string filespec = sourceNode.data.Filespec;
      int lenSourceBase = sourceNode.data.Filespec.Length;
      int lenDestBase = destNode.data.Filespec.Length;
      if (recursive)
      {
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
            if (!preview)
            {
              Directory.CreateDirectory(subDestNode.data.Filespec);
            }
            subSourceNode.LinkedNode = subDestNode;
            ProcessDirectory(preview, subSourceNode, subDestNode, recursive, archive, daysToArchive, ct);
            sourceNode.data.TotalDirectories++;
            Dispatcher.BeginInvoke((Action)(() =>
            {
              sourceNode.Items.Add(subSourceNode);
              destNode.Items.Add(subDestNode);
            }));
            TotalFolders++;
          }
        }
        catch (UnauthorizedAccessException)
        {
          Dispatcher.BeginInvoke((Action)(() =>
          {
            destNode.UpdateMethod = sourceNode.UpdateMethod = AvailableUpdateMethods.Disregard;
          }));
        }
        catch (Exception)
        {
          //throw;
        }
      }
      try
      {
        foreach (var f in Directory.GetFiles(sourceNode.data.Filespec))
        {
          try
          {
            filespec = f;
            if (ct.IsCancellationRequested)
            {
              return;
            }
            TotalFiles++;
            if (TotalCount > 0)
            {
              Progress = (int)(((decimal)TotalFiles / TotalCount) * 100);
            }
            var subDir = f.Substring(lenSourceBase);
            var sourcefileinfo = new FileInfo(f);
            var destFilespec = destNode.data.Filespec + subDir;
            var subSourceNode = new NodeModel(new NodeData(sourcefileinfo.Name), sourceNode, false);
            subSourceNode.data.Created = sourcefileinfo.CreationTime;
            subSourceNode.data.Filespec = sourcefileinfo.FullName;
            subSourceNode.data.LastModified = sourcefileinfo.LastWriteTime;
            subSourceNode.data.Name = sourcefileinfo.Name;
            subSourceNode.data.TotalData = sourcefileinfo.Length;
            sourceNode.data.TotalFiles++;
            sourceNode.data.TotalData += sourcefileinfo.Length;
            NodeModel subDestNode = null;
            if (File.Exists(destFilespec))
            {
              var destfileinfo = new FileInfo(destFilespec);
              subDestNode = new NodeModel(new NodeData(destfileinfo.Name), destNode, false) { LinkedNode = subSourceNode };
              subSourceNode.LinkedNode = subDestNode;
              subDestNode.data.Created = destfileinfo.CreationTime;
              subDestNode.data.Filespec = destfileinfo.FullName;
              subDestNode.data.LastModified = destfileinfo.LastWriteTime;
              subDestNode.data.Name = destfileinfo.Name;
              subDestNode.data.TotalData = destfileinfo.Length;
              if (archive && (DateTime.Now - sourcefileinfo.LastWriteTimeUtc).TotalDays > daysToArchive)
              {
                // archive copies to destination and removes the original (regardless of delete original option) if the last write time is older than the days specified
                subSourceNode.UpdateMethod = AvailableUpdateMethods.Delete;
                if (preview)
                {
                  subDestNode.UpdateMethod = AvailableUpdateMethods.ToArchive;
                }
                else
                {
                  if (sourcefileinfo.LastWriteTimeUtc > destfileinfo.LastWriteTimeUtc)
                  {
                    // file not up to date, copy original
                    try
                    {
                      File.Copy(sourcefileinfo.FullName, destfileinfo.FullName, true);
                      subDestNode.UpdateMethod = AvailableUpdateMethods.UpToDate;

                    }
                    catch (Exception)
                    {
                      subDestNode.UpdateMethod = subSourceNode.UpdateMethod = AvailableUpdateMethods.Delete;
                    }
                  }
                  try
                  {
                    File.Delete(sourcefileinfo.FullName);
                    subDestNode.UpdateMethod = AvailableUpdateMethods.UpToDate;
                  }
                  catch (Exception)
                  {
                    subDestNode.UpdateMethod = subSourceNode.UpdateMethod = AvailableUpdateMethods.Delete;
                  }
                }
              }
              else if (sourcefileinfo.LastWriteTimeUtc == destfileinfo.LastWriteTimeUtc)
              {
                subDestNode.UpdateMethod = subSourceNode.UpdateMethod = AvailableUpdateMethods.UpToDate;
              }
              else if (sourcefileinfo.LastWriteTimeUtc > destfileinfo.LastWriteTimeUtc)
              {
                if (preview)
                {
                  subDestNode.UpdateMethod = subSourceNode.UpdateMethod = AvailableUpdateMethods.ToDestination;
                }
                else
                {
                  try
                  {
                    File.Copy(sourcefileinfo.FullName, destfileinfo.FullName, true);
                    subDestNode.UpdateMethod = subSourceNode.UpdateMethod = AvailableUpdateMethods.UpToDate;
                  }
                  catch (Exception)
                  {
                    subDestNode.UpdateMethod = subSourceNode.UpdateMethod = AvailableUpdateMethods.Delete;
                  }
                }
              }
              else if (sourcefileinfo.LastWriteTimeUtc < destfileinfo.LastWriteTimeUtc)
              {
                if (preview)
                {
                  subDestNode.UpdateMethod = subSourceNode.UpdateMethod = AvailableUpdateMethods.ToSource;
                }
                else
                {
                }
              }

            }
            else
            {
              // destination file does not exist
              subDestNode = new NodeModel(new NodeData(destFilespec), destNode, false) { LinkedNode = subSourceNode };
              subSourceNode.LinkedNode = subDestNode;
              //subDestNode.data.Filespec = destFilespec;
              if (preview)
              {
                subDestNode.data.Created = DateTime.Now;
                subDestNode.data.LastModified = DateTime.Now;
                subDestNode.data.Name = Path.GetFileName(destFilespec);
                subSourceNode.UpdateMethod = AvailableUpdateMethods.ToDestination;
                subDestNode.UpdateMethod = AvailableUpdateMethods.Add;
              }
              else
              {
                try
                {
                  File.Copy(sourcefileinfo.FullName, destFilespec);
                  subDestNode.UpdateMethod = subSourceNode.UpdateMethod = AvailableUpdateMethods.UpToDate;
                }
                catch (Exception)
                {
                  subDestNode.UpdateMethod = subSourceNode.UpdateMethod = AvailableUpdateMethods.Delete;
                }
              }
            }
            Dispatcher.BeginInvoke((Action)(() =>
            {
              sourceNode.Items.Add(subSourceNode);
              destNode.Items.Add(subDestNode);
            }));
            TotalSize += sourcefileinfo.Length;

          }
          catch (UnauthorizedAccessException)
          {
            Dispatcher.BeginInvoke((Action)(() =>
            {
              destNode.UpdateMethod = sourceNode.UpdateMethod = AvailableUpdateMethods.Disregard;
            }));
          }
          catch (Exception)
          {
            //throw;
          }
        }
        if (Directory.Exists(destNode.data.Filespec))
        {
          // iterate through the destination folder to find existing files only in destination folder

          bool found;
          foreach (var destfile in Directory.GetFiles(destNode.data.Filespec))
          {
            found = false;
            var subDir = destfile.Substring(lenDestBase);
            foreach (var sourcefile in Directory.GetFiles(sourceNode.data.Filespec))
            {
              if (Path.GetFileName(sourcefile) == Path.GetFileName(destfile))
              {
                found = true;
                break;
              }
            }
            if (!found)
            {
              var subSourceNode = new NodeModel(new NodeData(sourceNode.data.Filespec + subDir), sourceNode, false) { UpdateMethod = AvailableUpdateMethods.Disregard };
              var destFileInfo = new FileInfo(destfile);
              var subDestNode = new NodeModel(new NodeData(destfile), destNode, false) { LinkedNode = subSourceNode, UpdateMethod = AvailableUpdateMethods.Question };
              subDestNode.data.TotalData = destFileInfo.Length;
              subDestNode.data.Created = destFileInfo.CreationTime;
              subDestNode.data.LastModified = destFileInfo.LastWriteTime;
              subSourceNode.LinkedNode = subDestNode;
              RecursiveSetParentIcon(subDestNode, AvailableUpdateMethods.Question);
              Dispatcher.BeginInvoke((Action)(() =>
              {
                sourceNode.Items.Add(subSourceNode);
                destNode.Items.Add(subDestNode);
              }));
            }
          }
        }
      }
      catch (UnauthorizedAccessException)
      {
      }
      catch (Exception)
      {
        throw;
      }
    }
    private void RecursiveSetParentIcon(NodeModel item, AvailableUpdateMethods method)
    {
      if (item.Parent != null)
      {
        item.Parent.UpdateMethod = method;
        RecursiveSetParentIcon(item.Parent, method);
      }
    }
    private async Task ProcessAsync(bool preview)
    {
      try
      {
        if (!ToggleProcessing()) { return; }
        string s = txtSource.Text;
        if (Directory.Exists(s))
        {
          cts = new CancellationTokenSource();
          CancellationToken token = cts.Token;
          _ = Process(preview, s, txtDestination.Text, Nodes, Recursive, chkArchive.IsChecked == true, int.TryParse(txtArchiveDays.Text, out int i) ? i : settings.Default.LastArchiveDays, token);
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
            }
          });
          CompleteProcessing();
        }
      }
      catch (Exception ex)
      {
        UpdateStatus(ex.Message);
      }
    }
    async Task<string> Process(bool preview, string sourceDirectory, string destDirectory, EvalData nodebase, bool recursive, bool archive, int daysToArchive, CancellationToken ct)
    {
      var tcs = new TaskCompletionSource<string>();
      if (!string.IsNullOrEmpty(sourceDirectory) && !string.IsNullOrEmpty(destDirectory) && nodebase != null && !destDirectory.StartsWith(sourceDirectory) && !sourceDirectory.StartsWith(destDirectory))
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
              ProcessDirectory(preview, sourceNode, destNode, recursive, archive, daysToArchive, ct);

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
      else
      {
        UpdateStatus("Unable to process folders as source cannot contain destination and vice versa.");
      }
      processingThread = null;
      cts = null;
      tcs.SetResult("Complete");
      return tcs.Task.Result;
    }
  }
}
