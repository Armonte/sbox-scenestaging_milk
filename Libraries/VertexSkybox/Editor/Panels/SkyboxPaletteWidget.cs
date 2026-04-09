using Sandbox;
using System;

namespace Editor;

/// <summary>
/// 40-slot color palette widget for the skybox editor sidebar.
/// Left-click picks a color for the left brush, right-click for the right brush.
/// Double-click opens a color picker to edit the slot.
/// Matches the original Vertex Skybox Editor palette behavior.
/// </summary>
public class SkyboxPaletteWidget : Widget
{
	private const int SlotCount = 40;
	private const int Columns = 10;
	private const int Rows = 4;
	private const float SlotSize = 28f;
	private const float SlotGap = 2f;

	private SkyboxEditorToolEntry _tool;
	private int _hoveredSlot = -1;
	private int _selectedSlot = -1;

	public SkyboxPaletteWidget( SkyboxEditorToolEntry tool, Widget parent = null ) : base( parent )
	{
		_tool = tool;

		float totalWidth = Columns * (SlotSize + SlotGap) - SlotGap;
		float totalHeight = Rows * (SlotSize + SlotGap) - SlotGap;

		MinimumWidth = totalWidth + 8;
		FixedHeight = totalHeight + 8;
	}

	protected override void OnPaint()
	{
		var data = _tool.Session?.Target?.Data;
		if ( data == null ) return;

		float x0 = 4f, y0 = 4f;

		for ( int i = 0; i < SlotCount; i++ )
		{
			int col = i % Columns;
			int row = i / Columns;
			float x = x0 + col * (SlotSize + SlotGap);
			float y = y0 + row * (SlotSize + SlotGap);

			var rect = new Rect( x, y, SlotSize, SlotSize );
			var color = data.Palette[i].ToColor();

			// Fill with palette color
			Paint.ClearPen();
			Paint.SetBrush( color );
			Paint.DrawRect( rect, 3 );

			// Selected slot highlight
			if ( i == _selectedSlot )
			{
				Paint.ClearBrush();
				Paint.SetPen( Color.White, 2f );
				Paint.DrawRect( rect.Shrink( -1 ), 3 );
			}
			// Hover highlight
			else if ( i == _hoveredSlot )
			{
				Paint.ClearBrush();
				Paint.SetPen( Color.White.WithAlpha( 0.5f ), 1f );
				Paint.DrawRect( rect.Shrink( -1 ), 3 );
			}

			// Show slot index for used slots (small text)
			if ( i < data.PaletteUsedCount )
			{
				Paint.SetPen( GetContrastColor( color ), 1f );
				Paint.ClearBrush();
				Paint.SetFont( "Poppins", 7f );
				Paint.DrawText( rect.Shrink( 2 ), $"{i}", TextFlag.RightBottom );
			}
		}
	}

	protected override void OnMouseMove( MouseEvent e )
	{
		_hoveredSlot = GetSlotAtPosition( e.LocalPosition );
		Update();
	}

	protected override void OnMousePress( MouseEvent e )
	{
		int slot = GetSlotAtPosition( e.LocalPosition );
		if ( slot < 0 || slot >= SlotCount ) return;

		var data = _tool.Session?.Target?.Data;
		if ( data == null ) return;

		var color = data.Palette[slot];
		_selectedSlot = slot;

		if ( e.ButtonState.HasFlag( MouseButtons.Left ) )
		{
			_tool.Session.LeftColor = color;
			_tool.LeftPaintColor = color.ToColor();
		}
		else if ( e.ButtonState.HasFlag( MouseButtons.Right ) )
		{
			_tool.Session.RightColor = color;
			_tool.RightPaintColor = color.ToColor();
		}

		Update();
	}

	protected override void OnDoubleClick( MouseEvent e )
	{
		int slot = GetSlotAtPosition( e.LocalPosition );
		if ( slot < 0 || slot >= SlotCount ) return;

		var data = _tool.Session?.Target?.Data;
		if ( data == null ) return;

		// Open color picker popup
		var currentColor = data.Palette[slot].ToColor();
		int capturedSlot = slot;
		ColorPicker.OpenColorPopup( currentColor, c =>
		{
			var d = _tool.Session?.Target?.Data;
			if ( d == null ) return;

			d.Palette[capturedSlot] = new Color32(
				(byte)(c.r * 255),
				(byte)(c.g * 255),
				(byte)(c.b * 255),
				255
			);

			if ( capturedSlot >= d.PaletteUsedCount )
				d.PaletteUsedCount = capturedSlot + 1;

			Update();
		}, ScreenRect.BottomLeft );
	}

	protected override void OnMouseLeave()
	{
		_hoveredSlot = -1;
		Update();
	}

	private int GetSlotAtPosition( Vector2 pos )
	{
		float x0 = 4f, y0 = 4f;
		int col = (int)((pos.x - x0) / (SlotSize + SlotGap));
		int row = (int)((pos.y - y0) / (SlotSize + SlotGap));

		if ( col < 0 || col >= Columns || row < 0 || row >= Rows )
			return -1;

		// Check if actually inside the slot rect (not in the gap)
		float slotX = x0 + col * (SlotSize + SlotGap);
		float slotY = y0 + row * (SlotSize + SlotGap);
		if ( pos.x < slotX || pos.x > slotX + SlotSize ) return -1;
		if ( pos.y < slotY || pos.y > slotY + SlotSize ) return -1;

		return row * Columns + col;
	}

	private static Color GetContrastColor( Color c )
	{
		float luma = c.r * 0.299f + c.g * 0.587f + c.b * 0.114f;
		return luma > 0.5f ? Color.Black.WithAlpha( 0.6f ) : Color.White.WithAlpha( 0.6f );
	}

	/// <summary>
	/// Generate palette by sampling representative colors from the sky's vertices.
	/// Uses simple evenly-spaced sampling across all vertices.
	/// </summary>
	public static void GeneratePaletteFromSky( SkyboxData data )
	{
		if ( data == null || data.Vertices.Count == 0 ) return;

		int sampleCount = Math.Min( SlotCount, data.Vertices.Count );
		float step = data.Vertices.Count / (float)sampleCount;

		for ( int i = 0; i < sampleCount; i++ )
		{
			int idx = (int)(i * step);
			if ( idx >= data.Vertices.Count ) idx = data.Vertices.Count - 1;
			data.Palette[i] = data.Vertices[idx].Color;
		}

		// Fill remaining with black
		for ( int i = sampleCount; i < SlotCount; i++ )
			data.Palette[i] = new Color32( 0, 0, 0, 255 );

		data.PaletteUsedCount = sampleCount;
	}
}
