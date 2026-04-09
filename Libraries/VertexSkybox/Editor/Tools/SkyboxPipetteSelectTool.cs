using Sandbox;
using System;

namespace Editor;

/// <summary>
/// Pipette Select: selects vertices with similar color to the clicked vertex.
/// Click = replace selection, Shift+Click = additive, Ctrl+Click = subtractive.
/// Tolerance based on brush radius (larger brush = more lenient matching).
/// </summary>
[Title( "Pipette Select" )]
[Icon( "format_color_fill" )]
[Group( "2" )]
[Order( 4 )]
public class SkyboxPipetteSelectTool : SkyboxSubTool
{
	public SkyboxPipetteSelectTool( SkyboxEditorToolEntry parent ) : base( parent ) { }

	public override void OnEnabled() { }
	public override void OnDisabled() { }

	public override void OnUpdate()
	{
		if ( Target == null || Data == null ) return;

		UpdateCursor();
		DrawOverlay();
		DrawSelection();
		ConsumeMouseInput();

		if ( !Session.CursorOnSphere ) return;

		if ( Gizmo.WasLeftMousePressed )
			SelectByColor();
	}

	private void SelectByColor()
	{
		int hovered = Session.HoveredVertex;
		if ( hovered < 0 || hovered >= Data.Vertices.Count ) return;

		var targetColor = Data.Vertices[hovered].Color;
		float tolerance = Session.BrushRadius * 3f; // Larger brush = more lenient

		bool additive = Gizmo.IsShiftPressed;
		bool subtractive = Gizmo.IsCtrlPressed;

		if ( !additive && !subtractive )
			Session.SelectedVertices.Clear();

		for ( int i = 0; i < Data.Vertices.Count; i++ )
		{
			var c = Data.Vertices[i].Color;
			float diff = MathF.Abs( c.r - targetColor.r ) +
						MathF.Abs( c.g - targetColor.g ) +
						MathF.Abs( c.b - targetColor.b );

			if ( diff <= tolerance )
			{
				if ( subtractive )
					Session.SelectedVertices.Remove( i );
				else
					Session.SelectedVertices.Add( i );
			}
		}
	}

	private void DrawSelection()
	{
		if ( Session.SelectedVertices.Count == 0 ) return;

		using ( Gizmo.Scope( "pipsel_overlay" ) )
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
