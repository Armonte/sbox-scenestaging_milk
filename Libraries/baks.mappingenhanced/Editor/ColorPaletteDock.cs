using Sandbox;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Editor;

[Dock( "Editor", "Color Palette", "palette" )]
public class ColorPaletteDock : Widget
{
	const int MaxColors = 24;
	const int PaletteColumns = 6;

	readonly List<Color> _paletteColors = new();
	readonly ColorSlotWidget[] _slots;

	readonly List<string> _paletteNames = new();
	string _paletteId = "Default";

	Widget HeaderBar;
	Label PaletteNameLabel;

	public string PaletteId
	{
		get => _paletteId;
		set
		{
			if ( string.IsNullOrEmpty( value ) || _paletteId == value )
				return;

			_paletteId = value;
			SaveActivePalette();
			LoadPaletteFromCookie();
			UpdateHeader();
		}
	}

	public ColorPaletteDock( Widget parent ) : base( parent )
	{
		WindowTitle = "Color Palette";
		Size = new Vector2( 400, 500 );
		
		FixedWidth = 220;

		Layout = Layout.Column();
		Layout.Spacing = 0;

		CreateHeader();

		Layout.AddStretchCell();

		var grid = Layout.Grid();
		grid.Spacing = 4;
		grid.Margin = 8;
		Layout.Add( grid );

		OnPaintOverride = () =>
		{
			Paint.SetBrushAndPen( Theme.ControlBackground );
			Paint.DrawRect( Paint.LocalRect, 0 );
			return false;
		};

		_slots = new ColorSlotWidget[MaxColors];

		for ( int i = 0; i < MaxColors; i++ )
		{
			var row = i / PaletteColumns;
			var col = i % PaletteColumns;

			var slot = new ColorSlotWidget( this )
			{
				FixedSize = 48
			};

			_slots[i] = slot;
			grid.AddCell( row, col, slot );
		}
		Layout.AddStretchCell();
		LoadPalettes();
		LoadPaletteFromCookie();
	}

	void CreateHeader()
	{
		HeaderBar = new Widget( this );
		HeaderBar.Layout = Layout.Row();
		HeaderBar.Layout.Spacing = 8;
		HeaderBar.Layout.Margin = 8;
		HeaderBar.FixedHeight = 36;

		HeaderBar.OnPaintOverride = () =>
		{
			Paint.SetBrushAndPen( Theme.ButtonBackground );
			Paint.DrawRect( HeaderBar.LocalRect, 0 );
			return false;
		};

		Layout.Add( HeaderBar );

		PaletteNameLabel = new Label( "Default" );
		PaletteNameLabel.ToolTip = "Current palette";
		HeaderBar.Layout.Add( PaletteNameLabel, 1 );

		var menuButton = new Button();
		menuButton.Text = "≡";
		menuButton.FixedWidth = 36;
		menuButton.ToolTip = "Palette menu";
		menuButton.Pressed += ShowPaletteMenu;
		HeaderBar.Layout.Add( menuButton );

		var addButton = new Button();
		addButton.Text = "+";
		addButton.FixedWidth = 36;
		addButton.ToolTip = "Add color from picker";
		addButton.Pressed += AddColorFromPicker;
		HeaderBar.Layout.Add( addButton );
	}

	void UpdateHeader()
	{
		PaletteNameLabel.Text = _paletteId;
	}

	void ShowPaletteMenu()
	{
		var m = new ContextMenu();
		AddPaletteMenu( m );
		m.OpenAtCursor( false );
	}

	void AddColorFromPicker()
	{
		// Find the first empty slot
		ColorSlotWidget firstEmptySlot = null;
		for ( int i = 0; i < _slots.Length; i++ )
		{
			if ( !IsColorValid( _slots[i].SlotColor ) )
			{
				firstEmptySlot = _slots[i];
				break;
			}
		}

		if ( firstEmptySlot != null )
		{
			SlotAssignColor( firstEmptySlot );
		}
		else
		{
			Log.Warning( "Color palette is full. Please clear a slot first." );
		}
	}

	void LoadPalettes()
	{
		_paletteNames.Clear();

		string rawNames;
		try { rawNames = ProjectCookie.Get( "ColorPalette.Names", string.Empty ); }
		catch { rawNames = string.Empty; }

		if ( string.IsNullOrWhiteSpace( rawNames ) )
		{
			_paletteNames.Add( "Default" );
		}
		else
		{
			foreach ( var name in rawNames.Split( ';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries ) )
			{
				if ( !_paletteNames.Contains( name ) )
					_paletteNames.Add( name );
			}

			if ( _paletteNames.Count == 0 )
				_paletteNames.Add( "Default" );
		}

		try { _paletteId = ProjectCookie.Get( "ColorPalette.Active", _paletteNames[0] ); }
		catch { _paletteId = _paletteNames[0]; }

		if ( !_paletteNames.Contains( _paletteId ) )
			_paletteId = _paletteNames[0];
	}

	void SavePalettes()
	{
		ProjectCookie.Set( "ColorPalette.Names", string.Join( ";", _paletteNames ) );
		SaveActivePalette();
	}

	void SaveActivePalette()
	{
		ProjectCookie.Set( "ColorPalette.Active", _paletteId ?? string.Empty );
	}

	internal void AddPaletteMenu( ContextMenu m )
	{
		LoadPalettes();

		var p = m.AddMenu( "Palettes", "palette" );

		foreach ( var name in _paletteNames )
		{
			var localName = name;
			var icon = (localName == _paletteId) ? "check" : "palette";
			p.AddOption( localName, icon, () => PaletteId = localName );
		}

		p.AddSeparator();

		p.AddOption( "New Palette…", "add", ShowCreatePalettePopup );
		p.AddOption( "Rename Palette…", "edit", () => ShowRenamePalettePopup( _paletteId ) ).Enabled = _paletteNames.Count > 0;
		p.AddOption( "Duplicate Palette", "content_copy", () => DuplicatePalette( _paletteId ) ).Enabled = _paletteNames.Count > 0;

		var del = p.AddOption( "Delete Palette", "delete", () => DeletePalette( _paletteId ) );
		del.Enabled = _paletteNames.Count > 1;

		m.AddSeparator();
		m.AddOption( "Clear All Colors", "clear", ClearAllColors ).Enabled = _paletteColors.Any( c => IsColorValid( c ) );
	}

	void ShowCreatePalettePopup()
	{
		var popup = new PopupWidget( this );
		popup.FixedWidth = 220;
		popup.Layout = Layout.Column();
		popup.Layout.Margin = 8;
		popup.Layout.Spacing = 4;

		_ = popup.Layout.Add( new Label.Small( "New palette" ) );
		var entry = popup.Layout.Add( new LineEdit( popup ) );
		entry.FixedHeight = Theme.RowHeight;
		entry.PlaceholderText = "Palette name…";

		void Commit()
		{
			var name = entry.Value?.Trim();
			if ( string.IsNullOrEmpty( name ) ) { popup.Destroy(); return; }
			if ( _paletteNames.Contains( name ) ) { popup.Destroy(); return; }

			_paletteNames.Add( name );
			_paletteId = name;

			SavePalettes();
			LoadPaletteFromCookie();
			UpdateHeader();

			popup.Destroy();
		}

		entry.ReturnPressed += Commit;

		popup.OpenAtCursor();
		entry.Focus();
	}

	void ShowRenamePalettePopup( string oldName )
	{
		if ( string.IsNullOrEmpty( oldName ) )
			return;

		var popup = new PopupWidget( this );
		popup.FixedWidth = 220;
		popup.Layout = Layout.Column();
		popup.Layout.Margin = 8;
		popup.Layout.Spacing = 4;

		_ = popup.Layout.Add( new Label.Small( "Rename palette" ) );
		var entry = popup.Layout.Add( new LineEdit( popup ) );
		entry.FixedHeight = Theme.RowHeight;
		entry.Value = oldName;

		void Commit()
		{
			var newName = entry.Value?.Trim();
			if ( string.IsNullOrEmpty( newName ) || newName == oldName ) { popup.Destroy(); return; }
			if ( _paletteNames.Contains( newName ) ) { popup.Destroy(); return; }

			RenamePalette( oldName, newName );
			popup.Destroy();
		}

		entry.ReturnPressed += Commit;

		popup.OpenAtCursor();
		entry.Focus();
	}

	void RenamePalette( string oldName, string newName )
	{
		var idx = _paletteNames.IndexOf( oldName );
		if ( idx < 0 ) return;

		_paletteNames[idx] = newName;

		var oldKey = $"ColorPalette.{oldName}";
		var newKey = $"ColorPalette.{newName}";

		try
		{
			var data = ProjectCookie.Get( oldKey, string.Empty );
			ProjectCookie.Set( newKey, data );
			ProjectCookie.Set( oldKey, string.Empty );
		}
		catch { }

		if ( _paletteId == oldName )
			_paletteId = newName;

		SavePalettes();
		LoadPaletteFromCookie();
		UpdateHeader();
	}

	void DuplicatePalette( string sourceName )
	{
		if ( string.IsNullOrEmpty( sourceName ) )
			return;

		var baseName = $"{sourceName} Copy";
		var newName = baseName;
		int counter = 2;

		while ( _paletteNames.Contains( newName ) )
			newName = $"{baseName} {counter++}";

		_paletteNames.Add( newName );

		var srcKey = $"ColorPalette.{sourceName}";
		var dstKey = $"ColorPalette.{newName}";

		try
		{
			var data = ProjectCookie.Get( srcKey, string.Empty );
			ProjectCookie.Set( dstKey, data );
		}
		catch { }

		_paletteId = newName;

		SavePalettes();
		LoadPaletteFromCookie();
		UpdateHeader();
	}

	void DeletePalette( string name )
	{
		if ( _paletteNames.Count <= 1 )
			return;

		var idx = _paletteNames.IndexOf( name );
		if ( idx < 0 )
			return;

		_paletteNames.RemoveAt( idx );

		var key = $"ColorPalette.{name}";
		try { ProjectCookie.Set( key, string.Empty ); }
		catch { }

		_paletteId = _paletteNames[Math.Clamp( idx - 1, 0, _paletteNames.Count - 1 )];

		SavePalettes();
		LoadPaletteFromCookie();
		UpdateHeader();
	}

	void ClearAllColors()
	{
		_paletteColors.Clear();
		UpdateSlots();
		SavePaletteToCookie();
	}

	public void AddColor( Color color )
	{
		// Remove duplicates
		_paletteColors.RemoveAll( c => ColorsEqual( c, color ) );

		// Add to the first empty slot or append
		var firstEmptyIndex = -1;
		for ( int i = 0; i < _slots.Length; i++ )
		{
			if ( i >= _paletteColors.Count || !IsColorValid( _paletteColors[i] ) )
			{
				firstEmptyIndex = i;
				break;
			}
		}

		if ( firstEmptyIndex >= 0 )
		{
			while ( _paletteColors.Count <= firstEmptyIndex )
				_paletteColors.Add( default );

			_paletteColors[firstEmptyIndex] = color;
		}
		else if ( _paletteColors.Count < _slots.Length )
		{
			_paletteColors.Add( color );
		}

		UpdateSlots();
		SavePaletteToCookie();
	}

	internal bool IsColorValid( Color color )
	{
		// Check if color has been set (not default)
		return color.a > 0;
	}

	bool ColorsEqual( Color a, Color b, float tolerance = 0.001f )
	{
		return Math.Abs( a.r - b.r ) < tolerance &&
			   Math.Abs( a.g - b.g ) < tolerance &&
			   Math.Abs( a.b - b.b ) < tolerance &&
			   Math.Abs( a.a - b.a ) < tolerance;
	}

	void UpdateSlots()
	{
		for ( int i = 0; i < _slots.Length; i++ )
		{
			if ( i < _paletteColors.Count && IsColorValid( _paletteColors[i] ) )
				_slots[i].SlotColor = _paletteColors[i];
			else
				_slots[i].SlotColor = default;
		}
	}

	internal void SlotClicked( Color color )
	{
		if ( !IsColorValid( color ) ) return;

		// Copy to clipboard
		var hexColor = ColorToHex( color );
		EditorUtility.Clipboard.Copy( hexColor );

		// Log feedback (Toast doesn't exist in s&box editor)
		Log.Info( $"Color copied to clipboard: {hexColor}" );
	}

	internal string ColorToHex( Color color )
	{
		int r = (int)(color.r * 255);
		int g = (int)(color.g * 255);
		int b = (int)(color.b * 255);
		return $"#{r:X2}{g:X2}{b:X2}";
	}

	private void SwapColors( ColorSlotWidget targetSlot, Color draggedColor )
	{
		var targetIndex = Array.IndexOf( _slots, targetSlot );
		if ( targetIndex < 0 ) return;

		// Find the source slot with the dragged color
		var sourceIndex = -1;
		for ( int i = 0; i < _paletteColors.Count; i++ )
		{
			if ( IsColorValid( _paletteColors[i] ) && ColorsEqual( _paletteColors[i], draggedColor ) )
			{
				sourceIndex = i;
				break;
			}
		}

		if ( sourceIndex < 0 ) return;

		// Ensure both indices are within bounds
		while ( _paletteColors.Count <= Math.Max( sourceIndex, targetIndex ) )
			_paletteColors.Add( default );

		// Swap the colors
		var temp = _paletteColors[targetIndex];
		_paletteColors[targetIndex] = _paletteColors[sourceIndex];
		_paletteColors[sourceIndex] = temp;

		UpdateSlots();
		SavePaletteToCookie();
	}

	private void SlotSetColor( ColorSlotWidget slot, Color color )
	{
		if ( slot is null ) return;

		var index = Array.IndexOf( _slots, slot );
		if ( index < 0 ) return;

		if ( index >= _paletteColors.Count )
		{
			while ( _paletteColors.Count <= index )
				_paletteColors.Add( default );
		}

		_paletteColors[index] = color;
		UpdateSlots();
		SavePaletteToCookie();
	}

	private void SlotAssignColor( ColorSlotWidget slot )
	{
		var popup = new PopupWidget( this );
		popup.Layout = Layout.Column();
		popup.Layout.Margin = 12;
		popup.Layout.Spacing = 8;

		var label = new Label.Small( "Select a color:" );
		popup.Layout.Add( label );

		var picker = new ColorPicker( popup );
		picker.FixedSize = new Vector2( 300, 400 );
		picker.Value = IsColorValid( slot.SlotColor ) ? slot.SlotColor : Color.White;
		popup.Layout.Add( picker );

		var buttonLayout = popup.Layout.AddRow();
		buttonLayout.Spacing = 8;

		var cancelButton = new Button( "Cancel" );
		cancelButton.Pressed += () => popup.Destroy();
		buttonLayout.Add( cancelButton, 1 );

		var setButton = new Button( "Set Color" );
		setButton.Pressed += () =>
		{
			SlotSetColor( slot, picker.Value );
			popup.Destroy();
		};
		buttonLayout.Add( setButton, 1 );

		popup.OpenAtCursor();
	}

	private void SlotClear( ColorSlotWidget slot ) => SlotSetColor( slot, default );

	void SavePaletteToCookie()
	{
		// Ensure we have exactly MaxColors entries
		while ( _paletteColors.Count < MaxColors )
			_paletteColors.Add( default );

		var parts = _paletteColors
			.Take( MaxColors )
			.Select( c => IsColorValid( c ) ? ColorToHex( c ) : string.Empty );

		ProjectCookie.Set( $"ColorPalette.{_paletteId}", string.Join( ";", parts ) );
	}

	void LoadPaletteFromCookie()
	{
		string data;
		try { data = ProjectCookie.Get( $"ColorPalette.{_paletteId}", string.Empty ); }
		catch { data = string.Empty; }

		_paletteColors.Clear();

		if ( string.IsNullOrEmpty( data ) )
		{
			UpdateSlots();
			return;
		}

		var parts = data.Split( ';' );

		for ( int i = 0; i < MaxColors; i++ )
		{
			if ( i >= parts.Length || string.IsNullOrWhiteSpace( parts[i] ) )
			{
				_paletteColors.Add( default );
				continue;
			}

			var hexString = parts[i].Trim();
			if ( TryParseHex( hexString, out var color ) )
			{
				_paletteColors.Add( color );
			}
			else
			{
				_paletteColors.Add( default );
			}
		}

		UpdateSlots();
	}

	bool TryParseHex( string hex, out Color color )
	{
		color = default;

		if ( string.IsNullOrEmpty( hex ) )
			return false;

		hex = hex.TrimStart( '#' );

		if ( hex.Length != 6 && hex.Length != 8 )
			return false;

		try
		{
			int r = Convert.ToInt32( hex.Substring( 0, 2 ), 16 );
			int g = Convert.ToInt32( hex.Substring( 2, 2 ), 16 );
			int b = Convert.ToInt32( hex.Substring( 4, 2 ), 16 );
			int a = hex.Length == 8 ? Convert.ToInt32( hex.Substring( 6, 2 ), 16 ) : 255;

			color = new Color( r / 255f, g / 255f, b / 255f, a / 255f );
			return true;
		}
		catch
		{
			return false;
		}
	}

	class ColorSlotWidget : Widget
	{
		readonly ColorPaletteDock _dock;
		bool _isValidDropHover;

		public Color SlotColor
		{
			get => field;
			set
			{
				if ( field == value ) return;
				field = value;
				Update();
			}
		}

		public ColorSlotWidget( ColorPaletteDock dock ) : base( dock )
		{
			_dock = dock;
			Cursor = CursorShape.Finger;
			ToolTip = "Left click to copy, drag to reorder, right click for options";

			IsDraggable = true;
			AcceptDrops = true;
		}

		protected override void OnMouseClick( MouseEvent e )
		{
			base.OnMouseClick( e );

			if ( e.LeftMouseButton )
			{
				if ( _dock.IsColorValid( SlotColor ) )
				{
					_dock.SlotClicked( SlotColor );
				}
				else
				{
					_dock.SlotAssignColor( this );
				}
			}
		}

		protected override void OnDragStart()
		{
			if ( !_dock.IsColorValid( SlotColor ) )
				return;

			var drag = new Drag( this );
			drag.Data.Object = SlotColor;
			drag.Execute();
		}

		public override void OnDragLeave()
		{
			base.OnDragLeave();
			_isValidDropHover = false;
			Update();
		}

		public override void OnDragHover( DragEvent ev )
		{
			if ( ev.Data.Object is Color )
			{
				ev.Action = DropAction.Move;
				_isValidDropHover = true;
				Update();
			}
		}

		public override void OnDragDrop( DragEvent ev )
		{
			base.OnDragDrop( ev );

			if ( ev.Data.Object is Color droppedColor )
			{
				_dock.SwapColors( this, droppedColor );
				ev.Action = DropAction.Move;
			}

			_isValidDropHover = false;
			Update();
		}

		protected override void OnContextMenu( ContextMenuEvent e )
		{
			var m = new ContextMenu();
			bool hasColor = _dock.IsColorValid( SlotColor );

			if ( hasColor )
			{
				var hexColor = _dock.ColorToHex( SlotColor );
				m.AddOption( $"Copy {hexColor}", "content_copy", () => _dock.SlotClicked( SlotColor ) );
				m.AddSeparator();
			}

			var text = hasColor ? "Change Color" : "Set Color";
			m.AddOption( text, "palette", () => _dock.SlotAssignColor( this ) );

			m.AddSeparator();
			_dock.AddPaletteMenu( m );

			m.AddSeparator();
			m.AddOption( "Clear", "backspace", () => _dock.SlotClear( this ) ).Enabled = hasColor;

			m.OpenAtCursor( false );
			e.Accepted = true;
		}

		protected override void OnPaint()
		{
			Paint.ClearPen();
			Paint.ClearBrush();

			var controlRect = Paint.LocalRect;
			controlRect = controlRect.Shrink( 2 );

			Paint.Antialiasing = true;
			Paint.TextAntialiasing = true;

			if ( _dock.IsColorValid( SlotColor ) )
			{
				// Draw color
				Paint.SetBrush( SlotColor );
				Paint.DrawRect( controlRect );

				// Draw border
				if ( _isValidDropHover )
				{
					Paint.SetPen( Theme.Green );
					Paint.DrawRect( controlRect );
				}
				else if ( Paint.HasMouseOver )
				{
					Paint.SetPen( Color.White, 3, PenStyle.Dot );
					Paint.DrawRect( controlRect );
				}
				else
				{
					Paint.SetPen( SlotColor.Darken( 0.3f ), 2, PenStyle.Dot );
					Paint.DrawRect( controlRect );
				}
			}
			else
			{
				// Empty slot
				var baseFill = Theme.Text.WithAlpha( 0.01f );
				var baseLine = Theme.Text.WithAlpha( 0.1f );
				var iconColor = Theme.Text.WithAlpha( 0.1f );

				if ( _isValidDropHover )
				{
					baseFill = Theme.Green.WithAlpha( 0.05f );
					baseLine = Theme.Green.WithAlpha( 0.8f );
					iconColor = Theme.Green;
				}
				else if ( Paint.HasMouseOver )
				{
					baseFill = Theme.Text.WithAlpha( 0.04f );
					baseLine = Theme.Text.WithAlpha( 0.2f );
					iconColor = Theme.Text.WithAlpha( 0.2f );
				}

				Paint.SetBrushAndPen( baseFill, baseLine, style: _isValidDropHover ? PenStyle.Solid : PenStyle.Dot );
				Paint.DrawRect( controlRect, 4 );

				Paint.SetPen( iconColor );
				Paint.DrawIcon( LocalRect.Shrink( 2 ), "add", 16 );
			}
		}
	}
}
