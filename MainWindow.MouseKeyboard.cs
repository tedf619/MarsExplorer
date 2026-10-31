using System.Windows.Input;

namespace MarsExplorer;

public partial class MainWindow
{
  bool isDraggingMouse;
  System.Windows.Point lastMousePosition;
  
  void ViewHost_MouseWheel(object sender, MouseWheelEventArgs e)
  {
    System.Windows.Point mouseLocation = e.GetPosition(ViewHost);
    ZoomAt(mouseLocation, Math.Pow(0.8, e.Delta / 120.0));
    UpdateStatusbarCursorText(e.GetPosition(ViewHost));
    e.Handled = true;
  }

  void ViewHost_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
  {
    System.Windows.Point mouseLocation = e.GetPosition(ViewHost);
    if (e.ClickCount == 2)
    {
      ZoomAt(mouseLocation, 0.5);
      return;
    }
    isDraggingMouse = true;
    lastMousePosition = mouseLocation;
    ViewHost.CaptureMouse();
  }

  void ViewHost_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
  {
    isDraggingMouse = false;
    ViewHost.ReleaseMouseCapture();
  }

  void ViewHost_MouseMove(object sender, MouseEventArgs e)
  {
    System.Windows.Point mouseLocation = e.GetPosition(ViewHost);

    if (isDraggingMouse && e.LeftButton == MouseButtonState.Pressed)
    {
      double dx = mouseLocation.X - lastMousePosition.X, dy = mouseLocation.Y - lastMousePosition.Y;
      lastMousePosition = mouseLocation;

      double mpp = MetersPerPixel;
      double cosLat = Math.Max(0.02, Math.Cos(cameraLatitude));
      cameraLongitude = WrapLon(cameraLongitude - dx * mpp / (MarsRadius * cosLat));
      cameraLatitude = Math.Clamp(cameraLatitude + dy * mpp / MarsRadius, -MaxLatitudeRadians, MaxLatitudeRadians);
      UpdateCamera();
    }

    UpdateStatusbarCursorText(mouseLocation);
  }
}
