using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Media3D;

namespace MarsExplorer;

public partial class MainWindow : Window
{
  const double FovDegrees = 45;  // Field of View from camera
  readonly double FovTan = Math.Tan(FovDegrees * Math.PI / 180);
  const double MinAltitude = 50_000;      // 50 km
  const double MaxAltitude = 25_000_000;  // 25,000 km
  const double MaxLatitudeRadians = 89.5 * Math.PI / 180;
  const double MarsRadius = MarsMath.Radius;  // meters

  TilesetRenderer tilesetRenderer;

  public MainWindow()
  {
    InitializeComponent();

    string apiToken = Environment.GetEnvironmentVariable("MARS_CESIUM_ION_TOKEN") ?? string.Empty;

    if (apiToken == string.Empty)
    {
      MessageBox.Show("Get a free API token from Cesium ion.", "No API Token", MessageBoxButton.OK, MessageBoxImage.Error);
      Close();
    }

    tilesetRenderer = new TilesetRenderer(SceneRoot, apiToken);

    CompositionTarget.Rendering += CompositionTarget_Rendering;

    ResetView();
  }

  async void Window_Loaded(object sender, RoutedEventArgs e)
  {
    await StartAsync();
  }

  void Window_KeyDown(object sender, KeyEventArgs e)
  {
    switch (e.Key)
    {
      case Key.R: ResetView(); break;
      case Key.Add or Key.OemPlus: ZoomAt(ViewCenter, 0.8); break;
      case Key.Subtract or Key.OemMinus: ZoomAt(ViewCenter, 1.25); break;
    }
  }

  async Task StartAsync()
  {
    ShowOverlay("Connecting to Cesium ion…");
    SceneRoot.Children.Clear();
    StatusBarMessage.Text = "";

    try
    {
      await tilesetRenderer.LoadRootTilesetAsync();

      HideOverlay();
    }
    catch (Exception ex)
    {
      ShowOverlay(ex.Message);
    }
  }

  void ShowOverlay(string text)
  {
    Overlay.Text = text;
    Overlay.Visibility = Visibility.Visible;
  }

  void HideOverlay()
  {
    Overlay.Visibility = Visibility.Collapsed;
  }

  /// <summary>
  /// CompositionTarget is a static WPF class that represents the WPF display surface.
  /// Its Rendering event fires just before WPF renders the composition tree. This handler
  /// is where we update the Mars tiles before they are displayed.
  /// </summary>
  void CompositionTarget_Rendering(object? sender, EventArgs e)
  {
    if (tilesetRenderer.Update(MakeCameraInfo()))
      UpdateStatusbarTilesText();
  }
}
