using Sandbox;
using System;
using System.Collections.Generic;

namespace Editor;

/// <summary>
/// Create tool: click to add vertices on the sphere surface.
/// Drag to create a line of vertices separated by brush radius.
/// New vertex color = current left paint color.
/// </summary>
[Title( "Create" )]
[Icon( "add_circle" )]
[Group( "2" )]
[Order( 5 )]
public class SkyboxCreateTool : SkyboxSubTool
{
	private IDisposable _undoScope;
	private bool _creating;
	private Vector3 _lastPlacedLocal;

	public SkyboxCreateTool( SkyboxEditorToolEntry parent ) : base( parent ) { }

	public override void OnEnabled() { }

	public override void OnDisabled()
	{
		EndCreate();
	}

	public override void OnUpdate()
	{
		if ( Target == null || Data == null ) return;

		UpdateCursor();
		DrawOverlay();
		ConsumeMouseInput();

		if ( !Session.CursorOnSphere ) return;

		if ( Gizmo.WasLeftMousePressed )
		{
			BeginCreate();
			PlaceVertex();
		}

		if ( Gizmo.IsLeftMouseDown && _creating )
		{
			// Place additional vertices when cursor moves far enough
			var cursor = Session.CursorPosition;
			float spacing = Session.BrushRadius;
			if ( cursor.Distance( _lastPlacedLocal ) >= spacing )
				PlaceVertex();
		}

		if ( Gizmo.WasLeftMouseReleased && _creating )
			EndCreate();
	}

	private void BeginCreate()
	{
		if ( _creating ) return;
		_creating = true;

		Target.SaveState();
		_undoScope = SceneEditorSession.Active
			.UndoScope( "Skybox Create Vertices" )
			.WithComponentChanges( Target )
			.Push();
	}

	private void PlaceVertex()
	{
		float sphereRadius = Data.SphereRadius > 0 ? Data.SphereRadius : 100f;

		// The cursor position is in rendered space (X negated).
		// Convert back to data space for storage.
		var cursorLocal = Session.CursorPosition;
		var dataPos = new Vector3( -cursorLocal.x, cursorLocal.y, cursorLocal.z );
		dataPos = SphereConstraint.ProjectOntoSphere( dataPos, sphereRadius );

		var newVert = new SkyboxVertex(
			dataPos.x, dataPos.y, dataPos.z,
			Session.LeftColor.r, Session.LeftColor.g, Session.LeftColor.b
		);

		Data.Vertices.Add( newVert );
		Data.InvalidateAdjacency();
		_lastPlacedLocal = cursorLocal;

		Target.RebuildMesh();
	}

	private void EndCreate()
	{
		if ( !_creating ) return;
		_creating = false;

		Target.SaveState();
		_undoScope?.Dispose();
		_undoScope = null;
	}
}
