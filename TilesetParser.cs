using System.IO;
using System.Text.Json;
using System.Windows.Media.Media3D;

using MarsExplorer.Models;

namespace MarsExplorer;

/// <summary>
/// A tileset is a hierarchical collection of spatial data (such as 3D meshes, point clouds, imagery, 
/// or terrain) that has been divided into manageable chunks called tiles and organized into a spatial 
/// data structure (like a quadtree or bounding volume hierarchy).
///
/// In modern 3D geospatial engines(like Cesium), tilesets are the core mechanism that allow applications 
/// to stream and render massive datasets—ranging from an entire city to planetary body — smoothly without 
/// overwhelming the computer's memory or graphics system.
/// </summary>
/// 
public enum TileState { Unloaded, Loading, Loaded, Failed }

public enum ContentKind { None, Model, ExternalTileset }

public readonly record struct BoundingSphere(Vector3D Center, double Radius);

/// <summary>
/// Parses 3D tileset.json returned from Cesium ion. 
/// </summary>
public static class TilesetParser
{
  public static HierarchicalTile Parse(byte[] json, Uri baseUri, Matrix3D parentWorld, HierarchicalTile? parent)
  {
    Models.TilesetJson? response = JsonSerializer.Deserialize<TilesetJson>(json);

    if (response == null)
      throw new InvalidDataException("Invalid tileset.json response.");

    if (response.Root == null)
      throw new InvalidDataException("tileset.json response has no root tile.");

    return ParseTileRecursively(response.Root, baseUri, parentWorld, parent);
  }

  static HierarchicalTile ParseTileRecursively(Models.Tile t, Uri baseUri, Matrix3D parentWorld, HierarchicalTile? parentTile)
  {
    var local = Matrix3D.Identity;
    if (t.Transform != null && t.Transform.Count == 16)
    {
      var a = new double[16];
      int i = 0;
      foreach (var v in t.Transform)
        a[i++] = v;

      // 3D Tiles stores a column-major column-vector matrix; reading the same 16 numbers row by row
      // gives the equivalent row-vector matrix that WPF uses.
      local = new Matrix3D(a[0], a[1], a[2], a[3], a[4], a[5], a[6], a[7],
                           a[8], a[9], a[10], a[11], a[12], a[13], a[14], a[15]);
    }

    var world = local * parentWorld;

    var tile = new HierarchicalTile
    {
      Parent = parentTile,
      WorldTransform = world,
      GeometricError = t.GeometricError
    };

    tile.Bounds = ParseBounds(t.BoundingVolume, world);

    TileContent? content = t.Content;

    if (content == null && t.Contents != null && t.Contents.Count > 0)
      content = t.Contents[0];

    if (content is { } ce)
    {
      if (!string.IsNullOrEmpty(ce.Uri))
      {
        tile.ContentUri = new Uri(baseUri, ce.Uri);
        string path = tile.ContentUri.AbsolutePath.ToLower();
        tile.Kind = path.EndsWith(".json") ? ContentKind.ExternalTileset : ContentKind.Model;
      }
    }

    if (t.Children != null)
      foreach (var child in t.Children)
        tile.Children.Add(ParseTileRecursively(child, baseUri, world, tile));

    return tile;
  }

  static BoundingSphere ParseBounds(BoundingVolume bv, Matrix3D world)
  {
    double scale = world.Transform(new Vector3D(1, 0, 0)).Length;

    if (scale <= 0)
      scale = 1;

    if (bv.Region != null)
    {
      var r = bv.Region.Select(x => x).ToArray();
      return RegionSphere(r[0], r[1], r[2], r[3], r[4], r[5]);
    }

    if (bv.Box != null)
    {
      var b = bv.Box.Select(x => x).ToArray();
      var c = world.Transform(new Point3D(b[0], b[1], b[2]));
      double r2 = 0;

      for (int axis = 0; axis < 3; axis++)
        r2 += b[3 + axis * 3] * b[3 + axis * 3] + b[4 + axis * 3] * b[4 + axis * 3] + b[5 + axis * 3] * b[5 + axis * 3];

      return new BoundingSphere(new Vector3D(c.X, c.Y, c.Z), Math.Sqrt(r2) * scale);
    }

    if (bv.Sphere == null)
      throw new InvalidDataException("Tile has an unsupported bounding volume.");

    var s = bv.Sphere.Select(x => x).ToArray();
    var w = world.Transform(new Point3D(s[0], s[1], s[2]));

    return new BoundingSphere(new Vector3D(w.X, w.Y, w.Z), s[3] * scale);
  }

  /// <summary>
  /// Enclosing sphere of a longitude/latitude/height region (angles in radians) on the Martian sphere.
  /// </summary>
  static BoundingSphere RegionSphere(double west, double south, double east, double north, double minH, double maxH)
  {
    const double R = MarsMath.Radius;
    double lonSpan = east - west, latSpan = north - south;

    if (lonSpan > 3.0 || latSpan > 3.0)
      return new BoundingSphere(new Vector3D(0, 0, 0), R + Math.Max(maxH, 0) + 1);

    const int n = 8;
    var pts = new List<Vector3D>((n + 1) * (n + 1) * 2);

    for (int i = 0; i <= n; i++)
      for (int j = 0; j <= n; j++)
        foreach (double h in new[] { minH, maxH })
          pts.Add(MarsMath.FromLatLon(south + latSpan * j / n, west + lonSpan * i / n, R + h));

    var c = new Vector3D(pts.Average(p => p.X), pts.Average(p => p.Y), pts.Average(p => p.Z));
    double maxDist = pts.Max(p => (p - c).Length);
    double step = Math.Max(lonSpan, latSpan) / n;
    double sagitta = R * step * step / 8.0; // surface bulges between sample points

    return new BoundingSphere(c, maxDist + sagitta + 1);
  }
}
