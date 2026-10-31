using System.Buffers.Binary;
using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Media.Media3D;

using MarsExplorer.Models;

namespace MarsExplorer;

/// <summary>
/// GLB is the Graphics Library Binary format, often described as the "JPEG of 3D". GLB is the binary 
/// container format for glTF (Graphics Library Transmission Format) data, an open-standard 3D file format.
/// 
/// A glTF file includes everything needed to render 3D terrain tiles: triangle meshes, node transforms, 
/// embedded base-colour textures, etc.
/// </summary>
public static class GlbReader
{
  public static List<GlbPrimitive> GetGlbPrimitives(byte[] glb, Vector3D rtcCenter)
  {
    (ReadOnlyMemory<byte> json, int binStart, int offset) = GetJsonDocument(glb);

    using JsonDocument doc = JsonDocument.Parse(json);
    Models.GltfRoot? gltfRoot = doc.RootElement.Deserialize<Models.GltfRoot>();

    if (gltfRoot == null || gltfRoot.Nodes == null || gltfRoot.Meshes == null) return [];

    binStart = binStart < 0 ? 0 : binStart;
    var nodeProcessor = new NodeProcessor(gltfRoot, glb, binStart, rtcCenter);

    if (gltfRoot.Scenes != null)
    {
      int sceneIndex = gltfRoot.Scene ?? 0;
      var scene = gltfRoot.Scenes[sceneIndex];

      foreach (var nodeIndex in scene.Nodes)
        nodeProcessor.ProcessNodeRecursively(nodeIndex, Matrix3D.Identity);
    }

    return nodeProcessor.Primitives;
  }

  static (ReadOnlyMemory<byte> json, int binStart, int offset) GetJsonDocument(byte[] glb)
  {
    if (glb.Length < 20 || BinaryPrimitives.ReadUInt32LittleEndian(glb) != 0x46546C67)
      throw new InvalidDataException("Tile content is not a binary glTF.");

    int offset = 12, binStart = -1;
    ReadOnlyMemory<byte> json = default;

    while (offset + 8 <= glb.Length)
    {
      int len = (int)BinaryPrimitives.ReadUInt32LittleEndian(glb.AsSpan(offset));
      uint type = BinaryPrimitives.ReadUInt32LittleEndian(glb.AsSpan(offset + 4));
      offset += 8;

      const long JsonChunkType = 0x4E4F534A;    // the characters "JSON"!
      const long BinaryChunkType = 0x004E4942;  // the characters "BIN"!

      if (type == JsonChunkType)
        json = glb.AsMemory(offset, len);
      else if (type == BinaryChunkType && binStart < 0)
        binStart = offset;

      offset += len;
    }

    if (json.IsEmpty)
      throw new InvalidDataException("GLB has no JSON chunk.");

    return (json, binStart, offset);
  }

  class NodeProcessor
  {
    readonly GltfRoot gltfRoot;
    readonly byte[] data;
    readonly int binStart;
    readonly Vector3D rtc;
    public List<GlbPrimitive> Primitives { get; } = new();

    public NodeProcessor(GltfRoot root, byte[] data, int bin, Vector3D rtc)
    {
      this.gltfRoot = root;
      this.data = data;
      this.binStart = bin;
      this.rtc = rtc;
    }

    public void ProcessNodeRecursively(int nodeIndex, Matrix3D parentTransform)
    {
      Node node = gltfRoot.Nodes[nodeIndex];
      Matrix3D nodeTransform = GetNodeTransform(node);
      Matrix3D worldTransform = nodeTransform * parentTransform;  // matrix multiplication

      if (node.Mesh != null)
        AddMesh(node.Mesh.Value, worldTransform);

      if (node.Children == null) return;

      foreach (var childNode in node.Children)
        ProcessNodeRecursively(childNode, worldTransform);
    }

    static Matrix3D GetNodeTransform(Node node)
    {
      if (node.Matrix != null && node.Matrix.Count == 16)
      {
        var a = node.Matrix;
        return new Matrix3D(a[0], a[1], a[2], a[3], a[4], a[5], a[6], a[7],
                            a[8], a[9], a[10], a[11], a[12], a[13], a[14], a[15]);
      }

      var mat = Matrix3D.Identity;
      return mat;
    }

    void AddMesh(int meshIdx, Matrix3D world)
    {
      var mesh = gltfRoot.Meshes[meshIdx];

      foreach (Primitive primitive in mesh.Primitives)
      {
        var attributes = primitive.Attributes;
        double[] vertices = ReadAccessor(attributes["POSITION"], out int pc, out int n);

        var pts = new Point3D[n];
        for (int i = 0; i < n; i++)
        {
          Point3D p = world.Transform(new Point3D(vertices[i * pc], vertices[i * pc + 1], vertices[i * pc + 2]));

          // Coordinate adjustment: glTF is Y-up, 3D Tiles is Z-up: (x, y, z) -> (x, -z, y). (RTC center is already Z-up)
          pts[i] = new Point3D(p.X + rtc.X, -p.Z + rtc.Y, p.Y + rtc.Z);
        }

        int[] indices;
        var rawValues = ReadAccessor(primitive.Indices, out _, out int ic);
        indices = new int[ic];

        for (int i = 0; i < ic; i++)
          indices[i] = (int)rawValues[i];

        System.Windows.Point[]? uvs = null;
        if (attributes.ContainsKey("TEXCOORD_0"))
        {
          int tc = attributes["TEXCOORD_0"];
          rawValues = ReadAccessor(tc, out int uc, out int un);
          uvs = new Point[un];

          for (int i = 0; i < un; i++)
            uvs[i] = new Point(rawValues[i * uc], rawValues[i * uc + 1]);
        }

        var (imageBytes, mime) = GetImageBytes(primitive);

        var glbPrimitive = new GlbPrimitive
        {
          Positions = pts,
          Indices = indices,
          Uvs = uvs,
          ImageBytes = imageBytes,
          ImageMime = mime
        };

        Primitives.Add(glbPrimitive);
      }
    }

    (byte[]? imageBytes, string? mime) GetImageBytes(Primitive prim)
    {
      int materialIndex = prim.Material;

      if (gltfRoot.Materials == null || gltfRoot.Materials.Count == 0) return (null, null);

      var material = gltfRoot.Materials[materialIndex];

      if (material.PbrMetallicRoughness == null || material.PbrMetallicRoughness.BaseColorTexture == null) return (null, null);

      var bct = material.PbrMetallicRoughness.BaseColorTexture;

      if (gltfRoot.Textures == null || bct.Index >= gltfRoot.Textures.Count) return (null, null);

      var texture = gltfRoot.Textures[bct.Index];

      int textureIndex = texture.Source;

      if (gltfRoot.Images == null || textureIndex >= gltfRoot.Images.Count) return (null, null);

      GltfImage image = gltfRoot.Images[textureIndex];

      string mime = image.MimeType;

      if (mime == "image/ktx2")
        throw new NotSupportedException("Tiles use KTX2 textures, which are not implemented.");

      // extract image bytes from buffer
      int bufferViewIndex = image.BufferView;
      var bufferView = gltfRoot.BufferViews[bufferViewIndex];
      int imageStart = binStart + bufferView.ByteOffset;
      int imageLength = bufferView.ByteLength;
      var imageBytes = new byte[imageLength];
      System.Buffer.BlockCopy(data, imageStart, imageBytes, 0, imageLength);

      return (imageBytes, mime);
    }

    enum AccessorDataType
    {
      Byte = 5120,
      UnsignedByte = 5121,
      Short = 5122,
      UnsignedShort = 5123,
      UnsignedInt = 5125,
      Float = 5126
    }

    // extract the value of scalars or vectors from the given Accessor buffer
    double[] ReadAccessor(int idx, out int componentsPerItem, out int numberOfItems)
    {
      var acc = gltfRoot.Accessors[idx];
      numberOfItems = acc.Count;
      AccessorDataType componentType = (AccessorDataType) acc.ComponentType;

      componentsPerItem = acc.Type switch
      {
        "SCALAR" => 1, // single numbers
        "VEC2" => 2,   // 2-component vectors (e.g. UV texture coordinates)
        "VEC3" => 3,   // 3-component vectors (e.g. 3D positions)
        "VEC4" => 4,   // 4-component vectors (e.g. tangents and vertex colors).
        var other => throw new NotSupportedException($"glTF accessor type {other} is not supported.")
      };

      var result = new double[numberOfItems * componentsPerItem];
      int bvIdx = acc.BufferView;

      var bv = gltfRoot.BufferViews[bvIdx];

      int componentItemSize = componentType switch
      {
        AccessorDataType.Byte or AccessorDataType.UnsignedByte => 1,
        AccessorDataType.Short or AccessorDataType.UnsignedShort => 2,
        AccessorDataType.UnsignedInt or AccessorDataType.Float => 4,
        _ => throw new NotSupportedException($"glTF component type {componentType} is not supported.")
      };

      int offset = bv.ByteOffset;
      int stride = componentItemSize * componentsPerItem;
      int baseOff = binStart + offset;

      for (int i = 0; i < numberOfItems; i++)
        for (int c = 0; c < componentsPerItem; c++)
          result[i * componentsPerItem + c] = ParseValue(baseOff + i * stride + c * componentItemSize, componentType);

      return result;
    }

    double ParseValue(int dataIndex, AccessorDataType valueType, bool normalized = false)
    {
      Span<byte> span = data.AsSpan(dataIndex);

      switch (valueType)
      {
        case AccessorDataType.Byte:
          {
            sbyte v = (sbyte)span[0];
            return normalized ? Math.Max(v / 127.0, -1.0) : v;
          }
        case AccessorDataType.UnsignedByte:
          return normalized ? span[0] / 255.0 : span[0];
        case AccessorDataType.Short:
          {
            short v = BinaryPrimitives.ReadInt16LittleEndian(span);
            return normalized ? Math.Max(v / 32767.0, -1.0) : v;
          }
        case AccessorDataType.UnsignedShort:
          {
            ushort v = BinaryPrimitives.ReadUInt16LittleEndian(span);
            return normalized ? v / 65535.0 : v;
          }
        case AccessorDataType.UnsignedInt:
          return BinaryPrimitives.ReadUInt32LittleEndian(span);
        default:
          return BinaryPrimitives.ReadSingleLittleEndian(span);  // floating point
      }
    }
  }
}
