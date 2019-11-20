using System;
using System.Windows;
using System.Windows.Forms;
using System.Windows.Media;
using System.Windows.Input;
using settings = FileManager.Properties.Settings;

namespace FileManager
{
  /// <summary>
  /// Interaction logic for MainWindow.xaml
  /// </summary>
  public partial class winMain : Window
  {
    System.Threading.Timer clrHeader;

    public winMain()
    {
      try
      {
        InitializeComponent();
      }
      catch (Exception ex)
      {
        System.Windows.MessageBox.Show(ex.Message, "Fatal Error",  MessageBoxButton.OK,MessageBoxImage.Error);
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
      txtSource.Text = settings.Default.LastSource;
      txtDestination.Text = settings.Default.LastDestination;
      txtLog.Text = settings.Default.LastLogFile;
    }
    private void SaveSettings()
    {
      settings.Default.LastWindowState = this.WindowState;
      settings.Default.LastWindowRect = this.RestoreBounds;
      settings.Default.LastSource = txtSource.Text;
      settings.Default.LastDestination = txtDestination.Text;
      settings.Default.LastLogFile = txtLog.Text;
      settings.Default.Save();
    }
    private void Window_Initialized(object sender, EventArgs e)
    {
      txtVersion.Text = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version.ToString();
      progressbar.Value = 0;
      progressbar.Visibility = Visibility.Hidden; txtProgress.Visibility = Visibility.Hidden;
      lblStatus.Content = "";
      lblFileCount.Content = "";
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
  }
}
