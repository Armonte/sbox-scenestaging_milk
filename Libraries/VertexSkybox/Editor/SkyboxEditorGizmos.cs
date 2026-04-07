using Sandbox;
using System;

namespace Editor;

/// <summary>
/// Static gizmo drawing and cursor logic for the skybox editor.
/// Separated so it can be called from the main editor tool entry point.
/// </summary>
public static class SkyboxEditorGizmos
{
	public static void UpdateCursor( SkyboxEditorSession session )
	{
		var data = session.Target?.Data;
		if ( data == null ) return;

		var scene = session.Target.Scene;
		if ( scene == null ) return;

		// Use Scene.Trace like the Clutter tool does — traces against actual rendered meshes
		var tr = scene.Trace.Ray( Gizmo.CurrentRay, 50000 )
			.UseRenderMeshes( true )
			.Run();

		if ( tr.Hit )
		{
			session.CursorOnSphere = true;
			session.CursorWorldPosition = tr.HitPosition;

			// Convert world hit to local render space for vertex lookup
			var worldPos = session.Target.WorldPosition;
			float scale = session.Target.SkyboxScale;
			var localHit = (tr.HitPosition - worldPos) / scale;

			session.CursorPosition = localHit;
			session.HoveredVertex = FindNearestVertexRenderSpace( data, localHit );
		}
		else
		{
			session.CursorOnSphere = false;
			session.HoveredVertex = -1;
		}
	}

	/// <summary>
	/// Find nearest vertex by comparing render positions.
	/// </summary>
	private static int FindNearestVertexRenderSpace( SkyboxData data, Vector3 renderPoint )
	{
		if ( data.Vertices.Count == 0 ) return -1;

		int nearest = 0;
		float nearestDist = data.Vertices[0].RenderPosition.DistanceSquared( renderPoint );

		for ( int i = 1; i < data.Vertices.Count; i++ )
		{
			float dist = data.Vertices[i].RenderPosition.DistanceSquared( renderPoint );
			if ( dist < nearestDist )
			{
				nearestDist = dist;
				nearest = i;
			}
		}

		return nearest;
	}

	public static void DrawOverlay( SkyboxEditorSession session )
	{
		var data = session.Target?.Data;
		if ( data == null || data.Vertices.Count == 0 ) return;

		var worldTransform = session.Target.WorldTransform;
		float scale = session.Target.SkyboxScale;

		using ( Gizmo.Scope( "skybox_editor", new Transform( worldTransform.Position, worldTransform.Rotation, scale ) ) )
		{
			if ( session.ShowEdges ) DrawEdges( data );
			if ( session.ShowVertices ) DrawVertices( data, session );
			if ( session.ShowSelection && session.SelectedVertices.Count > 0 ) DrawSelection( data, session );
			if ( session.CursorOnSphere ) DrawCursor( data, session );
		}
	}

	private static void DrawEdges( SkyboxData data )
	{
		Gizmo.Draw.Color = new Color( 0.4f, 0.4f, 0.4f, 0.5f );
		Gizmo.Draw.LineThickness = 1f;

		foreach ( var edge in data.Edges )
		{
			if ( edge.V1 >= data.Vertices.Count || edge.V2 >= data.Vertices.Count )
				continue;

			var p1 = data.Vertices[edge.V1].RenderPosition;
			var p2 = data.Vertices[edge.V2].RenderPosition;
			Gizmo.Draw.Line( p1, p2 );
		}
	}

	private static void DrawVertices( SkyboxData data, SkyboxEditorSession session )
	{
		float dotSize = 0.5f;

		for ( int i = 0; i < data.Vertices.Count; i++ )
		{
			var v = data.Vertices[i];
			var pos = v.RenderPosition;

			Gizmo.Draw.Color = v.Color.ToColor();

			if ( i == session.HoveredVertex )
			{
				Gizmo.Draw.Color = Color.Yellow;
				Gizmo.Draw.LineSphere( pos, dotSize * 2f, 4 );
			}
			else
			{
				Gizmo.Draw.LineSphere( pos, dotSize, 3 );
			}
		}
	}

	private static void DrawSelection( SkyboxData data, SkyboxEditorSession session )
	{
		Gizmo.Draw.Color = new Color( 1f, 0.5f, 0f, 0.9f );
		float dotSize = 0.8f;

		foreach ( int idx in session.SelectedVertices )
		{
			if ( idx >= data.Vertices.Count ) continue;
			var pos = data.Vertices[idx].RenderPosition;
			Gizmo.Draw.LineSphere( pos, dotSize, 4 );
		}
	}

	private static void DrawCursor( SkyboxData data, SkyboxEditorSession session )
	{
		// Draw cursor at the hovered vertex's render position — same path as the yellow dot
		// which is confirmed to be correct
		if ( session.HoveredVertex < 0 || session.HoveredVertex >= data.Vertices.Count )
			return;

		var pos = data.Vertices[session.HoveredVertex].RenderPosition;

		Gizmo.Draw.Color = Color.White;
		Gizmo.Draw.LineThickness = 1.5f;
		Gizmo.Draw.LineSphere( pos, 0.4f, 4 );

		if ( session.BrushRadius > 0.1f )
		{
			Gizmo.Draw.Color = new Color( 1f, 1f, 1f, 0.4f );
			var normal = pos.Normal;
			var up = MathF.Abs( Vector3.Dot( normal, Vector3.Up ) ) > 0.99f ? Vector3.Forward : Vector3.Up;
			var rot = Rotation.LookAt( normal, up );
			using ( Gizmo.Scope( "brush", new Transform( pos, rot ) ) )
			{
				Gizmo.Draw.LineCircle( Vector3.Zero, session.BrushRadius, 0, 360, 32 );
			}
		}
	}
}
