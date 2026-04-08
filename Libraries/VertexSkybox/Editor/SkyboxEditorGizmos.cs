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
	/// <summary>
	/// Effective scale combining SkyboxScale and the GameObject's world scale.
	/// </summary>
	public static float GetEffectiveScale( SkyboxComponent target )
	{
		return target.SkyboxScale * target.WorldScale.x;
	}

	public static Vector3 ToWorld( Vector3 local, SkyboxComponent target )
	{
		float s = GetEffectiveScale( target );
		return target.WorldPosition + target.WorldRotation * (local * s);
	}

	public static void UpdateCursor( SkyboxEditorSession session )
	{
		var data = session.Target?.Data;
		if ( data == null ) return;

		var ray = Gizmo.CurrentRay;
		var worldPos = session.Target.WorldPosition;
		float eScale = GetEffectiveScale( session.Target );
		float radius = (data.SphereRadius > 0 ? data.SphereRadius : 100f) * eScale;

		if ( SphereConstraint.RaySphereIntersect( ray, worldPos, radius, out var hitPoint ) )
		{
			session.CursorOnSphere = true;
			session.CursorWorldPosition = hitPoint;

			var localHit = (hitPoint - worldPos) / eScale;
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
			Gizmo.Draw.LineSphere( cursorWorld, 1f * GetEffectiveScale( target ), 4 );

			// Brush circle
			using ( Gizmo.Scope( "cursor" ) )
			{
				Gizmo.Transform = new Transform( cursorWorld, Rotation.LookAt( normal ) );
				Gizmo.Draw.Color = Color.White.WithAlpha( 0.6f );
				Gizmo.Draw.LineThickness = 2f;
				Gizmo.Draw.LineCircle( 0, session.BrushRadius * GetEffectiveScale( target ) );
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

		// Ctrl + Left click = pick color (pipette)
		if ( Gizmo.IsCtrlPressed && Gizmo.WasLeftMousePressed )
		{
			if ( session.HoveredVertex >= 0 && session.HoveredVertex < session.Target.Data.Vertices.Count )
			{
				session.LeftColor = session.Target.Data.Vertices[session.HoveredVertex].Color;
			}
			return;
		}

		// Left click/drag = paint with left color
		if ( Gizmo.IsLeftMouseDown )
			PaintVerticesInBrush( session, session.LeftColor, session.LeftOpacity );

		// Shift + Left click/drag = paint with right color
		if ( Gizmo.IsShiftPressed && Gizmo.IsLeftMouseDown )
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

			// Lerp in float space to avoid byte overflow
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

			data.Vertices[i] = v;
			changed = true;
		}

		if ( changed )
		{
			Log.Info( $"[Paint] Painted vertices, brush={session.BrushRadius}, cursor=({cursor.x:F1},{cursor.y:F1},{cursor.z:F1})" );
			session.Target.RebuildMesh();
		}
	}
}
