using System.Text.Json.Serialization;
using System.Windows.Media.Media3D;

namespace MarsExplorer.Models;

/// <summary>
/// GlbPrimitive (Graphics Library Binary Primitive contains a triangle list from a glTF, converted to the 3D Tiles (Z-up frame)
/// GlbPrimitive properties:
/// 
/// Positions: The (x,y,z) position of each triangle vertex. For every Position item there is a corresponding UV item
///            and an Indices value.
/// 
/// UVs:       The normalized coordinates of each texture image's triangle vertex. U is horizontal, V is vertical.
///            UV coordinates are almost always normalized, meaning every value is between 0.0 and 1.0:
///                - (0, 0):     The top-left corner of the texture image.
///                - (1, 1):     The bottom-right corner of the texture image.
///                - (0.5, 0.5): The exact center of the image.
///            When WPF renders the triangle, it stretches, skews, and interpolates the pixels of the 2D image 
///            between those vertices so the texture fits seamlessly over the geometry.
///          
/// Indices:   Each index in this array specifies an item in the Positions and UVs arrays. 
///          
/// Image:     The texture (picture tile) to use to extract triangular fragments from, based on UV coordinates.
/// 
/// ImageMime: the type of texture image, e.g. image/png
/// </summary>
public sealed class GlbPrimitive
{
  public Point3D[] Positions { get; set; } = Array.Empty<Point3D>();  // the (x,y,z) coordinates of each vertex of the mesh triangles
  public System.Windows.Point[]? Uvs { get; set; }
  public int[] Indices { get; set; } = Array.Empty<int>();
  public byte[]? ImageBytes { get; set; }
  public string? ImageMime { get; set; }
}

/// <summary>
/// The classes are used to deserialize the JSON response from Cesium ion listing the user's assets.
/// </summary>

public class AssetEndpointResponse
{
  [JsonPropertyName("type")]
  public string Type { get; set; } = null!;

  [JsonPropertyName("attributions")]
  public List<Attribution> Attributions { get; set; } = new();

  [JsonPropertyName("url")]
  public string Url { get; set; } = null!;

  [JsonPropertyName("accessToken")]
  public string AccessToken { get; set; } = null!;
}

public class Attribution
{
  [JsonPropertyName("html")]
  public string Html { get; set; } = null!;

  [JsonPropertyName("collapsible")]
  public bool Collapsible { get; set; }
}


/// <summary>
/// The classes are used to deserialize JSON documents in standard glTF (GL Transmission Format).
/// This format is used by  Cesium ion and includes 3D tiles.
/// </summary>
public class GltfRoot
{
  [JsonPropertyName("accessors")]
  public List<Accessor> Accessors { get; set; } = new();

  [JsonPropertyName("asset")]
  public GltfAsset Asset { get; set; } = null!;

  [JsonPropertyName("bufferViews")]
  public List<BufferView> BufferViews { get; set; } = new();

  [JsonPropertyName("buffers")]
  public List<Buffer> Buffers { get; set; } = new();

  [JsonPropertyName("extensionsUsed")]
  public List<string>? ExtensionsUsed { get; set; }

  [JsonPropertyName("images")]
  public List<GltfImage>? Images { get; set; }

  [JsonPropertyName("materials")]
  public List<Material>? Materials { get; set; }

  [JsonPropertyName("meshes")]
  public List<Mesh> Meshes { get; set; } = new();

  [JsonPropertyName("nodes")]
  public List<Node> Nodes { get; set; } = new();

  [JsonPropertyName("samplers")]
  public List<Sampler>? Samplers { get; set; }

  [JsonPropertyName("scene")]
  public int? Scene { get; set; }

  [JsonPropertyName("scenes")]
  public List<Scene> Scenes { get; set; } = new();

  [JsonPropertyName("textures")]
  public List<Texture>? Textures { get; set; }
}

public class Accessor
{
  [JsonPropertyName("bufferView")]
  public int BufferView { get; set; }

  [JsonPropertyName("componentType")]
  public int ComponentType { get; set; }

  [JsonPropertyName("count")]
  public int Count { get; set; }

  [JsonPropertyName("max")]
  public List<double>? Max { get; set; }

  [JsonPropertyName("min")]
  public List<double>? Min { get; set; }

  [JsonPropertyName("type")]
  public string Type { get; set; } = null!;
}

public class GltfAsset
{
  [JsonPropertyName("generator")]
  public string Generator { get; set; } = null!;

  [JsonPropertyName("version")]
  public string Version { get; set; } = null!;
}

public class BufferView
{
  [JsonPropertyName("buffer")]
  public int Buffer { get; set; }

  [JsonPropertyName("byteLength")]
  public int ByteLength { get; set; }

  [JsonPropertyName("byteOffset")]
  public int ByteOffset { get; set; }

  [JsonPropertyName("target")]
  public int? Target { get; set; }
}

public class Buffer
{
  [JsonPropertyName("byteLength")]
  public int ByteLength { get; set; }
}

public class GltfImage
{
  [JsonPropertyName("bufferView")]
  public int BufferView { get; set; }

  [JsonPropertyName("mimeType")]
  public string MimeType { get; set; } = null!;
}

public class Material
{
  [JsonPropertyName("alphaCutoff")]
  public double? AlphaCutoff { get; set; }

  [JsonPropertyName("alphaMode")]
  public string? AlphaMode { get; set; }

  [JsonPropertyName("extensions")]
  public Dictionary<string, object>? Extensions { get; set; }

  [JsonPropertyName("name")]
  public string? Name { get; set; }

  [JsonPropertyName("pbrMetallicRoughness")]
  public PbrMetallicRoughness? PbrMetallicRoughness { get; set; }
}

public class PbrMetallicRoughness
{
  [JsonPropertyName("baseColorFactor")]
  public List<double>? BaseColorFactor { get; set; }

  [JsonPropertyName("baseColorTexture")]
  public TextureInfo? BaseColorTexture { get; set; }

  [JsonPropertyName("metallicFactor")]
  public double? MetallicFactor { get; set; }

  [JsonPropertyName("roughnessFactor")]
  public double? RoughnessFactor { get; set; }
}

public class TextureInfo
{
  [JsonPropertyName("index")]
  public int Index { get; set; }

  [JsonPropertyName("texCoord")]
  public int TexCoord { get; set; }
}

public class Mesh
{
  [JsonPropertyName("primitives")]
  public List<Primitive> Primitives { get; set; } = new();
}

public class Primitive
{
  [JsonPropertyName("attributes")]
  public Dictionary<string, int> Attributes { get; set; } = new();

  [JsonPropertyName("indices")]
  public int Indices { get; set; }

  [JsonPropertyName("material")]
  public int Material { get; set; }
}

public class Node
{
  [JsonPropertyName("children")]
  public List<int>? Children { get; set; }

  [JsonPropertyName("matrix")]
  public List<double>? Matrix { get; set; }

  [JsonPropertyName("mesh")]
  public int? Mesh { get; set; }
}

public class Sampler
{
  [JsonPropertyName("magFilter")]
  public int? MagFilter { get; set; }

  [JsonPropertyName("minFilter")]
  public int? MinFilter { get; set; }

  [JsonPropertyName("wrapS")]
  public int? WrapS { get; set; }

  [JsonPropertyName("wrapT")]
  public int? WrapT { get; set; }
}

public class Scene
{
  [JsonPropertyName("nodes")]
  public List<int> Nodes { get; set; } = new();
}

public class Texture
{
  [JsonPropertyName("sampler")]
  public int Sampler { get; set; }

  [JsonPropertyName("source")]
  public int Source { get; set; }
}


/// <summary>
/// The classes are used to deserialize JSON documents in standard glTF (GL Transmission Format), representing sub-tilesets.
/// Sub-tilesets often include specific child hierarchies, bounding regions, and external content links.
/// </summary>
 public class TilesetJson
{
  [JsonPropertyName("asset")]
  public TilesetAsset Asset { get; set; } = null!;

  [JsonPropertyName("geometricError")]
  public double GeometricError { get; set; }

  [JsonPropertyName("root")]
  public Tile Root { get; set; } = null!;

  [JsonPropertyName("properties")]
  public Dictionary<string, object>? Properties { get; set; }
}

public class TilesetAsset
{
  [JsonPropertyName("version")]
  public string Version { get; set; } = null!;

  [JsonPropertyName("tilesetVersion")]
  public string? TilesetVersion { get; set; }
}

public class Tile
{
  [JsonPropertyName("boundingVolume")]
  public BoundingVolume BoundingVolume { get; set; } = null!;

  [JsonPropertyName("viewerRequestVolume")]
  public BoundingVolume? ViewerRequestVolume { get; set; }

  [JsonPropertyName("geometricError")]
  public double GeometricError { get; set; }

  [JsonPropertyName("refine")]
  public string? Refine { get; set; } // "REPLACE" or "ADD"

  [JsonPropertyName("transform")]
  public List<double>? Transform { get; set; } // 4x4 affine transformation matrix (16 elements)

  [JsonPropertyName("content")]
  public TileContent? Content { get; set; }

  [JsonPropertyName("contents")]
  public List<TileContent>? Contents { get; set; }

  [JsonPropertyName("children")]
  public List<Tile>? Children { get; set; }
}

public class BoundingVolume
{
  [JsonPropertyName("box")]
  public List<double>? Box { get; set; }  // Oriented Bounding Box, represented by 12 numbers:
                                          // centerX, centerY, centerZ, // 0-2: Coordinates of geometric center
                                          // xAxis X, xAxis Y, xAxis Z, // 3-5: X-axis direction & half-length
                                          // yAxis X, yAxis Y, yAxis Z, // 6-8: Y-axis direction & half-length
                                          // zAxis X, zAxis Y, zAxis Z  // 9-11: Z-axis direction & half-length

  [JsonPropertyName("region")]
  public List<double>? Region { get; set; } // [west, south, east, north, minHeight, maxHeight] in radians/meters

  [JsonPropertyName("sphere")]
  public List<double>? Sphere { get; set; } // [x, y, z, radius]
}

public class TileContent
{
  [JsonPropertyName("uri")]
  public string? Uri { get; set; } // Path glTF tileset.json

  [JsonPropertyName("boundingVolume")]
  public BoundingVolume? BoundingVolume { get; set; }
}

/// <summary>One node of a 3D Tiles tileset.</summary>
public class HierarchicalTile
{
  public HierarchicalTile? Parent { get; init; }
  public List<HierarchicalTile> Children { get; } = new();
  public Uri? ContentUri { get; set; }
  public ContentKind Kind { get; set; }
  public double GeometricError { get; set; }
  public BoundingSphere Bounds { get; set; }

  /// <summary>Cumulative tile transform (row-vector convention, like WPF's Matrix3D).</summary>
  public Matrix3D WorldTransform { get; set; } = Matrix3D.Identity;

  public TileState State;
  public ModelVisual3D? Visual;
  public int LastUsedFrame;
  public DateTime RetryAt;
}

