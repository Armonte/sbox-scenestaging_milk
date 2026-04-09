using Sandbox;
using System;
using System.Collections.Generic;

namespace Editor;

/// <summary>
/// Triangle fill tool: click 3 vertices to create a triangle between them.
/// After the first triangle, subsequent clicks chain: the last 2 vertices
/// are reused, forming triangle strips.
/// </summary>
[Title( "Triangle Fill" )]
[Icon( "change_history" )]
[Group( "3" )]
[Order( 9 )]
public class SkyboxTriFillTool : SkyboxSubTool
{
	private List<int> _pickedVerts = new();

	public SkyboxTriFillTool( SkyboxEditorToolEntry parent ) : base( parent ) { }

	public override void OnEnabled()
	{
		_pickedVerts.Clear();
	}

	public override void OnDisabled()
	{
		_pickedVerts.Clear();
	}

	public override void OnUpdate()
	{
		if ( Target == null || Data == null ) return;

		UpdateCursor();
		DrawOverlay();
		DrawPickedPreview();
		ConsumeMouseInput();

		if ( !Session.CursorOnSphere ) return;

		if ( Gizmo.WasLeftMousePressed )
			PickVertex();

		// Right-click to reset chain
		if ( Gizmo.WasRightMousePressed )
			_pickedVerts.Clear();
	}

	private void PickVertex()
	{
		int hovered = Session.HoveredVertex;
		if ( hovered < 0 || hovered >= Data.Vertices.Count ) return;

		// Don't add the same vertex twice in a row
		if ( _pickedVerts.Count > 0 && _pickedVerts[_pickedVerts.Count - 1] == hovered )
			return;

		_pickedVerts.Add( hovered );

		if ( _pickedVerts.Count >= 3 )
		{
			int v0 = _pickedVerts[_pickedVerts.Count - 3];
			int v1 = _pickedVerts[_pickedVerts.Count - 2];
			int v2 = _pickedVerts[_pickedVerts.Count - 1];

			CreateTriangle( v0, v1, v2 );

			// Keep last 2 for chaining
			_pickedVerts.Clear();
			_pickedVerts.Add( v1 );
			_pickedVerts.Add( v2 );
		}
	}

	private void CreateTriangle( int v0, int v1, int v2 )
	{
		Target?.SaveState();
		using ( SceneEditorSession.Active
			.UndoScope( "Skybox Triangle Fill" )
			.WithComponentChanges( Target )
			.Push() )
		{
			// Create or find edges
			int e0 = GetOrCreateEdge( v0, v1 );
			int e1 = GetOrCreateEdge( v1, v2 );
			int e2 = GetOrCreateEdge( v0, v2 );

			Data.Triangles.Add( new SkyboxTriangle( v0, v1, v2, e0, e1, e2 ) );
			Data.InvalidateAdjacency();

			Target?.SaveState();
		}

		Target.RebuildMesh();
	}

	private int GetOrCreateEdge( int a, int b )
	{
		int existing = Data.FindEdge( a, b );
		if ( existing >= 0 ) return existing;

		int idx = Data.Edges.Count;
		Data.Edges.Add( new SkyboxEdge( a, b ) );
		Data.InvalidateAdjacency();
		return idx;
	}

	private void DrawPickedPreview()
	{
		if ( _pickedVerts.Count == 0 ) return;

		using ( Gizmo.Scope( "trifill_preview" ) )
		{
			Gizmo.Draw.IgnoreDepth = true;
			Gizmo.Draw.LineThickness = 2f;

			// Draw picked vertices as green
			Gizmo.Draw.Color = Color.Green;
			foreach ( var idx in _pickedVerts )
			{
				if ( idx < 0 || idx >= Data.Vertices.Count ) continue;
				var wp = SkyboxEditorGizmos.ToWorld(
					SkyboxEditorGizmos.GetRenderedPos( Data.Vertices[idx] ), Target );
				Gizmo.Draw.Sprite( wp, 10f, null, false );
			}

			// Draw lines between picked vertices
			Gizmo.Draw.Color = Color.Green.WithAlpha( 0.6f );
			for ( int i = 1; i < _pickedVerts.Count; i++ )
			{
				int a = _pickedVerts[i - 1], b = _pickedVerts[i];
				if ( a >= Data.Vertices.Count || b >= Data.Vertices.Count ) continue;

				var wpA = SkyboxEditorGizmos.ToWorld(
					SkyboxEditorGizmos.GetRenderedPos( Data.Vertices[a] ), Target );
				var wpB = SkyboxEditorGizmos.ToWorld(
					SkyboxEditorGizmos.GetRenderedPos( Data.Vertices[b] ), Target );
				Gizmo.Draw.Line( wpA, wpB );
			}

			// Preview line to hovered vertex
			if ( Session.HoveredVertex >= 0 && Session.HoveredVertex < Data.Vertices.Count && _pickedVerts.Count > 0 )
			{
				Gizmo.Draw.Color = Color.Green.WithAlpha( 0.3f );
				int last = _pickedVerts[_pickedVerts.Count - 1];
				if ( last < Data.Vertices.Count )
				{
					var wpLast = SkyboxEditorGizmos.ToWorld(
						SkyboxEditorGizmos.GetRenderedPos( Data.Vertices[last] ), Target );
					var wpHover = SkyboxEditorGizmos.ToWorld(
						SkyboxEditorGizmos.GetRenderedPos( Data.Vertices[Session.HoveredVertex] ), Target );
					Gizmo.Draw.Line( wpLast, wpHover );
				}
			}
		}
	}
}
