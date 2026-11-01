# Mars Explorer

A .Net 10 WPF (Windows Presentation Foundation) desktop app to view planet Mars in high resolution 3D.

## Features
* Great project for getting started with 3D applications in WPF.
* No dependencies on GitHub packages or third-party components.
* Uses free web service APIs from Cesium ion.
* Requires a free API key to access 3D Mars Tiles.
* Supports zooming and rotating with the mouse.

The following figure shows Mars Explorer at startup time.

<img width="925" height="592" alt="image" src="https://github.com/user-attachments/assets/9f6665f0-7389-46b8-a97e-149d3a560c68" />

*Figure 1* - The app showing the initial appearance of Mars.
<br><br><br>

<img width="925" height="592" alt="image" src="https://github.com/user-attachments/assets/1bd8e348-6bd3-4c54-aa54-163440f2a5f1" />

*Figure 2* - Zooming in a bit and rotating Mars.
<br><br><br>

<img width="925" height="592" alt="image" src="https://github.com/user-attachments/assets/67472f1f-b906-43ca-9500-21fb3624343e" />

*Figure 3* - A closeup of Mars.
<br><br>

The Mars image is actually made up of set of *tiles*. The tiles contain images from the **Cesium Mars** asset exposed by the online service **Cesium ion**. The Mars asset is built from NASA terrain and imagery. The resolution provides good visual quality as you zoom in. As you get closer and closer, the app retrieves higher and higher resolution tiles. Once you reach an altitude of about 500 km, Cesium ion doesn't have any higher resolution tiles so the app starts magnifying the existing tiles. This makes the image become increasingly blurry, as shown in the next figure.

<img width="925" height="592" alt="image" src="https://github.com/user-attachments/assets/6af28c29-be3d-40f2-9883-95d8d7759820" />

*Figure 4* - The Mars surface starts getting blurry if you zoom in too close.

## Getting Access to Cesium ion
Before you can run the Mars Explorer app, you need two things from Cesium ion:

1. Access to the Mars asset.
2. An Access Token.

To get access to Mars data, follow these steps:

1. Go to ```https://ion.cesium.com``` and signup for a free account if you don't have one.
2. Go to ```https://ion.cesium.com/assetdepot``` to see all available assets.
3. Search for the asset *Mars* and then click the "+" button, as shown in the following figure.

<img width="1312" height="403" alt="image" src="https://github.com/user-attachments/assets/3f819014-92dd-4ccb-ad61-44779b7eb699" />

*Figure 5* - Adding the Cesium Mars asset to your Cesium assets.
<br><br>
If now you go to your **My Assets** page, you should see the Cesium Mars asset listed with its asset ID, as shown in the next figure.

<img width="1312" height="515" alt="image" src="https://github.com/user-attachments/assets/76d0b95d-3ce8-49fe-a152-e28c2a39d90f" />

*Figure 6* - The newly added Cesium Mars asset in your asset list.

Note the ID of the Cesium Mars asset: 2684829. This is the value used in Mars Explorer, as shown in the next listing fragment.

```csharp
public static class CesiumIonApi
{
  const int CesiumMarsAssetId = 3644333;
  // ...
```
*Listing 1* - Setting the Cesium Mars asset ID in Mars Explorer.

Now you need an access token. Do the following:

1. Go to ```https://ion.cesium.com/tokens```.
2. Click the **Create token** button.
3. Make sure the token has **assets:read** scope.
4. Copy the token value and save it as a Windows Environment variable named **Mars_CESIUM_ION_TOKEN**.
5. Or just hard code the value in ```MainWindow.xaml.cs```, if you don't mind embedding your private token in the app.
6. Restart Visual Studio (if running), so it loads the new Environment variables.

# Welcome to the 3D World

A 3D application is quite a bit more complicated than a typical app because there's a lot to take care of. Think about what goes into the making of a movie: you have the the set, the actors, the backdrop, the costumes, the lighting, the camera, etc. In software you have to deal with all the same problems. Instead of actors you have objects, instead of costumes you have textures, but the overall objective is the same. You're dealing with how to shoot a scene, from what perspective, from what distance and with what lighting. There is also some non-trivial matrix math sprinkled about.
<p>
Before getting too deep in the weeds, make sure you understand the components. The following figure shows the basic ones, as they apply to a WPF scene and the Mars Explorer.
<p>
   
<img width="883" height="207" alt="image" src="https://github.com/user-attachments/assets/6d125313-2761-4383-8694-0c1b3870586b" />

*Figure 7* - The basic components of a 3D scene.

# The UI

The 3D scenery components listed above need a container to  host them. The following shows the WPF Window that hosts the viewport and other basic UI elements.

<img width="725" height="474" alt="image" src="https://github.com/user-attachments/assets/96af4dda-719a-4cc3-acb4-c62f6f331c74" />

*Figure 8* - The top level UI supporting the 3D framework.

There are several more components involved that aren't shown because they are created programmatically or don't have a UI. Visible or not, most of them are required.
The following figure shows the complete hierarchy.

<img width="783" height="323" alt="image" src="https://github.com/user-attachments/assets/55569799-b194-4692-b47c-1f6c4a1e8e56" />

*Figure 9* - The parts of the WPF application.

# How it Works

There are three basic tasks in this app to display Mars:

1. Retrieving the Mars images.
2. Composing the images into a 3D image.
3. Displaying the image.

Ironically, displaying the image is the simplest task. All you have to do is put the image data in the right format and WPF does the rest.
The following sequence diagram shows how Mars Explorer retrieves and composes the 3D images.

<img width="733" height="502" alt="image" src="https://github.com/user-attachments/assets/8cfc01ee-8ad8-4cd6-b023-b8a1bfe0fd21" />

*Figure 10* - How images are downloaded and formatted for display.

The images are contained in a binary data structure called a Tileset, which contains a hierarchy of tiles in a *quadtree*. The hierarchy basically contains a root tile which shows the entire planet. Its children break down Mars into smaller and smaller images, used when you start zooming in. See the later **TLDR** section if you're interested in the details.

TilesetRenderer  gets the binary Tileset from Cesium ion. The data is then parsed and formatted by TilesetParser. The root tile is saved in TilesetRenderer and used as the basis for the displayed tiles. The following listing shows the code that controls the downloading and parsing:

```csharp
public sealed class TilesetRenderer
{
  HierarchicalTile? rootTile;

  public async Task LoadRootTilesetAsync()
  {
    var bytes = await Task.Run(() => CesiumIonApi.GetTilesetAsync(tilesetUrl));
    rootTile = await Task.Run(() => TilesetParser.Parse(bytes, tilesetUrl, Matrix3D.Identity, parent: null));
  }
}
```
*Listing 2* - Downloading and formatting a Tileset for display.

Once the root tile has been setup, the whole Tileset is displayed like this:

```csharp
public sealed class TilesetRenderer
{
  ModelVisual3D sceneRoot;
  HierarchicalTile? rootTile;

  public bool Update(CameraInfo cam)
  {
    //...

    var render = new List<HierarchicalTile>();
    var needed = new List<(HierarchicalTile tile, double screenSpaceError)>();

    // determine which tiles are ready for display and which needed to be downloaded
    Select(rootTile, cam, render, needed);

    var keep = new HashSet<HierarchicalTile>(render.Where(t => t.Visual != null));

    // remove the tiles no longer visible
    foreach (var t in _attached.Where(t => !keep.Contains(t)).ToList())
    {
      if (t.Visual != null)
        sceneRoot.Children.Remove(t.Visual);

      _attached.Remove(t);
    }

    // add the tiles to be displayed
    foreach (var t in keep)
    {
      if (_attached.Add(t))
        sceneRoot.Children.Add(t.Visual!);
    }

    // download the missing tiles
    foreach (var (tile, _) in needed.OrderByDescending(n => n.screenSpaceError))
    {
      if (CanStart(tile))
        _ = GetChildTilesetAsync(tile);
    }

    return true;
  }
}
```
*Listing 3* - Displaying the formatted Tileset.

The method TilesetRenderer.Update is called indirectly by the WPF framework, when CompositionTarget (which controls the 3D display) fires the Rendering event. The following listing shows the relevant code.

```csharp
public class MainWindow : Window
{
  TilesetRenderer tilesetRenderer;

  public MainWindow()
  {
    InitializeComponent();

    tilesetRenderer = new TilesetRenderer(SceneRoot, apiToken);
    CompositionTarget.Rendering += CompositionTarget_Rendering;
  }

  void CompositionTarget_Rendering(object? sender, EventArgs e)
  {
    tilesetRenderer.Update(MakeCameraInfo());
  }
}
```
*Listing 4* - How the TilesetRenderer.Update method is called via a WPF event.

# TLDR

## Tilesets

Tilesets are essentially a hierarchy of tiles. At each level, the Mars area covered decreases as the resolution increases. The following figure shows the idea.

<img width="1063" height="512" alt="image" src="https://github.com/user-attachments/assets/d266256b-499e-4bc5-9bfe-f214eb88756d" />

*Figure 11* - How Tiles are made up by smaller and smaller tiles.

Every node references a tile. Each node can have 4 children, so each tile can also have 4 child tiles. Basically each tile can be divided into four children referencing the Northeast, Southeast, Southwest and Northwest sub-tiles.

The Root Node, shown in blue, is a single tile containing a low resolution image of the entire planet.
The green, orange, yellow and blue nodes reference tiles covering smaller and smaller areas with increasing resolution.
Each node has the following main properties:

1. GeometricError. The geometric approximation error for this tile.
2. BoundingVolume. A 3D structure that encloses the node's tile and all its children.
3. Content. The tile image. 
4. Children. The four sub-nodes (if any).

The list of nodes (and therefore tiles) is conveyed by a JSON document called a Tileset.json, shown in the next listing.

#### Tileset.json 

```json
{
  "asset": {
    "version": "1.1"
  },
  "geometricError": 500,
  "root": {
    "boundingVolume": {
      "sphere": [
        -1170000,
        4300000,
        3600000,
        10000
      ]
    },
    "geometricError": 250,
    "refine": "REPLACE",
    "content": {
      "uri": "tile.b3dm"
    },
    "children": [
      {
        "boundingVolume": {
          "sphere": [
            -1165000,
            4300000,
            3600000,
            3000
          ]
        },
        "geometricError": 50,
        "content": {
          "uri": "tile_1.b3dm"
        }
      }
      {... other children...}
    ]
  }
}
```
*Listing 5* - The structure of a Tileset.json document.

A node's geometricError determines whether the node has adequate resolution for the current level of zoom. If not, the children nodes are processed. Here is the code involved:

```csharp
public class TilesetRenderer
{
  // determine the overall error of the current tile, based on the GeometricError
  static double ScreenSpaceError(HierarchicalTile t, CameraInfo cam)
  {
    double dist = Math.Max((t.Bounds.Center - cam.Position).Length - t.Bounds.Radius, 1.0);
    return t.GeometricError * cam.ViewportHeightPx / (dist * 2 * cam.TanHalfVertical);
  }

  // returns true if the currently loaded tiles have sufficiently low error and are ready for display
  bool Select(HierarchicalTile t, CameraInfo cam, List<HierarchicalTile> output, List<(HierarchicalTile, double)> needed)
  {
    double screenSpaceError = ScreenSpaceError(t, cam);
    bool needMoreDetailedTiles = t.Children.Count > 0 && screenSpaceError > MaxScreenSpaceError;  // 16

    if (t.Kind == ContentKind.ExternalTileset)
    {
      if (t.State != TileState.Loaded)
      {
        needed.Add((t, double.MaxValue));  // need to download this tile
        return false;
      }
      needMoreDetailedTiles = t.Children.Count > 0;
    }

    if (!needMoreDetailedTiles)
    {
      // error is okay, or no children left: use this tile
      output. Add(t); // use this tile
      return true; 
    }

    // check child tiles recursively...
  }
}
```
*Listing 6* - How geometricError is used with screenSpaceError.

The renderer uses geometricError and the camera's distance, field of view, and viewport size to estimate how noticeable any missing detail would be in screen pixels. 
The combined value is commonly called ScreenSpaceError. The constant ```TilesetRenderer.MaxScreenSpaceError``` is set to the value 16, but you can play with it to minimize blurriness and avoid unnecessary sub-tile loading.

## Meshes
3D objects aren't built out of thin air. They have a *skeleton* that supports the outer skin. The skeleton is built on a *mesh* structure, which is basically a scaffolding. The following figure shows a sample sphere with a mesh structure having 12 rings (for the latitude) and 24 segments (for the longitude).

<img width="519" height="519" alt="image" src="https://github.com/user-attachments/assets/8e315b37-b289-4366-a226-c871fadb82bb" />

*Figure 12* - A mesh structure for a sample sphere.

Tiles are then laid over the meshes and yield a textured skin, as shown in the next figure.

<img width="546" height="524" alt="image" src="https://github.com/user-attachments/assets/3dd14166-fde2-4e00-859c-3d1080755994" />

*Figure 13* - A texture applied to the tiles laid over the mesh.

The skin shows the effect of ambient lighting, based on the material's coarseness and reflectivity.
<p>
<p>

In Mars Explorer, the meshes are defined in the GLB (Graphics Library Binary) data downloaded from Cesium ion. Each mesh is a triangle whose vertices are defined in (x,y,z) coordinates. The origin of the coordinate system is the center of the planet. These vertices are defined in a POSITION structure as an array of 3 floats. The positions are

```csharp
public class GlbReader
{
   //...

   void AddMesh(int meshIdx, Matrix3D world)
   {
     var mesh = gltfRoot.Meshes[meshIdx];

     foreach (Primitive primitive in mesh.Primitives)
     {
       var attributes = primitive.Attributes;

       // read the 3 (x,y,z) vertex coordinates of this triangle mesh
       double[] vertices = ReadAccessor(attributes["POSITION"], out int pc, out int n);

       var pts = new Point3D[n];
       for (int i = 0; i < n; i++)
       {
         // place each mesh vertex on the Mars sphere
         Point3D p = world.Transform(new Point3D(vertices[i * pc], vertices[i * pc + 1], vertices[i * pc + 2]));

         // Coordinate adjustment: glTF is Y-up, 3D Tiles is Z-up: (x, y, z) -> (x, -z, y).
         pts[i] = new Point3D(p.X + rtc.X, -p.Z + rtc.Y, p.Y + rtc.Z);
       }

       //...
    }
  }
}
```
*Listing 7* - Extracting mesh coordinates and wrapping them around the spherical Mars.

## Textures and UV Coordinates

Once all the meshes have been determined, you still don't have anything to display on the screen. The meshes are just an invisible scaffolding. Now you need to cover the meshes with something.
That something is a *texture*. Textures are 2D images wrapped around a a 3D mesh. The following figure shows the texture for the entire Mars surface.

<img width="2048" height="1024" alt="image" src="https://github.com/user-attachments/assets/e0fb3b1b-50ec-449a-9c5f-dc073b8bb392" />

*Figure 14* - The texture for the entire Mars surface.

A texture is an image that is mapped onto the surface of a mesh. Meshes are very small while the image is huge. Each triangular mesh will correspond to a small piece of the big image. To map a texture fragment onto a triangular mesh, you need basically two things:

1. The three (x,y,z) coordinates of the mesh vertices.
2. The three (u, v) coordinates of the image vertices.

But what are (u, v) coordinates? They are 2D coordinates on the texture image, normalized into the range (0,1). U represents the horizontal position in the texture, where 0 is the leftmost pixel and 1 is the rightmost. V represents the vertical position in the texture, where 0 is the topmost pixel and 1 the bottommost. The three (u,v) texture coordinates are read when the mesh (x,y,z) coordinates are read, although I didn't show that code in the previous listing. 
<p><p>
So for every mesh there is a texture mapped to it. The WPF 3D framework has built-in code to map each texture over the corresponding mesh. You basically create a '''GeometryModel3D''' and pass it the mesh and the image like this:

```csharp
public class TileModelBuilder
{
   GeometryModel3D GetGeometryModel3D(GlbPrimitive primitive, HierarchicalTile tile)
  {
   if (primitive.ImageBytes != null && primitive.Uvs != null)
      material = MakeTextureMaterial(primitive.ImageBytes, primitive.ImageMime);

    var model = new GeometryModel3D(mesh, material) { BackMaterial = material };
    model.Freeze();
    return model;
  }
}
```
*Listing 8* - Associating a texture image with a mesh.

Once the GeometryModel3D object is created, it is subsequently passed to the WPF 3D system like this:

```csharp
public class TilesetRenderer
{
  ModelVisual3D sceneRoot;

  public TilesetRenderer(ModelVisual3D sceneRoot)
  {
    this.sceneRoot = sceneRoot;
  }

public bool Update(CameraInfo cam)
{
  // find tiles ready for display
  var render = new List<HierarchicalTile>();
  Select(rootTile, cam, render, needed);
  var keep = new HashSet<HierarchicalTile>(render.Where(t => t.Visual != null));

  // add tiles to SceneRoot
  foreach (var t in keep)
    sceneRoot.Children.Add(t.Visual!);

  // ...
}
```
*Listing 9* - Passing a GeometryModel3D complex object back to the WPF SceneRoot.

Once the GeometryModel3D is attached to SceneRoot, WPF will take care of rendering it appropriately.

## Conclusion

As you can see, creating a 3D application is no joke. Keep in mind that I left out a lot of details. There are lots of moving parts with lots of mathematics. Debugging mistakes is not that easy. Get one thing wrong and the display with generally show one of two things: nothing or chaos. The image *might* contain clues to your mistake, but often the garbage won't make any sense. My recommendation is to handle one step at a time, ensuring it works correctly before proceeding to the next. I'd follow this order:

1. Download your GLB data. This isn't too hard.
2. Parse it. This is the hardest step. There is a lot of data to parse. Mistakes here will really mess up the display.
3. Display it. Not hard but you need to hook up you Tiles to the WPF SceneRoot. Mistakes here will generally yield a blank display.

And that's it folks! Happy coding.

Many thanks to Gemini and ChatGPT for imagery and coding tips.


