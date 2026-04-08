using Sandbox;
using System;

namespace Editor;

/// <summary>
/// Gizmo drawing and cursor logic for the skybox editor.
/// Follows the same patterns as VertexPaintTool and ClutterTool.
/// </summary>
public static class SkyboxEditorGizmos
{
	public static float DrawRadius { get; set; } = 15f;

	/// <summary>
	/// Get the rendered position of a vertex (matching ModelBuilder: negated X).
	/// </summary>
	public static Vector3 GetRenderedPos( SkyboxVertex v )
	{
		var rp = v.RenderPosition;
		return new Vector3( -rp.x, rp.y, rp.z );
	}

	/// <summary>
	/// Convert local rendered position to world space.
	/// </summary>
	public static Vector3 ToWorld( Vector3 local, SkyboxComponent target )
	{
		return target.WorldPosition + target.WorldRotation * (local * target.SkyboxScale);
	}

	public static void UpdateCursor( SkyboxEditorSession session )
	{
		var data = session.Target?.Data;
		if ( data == null ) return;

		var ray = Gizmo.CurrentRay;
		var worldPos = session.Target.WorldPosition;
		float scale = session.Target.SkyboxScale;
		float radius = (data.SphereRadius > 0 ? data.SphereRadius : 100f) * scale;

		if ( SphereConstraint.RaySphereIntersect( ray, worldPos, radius, out var hitPoint ) )
		{
			session.CursorOnSphere = true;
			session.CursorWorldPosition = hitPoint;

			var localHit = (hitPoint - worldPos) / scale;
			session.CursorPosition = localHit;
			session.HoveredVertex = FindNearestVertex( data, localHit );
		}
		else
		{
			session.CursorOnSphere = false;
			session.HoveredVertex = -1;
		}
	}

	private static int FindNearestVertex( SkyboxData data, Vector3 localPoint )
	{
		if ( data.Vertices.Count == 0 ) return -1;

		int nearest = 0;
		float nearestDist = GetRenderedPos( data.Vertices[0] ).DistanceSquared( localPoint );

		for ( int i = 1; i < data.Vertices.Count; i++ )
		{
			float dist = GetRenderedPos( data.Vertices[i] ).DistanceSquared( localPoint );
			if ( dist < nearestDist )
			{
				nearestDist = dist;
				nearest = i;
			}
		}

		return nearest;
	}

	/// <summary>
	/// Draw overlays. Follows the VertexPaintTool pattern:
	/// - Single Gizmo.Scope
	/// - IgnoreDepth = true
	/// - Sprite for vertices (screen-space size)
	/// - Only draw near cursor
	/// </summary>
	public static void DrawOverlay( SkyboxEditorSession session, bool showEdges, bool showVertices )
	{
		var target = session?.Target;
		if ( target == null ) return;

		var data = target.Data;
		if ( data == null || data.Vertices.Count == 0 ) return;

		if ( !session.CursorOnSphere ) return;
		if ( session.HoveredVertex < 0 ) return;

		var cursorLocal = session.CursorPosition;
		float drawRadSq = DrawRadius * DrawRadius;

		using ( Gizmo.Scope( "skybox_overlay" ) )
		{
			Gizmo.Draw.IgnoreDepth = true;

			// Cursor — draw at the world hit position
			var cursorWorld = session.CursorWorldPosition;
			var normal = (cursorWorld - target.WorldPosition).Normal;

			// Dot at cursor
			Gizmo.Draw.Color = Color.White;
			Gizmo.Draw.LineSphere( cursorWorld, 3f * target.SkyboxScale, 4 );

			// Brush circle
			using ( Gizmo.Scope( "cursor" ) )
			{
				Gizmo.Transform = new Transform( cursorWorld, Rotation.LookAt( normal ) );
				Gizmo.Draw.Color = Color.White.WithAlpha( 0.6f );
				Gizmo.Draw.LineThickness = 2f;
				Gizmo.Draw.LineCircle( 0, session.BrushRadius * target.SkyboxScale );
			}

			// Vertices near cursor
			if ( showVertices )
			{
				int drawn = 0;
				for ( int i = 0; i < data.Vertices.Count && drawn < 200; i++ )
				{
					var localPos = GetRenderedPos( data.Vertices[i] );
					if ( localPos.DistanceSquared( cursorLocal ) > drawRadSq )
						continue;

					var wp = ToWorld( localPos, target );

					if ( i == session.HoveredVertex )
					{
						Gizmo.Draw.Color = Color.Yellow;
						Gizmo.Draw.Sprite( wp, 12f, null, false );
					}
					else
					{
						Gizmo.Draw.Color = data.Vertices[i].Color.ToColor();
						Gizmo.Draw.Sprite( wp, 6f, null, false );
					}

					drawn++;
				}
			}

			// Edges near cursor
			if ( showEdges )
			{
				Gizmo.Draw.Color = new Color( 0.7f, 0.7f, 0.7f, 0.5f );
				Gizmo.Draw.LineThickness = 1f;

				int drawn = 0;
				for ( int i = 0; i < data.Edges.Count && drawn < 200; i++ )
				{
					var edge = data.Edges[i];
					if ( edge.V1 >= data.Vertices.Count || edge.V2 >= data.Vertices.Count )
						continue;

					var lp1 = GetRenderedPos( data.Vertices[edge.V1] );
					var lp2 = GetRenderedPos( data.Vertices[edge.V2] );

					if ( lp1.DistanceSquared( cursorLocal ) > drawRadSq &&
						lp2.DistanceSquared( cursorLocal ) > drawRadSq )
						continue;

					Gizmo.Draw.Line( ToWorld( lp1, target ), ToWorld( lp2, target ) );
					drawn++;
				}
			}
		}
	}

	public static void HandleInput( SkyboxEditorSession session )
	{
		if ( !session.CursorOnSphere ) return;
		if ( session.Target?.Data == null ) return;

		if ( Gizmo.IsLeftMouseDown )
			PaintVerticesInBrush( session, session.LeftColor, session.LeftOpacity );

		if ( Gizmo.IsRightMouseDown )
			PaintVerticesInBrush( session, session.RightColor, session.RightOpacity );
	}

	private static void PaintVerticesInBrush( SkyboxEditorSession session, Color32 color, float opacity )
	{
		var data = session.Target.Data;
		var cursor = session.CursorPosition;
		float radiusSq = session.BrushRadius * session.BrushRadius;
		bool changed = false;

		for ( int i = 0; i < data.Vertices.Count; i++ )
		{
			var v = data.Vertices[i];
			float distSq = GetRenderedPos( v ).DistanceSquared( cursor );
			if ( distSq > radiusSq ) continue;

			float t = 1f - MathF.Sqrt( distSq ) / session.BrushRadius;
			t *= opacity;

			var oldColor = v.Color;
			v.Color = new Color32(
				(byte)(oldColor.r + (color.r - oldColor.r) * t),
				(byte)(oldColor.g + (color.g - oldColor.g) * t),
				(byte)(oldColor.b + (color.b - oldColor.b) * t),
				255
			);

			data.Vertices[i] = v;
			changed = true;
		}

		if ( changed )
			session.Target.RebuildMesh();
	}
}
