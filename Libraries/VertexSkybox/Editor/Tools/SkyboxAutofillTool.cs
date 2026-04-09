using Sandbox;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Editor;

/// <summary>
/// Autofill tool: automatically triangulates vertices within the brush radius.
/// Uses a simple incremental approach — connects nearby vertices that don't
/// already share triangles.
/// </summary>
[Title( "Autofill" )]
[Icon( "auto_fix_high" )]
public class SkyboxAutofillTool : SkyboxSubTool
{
	private IDisposable _undoScope;
	private bool _filling;

	public SkyboxAutofillTool( SkyboxEditorToolEntry parent ) : base( parent ) { }

	public override void OnEnabled() { }

	public override void OnDisabled()
	{
		EndFill();
	}

	public override void OnUpdate()
	{
		if ( Target == null || Data == null ) return;

		UpdateCursor();
		DrawOverlay();
		ConsumeMouseInput();

		if ( !Session.CursorOnSphere ) return;

		if ( Gizmo.WasLeftMousePressed )
			BeginFill();

		if ( Gizmo.IsLeftMouseDown && _filling && HasCursorMoved() )
			AutofillAtCursor();

		if ( Gizmo.WasLeftMouseReleased && _filling )
			EndFill();
	}

	private void BeginFill()
	{
		_filling = true;

		Target.SaveState();
		_undoScope = SceneEditorSession.Active
			.UndoScope( "Skybox Autofill" )
			.WithComponentChanges( Target )
			.Push();
	}

	private void AutofillAtCursor()
	{
		var vertsInBrush = GetVerticesInBrush( Session.BrushRadius );
		if ( vertsInBrush.Count < 3 ) return;

		var indices = vertsInBrush.Select( v => v.Index ).ToList();
		bool changed = false;

		// Try all triplets of nearby vertices and create triangles where none exist
		for ( int i = 0; i < indices.Count; i++ )
		{
			for ( int j = i + 1; j < indices.Count; j++ )
			{
				for ( int k = j + 1; k < indices.Count; k++ )
				{
					int v0 = indices[i], v1 = indices[j], v2 = indices[k];

					// Skip if triangle already exists
					if ( TriangleExists( v0, v1, v2 ) ) continue;

					// Skip if any edge of this triangle would cross existing triangles badly
					// Simple heuristic: only create if the triangle is reasonably small
					var p0 = SkyboxEditorGizmos.GetRenderedPos( Data.Vertices[v0] );
					var p1 = SkyboxEditorGizmos.GetRenderedPos( Data.Vertices[v1] );
					var p2 = SkyboxEditorGizmos.GetRenderedPos( Data.Vertices[v2] );

					float maxEdgeLen = Session.BrushRadius * 1.5f;
					if ( p0.Distance( p1 ) > maxEdgeLen ||
						p1.Distance( p2 ) > maxEdgeLen ||
						p0.Distance( p2 ) > maxEdgeLen )
						continue;

					// Create the triangle
					int e0 = GetOrCreateEdge( v0, v1 );
					int e1 = GetOrCreateEdge( v1, v2 );
					int e2 = GetOrCreateEdge( v0, v2 );

					Data.Triangles.Add( new SkyboxTriangle( v0, v1, v2, e0, e1, e2 ) );
					changed = true;
				}
			}
		}

		if ( changed )
		{
			Data.InvalidateAdjacency();
			Target.RebuildMesh();
		}
	}

	private bool TriangleExists( int v0, int v1, int v2 )
	{
		var sorted = new[] { v0, v1, v2 };
		Array.Sort( sorted );

		for ( int i = 0; i < Data.Triangles.Count; i++ )
		{
			var tri = Data.Triangles[i];
			var triSorted = new[] { tri.V0, tri.V1, tri.V2 };
			Array.Sort( triSorted );

			if ( triSorted[0] == sorted[0] && triSorted[1] == sorted[1] && triSorted[2] == sorted[2] )
				return true;
		}

		return false;
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

	private void EndFill()
	{
		if ( !_filling ) return;
		_filling = false;

		Target.SaveState();
		_undoScope?.Dispose();
		_undoScope = null;
	}
}
