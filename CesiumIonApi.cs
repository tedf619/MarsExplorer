using System.IO;
using System.IO.Compression;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;

namespace MarsExplorer;

public record CesiumIonEndpoint(string Type, Uri Uri, string? AccessToken);  // Type is e.g. "3DTILES"

/// <summary>All access to Cesium ion goes through this class.
public static class CesiumIonApi
{
  const int CesiumMarsAssetId = 3644333;  // a 3D tileset

  static HttpClient http;

  static CesiumIonApi()
  {
    http = new(new HttpClientHandler { AutomaticDecompression = DecompressionMethods.All });
  }

  public static async Task<CesiumIonEndpoint> GetEndpointAsync(string userToken)
  {
    using var request = new HttpRequestMessage(HttpMethod.Get, $"https://api.cesium.com/v1/assets/{CesiumMarsAssetId}/endpoint");
    request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", userToken);

    using HttpResponseMessage resp = await http.SendAsync(request).ConfigureAwait(false);
    EnsureSuccessStatusCode(resp, CesiumMarsAssetId);

    await using var stream = await resp.Content.ReadAsStreamAsync().ConfigureAwait(false);
    Models.AssetEndpointResponse? response = await JsonSerializer.DeserializeAsync<Models.AssetEndpointResponse>(stream);
    if (response == null) throw new HttpRequestException("Cesium ion returned a null response.");

    return new CesiumIonEndpoint(response.Type, new Uri(response.Url), response.AccessToken);
  }

  static void EnsureSuccessStatusCode(HttpResponseMessage response, int assetId)
  {
    if (response.IsSuccessStatusCode) return;

    string msg = response.StatusCode switch
    {
      HttpStatusCode.Unauthorized => "Your Cesium ion access token isn't valid (HTTP 401)." +
                                     "\nIf you think the token is valid, make sure it has the Cesium 'assets:read' scope.",
      HttpStatusCode.Forbidden => "This token is not allowed to read the Mars asset (HTTP 403). It must have the Cesium 'assets:read' scope.",
      HttpStatusCode.NotFound => $"Asset {assetId} was not found (HTTP 404). " +
                                 "Add 'Cesium Mars' from the Cesium ion Asset Depot to My Assets in the Cesium ion dashboard.",
      _ => $"Cesium ion returned HTTP {(int)response.StatusCode} {response.ReasonPhrase}."
    };
    throw new HttpRequestException(msg, null, response.StatusCode);
  }

  public static async Task<byte[]> GetTilesetAsync(Uri uri, string? accessToken)
  {
    using var request = new HttpRequestMessage(HttpMethod.Get, uri);

    if (accessToken != null)
      request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

    using var resp = await http.SendAsync(request);
    resp.EnsureSuccessStatusCode();
    var bytes = await resp.Content.ReadAsByteArrayAsync().ConfigureAwait(false);

    if (IsGzipFile(bytes))
      bytes = UnzipFile(bytes);

    return bytes;
  }

  static bool IsGzipFile(byte[] bytes)
  {
    // check for GZIP magic numbers in header
    return bytes.Length > 2 && bytes[0] == 0x1f && bytes[1] == 0x8b;
  }

  static byte[] UnzipFile(byte[] bytes)
  {
    using var src = new MemoryStream(bytes);
    using var gz = new GZipStream(src, CompressionMode.Decompress);
    using var dst = new MemoryStream();
    gz.CopyTo(dst);
    bytes = dst.ToArray();
    return bytes;
  }
}
