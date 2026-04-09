using Sandbox;
using System;
using System.Collections.Generic;

namespace Editor;

/// <summary>
/// Selection Group Brush: assigns a group index (0-9) to vertices in brush.
/// Press digit keys 0-9 to set the active group.
/// Left-click/drag to paint the group onto vertices.
/// Outside this tool, pressing 0-9 selects that group.
/// </summary>
[Title( "Selection Group" )]
[Icon( "workspaces" )]
[Group( "2" )]
[Order( 4 )]
public class SkyboxSelectionGroupTool : SkyboxSubTool
{
	private IDisposable _undoScope;
	private bool _painting;
	private int _activeGroup = 0;
	private HashSet<KeyCode> _keysLastFrame = new();

	public SkyboxSelectionGroupTool( SkyboxEditorToolEntry parent ) : base( parent ) { }

	public override void OnEnabled() { }

	public override void OnDisabled()
	{
		EndPaint();
	}

	public override void OnUpdate()
	{
		if ( Target == null || Data == null ) return;

		UpdateCursor();
		DrawOverlay();
		ConsumeMouseInput();

		// Check digit keys for group selection
		for ( int d = 0; d <= 9; d++ )
		{
			var key = (KeyCode)((int)KeyCode.Num0 + d);
			if ( Application.IsKeyDown( key ) && !_keysLastFrame.Contains( key ) )
				_activeGroup = d;
		}

		// Track keys for next frame
		_keysLastFrame.Clear();
		for ( int d = 0; d <= 9; d++ )
		{
			var key = (KeyCode)((int)KeyCode.Num0 + d);
			if ( Application.IsKeyDown( key ) )
				_keysLastFrame.Add( key );
		}

		// Draw active group indicator
		DrawGroupInfo();

		if ( !Session.CursorOnSphere ) return;

		if ( Gizmo.WasLeftMousePressed )
			BeginPaint();

		if ( Gizmo.IsLeftMouseDown && _painting && HasCursorMoved() )
			PaintGroup();

		if ( Gizmo.WasLeftMouseReleased && _painting )
			EndPaint();
	}

	private void BeginPaint()
	{
		_painting = true;

		Target?.SaveState();
		_undoScope = SceneEditorSession.Active
			.UndoScope( "Assign Selection Group" )
			.WithComponentChanges( Target )
			.Push();

		PaintGroup();
	}

	private void PaintGroup()
	{
		var verts = GetVerticesInBrush( Session.BrushRadius );
		if ( verts.Count == 0 ) return;

		bool changed = false;
		foreach ( var (index, _) in verts )
		{
			var v = Data.Vertices[index];
			if ( v.SelectionGroup != _activeGroup )
			{
				v.SelectionGroup = (byte)_activeGroup;
				Data.Vertices[index] = v;
				changed = true;
			}
		}

		if ( changed )
			Target.RebuildMesh();
	}

	private void EndPaint()
	{
		if ( !_painting ) return;
		_painting = false;

		Target?.SaveState();
		_undoScope?.Dispose();
		_undoScope = null;
	}

	private void DrawGroupInfo()
	{
		// Show colored vertices by group near cursor
		if ( !Session.CursorOnSphere ) return;

		using ( Gizmo.Scope( "group_overlay" ) )
		{
			Gizmo.Draw.IgnoreDepth = true;

			var cursor = Session.CursorPosition;
			float drawRadSq = SkyboxEditorGizmos.DrawRadius * SkyboxEditorGizmos.DrawRadius;

			for ( int i = 0; i < Data.Vertices.Count; i++ )
			{
				var localPos = SkyboxEditorGizmos.GetRenderedPos( Data.Vertices[i] );
				if ( localPos.DistanceSquared( cursor ) > drawRadSq ) continue;

				var v = Data.Vertices[i];
				if ( v.SelectionGroup == _activeGroup )
				{
					var wp = SkyboxEditorGizmos.ToWorld( localPos, Target );
					Gizmo.Draw.Color = Color.Cyan;
					Gizmo.Draw.Sprite( wp, 8f, null, false );
				}
			}
		}
	}
}
