using Sandbox;
using System;
using System.Collections.Generic;

namespace Editor;

/// <summary>
/// Grab tool: drag vertices on the sphere surface with falloff.
/// Vertices in brush radius follow cursor with hardness-aware falloff,
/// re-projected onto sphere. Uses cursor-delta guard.
/// </summary>
[Title( "Grab" )]
[Icon( "open_with" )]
[Group( "2" )]
[Order( 3 )]
public class SkyboxGrabTool : SkyboxSubTool
{
	private IDisposable _undoScope;
	private bool _grabbing;
	private Vector3 _lastCursorLocal;
	private List<(int Index, float Falloff)> _grabbedVerts;

	public SkyboxGrabTool( SkyboxEditorToolEntry parent ) : base( parent ) { }

	public override void OnEnabled() { }

	public override void OnDisabled()
	{
		EndGrab();
	}

	public override void OnUpdate()
	{
		if ( Target == null || Data == null ) return;

		UpdateCursor();
		DrawOverlay();
		ConsumeMouseInput();

		if ( !Session.CursorOnSphere ) return;

		if ( Gizmo.WasLeftMousePressed && !_grabbing )
			BeginGrab();

		if ( Gizmo.IsLeftMouseDown && _grabbing && HasCursorMoved() )
			ContinueGrab();

		if ( Gizmo.WasLeftMouseReleased && _grabbing )
			EndGrab();
	}

	private void BeginGrab()
	{
		_grabbedVerts = GetVerticesInBrushWithHardness( Session.BrushRadius, 0.4f );
		if ( _grabbedVerts.Count == 0 ) return;

		_grabbing = true;
		_lastCursorLocal = Session.CursorPosition;

		Target?.SaveState();
		_undoScope = SceneEditorSession.Active
			.UndoScope( "Skybox Grab" )
			.WithComponentChanges( Target )
			.Push();
	}

	private void ContinueGrab()
	{
		var cursorLocal = Session.CursorPosition;
		var delta = cursorLocal - _lastCursorLocal;
		_lastCursorLocal = cursorLocal;

		if ( delta.LengthSquared < 0.0001f ) return;

		float sphereRadius = Data.SphereRadius > 0 ? Data.SphereRadius : 100f;
		bool changed = false;

		foreach ( var (index, falloff) in _grabbedVerts )
		{
			var v = Data.Vertices[index];
			var newPos = v.Position + delta * falloff;
			v.Position = SphereConstraint.ProjectOntoSphere( newPos, sphereRadius );
			Data.Vertices[index] = v;
			changed = true;
		}

		if ( changed )
			Target.RebuildMesh();
	}

	private void EndGrab()
	{
		if ( !_grabbing ) return;
		_grabbing = false;
		_grabbedVerts = null;

		Target?.SaveState();
		_undoScope?.Dispose();
		_undoScope = null;
	}
}
