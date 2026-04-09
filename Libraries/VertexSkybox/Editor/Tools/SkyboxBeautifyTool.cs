using Sandbox;
using System;
using System.Collections.Generic;

namespace Editor;

/// <summary>
/// Beautify tool: flips edges in the brush area to reduce sharp angles.
/// Uses Delaunay criterion — if an edge's opposite vertex is inside
/// the circumcircle, flip it.
/// </summary>
[Title( "Beautify" )]
[Icon( "auto_awesome" )]
[Group( "3" )]
[Order( 11 )]
public class SkyboxBeautifyTool : SkyboxSubTool
{
	private IDisposable _undoScope;
	private bool _beautifying;

	public SkyboxBeautifyTool( SkyboxEditorToolEntry parent ) : base( parent ) { }

	public override void OnEnabled() { }

	public override void OnDisabled()
	{
		EndBeautify();
	}

	public override void OnUpdate()
	{
		if ( Target == null || Data == null ) return;

		UpdateCursor();
		DrawOverlay();
		ConsumeMouseInput();

		if ( !Session.CursorOnSphere ) return;

		if ( Gizmo.WasLeftMousePressed )
			BeginBeautify();

		if ( Gizmo.IsLeftMouseDown && _beautifying && HasCursorMoved() )
			BeautifyAtCursor();

		if ( Gizmo.WasLeftMouseReleased && _beautifying )
			EndBeautify();
	}

	private void BeginBeautify()
	{
		_beautifying = true;

		Target?.SaveState();
		_undoScope = SceneEditorSession.Active
			.UndoScope( "Skybox Beautify" )
			.WithComponentChanges( Target )
			.Push();
	}

	private void BeautifyAtCursor()
	{
		var vertsInBrush = GetVerticesInBrush( Session.BrushRadius );
		if ( vertsInBrush.Count == 0 ) return;

		var vertSet = new HashSet<int>();
		foreach ( var (index, _) in vertsInBrush )
			vertSet.Add( index );

		// Find edges within the brush area
		var edgesToCheck = new HashSet<int>();
		foreach ( var vi in vertSet )
		{
			var edgesForVert = Data.GetEdgesForVertex( vi );
			foreach ( var ei in edgesForVert )
				edgesToCheck.Add( ei );
		}

		bool anyFlipped = false;

		foreach ( var edgeIdx in edgesToCheck )
		{
			if ( edgeIdx >= Data.Edges.Count ) continue;

			if ( ShouldFlip( edgeIdx ) )
			{
				FlipEdge( edgeIdx );
				anyFlipped = true;
			}
		}

		if ( anyFlipped )
		{
			Data.InvalidateAdjacency();
			Target.RebuildMesh();
		}
	}

	/// <summary>
	/// Check Delaunay criterion: should this edge be flipped?
	/// An edge should be flipped if the opposite vertex of either adjacent
	/// triangle lies inside the circumcircle of the other triangle.
	/// </summary>
	private bool ShouldFlip( int edgeIdx )
	{
		var edge = Data.Edges[edgeIdx];
		int a = edge.V1, b = edge.V2;

		// Find the two triangles sharing this edge
		var sharedTris = new List<int>();
		for ( int i = 0; i < Data.Triangles.Count; i++ )
		{
			var tri = Data.Triangles[i];
			if ( TriContainsEdge( tri, a, b ) )
				sharedTris.Add( i );
		}

		if ( sharedTris.Count != 2 ) return false;

		var tri0 = Data.Triangles[sharedTris[0]];
		var tri1 = Data.Triangles[sharedTris[1]];

		int c = GetOpposite( tri0, a, b );
		int d = GetOpposite( tri1, a, b );
		if ( c < 0 || d < 0 ) return false;

		// Use minimum angle criterion: flip if it improves the minimum angle
		var pA = SkyboxEditorGizmos.GetRenderedPos( Data.Vertices[a] );
		var pB = SkyboxEditorGizmos.GetRenderedPos( Data.Vertices[b] );
		var pC = SkyboxEditorGizmos.GetRenderedPos( Data.Vertices[c] );
		var pD = SkyboxEditorGizmos.GetRenderedPos( Data.Vertices[d] );

		float minAngleBefore = MathF.Min(
			MinAngleOfTriangle( pA, pB, pC ),
			MinAngleOfTriangle( pA, pB, pD )
		);

		float minAngleAfter = MathF.Min(
			MinAngleOfTriangle( pC, pD, pA ),
			MinAngleOfTriangle( pC, pD, pB )
		);

		return minAngleAfter > minAngleBefore;
	}

	private void FlipEdge( int edgeIdx )
	{
		var edge = Data.Edges[edgeIdx];
		int a = edge.V1, b = edge.V2;

		var sharedTris = new List<int>();
		for ( int i = 0; i < Data.Triangles.Count; i++ )
		{
			var tri = Data.Triangles[i];
			if ( TriContainsEdge( tri, a, b ) )
				sharedTris.Add( i );
		}

		if ( sharedTris.Count != 2 ) return;

		int c = GetOpposite( Data.Triangles[sharedTris[0]], a, b );
		int d = GetOpposite( Data.Triangles[sharedTris[1]], a, b );
		if ( c < 0 || d < 0 ) return;

		// Flip edge from (A,B) to (C,D)
		Data.Edges[edgeIdx] = new SkyboxEdge( c, d );

		int eCA = Data.FindEdge( c, a ); if ( eCA < 0 ) eCA = edgeIdx;
		int eDA = Data.FindEdge( d, a ); if ( eDA < 0 ) eDA = edgeIdx;
		int eCB = Data.FindEdge( c, b ); if ( eCB < 0 ) eCB = edgeIdx;
		int eDB = Data.FindEdge( d, b ); if ( eDB < 0 ) eDB = edgeIdx;

		Data.Triangles[sharedTris[0]] = new SkyboxTriangle( c, d, a, edgeIdx, eDA, eCA );
		Data.Triangles[sharedTris[1]] = new SkyboxTriangle( c, d, b, edgeIdx, eDB, eCB );
	}

	private static bool TriContainsEdge( SkyboxTriangle tri, int a, int b )
	{
		int[] v = { tri.V0, tri.V1, tri.V2 };
		return Array.IndexOf( v, a ) >= 0 && Array.IndexOf( v, b ) >= 0;
	}

	private static int GetOpposite( SkyboxTriangle tri, int a, int b )
	{
		if ( tri.V0 != a && tri.V0 != b ) return tri.V0;
		if ( tri.V1 != a && tri.V1 != b ) return tri.V1;
		if ( tri.V2 != a && tri.V2 != b ) return tri.V2;
		return -1;
	}

	private static float MinAngleOfTriangle( Vector3 a, Vector3 b, Vector3 c )
	{
		var ab = (b - a).Normal;
		var ac = (c - a).Normal;
		var ba = (a - b).Normal;
		var bc = (c - b).Normal;
		var ca = (a - c).Normal;
		var cb = (b - c).Normal;

		float angle0 = MathF.Acos( Vector3.Dot( ab, ac ).Clamp( -1f, 1f ) );
		float angle1 = MathF.Acos( Vector3.Dot( ba, bc ).Clamp( -1f, 1f ) );
		float angle2 = MathF.Acos( Vector3.Dot( ca, cb ).Clamp( -1f, 1f ) );

		return MathF.Min( angle0, MathF.Min( angle1, angle2 ) );
	}

	private void EndBeautify()
	{
		if ( !_beautifying ) return;
		_beautifying = false;

		Target?.SaveState();
		_undoScope?.Dispose();
		_undoScope = null;
	}
}
