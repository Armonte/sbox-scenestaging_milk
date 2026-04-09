using Sandbox;
using System;

namespace Editor;

/// <summary>
/// Gradient tool: click-drag to create a linear gradient between two colors.
/// Start color = left paint color, end color = right paint color.
/// Press location → release location defines the gradient axis.
/// </summary>
[Title( "Gradient" )]
[Icon( "gradient" )]
[Group( "1" )]
[Order( 2 )]
public class SkyboxGradientTool : SkyboxSubTool
{
	private IDisposable _undoScope;
	private bool _dragging;
	private Vector3 _startLocal;
	private Vector3 _startWorld;

	public SkyboxGradientTool( SkyboxEditorToolEntry parent ) : base( parent ) { }

	public override void OnEnabled() { }

	public override void OnDisabled()
	{
		CancelDrag();
	}

	public override void OnUpdate()
	{
		if ( Target == null || Data == null ) return;

		UpdateCursor();
		DrawOverlay();
		ConsumeMouseInput();

		if ( !Session.CursorOnSphere ) return;

		if ( Gizmo.WasLeftMousePressed && !_dragging )
		{
			BeginDrag();
		}

		if ( _dragging )
		{
			DrawGradientPreview();
		}

		if ( Gizmo.WasLeftMouseReleased && _dragging )
		{
			ApplyGradient();
			EndDrag();
		}
	}

	private void BeginDrag()
	{
		_dragging = true;
		_startLocal = Session.CursorPosition;
		_startWorld = Session.CursorWorldPosition;

		Target.SaveState();
		_undoScope = SceneEditorSession.Active
			.UndoScope( "Skybox Gradient" )
			.WithComponentChanges( Target )
			.Push();
	}

	private void DrawGradientPreview()
	{
		var endWorld = Session.CursorWorldPosition;

		using ( Gizmo.Scope( "gradient_preview" ) )
		{
			Gizmo.Draw.IgnoreDepth = true;
			Gizmo.Draw.Color = Color.White;
			Gizmo.Draw.LineThickness = 2f;
			Gizmo.Draw.Line( _startWorld, endWorld );

			// Start marker
			Gizmo.Draw.Color = Session.LeftColor.ToColor();
			Gizmo.Draw.Sprite( _startWorld, 10f, null, false );

			// End marker
			Gizmo.Draw.Color = Session.RightColor.ToColor();
			Gizmo.Draw.Sprite( endWorld, 10f, null, false );
		}
	}

	private void ApplyGradient()
	{
		var endLocal = Session.CursorPosition;
		var axis = endLocal - _startLocal;
		float axisLenSq = axis.LengthSquared;
		if ( axisLenSq < 0.001f ) return;

		var startColor = Session.LeftColor;
		var endColor = Session.RightColor;
		float opacity = Session.LeftOpacity;
		bool changed = false;

		// Apply gradient to ALL vertices. The projection onto the gradient axis
		// determines the color blend (t=0 at start, t=1 at end).
		// Vertices outside the 0..1 range get clamped to start/end color.
		for ( int i = 0; i < Data.Vertices.Count; i++ )
		{
			var v = Data.Vertices[i];
			var pos = SkyboxEditorGizmos.GetRenderedPos( v );

			// Project vertex onto gradient axis to get t value
			float t = Vector3.Dot( pos - _startLocal, axis ) / axisLenSq;
			t = t.Clamp( 0f, 1f );

			// Lerp between start and end color at position t
			float gr = (startColor.r + (endColor.r - startColor.r) * t) / 255f;
			float gg = (startColor.g + (endColor.g - startColor.g) * t) / 255f;
			float gb = (startColor.b + (endColor.b - startColor.b) * t) / 255f;

			// Blend with existing color using opacity
			float or_ = v.Color.r / 255f;
			float og = v.Color.g / 255f;
			float ob = v.Color.b / 255f;

			v.Color = new Color32(
				(byte)(((or_ + (gr - or_) * opacity).Clamp( 0f, 1f )) * 255),
				(byte)(((og + (gg - og) * opacity).Clamp( 0f, 1f )) * 255),
				(byte)(((ob + (gb - ob) * opacity).Clamp( 0f, 1f )) * 255),
				255
			);

			Data.Vertices[i] = v;
			changed = true;
		}

		if ( changed )
			Target.RebuildMesh();
	}

	private void EndDrag()
	{
		_dragging = false;
		Target.SaveState();
		_undoScope?.Dispose();
		_undoScope = null;
	}

	private void CancelDrag()
	{
		if ( !_dragging ) return;
		_dragging = false;
		_undoScope?.Dispose();
		_undoScope = null;
	}
}
