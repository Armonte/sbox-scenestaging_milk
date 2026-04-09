using Sandbox;
using System;
using System.Collections.Generic;

namespace Editor;

/// <summary>
/// Abstract base for all skybox editor sub-tools.
/// Provides convenience accessors to the shared session/target/data,
/// wraps common gizmo helpers, and includes professional brush patterns
/// from s&box terrain/displacement tools.
/// </summary>
public abstract class SkyboxSubTool : EditorTool
{
	protected SkyboxEditorToolEntry Parent { get; }

	protected SkyboxSubTool( SkyboxEditorToolEntry parent )
	{
		Parent = parent;
	}

	/// <summary>The shared editing session.</summary>
	protected SkyboxEditorSession Session => Parent.Session;

	/// <summary>The SkyboxComponent being edited.</summary>
	protected SkyboxComponent Target => Session?.Target;

	/// <summary>The skybox geometry data.</summary>
	protected SkyboxData Data => Target?.Data;

	/// <summary>
	/// Update cursor position via ray-sphere intersection.
	/// Call at the start of OnUpdate() in every sub-tool.
	/// </summary>
	protected void UpdateCursor()
	{
		SkyboxEditorGizmos.UpdateCursor( Session );
	}

	/// <summary>
	/// Draw the standard overlay (cursor dot, brush circle, nearby vertices/edges).
	/// </summary>
	protected void DrawOverlay()
	{
		SkyboxEditorGizmos.DrawOverlay( Session, Parent.ShowEdges, Parent.ShowVertices );
	}

	/// <summary>
	/// Find all vertex indices within the given brush radius around the cursor.
	/// Returns (vertexIndex, falloff) pairs where falloff is 0..1 (1 at center).
	/// </summary>
	protected List<(int Index, float Falloff)> GetVerticesInBrush( float brushRadius )
	{
		return SkyboxEditorGizmos.GetVerticesInBrush( Session, brushRadius );
	}

	/// <summary>
	/// Consume all mouse input so the scene doesn't process it.
	/// Call in OnUpdate() for tools that use mouse for editing.
	/// Pattern from MeshTool/VertexPaintTool.
	/// </summary>
	protected void ConsumeMouseInput()
	{
		Gizmo.Hitbox.BBox( BBox.FromPositionAndSize( Vector3.Zero, 999999 ) );
	}

	/// <summary>
	/// Check if cursor has moved since last frame.
	/// From BaseBrushTool: prevents over-accumulation when mouse is held still.
	/// Use: if ( _dragging &amp;&amp; !HasCursorMoved() ) return;
	/// </summary>
	protected static bool HasCursorMoved()
	{
		return !Application.CursorDelta.IsNearZeroLength;
	}

	/// <summary>
	/// Compute falloff with hardness support.
	/// From DisplacementTool: hardness controls inner hard region.
	/// t = 0..1 (0 at center, 1 at radius edge).
	/// hardness = 0..1 (0 = fully soft, 1 = fully hard).
	/// Returns 0..1 falloff value.
	/// </summary>
	protected static float ComputeHardnessFalloff( float t, float hardness = 0.5f )
	{
		if ( hardness >= 1f ) return 1f;
		if ( t <= hardness ) return 1f;
		return 1f - ((t - hardness) / (1f - hardness)).Clamp( 0f, 1f );
	}

	/// <summary>
	/// Get vertices in brush with hardness-aware falloff.
	/// Returns (index, falloff) where falloff uses the hardness curve.
	/// </summary>
	protected List<(int Index, float Falloff)> GetVerticesInBrushWithHardness( float brushRadius, float hardness )
	{
		var raw = SkyboxEditorGizmos.GetVerticesInBrush( Session, brushRadius );
		var result = new List<(int, float)>( raw.Count );

		foreach ( var (index, linearFalloff) in raw )
		{
			// linearFalloff is 1 at center, 0 at edge. Convert to t = distance ratio.
			float t = 1f - linearFalloff;
			float falloff = ComputeHardnessFalloff( t, hardness );
			result.Add( (index, falloff) );
		}

		return result;
	}
}
