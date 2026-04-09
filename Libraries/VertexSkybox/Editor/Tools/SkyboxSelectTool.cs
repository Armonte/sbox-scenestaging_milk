using Sandbox;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Editor;

/// <summary>
/// Select + Transform tool: RMB to select vertices, LMB to transform.
/// Shift+RMB = additive, Ctrl+RMB = subtractive.
/// LMB = Move, Shift+LMB = Rotate, Ctrl+LMB = Scale.
/// All transforms re-project onto sphere.
/// </summary>
[Title( "Select" )]
[Icon( "near_me" )]
[Group( "2" )]
[Order( 4 )]
public class SkyboxSelectTool : SkyboxSubTool
{
	private enum TransformMode { None, Move, Rotate, Scale }

	private IDisposable _undoScope;
	private TransformMode _mode;
	private Vector3 _lastCursorLocal;
	private Vector3 _selectionCenter;

	public SkyboxSelectTool( SkyboxEditorToolEntry parent ) : base( parent ) { }

	public override void OnEnabled() { }

	public override void OnDisabled()
	{
		EndTransform();
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

		// LMB = transform selected vertices
		if ( Gizmo.WasLeftMousePressed && Session.SelectedVertices.Count > 0 )
			BeginTransform();

		if ( Gizmo.IsLeftMouseDown && _mode != TransformMode.None && HasCursorMoved() )
			ContinueTransform();

		if ( Gizmo.WasLeftMouseReleased && _mode != TransformMode.None )
			EndTransform();
	}

	private void HandleSelection()
	{
		int hovered = Session.HoveredVertex;
		if ( hovered < 0 || hovered >= Data.Vertices.Count ) return;

		if ( Gizmo.IsCtrlPressed )
			Session.SelectedVertices.Remove( hovered );
		else if ( Gizmo.IsShiftPressed )
			Session.SelectedVertices.Add( hovered );
		else
		{
			Session.SelectedVertices.Clear();
			Session.SelectedVertices.Add( hovered );
		}
	}

	private void BeginTransform()
	{
		if ( Gizmo.IsShiftPressed )
			_mode = TransformMode.Rotate;
		else if ( Gizmo.IsCtrlPressed )
			_mode = TransformMode.Scale;
		else
			_mode = TransformMode.Move;

		_lastCursorLocal = Session.CursorPosition;
		_selectionCenter = ComputeSelectionCenter();

		Target?.SaveState();
		_undoScope = SceneEditorSession.Active
			.UndoScope( $"Skybox {_mode} Vertices" )
			.WithComponentChanges( Target )
			.Push();
	}

	private void ContinueTransform()
	{
		switch ( _mode )
		{
			case TransformMode.Move: ContinueMove(); break;
			case TransformMode.Rotate: ContinueRotate(); break;
			case TransformMode.Scale: ContinueScale(); break;
		}
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
			v.Position = SphereConstraint.ProjectOntoSphere( v.Position + delta, sphereRadius );
			Data.Vertices[idx] = v;
			changed = true;
		}

		if ( changed )
			Target.RebuildMesh();
	}

	private void ContinueRotate()
	{
		var cursorLocal = Session.CursorPosition;
		var delta = cursorLocal - _lastCursorLocal;
		_lastCursorLocal = cursorLocal;

		if ( delta.LengthSquared < 0.0001f ) return;

		// Rotation amount from horizontal mouse movement
		float angle = delta.x * 0.5f;
		var center = _selectionCenter.Normal;
		var rotation = Rotation.FromAxis( center, angle );

		float sphereRadius = Data.SphereRadius > 0 ? Data.SphereRadius : 100f;
		bool changed = false;

		foreach ( var idx in Session.SelectedVertices )
		{
			if ( idx < 0 || idx >= Data.Vertices.Count ) continue;

			var v = Data.Vertices[idx];
			v.Position = SphereConstraint.ProjectOntoSphere( rotation * v.Position, sphereRadius );
			Data.Vertices[idx] = v;
			changed = true;
		}

		if ( changed )
			Target.RebuildMesh();
	}

	private void ContinueScale()
	{
		var cursorLocal = Session.CursorPosition;
		var delta = cursorLocal - _lastCursorLocal;
		_lastCursorLocal = cursorLocal;

		if ( delta.LengthSquared < 0.0001f ) return;

		// Scale factor from vertical mouse movement
		float scaleFactor = 1f + delta.z * 0.01f;
		scaleFactor = scaleFactor.Clamp( 0.9f, 1.1f );

		float sphereRadius = Data.SphereRadius > 0 ? Data.SphereRadius : 100f;
		bool changed = false;

		foreach ( var idx in Session.SelectedVertices )
		{
			if ( idx < 0 || idx >= Data.Vertices.Count ) continue;

			var v = Data.Vertices[idx];
			var offset = v.Position - _selectionCenter;
			v.Position = SphereConstraint.ProjectOntoSphere(
				_selectionCenter + offset * scaleFactor, sphereRadius );
			Data.Vertices[idx] = v;
			changed = true;
		}

		if ( changed )
			Target.RebuildMesh();
	}

	private void EndTransform()
	{
		if ( _mode == TransformMode.None ) return;
		_mode = TransformMode.None;

		Target?.SaveState();
		_undoScope?.Dispose();
		_undoScope = null;
	}

	private Vector3 ComputeSelectionCenter()
	{
		var sum = Vector3.Zero;
		int count = 0;
		foreach ( var idx in Session.SelectedVertices )
		{
			if ( idx < 0 || idx >= Data.Vertices.Count ) continue;
			sum += Data.Vertices[idx].Position;
			count++;
		}
		return count > 0 ? sum / count : Vector3.Zero;
	}

	private void DrawSelection()
	{
		if ( Session.SelectedVertices.Count == 0 ) return;

		using ( Gizmo.Scope( "selection_overlay" ) )
		{
			Gizmo.Draw.IgnoreDepth = true;
			Gizmo.Draw.Color = new Color( 1f, 0.6f, 0f, 0.9f );

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
