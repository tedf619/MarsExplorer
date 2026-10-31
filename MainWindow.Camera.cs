using System.Windows;
using System.Windows.Media.Media3D;

namespace MarsExplorer;

public partial class MainWindow
{
  // Camera hovers at (lat, lon, alt) above Mars, looking at the sphere's center.

  double cameraLatitude, cameraLongitude, cameraAltitude;
  Vector3D cameraPosition, cameraDirection, cameraUp, cameraRight;

  void ResetView()
  {
    cameraLatitude = 0;
    cameraLongitude = 0;
    cameraAltitude = 8_000_000;
    UpdateCamera();
  }

  double MetersPerPixel => 2 * cameraAltitude * FovTan / Math.Max(1, ViewHost.ActualWidth);

  void UpdateCamera()
  {
    cameraPosition = MarsMath.FromLatLon(cameraLatitude, cameraLongitude, MarsRadius + cameraAltitude);
    var dirHat = cameraPosition / cameraPosition.Length;
    cameraDirection = -dirHat;
    var north = new Vector3D(0, 0, 1) - dirHat * dirHat.Z;
    north.Normalize();
    cameraUp = north;
    cameraRight = Vector3D.CrossProduct(cameraDirection, cameraUp);

    // The camera sits at the origin and the whole scene is shifted by -position.
    Cam.Position = new Point3D(0, 0, 0);
    Cam.LookDirection = cameraDirection;
    Cam.UpDirection = cameraUp;
    Cam.FieldOfView = FovDegrees;
    SceneRoot.Transform = new TranslateTransform3D(-cameraPosition);

    double horizon = Math.Sqrt(Math.Max(0, (MarsRadius + cameraAltitude) * (MarsRadius + cameraAltitude) - MarsRadius * MarsRadius));
    Cam.NearPlaneDistance = Math.Max(1.0, cameraAltitude * 0.1);
    Cam.FarPlaneDistance = horizon + 40_000;

    tilesetRenderer.IsDirty = true;  // to update tiles for new camera position

    UpdateStatusbarAltitudeStatus();
  }

  CameraInfo MakeCameraInfo()
  {
    double w = Math.Max(1, ViewHost.ActualWidth), h = Math.Max(1, ViewHost.ActualHeight);
    double hfov = FovDegrees * Math.PI / 180;
    double vfov = 2 * Math.Atan(FovTan * h / w);
    return new CameraInfo(cameraPosition, cameraDirection, hfov, vfov, h);
  }

  /// <summary>Latitude/longitude (radians) of the point on the reference sphere under a view-space position.</summary>
  (double lat, double lon)? PickLatLon(Point p)
  {
    double w = ViewHost.ActualWidth, h = ViewHost.ActualHeight;
    if (w < 1 || h < 1) return null;

    double nx = 2 * p.X / w - 1;
    double ny = 1 - 2 * p.Y / h;
    double tanV = FovTan * h / w;

    var d = cameraDirection + cameraRight * (nx * FovTan) + cameraUp * (ny * tanV);
    d.Normalize();

    double b = Vector3D.DotProduct(cameraPosition, d);
    double c = cameraPosition.LengthSquared - MarsRadius * MarsRadius;
    double disc = b * b - c;
    if (disc < 0) return null;

    double t = -b - Math.Sqrt(disc);
    if (t < 0) return null;

    return MarsMath.ToLatLon(cameraPosition + d * t);
  }

  static double WrapLon(double lon)
  {
    while (lon > Math.PI) lon -= 2 * Math.PI;
    while (lon < -Math.PI) lon += 2 * Math.PI;
    return lon;
  }

  /// <summary>Multiplies the altitude by a factor, keeping the point under the cursor fixed.</summary>
  void ZoomAt(Point p, double factor)
  {
    var before = PickLatLon(p);
    cameraAltitude = Math.Clamp(cameraAltitude * factor, MinAltitude, MaxAltitude);
    UpdateCamera();

    if (before is { } a && PickLatLon(p) is { } b)
    {
      cameraLatitude = Math.Clamp(cameraLatitude + a.lat - b.lat, -MaxLatitudeRadians, MaxLatitudeRadians);
      cameraLongitude = WrapLon(cameraLongitude + a.lon - b.lon);
      UpdateCamera();
    }
  }

  Point ViewCenter => new(ViewHost.ActualWidth / 2, ViewHost.ActualHeight / 2);
}
