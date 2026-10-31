using System.Windows;

namespace MarsExplorer;

public partial class MainWindow
{
  void MenuItemViewFileExit_Click(object sender, RoutedEventArgs e)
  {
    Close();
  }

  void MenuItemViewResetView_Click(object sender, RoutedEventArgs e)
  {
    ResetView();
  }

  void MenuItemViewZoomIn_Click(object sender, RoutedEventArgs e)
  {
    ZoomAt(ViewCenter, 0.8);
  }

  void MenuItemViewZoomOut_Click(object sender, RoutedEventArgs e)
  {
    ZoomAt(ViewCenter, 1.25);
  }
}
