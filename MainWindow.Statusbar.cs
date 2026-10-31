using System.Windows;

namespace MarsExplorer;

public partial class MainWindow
{
  void UpdateStatusbarAltitudeStatus()
  {
    StatusBarAltitude.Text = cameraAltitude < 10_000 ? $"Altitude: {cameraAltitude:0} m" : $"Altitude: {cameraAltitude / 1000:#,0} km";
  }

  void UpdateStatusbarCursorText(Point point)
  {
    if (PickLatLon(point) is { } latlon)
    {
      double lat = latlon.lat * 180 / Math.PI;
      double lon = latlon.lon * 180 / Math.PI;

      StatusBarCursorPosition.Text = $"Latitude: {lat:0.0000}°      Longitude: {lon:0.0000}°";
    }
  }

  void UpdateStatusbarTilesText()
  {
    var r = tilesetRenderer;

    TilesText.Text = $"Tiles: {r.RenderedTiles} shown, {r.LoadingTiles} loading, {r.CachedTileCount} cached";
    StatusBarMessage.Text = r.LastError ?? "";
  }
}
