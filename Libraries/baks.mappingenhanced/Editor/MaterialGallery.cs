using Sandbox;
using Sandbox.UI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Editor;

[Dock( "Editor", "Materials Gallery", "collections" )]
public class MaterialGallery : Widget
{
	private MaterialGalleryView GalleryView;
	private LineEdit SearchBar;
	private Widget HeaderBar;
	private FloatSlider SizeSlider;
	private Label CountLabel;
	private Button RefreshButton;
	private Checkbox LocalMaterialsCheckbox;
	private ComboBox SortCombo;

	private static MaterialGallery _instance;
	private List<Package> _allPackages = new();
	private List<Asset> _localMaterials = new();
	private bool _isLoading = false;
	private bool _showLocalMaterials = false;
	private string _currentSort = "a-z";

	private static readonly Dictionary<string, string> SortOptions = new()
	{
		{ "a-z", "A-Z" },
		{ "z-a", "Z-A" },
		{ "newest", "Newest" },
		{ "updated", "Updated" }
	};

	public static MaterialGallery Instance
	{
		get
		{
			if ( !_instance.IsValid() ) return null;
			return _instance;
		}
		private set => _instance = value;
	}

	public MaterialGallery( Widget parent ) : base( parent )
	{
		Instance ??= this;

		WindowTitle = "Materials Gallery";
		Size = new Vector2( 1200, 800 );

		Layout = Layout.Column();
		Layout.Spacing = 0;

		CreateHeader();
		CreateGallery();

		_ = LoadMaterialsAsync();
	}

	private void CreateHeader()
	{
		HeaderBar = new Widget( this );
		HeaderBar.Layout = Layout.Row();
		HeaderBar.Layout.Spacing = 8;
		HeaderBar.Layout.Margin = 8;
		HeaderBar.FixedHeight = 44;

		Layout.Add( HeaderBar );

		SearchBar = new LineEdit();
		SearchBar.PlaceholderText = "⌕ Search materials...";
		SearchBar.TextEdited += ( text ) => FilterMaterials();
		SearchBar.MinimumHeight = 36;
		SearchBar.MinimumWidth = 200;
		HeaderBar.Layout.Add( SearchBar );

		SortCombo = new ComboBox();
		SortCombo.MinimumWidth = 100;
		SortCombo.ToolTip = "Sort materials";

		foreach ( var sort in SortOptions )
		{
			SortCombo.AddItem( sort.Value, null );
		}

		SortCombo.CurrentIndex = 0;
		SortCombo.ItemChanged += () =>
		{
			var sortKey = SortOptions.ElementAt( SortCombo.CurrentIndex ).Key;
			_currentSort = sortKey;
			ApplySortAndFilter();
		};
		HeaderBar.Layout.Add( SortCombo );

		LocalMaterialsCheckbox = new Checkbox();
		LocalMaterialsCheckbox.Text = "Local";
		LocalMaterialsCheckbox.ToolTip = "Include local project materials";
		LocalMaterialsCheckbox.State = CheckState.Off;
		LocalMaterialsCheckbox.StateChanged = isChecked =>
		{
			_showLocalMaterials = isChecked == CheckState.On;
			_ = LoadMaterialsAsync();
		};
		HeaderBar.Layout.Add( LocalMaterialsCheckbox );

		SizeSlider = new FloatSlider(HeaderBar);
		SizeSlider.Minimum = 64;
		SizeSlider.Maximum = 512;
		SizeSlider.Value = 256;
		SizeSlider.Step = 8;
		SizeSlider.MinimumWidth = 150;
		SizeSlider.ToolTip = "Thumbnail Size";
		SizeSlider.OnValueEdited += () =>
		{
			GalleryView?.SetThumbnailSize( (int)SizeSlider.Value );
		};
		HeaderBar.Layout.Add( SizeSlider );

		CountLabel = new Label( "Loading..." );
		CountLabel.MinimumWidth = 120;
		HeaderBar.Layout.Add( CountLabel );

		RefreshButton = new Button( "↻" );
		RefreshButton.FixedSize = 36;
		RefreshButton.ToolTip = "Refresh materials";
		RefreshButton.Clicked += () => _ = LoadMaterialsAsync();
		HeaderBar.Layout.Add( RefreshButton );
	}

	private void CreateGallery()
	{
		GalleryView = new MaterialGalleryView( this );
		GalleryView.OnPackageSelected = OnPackageSelected;
		GalleryView.OnAssetSelected = OnAssetSelected;
		Layout.Add( GalleryView );
	}

	private async Task LoadMaterialsAsync()
	{
		if ( _isLoading ) return;

		_isLoading = true;
		RefreshButton.Enabled = false;
		CountLabel.Text = "Loading...";

		try
		{
			_allPackages.Clear();
			_localMaterials.Clear();
			GalleryView.ClearCache();

			// Get organizations from settings
			var organizations = new List<string> { MappingToolSettings.DefaultOrganization };
			organizations.AddRange( MappingToolSettings.AdditionalOrganizations );

			// Remove duplicates and empty entries
			organizations = organizations
				.Where( org => !string.IsNullOrWhiteSpace( org ) )
				.Distinct( StringComparer.OrdinalIgnoreCase )
				.ToList();

			const int pageSize = 100;

			// Load materials from all configured organizations
			foreach ( var org in organizations )
			{
				try
				{
					int skip = 0;
					bool hasMore = true;

					while ( hasMore )
					{
						var query = $"type:material org:{org}";
						var result = await Package.FindAsync(
							query,
							take: pageSize,
							skip: skip
						);

						if ( result.Packages.Any() )
						{
							_allPackages.AddRange( result.Packages );
							skip += pageSize;

							CountLabel.Text = $"Loading... {_allPackages.Count} from {organizations.Count} org(s)";

							if ( result.Packages.Count() < pageSize )
							{
								hasMore = false;
							}
						}
						else
						{
							hasMore = false;
						}
					}
				}
				catch ( Exception ex )
				{
					Log.Warning( $"Failed to load materials from org '{org}': {ex.Message}" );
				}
			}

			if ( _showLocalMaterials )
			{
				_localMaterials = AssetSystem.All
					.Where( a => a.AssetType == AssetType.Material )
					.ToList();

				CountLabel.Text = $"Loading... {_allPackages.Count} online + {_localMaterials.Count} local";
			}

			if ( _allPackages.Count == 0 && _localMaterials.Count == 0 )
			{
				CountLabel.Text = "No materials found";
			}
			else
			{
				ApplySortAndFilter();
			}
		}
		catch ( System.Exception ex )
		{
			Log.Warning( $"Failed to load materials: {ex.Message}" );
			CountLabel.Text = "Error loading";
		}
		finally
		{
			_isLoading = false;
			RefreshButton.Enabled = true;
		}
	}

	private void FilterMaterials()
	{
		ApplySortAndFilter();
	}

	private void ApplySortAndFilter()
	{
		var packages = _allPackages;
		var localAssets = _localMaterials;

		if ( !string.IsNullOrWhiteSpace( SearchBar.Text ) )
		{
			var searchLower = SearchBar.Text.ToLower();

			packages = packages.Where( p =>
				p.Title.ToLower().Contains( searchLower ) ||
				p.Org.Title.ToLower().Contains( searchLower ) ||
				(p.Summary?.ToLower().Contains( searchLower ) ?? false)
			).ToList();

			localAssets = localAssets.Where( a =>
				a.Name.ToLower().Contains( searchLower ) ||
				a.Path.ToLower().Contains( searchLower )
			).ToList();
		}

		packages = SortPackages( packages );
		localAssets = SortAssets( localAssets );

		GalleryView.SetMaterials( packages, localAssets );
		UpdateCount();
	}

	private List<Package> SortPackages( List<Package> packages )
	{
		return _currentSort switch
		{
			"a-z" => packages.OrderBy( p => p.Title ).ToList(),
			"z-a" => packages.OrderByDescending( p => p.Title ).ToList(),
			"newest" => packages.OrderByDescending( p => p.Created ).ToList(),
			"updated" => packages.OrderByDescending( p => p.Updated ).ToList(),
			_ => packages
		};
	}

	private List<Asset> SortAssets( List<Asset> assets )
	{
		return _currentSort switch
		{
			"a-z" => assets.OrderBy( a => a.Name ).ToList(),
			"z-a" => assets.OrderByDescending( a => a.Name ).ToList(),
			"newest" => assets.OrderByDescending( a => a.LastOpened ).ToList(),
			_ => assets
		};
	}

	private void UpdateCount()
	{
		var current = GalleryView?.ItemCount ?? 0;
		var totalOnline = _allPackages.Count;
		var totalLocal = _localMaterials.Count;

		if ( _showLocalMaterials )
		{
			CountLabel.Text = $"{current} of {totalOnline + totalLocal} ({totalLocal} local)";
		}
		else if ( current == totalOnline )
		{
			CountLabel.Text = $"{totalOnline} materials";
		}
		else
		{
			CountLabel.Text = $"{current} of {totalOnline}";
		}
	}

	private void OnPackageSelected( Package package )
	{
		_ = SetActiveMaterial( package );
	}

	private void OnAssetSelected( Asset asset )
	{
		_ = SetActiveMaterialFromAsset( asset );
	}

	CancellationTokenSource packageCTS;

	private async Task SetActiveMaterial( Package package )
	{
		try
		{
			packageCTS?.Cancel();
			packageCTS = new CancellationTokenSource();

			package = await Package.FetchAsync( package.FullIdent, false );

			var asset = await AssetSystem.InstallAsync( package.FullIdent, true, null, packageCTS.Token );

			if ( asset is null )
			{
				Log.Warning( $"Failed to install material: {package.Title}" );
				return;
			}

			if ( packageCTS.Token.IsCancellationRequested )
				return;

			SetMaterialOnTool( asset );

			EditorUtility.InspectorObject = asset;
			EditorUtility.PlayAssetSound( asset );

			Log.Info( $"Installed material: {package.Title} at {asset.RelativePath}" );
		}
		catch ( System.Exception ex )
		{
			Log.Warning( $"Failed to install material: {ex.Message}" );
		}
	}

	private async Task SetActiveMaterialFromAsset( Asset asset )
	{
		try
		{
			await Task.Delay( 1 );

			SetMaterialOnTool( asset );

			EditorUtility.InspectorObject = asset;
			EditorUtility.PlayAssetSound( asset );

			Log.Info( $"Selected local material: {asset.Name}" );
		}
		catch ( System.Exception ex )
		{
			Log.Warning( $"Failed to set material: {ex.Message}" );
		}
	}

	private void SetMaterialOnTool( Asset asset )
	{
		var sceneView = SceneViewWidget.Current;
		if ( sceneView?.Tools?.CurrentTool is Editor.MeshEditor.MeshTool meshtool )
		{
			try
			{
				if ( asset.LoadResource<Material>() is Material mat )
				{
					meshtool.ActiveMaterial = mat;
				}
			}
			catch
			{
			}
		}
	}
}

public class MaterialGalleryView : Widget
{
	private ListView _listView;
	private List<object> _items = new();
	private int _thumbnailSize = 256;

	private Dictionary<string, Pixmap> _pixmapCache = new();
	private Dictionary<string, int> _loadingRetries = new();
	private const int MaxRetries = 5;

	public Action<Package> OnPackageSelected;
	public Action<Asset> OnAssetSelected;
	public int ItemCount => _items.Count;

	public MaterialGalleryView( Widget parent ) : base( parent )
	{
		Layout = Layout.Column();
		Layout.Spacing = 0;

		_listView = new ListView( this );
		_listView.ItemSize = new Vector2( _thumbnailSize + 16, _thumbnailSize + 16 );
		_listView.ItemSpacing = 1;
		_listView.ItemAlign = Align.Center;
		_listView.ItemPaint = PaintItem;
		_listView.ItemActivated = OnItemDoubleClicked;

		Layout.Add( _listView );
	}

	protected override void OnPaint()
	{
		base.OnPaint();

		Paint.ClearBrush();
		Paint.ClearPen();
		Paint.SetBrush( Theme.ControlBackground );
		Paint.DrawRect( LocalRect );
	}

	public void ClearCache()
	{
		_pixmapCache.Clear();
		_loadingRetries.Clear();
	}

	public void SetMaterials( List<Package> packages, List<Asset> localAssets )
	{
		_items.Clear();
		_items.AddRange( packages );
		_items.AddRange( localAssets );
		_listView.SetItems( _items );
	}

	public void SetThumbnailSize( int size )
	{
		_thumbnailSize = size;
		_listView.ItemSize = new Vector2( _thumbnailSize + 16, _thumbnailSize + 16 );

		ClearCache();

		_listView.SetItems( _items );
	}

	private void OnItemDoubleClicked( object item )
	{
		if ( item is Package package )
		{
			OnPackageSelected?.Invoke( package );
		}
		else if ( item is Asset asset )
		{
			OnAssetSelected?.Invoke( asset );
		}
	}

	private void PaintItem( VirtualWidget item )
	{
		var obj = item.Object;

		if ( obj is Package package )
		{
			PaintPackageItem( item, package );
		}
		else if ( obj is Asset asset )
		{
			PaintAssetItem( item, asset );
		}
	}

	private void PaintPackageItem( VirtualWidget item, Package package )
	{
		var rect = item.Rect;
		var hovered = item.Hovered;

		PaintCardBackground( rect, hovered );

		var titleBarHeight = Math.Max( 16, _thumbnailSize * 0.08f );
		var fontSize = Math.Max( 7, (int)(_thumbnailSize * 0.03f) );

		var thumbRect = rect.Shrink( 8, 8, 8, 8 + titleBarHeight );

		var thumbUrl = package.Thumb;
		if ( !string.IsNullOrEmpty( thumbUrl ) )
		{
			if ( _pixmapCache.TryGetValue( thumbUrl, out var cachedPixmap ) )
			{
				PaintThumbnail( thumbRect, cachedPixmap, hovered );
			}
			else
			{
				var retries = _loadingRetries.GetValueOrDefault( thumbUrl, 0 );

				if ( retries < MaxRetries )
				{
					PaintLoadingPlaceholder( thumbRect, retries );

					if ( retries == 0 || !_loadingRetries.ContainsKey( thumbUrl ) )
					{
						_ = LoadThumbnailAsync( thumbUrl );
					}
				}
				else
				{
					PaintErrorPlaceholder( thumbRect );
				}
			}
		}
		else
		{
			PaintErrorPlaceholder( thumbRect );
		}

		var titleRect = new Rect( rect.Left + 8, thumbRect.Bottom, rect.Width - 16, titleBarHeight );
		PaintTitleBar( titleRect, fontSize, package.Title );
	}

	private void PaintAssetItem( VirtualWidget item, Asset asset )
	{
		var rect = item.Rect;
		var hovered = item.Hovered;

		PaintCardBackground( rect, hovered, Theme.Green.WithAlpha( 0.2f ) );

		var titleBarHeight = Math.Max( 16, _thumbnailSize * 0.08f );
		var fontSize = Math.Max( 7, (int)(_thumbnailSize * 0.03f) );

		var thumbRect = rect.Shrink( 8, 8, 8, 8 + titleBarHeight );

		var thumbPath = asset.GetAssetThumb();
		if ( thumbPath != null )
		{
			PaintThumbnail( thumbRect, thumbPath, hovered );
		}
		else
		{
			PaintErrorPlaceholder( thumbRect );
		}

		var badgeRect = new Rect( thumbRect.Right - 50, thumbRect.Top + 4, 45, 16 );
		Paint.SetBrush( Theme.Green );
		Paint.DrawRect( badgeRect, 8 );
		Paint.SetPen( Color.White );
		Paint.SetDefaultFont( 7, 700 );
		Paint.DrawText( badgeRect, "LOCAL", TextFlag.Center );

		var titleRect = new Rect( rect.Left + 8, thumbRect.Bottom, rect.Width - 16, titleBarHeight );
		PaintTitleBar( titleRect, fontSize, asset.Name );
	}

	private void PaintCardBackground( Rect rect, bool hovered, Color? highlightColor = null )
	{
		Paint.ClearPen();

		if ( hovered )
		{
			Paint.SetBrush( highlightColor ?? Theme.Blue.WithAlpha( 0.2f ) );
			Paint.DrawRect( rect, 8 );
			Paint.SetPen( highlightColor ?? Theme.Blue, 2 );
			Paint.DrawRect( rect, 8 );
		}
		else
		{
			Paint.SetBrush( Theme.ControlBackground );
			Paint.DrawRect( rect, 8 );
		}
	}

	private void PaintThumbnail( Rect thumbRect, Pixmap pixmap, bool hovered )
	{
		Paint.SetBrush( Color.Black );
		Paint.DrawRect( thumbRect, 4 );
		Paint.Draw( thumbRect, pixmap );

		Paint.ClearBrush();
		Paint.SetPen( hovered ? Theme.Blue : Theme.ControlBackground.Lighten( 0.2f ), 1 );
		Paint.DrawRect( thumbRect, 4 );
	}

	private void PaintTitleBar( Rect titleRect, int fontSize, string title )
	{
		Paint.SetPen( Theme.TextControl );
		Paint.SetDefaultFont( fontSize, 600 );
		Paint.DrawText( titleRect, title, TextFlag.Center | TextFlag.SingleLine );
	}

	private async Task LoadThumbnailAsync( string thumbUrl )
	{
		var retryCount = _loadingRetries.GetValueOrDefault( thumbUrl, 0 );

		try
		{
			if ( retryCount > 0 )
			{
				var delay = (int)(Math.Pow( 2, retryCount ) * 100 - 100);
				await Task.Delay( delay );
			}

			var texture = Texture.Load( thumbUrl );

			if ( texture == null )
			{
				await Task.Delay( 200 );
				texture = Texture.Load( thumbUrl );
			}

			if ( texture != null )
			{
				var pixmap = Pixmap.FromTexture( texture );

				if ( pixmap != null )
				{
					_pixmapCache[thumbUrl] = pixmap;
					_loadingRetries.Remove( thumbUrl );
					Update();
					return;
				}
			}

			_loadingRetries[thumbUrl] = retryCount + 1;

			if ( retryCount + 1 < MaxRetries )
			{
				Update();
				_ = LoadThumbnailAsync( thumbUrl );
			}
			else
			{
				Update();
			}
		}
		catch ( Exception ex )
		{
			if ( retryCount == 0 )
			{
				Log.Warning( $"Failed to load thumbnail {thumbUrl}: {ex.Message}" );
			}

			_loadingRetries[thumbUrl] = retryCount + 1;

			if ( retryCount + 1 < MaxRetries )
			{
				Update();
				_ = LoadThumbnailAsync( thumbUrl );
			}
			else
			{
				Update();
			}
		}
	}

	private async Task LoadLocalThumbnailAsync( string thumbPath, Asset asset )
	{
		var retryCount = _loadingRetries.GetValueOrDefault( thumbPath, 0 );

		try
		{
			if ( retryCount > 0 )
			{
				var delay = (int)(Math.Pow( 2, retryCount ) * 100 - 100);
				await Task.Delay( delay );
			}

			var material = asset.LoadResource<Material>();
			if ( material != null )
			{
				var texture = material.GetTexture( "Color" ) ?? material.GetTexture( "Albedo" );
				if ( texture != null )
				{
					var pixmap = Pixmap.FromTexture( texture );
					if ( pixmap != null )
					{
						_pixmapCache[thumbPath] = pixmap;
						_loadingRetries.Remove( thumbPath );
						Update();
						return;
					}
				}
			}

			_loadingRetries[thumbPath] = retryCount + 1;

			if ( retryCount + 1 < MaxRetries )
			{
				Update();
				_ = LoadLocalThumbnailAsync( thumbPath, asset );
			}
			else
			{
				Update();
			}
		}
		catch ( Exception ex )
		{
			if ( retryCount == 0 )
			{
				Log.Warning( $"Failed to load local thumbnail for {asset.Name}: {ex.Message}" );
			}

			_loadingRetries[thumbPath] = retryCount + 1;

			if ( retryCount + 1 < MaxRetries )
			{
				Update();
				_ = LoadLocalThumbnailAsync( thumbPath, asset );
			}
			else
			{
				Update();
			}
		}
	}

	private void PaintLoadingPlaceholder( Rect thumbRect, int retries )
	{
		Paint.SetBrush( Theme.ControlBackground.Darken( 0.3f ) );
		Paint.DrawRect( thumbRect, 4 );

		Paint.SetPen( Theme.TextDark );
		Paint.SetDefaultFont( 9 );

		var dots = new string( '●', Math.Min( retries + 1, 3 ) );
		Paint.DrawText( thumbRect, dots, TextFlag.Center );
	}

	private void PaintErrorPlaceholder( Rect thumbRect )
	{
		Paint.SetBrush( Theme.ControlBackground.Darken( 0.5f ) );
		Paint.DrawRect( thumbRect, 4 );

		Paint.SetPen( Theme.Red.WithAlpha( 0.5f ) );
		Paint.SetDefaultFont( 10 );
		Paint.DrawText( thumbRect, "✖", TextFlag.Center );
	}
}
