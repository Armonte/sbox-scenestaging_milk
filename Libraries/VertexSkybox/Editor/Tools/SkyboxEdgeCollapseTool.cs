using Sandbox;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Editor;

/// <summary>
/// Edge collapse tool: click an edge to collapse it into a single vertex.
/// The new vertex is placed at the cursor position with color blended
/// from the two endpoints (weighted by distance).
/// </summary>
[Title( "Edge Collapse" )]
[Icon( "compress" )]
[Group( "3" )]
[Order( 8 )]
public class SkyboxEdgeCollapseTool : SkyboxSubTool
{
	private int _hoveredEdge = -1;

	public SkyboxEdgeCollapseTool( SkyboxEditorToolEntry parent ) : base( parent ) { }

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
			CollapseEdge( _hoveredEdge );
	}

	private void CollapseEdge( int edgeIndex )
	{
		var edge = Data.Edges[edgeIndex];
		int vA = edge.V1, vB = edge.V2;
		if ( vA >= Data.Vertices.Count || vB >= Data.Vertices.Count ) return;

		Target.SaveState();
		using ( SceneEditorSession.Active
			.UndoScope( "Skybox Edge Collapse" )
			.WithComponentChanges( Target )
			.Push() )
		{
			var vertA = Data.Vertices[vA];
			var vertB = Data.Vertices[vB];

			// Blend color weighted by distance to cursor
			var cursor = Session.CursorPosition;
			var posA = SkyboxEditorGizmos.GetRenderedPos( vertA );
			var posB = SkyboxEditorGizmos.GetRenderedPos( vertB );
			float dA = posA.Distance( cursor );
			float dB = posB.Distance( cursor );
			float total = dA + dB;
			float wA = total > 0.001f ? 1f - (dA / total) : 0.5f;
			float wB = 1f - wA;

			var blendedColor = new Color32(
				(byte)((vertA.Color.r * wA + vertB.Color.r * wB).Clamp( 0f, 255f )),
				(byte)((vertA.Color.g * wA + vertB.Color.g * wB).Clamp( 0f, 255f )),
				(byte)((vertA.Color.b * wA + vertB.Color.b * wB).Clamp( 0f, 255f )),
				255
			);

			// Place new vertex at cursor position on sphere
			float sphereRadius = Data.SphereRadius > 0 ? Data.SphereRadius : 100f;
			var newPos = new Vector3( -cursor.x, cursor.y, cursor.z ); // Undo render X-negate
			newPos = SphereConstraint.ProjectOntoSphere( newPos, sphereRadius );

			// Keep vA, remove vB. Move vA to new position.
			var newVert = vertA;
			newVert.Position = newPos;
			newVert.Color = blendedColor;
			Data.Vertices[vA] = newVert;

			// Remap all references from vB → vA
			for ( int i = 0; i < Data.Edges.Count; i++ )
			{
				var e = Data.Edges[i];
				if ( e.V1 == vB ) e.V1 = vA;
				if ( e.V2 == vB ) e.V2 = vA;
				Data.Edges[i] = e;
			}

			for ( int i = 0; i < Data.Triangles.Count; i++ )
			{
				var tri = Data.Triangles[i];
				if ( tri.V0 == vB ) tri.V0 = vA;
				if ( tri.V1 == vB ) tri.V1 = vA;
				if ( tri.V2 == vB ) tri.V2 = vA;
				Data.Triangles[i] = tri;
			}

			// Remove degenerate edges (self-loops: V1==V2)
			for ( int i = Data.Edges.Count - 1; i >= 0; i-- )
			{
				if ( Data.Edges[i].V1 == Data.Edges[i].V2 )
					Data.Edges.RemoveAt( i );
			}

			// Remove degenerate triangles (any two verts the same)
			for ( int i = Data.Triangles.Count - 1; i >= 0; i-- )
			{
				var tri = Data.Triangles[i];
				if ( tri.V0 == tri.V1 || tri.V1 == tri.V2 || tri.V0 == tri.V2 )
					Data.Triangles.RemoveAt( i );
			}

			// Remove vB and remap all indices above it
			Data.Vertices.RemoveAt( vB );
			RemapVertexIndices( vB );

			Data.InvalidateAdjacency();
			Target.SaveState();
		}

		Target.RebuildMesh();
	}

	private void RemapVertexIndices( int removedIndex )
	{
		for ( int i = 0; i < Data.Edges.Count; i++ )
		{
			var e = Data.Edges[i];
			if ( e.V1 > removedIndex ) e.V1--;
			if ( e.V2 > removedIndex ) e.V2--;
			Data.Edges[i] = e;
		}

		for ( int i = 0; i < Data.Triangles.Count; i++ )
		{
			var tri = Data.Triangles[i];
			if ( tri.V0 > removedIndex ) tri.V0--;
			if ( tri.V1 > removedIndex ) tri.V1--;
			if ( tri.V2 > removedIndex ) tri.V2--;
			Data.Triangles[i] = tri;
		}
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

		using ( Gizmo.Scope( "edge_collapse_highlight" ) )
		{
			Gizmo.Draw.IgnoreDepth = true;
			Gizmo.Draw.Color = Color.Red;
			Gizmo.Draw.LineThickness = 3f;
			Gizmo.Draw.Line( wp1, wp2 );
		}
	}

	private static float DistanceToSegment( Vector3 point, Vector3 a, Vector3 b )
	{
		var ab = b - a;
		float lenSq = ab.LengthSquared;
		if ( lenSq < 0.0001f ) return point.Distance( a );

		float t = Vector3.Dot( point - a, ab ) / lenSq;
		t = t.Clamp( 0f, 1f );
		return point.Distance( a + ab * t );
	}
}
