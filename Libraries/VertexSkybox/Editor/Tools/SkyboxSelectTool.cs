using Sandbox;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Editor;

/// <summary>
/// Select + Transform tool: RMB to select vertices, LMB to move selection.
/// Shift+RMB = additive, Ctrl+RMB = subtractive.
/// Move rotates selection around sphere center, re-projects onto sphere.
/// </summary>
[Title( "Select" )]
[Icon( "near_me" )]
public class SkyboxSelectTool : SkyboxSubTool
{
	private IDisposable _undoScope;
	private bool _moving;
	private Vector3 _lastCursorLocal;

	public SkyboxSelectTool( SkyboxEditorToolEntry parent ) : base( parent ) { }

	public override void OnEnabled() { }

	public override void OnDisabled()
	{
		EndMove();
	}

	public override void OnUpdate()
	{
		if ( Target == null || Data == null ) return;

		UpdateCursor();
		DrawOverlay();
		DrawSelection();
		ConsumeMouseInput();

		if ( !Session.CursorOnSphere ) return;

		// RMB = selection
		if ( Gizmo.WasRightMousePressed )
			HandleSelection();

		// LMB = move selected vertices
		if ( Gizmo.WasLeftMousePressed && Session.SelectedVertices.Count > 0 )
			BeginMove();

		if ( Gizmo.IsLeftMouseDown && _moving && HasCursorMoved() )
			ContinueMove();

		if ( Gizmo.WasLeftMouseReleased && _moving )
			EndMove();
	}

	private void HandleSelection()
	{
		int hovered = Session.HoveredVertex;
		if ( hovered < 0 || hovered >= Data.Vertices.Count ) return;

		if ( Gizmo.IsCtrlPressed )
		{
			// Subtractive
			Session.SelectedVertices.Remove( hovered );
		}
		else if ( Gizmo.IsShiftPressed )
		{
			// Additive
			Session.SelectedVertices.Add( hovered );
		}
		else
		{
			// Replace selection
			Session.SelectedVertices.Clear();
			Session.SelectedVertices.Add( hovered );
		}
	}

	private void BeginMove()
	{
		_moving = true;
		_lastCursorLocal = Session.CursorPosition;

		Target.SaveState();
		_undoScope = SceneEditorSession.Active
			.UndoScope( "Skybox Move Vertices" )
			.WithComponentChanges( Target )
			.Push();
	}

	private void ContinueMove()
	{
		var cursorLocal = Session.CursorPosition;
		var delta = cursorLocal - _lastCursorLocal;
		_lastCursorLocal = cursorLocal;

		if ( delta.LengthSquared < 0.0001f ) return;

		float sphereRadius = Data.SphereRadius > 0 ? Data.SphereRadius : 100f;
		bool changed = false;

		foreach ( var idx in Session.SelectedVertices )
		{
			if ( idx < 0 || idx >= Data.Vertices.Count ) continue;

			var v = Data.Vertices[idx];
			var newPos = v.Position + delta;
			v.Position = SphereConstraint.ProjectOntoSphere( newPos, sphereRadius );
			Data.Vertices[idx] = v;
			changed = true;
		}

		if ( changed )
			Target.RebuildMesh();
	}

	private void EndMove()
	{
		if ( !_moving ) return;
		_moving = false;

		Target.SaveState();
		_undoScope?.Dispose();
		_undoScope = null;
	}

	private void DrawSelection()
	{
		if ( Session.SelectedVertices.Count == 0 ) return;

		using ( Gizmo.Scope( "selection_overlay" ) )
		{
			Gizmo.Draw.IgnoreDepth = true;
			Gizmo.Draw.Color = new Color( 1f, 0.6f, 0f, 0.9f ); // Orange

			foreach ( var idx in Session.SelectedVertices )
			{
				if ( idx < 0 || idx >= Data.Vertices.Count ) continue;

				var wp = SkyboxEditorGizmos.ToWorld(
					SkyboxEditorGizmos.GetRenderedPos( Data.Vertices[idx] ), Target );
				Gizmo.Draw.Sprite( wp, 8f, null, false );
			}
		}
	}
}
