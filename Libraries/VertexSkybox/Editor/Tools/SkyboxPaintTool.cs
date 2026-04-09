using Sandbox;
using System;

namespace Editor;

/// <summary>
/// Paint tool for the skybox editor.
/// Left-click paints with primary color, Shift+Left with secondary.
/// Ctrl+Left picks color (pipette). Supports undo via UndoScope.
/// </summary>
[Title( "Paint" )]
[Icon( "brush" )]
public class SkyboxPaintTool : SkyboxSubTool
{
	private IDisposable _undoScope;
	private bool _strokeActive;

	public SkyboxPaintTool( SkyboxEditorToolEntry parent ) : base( parent ) { }

	public override void OnEnabled() { }

	public override void OnDisabled()
	{
		EndStroke();
	}

	public override void OnUpdate()
	{
		if ( Target == null || Data == null ) return;

		UpdateCursor();
		DrawOverlay();

		if ( !Session.CursorOnSphere ) return;

		// Ctrl + Left click = pipette
		if ( Gizmo.IsCtrlPressed && Gizmo.WasLeftMousePressed )
		{
			PickColor();
			return;
		}

		// Begin stroke on first press
		if ( Gizmo.WasLeftMousePressed )
			BeginStroke();

		// Paint while held
		if ( Gizmo.IsLeftMouseDown && _strokeActive )
		{
			if ( Gizmo.IsShiftPressed )
				PaintWithColor( Session.RightColor, Session.RightOpacity );
			else
				PaintWithColor( Session.LeftColor, Session.LeftOpacity );
		}

		// End stroke on release
		if ( Gizmo.WasLeftMouseReleased && _strokeActive )
			EndStroke();
	}

	private void PickColor()
	{
		if ( Session.HoveredVertex < 0 || Session.HoveredVertex >= Data.Vertices.Count )
			return;

		var picked = Data.Vertices[Session.HoveredVertex].Color;
		Session.LeftColor = picked;
		Parent.LeftPaintColor = picked.ToColor();
	}

	private void BeginStroke()
	{
		_strokeActive = true;

		// Snapshot current state so undo can restore it
		Target.SaveState();

		_undoScope = SceneEditorSession.Active
			.UndoScope( "Skybox Paint Stroke" )
			.WithComponentChanges( Target )
			.Push();
	}

	private void EndStroke()
	{
		if ( !_strokeActive ) return;
		_strokeActive = false;

		// Capture post-stroke state
		Target.SaveState();

		_undoScope?.Dispose();
		_undoScope = null;
	}

	private void PaintWithColor( Color32 color, float opacity )
	{
		var verts = GetVerticesInBrush( Session.BrushRadius );
		if ( verts.Count == 0 ) return;

		bool changed = false;

		foreach ( var (index, falloff) in verts )
		{
			var v = Data.Vertices[index];
			float t = falloff * opacity;

			float or = v.Color.r / 255f;
			float og = v.Color.g / 255f;
			float ob = v.Color.b / 255f;
			float nr = color.r / 255f;
			float ng = color.g / 255f;
			float nb = color.b / 255f;

			v.Color = new Color32(
				(byte)(((or + (nr - or) * t).Clamp( 0f, 1f )) * 255),
				(byte)(((og + (ng - og) * t).Clamp( 0f, 1f )) * 255),
				(byte)(((ob + (nb - ob) * t).Clamp( 0f, 1f )) * 255),
				255
			);

			Data.Vertices[index] = v;
			changed = true;
		}

		if ( changed )
			Target.RebuildMesh();
	}
}
