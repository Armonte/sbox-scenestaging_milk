using Sandbox;
using System;
using System.Collections.Generic;

namespace Editor;

/// <summary>
/// Abstract base for all skybox editor sub-tools.
/// Provides convenience accessors to the shared session/target/data
/// and wraps common gizmo helpers.
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
}
