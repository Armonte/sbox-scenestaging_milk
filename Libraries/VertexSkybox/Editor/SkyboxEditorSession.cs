using Sandbox;
using System;
using System.Collections.Generic;

namespace Editor;

/// <summary>
/// Holds the editing state for the skybox editor tool.
/// Persists across tool updates, tracks selection, cursor, display options.
/// </summary>
public class SkyboxEditorSession
{
	/// <summary>
	/// The SkyboxComponent being edited.
	/// </summary>
	public SkyboxComponent Target { get; private set; }

	// --- Cursor State ---

	/// <summary>
	/// Whether the cursor is currently intersecting the skybox sphere.
	/// </summary>
	public bool CursorOnSphere { get; set; }

	/// <summary>
	/// The cursor's position on the sphere surface (local render space).
	/// </summary>
	public Vector3 CursorPosition { get; set; }

	/// <summary>
	/// The cursor's position in world space (from Scene.Trace).
	/// </summary>
	public Vector3 CursorWorldPosition { get; set; }

	/// <summary>
	/// Index of the vertex nearest to the cursor, or -1.
	/// </summary>
	public int HoveredVertex { get; set; } = -1;

	// --- Selection ---

	/// <summary>
	/// Currently selected vertex indices.
	/// </summary>
	public HashSet<int> SelectedVertices { get; set; } = new();

	/// <summary>
	/// Current editing layer (0-5).
	/// </summary>
	public int CurrentLayer { get; set; } = 0;

	// --- Brush ---

	/// <summary>
	/// Brush radius for painting and geometry tools.
	/// </summary>
	public float BrushRadius { get; set; } = 5f;

	// --- Colors ---

	/// <summary>
	/// Left mouse button color.
	/// </summary>
	public Color32 LeftColor { get; set; } = new Color32( 255, 255, 255, 255 );

	/// <summary>
	/// Right mouse button color.
	/// </summary>
	public Color32 RightColor { get; set; } = new Color32( 0, 0, 0, 255 );

	/// <summary>
	/// Left button paint opacity (0-1).
	/// </summary>
	public float LeftOpacity { get; set; } = 1f;

	/// <summary>
	/// Right button paint opacity (0-1).
	/// </summary>
	public float RightOpacity { get; set; } = 1f;

	// --- Display ---

	public bool ShowEdges { get; set; } = false;
	public bool ShowVertices { get; set; } = false;
	public bool ShowSelection { get; set; } = true;
	public bool ShowTriangles { get; set; } = true;
	public bool ShowSketch { get; set; } = false;

	/// <summary>
	/// Set the target component.
	/// </summary>
	public void SetTarget( SkyboxComponent component )
	{
		Target = component;
		SelectedVertices.Clear();
		HoveredVertex = -1;
		CursorOnSphere = false;
	}
}
