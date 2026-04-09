using Sandbox;
using System;
using System.Collections.Generic;

namespace Editor;

/// <summary>
/// Edge flip tool: click an edge shared by two triangles to flip its diagonal.
/// The edge connecting (A,B) in a quad (A,B,C,D) flips to (C,D).
/// </summary>
[Title( "Edge Flip" )]
[Icon( "swap_horiz" )]
[Group( "3" )]
[Order( 7 )]
public class SkyboxEdgeFlipTool : SkyboxSubTool
{
	private int _hoveredEdge = -1;

	public SkyboxEdgeFlipTool( SkyboxEditorToolEntry parent ) : base( parent ) { }

	public override void OnEnabled() { }
	public override void OnDisabled() { }

	public override void OnUpdate()
	{
		if ( Target == null || Data == null ) return;

		UpdateCursor();
		DrawOverlay();
		ConsumeMouseInput();

		if ( !Session.CursorOnSphere ) return;

		_hoveredEdge = FindNearestEdge();
		DrawEdgeHighlight();

		if ( Gizmo.WasLeftMousePressed && _hoveredEdge >= 0 )
			FlipEdge( _hoveredEdge );
	}

	private int FindNearestEdge()
	{
		var cursor = Session.CursorPosition;
		float bestDist = float.MaxValue;
		int bestEdge = -1;
		float threshold = Session.BrushRadius * 0.5f;

		for ( int i = 0; i < Data.Edges.Count; i++ )
		{
			var edge = Data.Edges[i];
			if ( edge.V1 >= Data.Vertices.Count || edge.V2 >= Data.Vertices.Count ) continue;

			var p1 = SkyboxEditorGizmos.GetRenderedPos( Data.Vertices[edge.V1] );
			var p2 = SkyboxEditorGizmos.GetRenderedPos( Data.Vertices[edge.V2] );

			float dist = DistanceToSegment( cursor, p1, p2 );
			if ( dist < bestDist && dist < threshold )
			{
				bestDist = dist;
				bestEdge = i;
			}
		}

		return bestEdge;
	}

	private void DrawEdgeHighlight()
	{
		if ( _hoveredEdge < 0 || _hoveredEdge >= Data.Edges.Count ) return;

		var edge = Data.Edges[_hoveredEdge];
		if ( edge.V1 >= Data.Vertices.Count || edge.V2 >= Data.Vertices.Count ) return;

		var wp1 = SkyboxEditorGizmos.ToWorld( SkyboxEditorGizmos.GetRenderedPos( Data.Vertices[edge.V1] ), Target );
		var wp2 = SkyboxEditorGizmos.ToWorld( SkyboxEditorGizmos.GetRenderedPos( Data.Vertices[edge.V2] ), Target );

		using ( Gizmo.Scope( "edge_highlight" ) )
		{
			Gizmo.Draw.IgnoreDepth = true;
			Gizmo.Draw.Color = Color.Cyan;
			Gizmo.Draw.LineThickness = 3f;
			Gizmo.Draw.Line( wp1, wp2 );
		}
	}

	private void FlipEdge( int edgeIndex )
	{
		var edge = Data.Edges[edgeIndex];
		int a = edge.V1, b = edge.V2;

		// Find the two triangles that share this edge
		var sharedTris = new List<int>();
		for ( int i = 0; i < Data.Triangles.Count; i++ )
		{
			var tri = Data.Triangles[i];
			if ( TriangleContainsEdge( tri, a, b ) )
				sharedTris.Add( i );
		}

		if ( sharedTris.Count != 2 ) return; // Can only flip shared edges

		var tri0 = Data.Triangles[sharedTris[0]];
		var tri1 = Data.Triangles[sharedTris[1]];

		// Find the opposite vertices (C and D)
		int c = GetOppositeVertex( tri0, a, b );
		int d = GetOppositeVertex( tri1, a, b );
		if ( c < 0 || d < 0 ) return;

		Target?.SaveState();
		using ( SceneEditorSession.Active
			.UndoScope( "Skybox Edge Flip" )
			.WithComponentChanges( Target )
			.Push() )
		{
			// Flip: edge (A,B) becomes (C,D)
			var newEdge = new SkyboxEdge( c, d );
			Data.Edges[edgeIndex] = newEdge;

			// Rebuild triangles: (A,B,C) + (A,B,D) → (C,D,A) + (C,D,B)
			int eCA = Data.FindEdge( c, a ); if ( eCA < 0 ) eCA = edgeIndex;
			int eDA = Data.FindEdge( d, a ); if ( eDA < 0 ) eDA = edgeIndex;
			int eCB = Data.FindEdge( c, b ); if ( eCB < 0 ) eCB = edgeIndex;
			int eDB = Data.FindEdge( d, b ); if ( eDB < 0 ) eDB = edgeIndex;

			Data.Triangles[sharedTris[0]] = new SkyboxTriangle( c, d, a, edgeIndex, eDA, eCA );
			Data.Triangles[sharedTris[1]] = new SkyboxTriangle( c, d, b, edgeIndex, eDB, eCB );

			Data.InvalidateAdjacency();
			Target?.SaveState();
		}

		Target.RebuildMesh();
	}

	private static bool TriangleContainsEdge( SkyboxTriangle tri, int a, int b )
	{
		int[] verts = { tri.V0, tri.V1, tri.V2 };
		return Array.IndexOf( verts, a ) >= 0 && Array.IndexOf( verts, b ) >= 0;
	}

	private static int GetOppositeVertex( SkyboxTriangle tri, int a, int b )
	{
		if ( tri.V0 != a && tri.V0 != b ) return tri.V0;
		if ( tri.V1 != a && tri.V1 != b ) return tri.V1;
		if ( tri.V2 != a && tri.V2 != b ) return tri.V2;
		return -1;
	}

	private static float DistanceToSegment( Vector3 point, Vector3 a, Vector3 b )
	{
		var ab = b - a;
		float lenSq = ab.LengthSquared;
		if ( lenSq < 0.0001f ) return point.Distance( a );

		float t = Vector3.Dot( point - a, ab ) / lenSq;
		t = t.Clamp( 0f, 1f );
		var closest = a + ab * t;
		return point.Distance( closest );
	}
}
