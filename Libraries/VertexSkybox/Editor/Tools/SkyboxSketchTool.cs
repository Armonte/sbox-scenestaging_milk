using Sandbox;
using System;
using System.Collections.Generic;

namespace Editor;

/// <summary>
/// Sketch Pencil: draw reference lines on the sphere.
/// Left-click to draw, right-click to erase nearby sketch points.
/// Sketch data is stored in SkyboxData.SketchPoints and persists with .skye save.
/// </summary>
[Title( "Sketch" )]
[Icon( "edit" )]
[Group( "1" )]
[Order( 2 )]
public class SkyboxSketchTool : SkyboxSubTool
{
	private IDisposable _undoScope;
	private bool _drawing;
	private bool _erasing;

	public SkyboxSketchTool( SkyboxEditorToolEntry parent ) : base( parent ) { }

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
		DrawSketchLines();
		ConsumeMouseInput();

		if ( !Session.CursorOnSphere ) return;

		// Left click = draw
		if ( Gizmo.WasLeftMousePressed )
			BeginDraw();

		if ( Gizmo.IsLeftMouseDown && _drawing && HasCursorMoved() )
			AddSketchPoint();

		if ( Gizmo.WasLeftMouseReleased && _drawing )
			EndStroke();

		// Right click = erase
		if ( Gizmo.WasRightMousePressed )
			BeginErase();

		if ( Gizmo.IsRightMouseDown && _erasing )
			EraseNearCursor();

		if ( Gizmo.WasRightMouseReleased && _erasing )
			EndStroke();
	}

	private void BeginDraw()
	{
		_drawing = true;
		Target.SaveState();
		_undoScope = SceneEditorSession.Active
			.UndoScope( "Skybox Sketch Draw" )
			.WithComponentChanges( Target )
			.Push();

		// Add a NaN separator to start a new line segment
		Data.SketchPoints.Add( float.NaN );
		Data.SketchPoints.Add( float.NaN );
		Data.SketchPoints.Add( float.NaN );

		AddSketchPoint();
	}

	private void BeginErase()
	{
		_erasing = true;
		Target.SaveState();
		_undoScope = SceneEditorSession.Active
			.UndoScope( "Skybox Sketch Erase" )
			.WithComponentChanges( Target )
			.Push();
	}

	private void AddSketchPoint()
	{
		var pos = Session.CursorPosition;
		// Store in rendered space (X negated back to data space)
		Data.SketchPoints.Add( -pos.x );
		Data.SketchPoints.Add( pos.y );
		Data.SketchPoints.Add( pos.z );
	}

	private void EraseNearCursor()
	{
		var cursor = Session.CursorPosition;
		float radiusSq = Session.BrushRadius * Session.BrushRadius;

		// Walk through sketch points and remove those near cursor
		for ( int i = Data.SketchPoints.Count - 3; i >= 0; i -= 3 )
		{
			if ( i + 2 >= Data.SketchPoints.Count ) continue;

			float x = Data.SketchPoints[i];
			float y = Data.SketchPoints[i + 1];
			float z = Data.SketchPoints[i + 2];

			if ( float.IsNaN( x ) ) continue; // Skip separators

			// Compare in rendered space
			var sketchRendered = new Vector3( -x, y, z );
			if ( sketchRendered.DistanceSquared( cursor ) < radiusSq )
			{
				Data.SketchPoints.RemoveRange( i, 3 );
			}
		}
	}

	private void EndStroke()
	{
		_drawing = false;
		_erasing = false;
		Target.SaveState();
		_undoScope?.Dispose();
		_undoScope = null;
	}

	private void DrawSketchLines()
	{
		if ( Data.SketchPoints.Count < 6 ) return;

		using ( Gizmo.Scope( "sketch_lines" ) )
		{
			Gizmo.Draw.IgnoreDepth = true;
			Gizmo.Draw.Color = Color.White.WithAlpha( 0.7f );
			Gizmo.Draw.LineThickness = 2f;

			Vector3? lastPoint = null;

			for ( int i = 0; i + 2 < Data.SketchPoints.Count; i += 3 )
			{
				float x = Data.SketchPoints[i];
				float y = Data.SketchPoints[i + 1];
				float z = Data.SketchPoints[i + 2];

				if ( float.IsNaN( x ) )
				{
					lastPoint = null;
					continue;
				}

				var rendered = new Vector3( -x, y, z );
				var wp = SkyboxEditorGizmos.ToWorld( rendered, Target );

				if ( lastPoint.HasValue )
					Gizmo.Draw.Line( lastPoint.Value, wp );

				lastPoint = wp;
			}
		}
	}
}
