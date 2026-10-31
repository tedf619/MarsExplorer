using System.Windows.Media.Media3D;

namespace MarsExplorer;

/// <summary>
/// Mars-fixed Cartesian frame: +Z = north pole, +X = 0° longitude, spherical radius 3,389,500 m
/// (as defined by Cesium.Ellipsoid.MARS).
/// </summary>
public static class MarsMath
{
    public const double Radius = 3_389_500.0;  // 3,389.5 km

    public static Vector3D FromLatLon(double latRadians, double lonRadians, double radius)
    {
        double latCos = Math.Cos(latRadians);
        return new Vector3D(radius * latCos * Math.Cos(lonRadians), radius * latCos * Math.Sin(lonRadians), radius * Math.Sin(latRadians));
    }

    public static (double lat, double lon) ToLatLon(Vector3D p)
    {
        double vectorLength = p.Length;
        return (Math.Asin(Math.Clamp(p.Z / vectorLength, -1, 1)), Math.Atan2(p.Y, p.X));
    }
}
