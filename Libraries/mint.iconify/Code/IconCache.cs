using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Sandbox;

namespace Iconify;

/// <summary>
/// Fetches icons from the Iconify API and caches them as textures.
/// Handles disk cache and in-memory cache with proper null safety.
/// </summary>
public static class IconCache
{
	private static readonly Dictionary<string, Texture> MemoryCache = new();
	private static BaseFileSystem? _diskCache;
	private static bool _diskCacheReady;

	private static BaseFileSystem DiskCache
	{
		get
		{
			if ( !_diskCacheReady )
			{
				_diskCacheReady = true;
				try
				{
					if ( FileSystem.Data is not null )
					{
						FileSystem.Data.CreateDirectory( "iconify_cache" );
						_diskCache = FileSystem.Data.CreateSubSystem( "iconify_cache" );
					}
				}
				catch ( Exception e )
				{
					Log.Warning( $"[Iconify] Could not create disk cache: {e.Message}" );
					_diskCache = null;
				}
			}
			return _diskCache;
		}
	}

	/// <summary>
	/// Get an icon texture from cache or fetch from the Iconify API.
	/// </summary>
	public static async Task<Texture?> GetOrFetch( string prefix, string name, string color, int size )
	{
		var cacheKey = $"{prefix}_{name}_{color}_{size}";

		// Memory cache
		if ( MemoryCache.TryGetValue( cacheKey, out var cached ) && cached.IsValid() )
			return cached;

		// Disk cache
		var diskPath = $"{cacheKey}.svg";
		if ( DiskCache is not null && DiskCache.FileExists( diskPath ) )
		{
			try
			{
				var svgString = DiskCache.ReadAllText( diskPath );
				var tex = Texture.CreateFromSvgSource( svgString, size, size, null );
				if ( tex is not null && tex.IsValid() )
				{
					MemoryCache[cacheKey] = tex;
					return tex;
				}
			}
			catch { /* disk cache corrupted, re-fetch */ }
		}

		// Fetch from API
		var texture = await FetchFromApi( prefix, name, color, size, cacheKey );
		if ( texture is not null )
		{
			MemoryCache[cacheKey] = texture;
		}

		return texture;
	}

	private static async Task<Texture?> FetchFromApi( string prefix, string name, string color, int size, string cacheKey )
	{
		var encodedColor = Uri.EscapeDataString( color ?? "white" );
		var url = $"https://api.iconify.design/{prefix}/{name}.svg?color={encodedColor}&width={size}&height={size}";

		try
		{
			var svgString = await Http.RequestStringAsync( url );

			if ( string.IsNullOrEmpty( svgString ) )
			{
				Log.Warning( $"[Iconify] Empty response for {prefix}:{name}" );
				return null;
			}

			// Create texture directly from SVG string with size
			var texture = Texture.CreateFromSvgSource( svgString, size, size, null );

			if ( texture is not null && texture.IsValid() )
			{
				SaveToDiskCache( $"{cacheKey}.svg", System.Text.Encoding.UTF8.GetBytes( svgString ) );
			}

			return texture;
		}
		catch ( Exception e )
		{
			Log.Warning( $"[Iconify] API fetch failed for {prefix}:{name}: {e.Message}" );
			return null;
		}
	}

	private static void SaveToDiskCache( string path, byte[] data )
	{
		try
		{
			DiskCache?.WriteAllBytes( path, data );
		}
		catch { /* non-critical, just won't be cached */ }
	}
}
