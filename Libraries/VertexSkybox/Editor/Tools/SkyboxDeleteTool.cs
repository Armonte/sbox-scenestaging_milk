using Sandbox;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Editor;

/// <summary>
/// Delete tool: removes geometry touching the brush.
/// Default: deletes vertices + connected edges + triangles.
/// Shift held: deletes only edges/triangles, preserves vertices.
/// </summary>
[Title( "Delete" )]
[Icon( "delete" )]
[Group( "2" )]
[Order( 6 )]
public class SkyboxDeleteTool : SkyboxSubTool
{
	private IDisposable _undoScope;
	private bool _deleting;

	public SkyboxDeleteTool( SkyboxEditorToolEntry parent ) : base( parent ) { }

	public override void OnEnabled() { }

	public override void OnDisabled()
	{
		EndDelete();
	}

	public override void OnUpdate()
	{
		if ( Target == null || Data == null ) return;

		UpdateCursor();
		DrawOverlay();
		ConsumeMouseInput();

		if ( !Session.CursorOnSphere ) return;

		if ( Gizmo.WasLeftMousePressed )
			BeginDelete();

		if ( Gizmo.IsLeftMouseDown && _deleting && HasCursorMoved() )
			DeleteAtCursor( preserveVertices: Gizmo.IsShiftPressed );

		if ( Gizmo.WasLeftMouseReleased && _deleting )
			EndDelete();
	}

	private void BeginDelete()
	{
		_deleting = true;

		Target?.SaveState();
		_undoScope = SceneEditorSession.Active
			.UndoScope( "Skybox Delete" )
			.WithComponentChanges( Target )
			.Push();
	}

	private void DeleteAtCursor( bool preserveVertices )
	{
		var verts = GetVerticesInBrush( Session.BrushRadius );
		if ( verts.Count == 0 ) return;

		var vertIndices = new HashSet<int>( verts.Select( v => v.Index ) );

		// Collect triangles that reference any of these vertices
		var trisToRemove = new HashSet<int>();
		for ( int i = 0; i < Data.Triangles.Count; i++ )
		{
			var tri = Data.Triangles[i];
			if ( vertIndices.Contains( tri.V0 ) || vertIndices.Contains( tri.V1 ) || vertIndices.Contains( tri.V2 ) )
				trisToRemove.Add( i );
		}

		// Collect edges that reference any of these vertices
		var edgesToRemove = new HashSet<int>();
		for ( int i = 0; i < Data.Edges.Count; i++ )
		{
			var edge = Data.Edges[i];
			if ( vertIndices.Contains( edge.V1 ) || vertIndices.Contains( edge.V2 ) )
				edgesToRemove.Add( i );
		}

		if ( trisToRemove.Count == 0 && edgesToRemove.Count == 0 && (preserveVertices || vertIndices.Count == 0) )
			return;

		if ( preserveVertices )
		{
			// Only remove triangles and edges, keep vertices
			RemoveTrianglesAndEdges( trisToRemove, edgesToRemove );
		}
		else
		{
			// Remove everything: vertices, edges, triangles
			// Must remove in reverse order and remap indices
			RemoveAll( vertIndices, edgesToRemove, trisToRemove );
		}

		Data.InvalidateAdjacency();
		Session.SelectedVertices.Clear();
		Target.RebuildMesh();
	}

	private void RemoveTrianglesAndEdges( HashSet<int> trisToRemove, HashSet<int> edgesToRemove )
	{
		// Build edge index remap
		var edgeRemap = BuildRemovalRemap( Data.Edges.Count, edgesToRemove );

		// Remove triangles (reverse order to preserve indices)
		foreach ( var i in trisToRemove.OrderByDescending( x => x ) )
			Data.Triangles.RemoveAt( i );

		// Remove edges
		foreach ( var i in edgesToRemove.OrderByDescending( x => x ) )
			Data.Edges.RemoveAt( i );

		// Remap edge indices in remaining triangles
		RemapTriangleEdges( edgeRemap );
	}

	private void RemoveAll( HashSet<int> vertIndices, HashSet<int> edgesToRemove, HashSet<int> trisToRemove )
	{
		// Build remaps
		var vertRemap = BuildRemovalRemap( Data.Vertices.Count, vertIndices );
		var edgeRemap = BuildRemovalRemap( Data.Edges.Count, edgesToRemove );

		// Remove triangles
		foreach ( var i in trisToRemove.OrderByDescending( x => x ) )
			Data.Triangles.RemoveAt( i );

		// Remove edges
		foreach ( var i in edgesToRemove.OrderByDescending( x => x ) )
			Data.Edges.RemoveAt( i );

		// Remove vertices
		foreach ( var i in vertIndices.OrderByDescending( x => x ) )
			Data.Vertices.RemoveAt( i );

		// Remap all indices
		RemapAllIndices( vertRemap, edgeRemap );
	}

	/// <summary>
	/// Build a remap table: old index → new index (-1 if removed).
	/// </summary>
	private static int[] BuildRemovalRemap( int count, HashSet<int> removed )
	{
		var remap = new int[count];
		int newIdx = 0;
		for ( int i = 0; i < count; i++ )
		{
			if ( removed.Contains( i ) )
				remap[i] = -1;
			else
				remap[i] = newIdx++;
		}
		return remap;
	}

	private void RemapTriangleEdges( int[] edgeRemap )
	{
		for ( int i = 0; i < Data.Triangles.Count; i++ )
		{
			var tri = Data.Triangles[i];
			tri.E0 = tri.E0 >= 0 && tri.E0 < edgeRemap.Length ? edgeRemap[tri.E0] : -1;
			tri.E1 = tri.E1 >= 0 && tri.E1 < edgeRemap.Length ? edgeRemap[tri.E1] : -1;
			tri.E2 = tri.E2 >= 0 && tri.E2 < edgeRemap.Length ? edgeRemap[tri.E2] : -1;
			Data.Triangles[i] = tri;
		}
	}

	private void RemapAllIndices( int[] vertRemap, int[] edgeRemap )
	{
		// Remap edges
		for ( int i = 0; i < Data.Edges.Count; i++ )
		{
			var edge = Data.Edges[i];
			edge.V1 = edge.V1 < vertRemap.Length ? vertRemap[edge.V1] : -1;
			edge.V2 = edge.V2 < vertRemap.Length ? vertRemap[edge.V2] : -1;
			Data.Edges[i] = edge;
		}

		// Remap triangles
		for ( int i = 0; i < Data.Triangles.Count; i++ )
		{
			var tri = Data.Triangles[i];
			tri.V0 = tri.V0 < vertRemap.Length ? vertRemap[tri.V0] : -1;
			tri.V1 = tri.V1 < vertRemap.Length ? vertRemap[tri.V1] : -1;
			tri.V2 = tri.V2 < vertRemap.Length ? vertRemap[tri.V2] : -1;
			tri.E0 = tri.E0 >= 0 && tri.E0 < edgeRemap.Length ? edgeRemap[tri.E0] : -1;
			tri.E1 = tri.E1 >= 0 && tri.E1 < edgeRemap.Length ? edgeRemap[tri.E1] : -1;
			tri.E2 = tri.E2 >= 0 && tri.E2 < edgeRemap.Length ? edgeRemap[tri.E2] : -1;
			Data.Triangles[i] = tri;
		}
	}

	private void EndDelete()
	{
		if ( !_deleting ) return;
		_deleting = false;

		Target?.SaveState();
		_undoScope?.Dispose();
		_undoScope = null;
	}
}
