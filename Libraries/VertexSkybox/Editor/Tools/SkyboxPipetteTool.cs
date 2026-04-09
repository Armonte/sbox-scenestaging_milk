using Sandbox;

namespace Editor;

/// <summary>
/// Pipette tool: sample color from vertices.
/// Left-click assigns to left paint color, right-click to right paint color.
/// </summary>
[Title( "Pipette" )]
[Icon( "colorize" )]
public class SkyboxPipetteTool : SkyboxSubTool
{
	public SkyboxPipetteTool( SkyboxEditorToolEntry parent ) : base( parent ) { }

	public override void OnEnabled() { }
	public override void OnDisabled() { }

	public override void OnUpdate()
	{
		if ( Target == null || Data == null ) return;

		UpdateCursor();
		DrawOverlay();
		ConsumeMouseInput();

		if ( !Session.CursorOnSphere ) return;
		if ( Session.HoveredVertex < 0 || Session.HoveredVertex >= Data.Vertices.Count ) return;

		// Draw a color preview at the hovered vertex
		DrawColorPreview();

		if ( Gizmo.WasLeftMousePressed )
		{
			var picked = Data.Vertices[Session.HoveredVertex].Color;
			Session.LeftColor = picked;
			Parent.LeftPaintColor = picked.ToColor();
		}

		if ( Gizmo.WasRightMousePressed )
		{
			var picked = Data.Vertices[Session.HoveredVertex].Color;
			Session.RightColor = picked;
			Parent.RightPaintColor = picked.ToColor();
		}
	}

	private void DrawColorPreview()
	{
		var v = Data.Vertices[Session.HoveredVertex];
		var wp = SkyboxEditorGizmos.ToWorld(
			SkyboxEditorGizmos.GetRenderedPos( v ), Target );

		using ( Gizmo.Scope( "pipette_preview" ) )
		{
			Gizmo.Draw.IgnoreDepth = true;

			// Outer ring in sampled color
			Gizmo.Draw.Color = v.Color.ToColor();
			Gizmo.Draw.Sprite( wp, 16f, null, false );

			// Inner dot white
			Gizmo.Draw.Color = Color.White;
			Gizmo.Draw.Sprite( wp, 4f, null, false );
		}
	}
}
