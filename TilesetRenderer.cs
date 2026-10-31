using System.Windows.Media.Media3D;

using MarsExplorer.Models;

namespace MarsExplorer;

/// <summary>Camera description in the Mars-fixed frame, used for culling and screen-space-error selection.</summary>
public sealed record CameraInfo(Vector3D Position, Vector3D Look, double HorizontalFov, double VerticalFov, double ViewportHeightPx)
{
  public double TanHalfVertical => Math.Tan(VerticalFov / 2);

  public double HalfDiagonal =>
      Math.Atan(Math.Sqrt(Math.Pow(Math.Tan(HorizontalFov / 2), 2) + Math.Pow(TanHalfVertical, 2)));
}

/// <summary>
/// Streams a 3D Tiles tileset: picks tiles by screen-space error, loads them in the background and
/// keeps the matching ModelVisual3D children attached to the scene root. UI-thread only.
/// </summary>
public sealed class TilesetRenderer
{
  const double MaxScreenSpaceError = 16;
  const int MaxInFlight = 8;  // max simultaneous tile downloads
  const int MaxCachedTiles = 1000;

  readonly ModelVisual3D sceneRoot;
  readonly HashSet<HierarchicalTile> _attached = new();
  readonly List<HierarchicalTile> cachedTiles = new();

  HierarchicalTile? rootTile;
  int frame;
  int inFlight;  // tiles still being downloaded

  public bool IsDirty { get; set; }
  public string? LastError { get; private set; }
  public int RenderedTiles => _attached.Count;
  public int LoadingTiles => inFlight;
  public int CachedTileCount => cachedTiles.Count;
  DateTime lastRendering = DateTime.MinValue;

  string apiToken = string.Empty;  // a single token for getting endpoint-specific access tokens
  string? accessToken;             // endpoint-specific

  public TilesetRenderer(ModelVisual3D sceneRoot, string apiToken)
  {
    this.apiToken = apiToken;
    this.sceneRoot = sceneRoot;
  }

  /// <summary>
  /// Retrieve the root of the Tile hierarchy
  /// </summary>
  /// <param name="tilesetUrl"></param>
  /// <returns></returns>
  public async Task LoadRootTilesetAsync()
  {
    CesiumIonEndpoint endpoint = await CesiumIonApi.GetEndpointAsync(apiToken);
    accessToken = endpoint.AccessToken;
    Uri tilesetUrl = endpoint.Uri;

    var bytes = await Task.Run(() => CesiumIonApi.GetTilesetAsync(tilesetUrl, accessToken));
    rootTile = await Task.Run(() => TilesetParser.Parse(bytes, tilesetUrl, Matrix3D.Identity, parent: null));

    IsDirty = true;
  }

  public bool Update(CameraInfo cam)
  {
    if (!IsDirty) return false;
    if (rootTile == null) return false;

    var now = DateTime.UtcNow;
    if ((now - lastRendering).TotalMilliseconds < 50) return false;

    frame++;

    var render = new List<HierarchicalTile>();
    var needed = new List<(HierarchicalTile tile, double screenSpaceError)>();

    _ = Select(rootTile, cam, render, needed);

    var keep = new HashSet<HierarchicalTile>(render.Where(t => t.Visual != null));

    foreach (var t in _attached.Where(t => !keep.Contains(t)).ToList())
    {
      if (t.Visual != null)
        sceneRoot.Children.Remove(t.Visual);

      _attached.Remove(t);
    }

    foreach (var t in keep)
    {
      t.LastUsedFrame = frame;

      if (_attached.Add(t))
        sceneRoot.Children.Add(t.Visual!);
    }

    foreach (var (tile, _) in needed.OrderByDescending(n => n.screenSpaceError))
    {
      if (inFlight >= MaxInFlight) break;

      if (CanStart(tile))
        _ = GetChildTilesetAsync(tile);
    }

    ConstrainCacheSize();

    IsDirty = false;
    lastRendering = now;
    return true;
  }

  static bool CanStart(HierarchicalTile t)
  {
    return t.State == TileState.Unloaded || (t.State == TileState.Failed && DateTime.UtcNow >= t.RetryAt);
  }

  /// <summary>
  /// Returns true when everything visible below <paramref name="t"/> can be drawn (either at the
  /// desired detail or by a coarser stand-in that is already in <paramref name="output"/>).
  /// </summary>
  bool Select(HierarchicalTile t, CameraInfo cam, List<HierarchicalTile> output, List<(HierarchicalTile, double)> needed)
  {
    if (!IsVisible(t, cam)) return true;

    double screenSpaceError = ScreenSpaceError(t, cam);
    bool needMoreDetailedTiles = t.Children.Count > 0 && screenSpaceError > MaxScreenSpaceError;

    if (t.Kind == ContentKind.ExternalTileset)
    {
      if (t.State != TileState.Loaded)
      {
        needed.Add((t, double.MaxValue));
        return false;
      }
      needMoreDetailedTiles = t.Children.Count > 0;
    }

    if (!needMoreDetailedTiles)
    {
      if (t.Kind != ContentKind.Model) return true;
      if (t.State == TileState.Loaded) 
      { 
        output.Add(t); 
        return true; 
      }

      needed.Add((t, screenSpaceError));
      return false;
    }

    var childOutput = new List<HierarchicalTile>();
    bool ready = true;

    // see if the tile's children are ready to be drawn
    foreach (var c in t.Children)
      ready &= Select(c, cam, childOutput, needed);

    if (ready)
    {
      output.AddRange(childOutput);
      return true;
    }

    // Children are still loading: keep showing this tile (if we have it) so there are no holes.
    if (t.Kind == ContentKind.Model)
    {
      if (t.State == TileState.Loaded) { output.Add(t); return true; }
      needed.Add((t, screenSpaceError));
    }
    output.AddRange(childOutput);
    return false;
  }

  static double ScreenSpaceError(HierarchicalTile t, CameraInfo cam)
  {
    double dist = Math.Max((t.Bounds.Center - cam.Position).Length - t.Bounds.Radius, 1.0);
    return t.GeometricError * cam.ViewportHeightPx / (dist * 2 * cam.TanHalfVertical);
  }

  static bool IsVisible(HierarchicalTile t, CameraInfo cam)
  {
    var b = t.Bounds;
    var v = b.Center - cam.Position;
    double d = v.Length;

    if (d <= b.Radius) return true;

    // Horizon culling: anything further around the sphere than the horizon
    // (plus a margin for mountains peeking over it) cannot be seen.
    double camLen = cam.Position.Length;
    double centerLen = b.Center.Length;

    if (camLen > MarsMath.Radius && centerLen > b.Radius)
    {
      double theta = Math.Acos(Math.Clamp(Vector3D.DotProduct(b.Center, cam.Position) / (centerLen * camLen), -1, 1));
      double size = Math.Asin(Math.Min(1, b.Radius / centerLen));
      double horizon = Math.Acos(Math.Min(1, MarsMath.Radius / camLen)) + 0.12;

      if (theta - size > horizon) return false;
    }

    // Cone approximation of the view frustum.
    double ang = Math.Acos(Math.Clamp(Vector3D.DotProduct(v, cam.Look) / d, -1, 1));
    double beta = Math.Asin(Math.Min(1, b.Radius / d));

    return ang - beta <= cam.HalfDiagonal;
  }

  /// <summary>
  /// Retrieve the given tileset and add it to its parent
  /// </summary>
  /// <param name="parentTile"></param>
  /// <returns></returns>
  async Task GetChildTilesetAsync(HierarchicalTile parentTile)
  {
    parentTile.State = TileState.Loading;
    inFlight++;
    try
    {
      var uri = parentTile.ContentUri!;
      var tilesetBytes = await CesiumIonApi.GetTilesetAsync(uri, accessToken);  // response in TilesetJson binary format

      if (parentTile.Kind == ContentKind.ExternalTileset)
      {
        HierarchicalTile rootTile = await Task.Run(() => TilesetParser.Parse(tilesetBytes, uri, parentTile.WorldTransform, parentTile));
        parentTile.Children.Clear();
        parentTile.Children.Add(rootTile);
      }
      else
      {
        var model = await Task.Run(() => TileModelBuilder.BuildModel3dGroup(tilesetBytes, parentTile));
        if (model != null)
        {
          parentTile.Visual = new ModelVisual3D
          {
            Content = model,
            Transform = new TranslateTransform3D(parentTile.Bounds.Center)
          };
        }
        cachedTiles.Add(parentTile);
      }

      parentTile.State = TileState.Loaded;
    }
    catch (NotSupportedException ex)
    {
      parentTile.State = TileState.Failed;
      parentTile.RetryAt = DateTime.MaxValue;
      LastError = ex.Message;
    }
    catch (Exception ex)
    {
      parentTile.State = TileState.Failed;
      parentTile.RetryAt = DateTime.UtcNow.AddSeconds(10);
      LastError = ex.Message;
    }
    finally
    {
      inFlight--;
      IsDirty = true;
    }
  }

  // remove excess tiles from the cache
  void ConstrainCacheSize()
  {
    int excess = cachedTiles.Count - MaxCachedTiles;
    if (excess <= 0) return;

    // find items to be evicted
    var targets = cachedTiles
        .Where(t => !_attached.Contains(t) && frame - t.LastUsedFrame > 30)
        .OrderBy(t => t.LastUsedFrame)
        .Take(excess)
        .ToList();

    // evict them from the cache
    foreach (var target in targets)
    {
      target.Visual = null;
      target.State = TileState.Unloaded;
      cachedTiles.Remove(target);
    }
  }
}
