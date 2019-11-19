using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
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
        MessageBox.Show(ex.Message, "Fatal Error",  MessageBoxButton.OK,MessageBoxImage.Error);
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
    }
    private void SaveSettings()
    {
      settings.Default.LastWindowState = this.WindowState;
      settings.Default.LastWindowRect = this.RestoreBounds;
      settings.Default.Save();
    }
    private void Window_Initialized(object sender, EventArgs e)
    {
      txtVersion.Text = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version.ToString();
      ClearStatus();
      ApplySettings();
    }
  }
}
