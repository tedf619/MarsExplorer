using System.Buffers.Binary;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Media.Media3D;

using MarsExplorer.Models;

namespace MarsExplorer;

/// <summary>
/// Turns tile content into a frozen WPF Model3DGroup whose vertices are expressed
/// relative to the tile's bounding-sphere center. The 3D tileset standard supports several 
/// formats: b3dm, glTF and older ones. This app supports only the first two:
/// 
/// 1 .B3DM - Batched 3D Model. A wrapper around a GLB (Graphics Library Binary) file. GLB contains glTF data in binary format.
/// 2. glTF - Graphics Library Transmission Format. It contains text data in JSON format.
///
/// Both formats are used to accelerate the download and rendering of maps containing large numbers of objects.
/// 
/// Note: B3DM, being a binary format, is far more efficient to transmit than glTF json.
/// </summary>
public static class TileModelBuilder
{
  static readonly System.Windows.Media.Media3D.Material FallbackMaterial = MakeFallback();

  static System.Windows.Media.Media3D.Material MakeFallback()
  {
    Color gray = Color.FromRgb(150, 150, 150);
    var brush = new SolidColorBrush(gray);
    brush.Freeze();
    var m = new EmissiveMaterial(brush);
    m.Freeze();
    return m;
  }

  /// <summary>
  /// A Model3DGroup is a WPF 3D container that lets you treat multiple 3D models—such as meshes, lights, and other 
  /// groups—as one unit. A transform applied to the group affects all of its children.
  /// 
  /// Model3DGroup derives from Model3D and has a Children collection of Model3D objects. Typical children include 
  /// GeometryModel3D (visible geometry/material), AmbientLight or DirectionalLight, and nested Model3DGroup objects,
  /// as shown in the diagram below.
  /// 
  /// Viewport3D
  /// └── ModelVisual3D
  ///     └── Model3DGroup             <<<<<
  ///           ├── Lights
  ///           ├── GeometryModel3D objects
  ///           └── Nested Model3DGroup objects
  /// 
  /// You place models and lights in a Model3DGroup, assign that group to a ModelVisual3D.Content, then add the 
  /// ModelVisual3D to a Viewport3D. The Viewport3D also needs a camera to view the scene.
  /// </summary>
  public static Model3DGroup? BuildModel3dGroup(byte[] content, HierarchicalTile tile)
  {
    (byte[] glb, Vector3D rtc) = GetGlb(content);

    List<GlbPrimitive> primitives = GlbReader.GetGlbPrimitives(glb, rtc);
    if (primitives.Count == 0) return null;

    var group = new Model3DGroup();

    foreach (var primitive in primitives)
    {
      GeometryModel3D model = GetGeometryModel3D(primitive, tile);
      group.Children.Add(model);
    }

    group.Freeze();
    return group;
  }

  static GeometryModel3D GetGeometryModel3D(GlbPrimitive primitive, HierarchicalTile tile)
  {
    Vector3D tileOrigin = tile.Bounds.Center;
    var positions = new Point3DCollection(primitive.Positions.Length);

    foreach (var pt in primitive.Positions)
    {
      Point3D w = tile.WorldTransform.Transform(pt);
      positions.Add(new Point3D(w.X - tileOrigin.X, w.Y - tileOrigin.Y, w.Z - tileOrigin.Z));
    }

    var mesh = new MeshGeometry3D
    {
      Positions = positions,
      TriangleIndices = new Int32Collection(primitive.Indices)
    };

    if (primitive.Uvs != null)
      mesh.TextureCoordinates = new PointCollection(primitive.Uvs);

    mesh.Freeze();

    System.Windows.Media.Media3D.Material material = FallbackMaterial;

    if (primitive.ImageBytes != null && primitive.Uvs != null)
      material = MakeTextureMaterial(primitive.ImageBytes, primitive.ImageMime);

    var model = new GeometryModel3D(mesh, material) { BackMaterial = material };
    model.Freeze();
    return model;
  }

  static (byte[] glb, Vector3D rtc) GetGlb(byte[] content)  // get GLB (Graphics Library Binary) data from API response
  {
    Vector3D rtc = default;
    byte[] glb;

    string magic = content.Length >= 4 ? Encoding.ASCII.GetString(content, 0, 4) : "";
    switch (magic)
    {
      case "b3dm":  // Batch 3D Model
        {
          uint ftJson = BinaryPrimitives.ReadUInt32LittleEndian(content.AsSpan(12));
          uint ftBin = BinaryPrimitives.ReadUInt32LittleEndian(content.AsSpan(16));
          uint btJson = BinaryPrimitives.ReadUInt32LittleEndian(content.AsSpan(20));
          uint btBin = BinaryPrimitives.ReadUInt32LittleEndian(content.AsSpan(24));
          if (ftJson > 0)
          {
            using var ft = JsonDocument.Parse(content.AsMemory(28, (int)ftJson));
            if (ft.RootElement.TryGetProperty("RTC_CENTER", out var c) && c.GetArrayLength() == 3)
              rtc = new Vector3D(c[0].GetDouble(), c[1].GetDouble(), c[2].GetDouble());  // relative-to-center
          }
          int glbStart = 28 + (int)(ftJson + ftBin + btJson + btBin);
          glb = content[glbStart..];
          break;
        }
      case "glTF":  // Graphics Library Transmission Format
        glb = content;
        break;
      default:
        throw new NotSupportedException($"Tile content type '{magic}' is not supported.");
    }

    return (glb, rtc);
  }

  static EmissiveMaterial MakeTextureMaterial(byte[] image, string? mime)
  {
    try
    {
      var bmp = new BitmapImage();
      bmp.BeginInit();
      bmp.CacheOption = BitmapCacheOption.OnLoad;
      bmp.StreamSource = new MemoryStream(image);
      bmp.EndInit();
      bmp.Freeze();

      var brush = new ImageBrush(bmp) { TileMode = TileMode.None };
      brush.Freeze();
      // Emissive so the imagery's baked-in lighting is shown unchanged and no scene lights are needed.
      var material = new EmissiveMaterial(brush);
      material.Freeze();
      return material;
    }
    catch (Exception ex) when (mime == "image/webp")
    {
      throw new NotSupportedException("Could not decode a WebP tile texture (needs the Windows WebP codec).", ex);
    }
  }
}
