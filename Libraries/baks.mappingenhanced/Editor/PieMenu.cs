using System;

namespace Editor;

/// <summary>
/// Blender-style radial menu with button boxes arranged in a circle.
/// Opens on Ctrl+Right Click.
/// </summary>
public class PieMenu : Widget
{
	public class PieOption
	{
		public string Text { get; set; }
		public string Icon { get; set; }
		public Action Action { get; set; }
		public bool Enabled { get; set; } = true;
	}

	private List<PieOption> _options = new();
	private int _hoveredIndex = -1;
	private Vector2 _centerPosition;
	private float _currentIndicatorAngle = -90f; // Start at top
	private float _targetIndicatorAngle = -90f;

	public float Radius { get; set; } = 180f;
	public float CenterRadius { get; set; } = 30f;
	public float CenterRingThickness { get; set; } = 10f;
	public float ButtonPadding { get; set; } = 20f;
	public float ButtonHeight { get; set; } = 28f;

	// s&box themed colors
	public Color ButtonColor { get; set; } = Theme.ButtonBackground;
	public Color ButtonHoverColor { get; set; } = Theme.Blue;
	public Color CenterRingColor { get; set; } = Theme.ControlBackground.Darken( 0.2f );
	public Color IndicatorColor { get; set; } = Theme.Yellow;

	// Track which button this menu should respond to
	public MouseButtons TriggerButton { get; set; } = MouseButtons.Forward;

	public PieMenu( Widget parent = null ) : base( parent )
	{
		WindowFlags = WindowFlags.Popup | WindowFlags.FramelessWindowHint | WindowFlags.NoDropShadowWindowHint;
		TranslucentBackground = true;
	}

	public PieOption AddOption( string text, string icon = null, Action action = null )
	{
		var option = new PieOption
		{
			Text = text,
			Icon = icon,
			Action = action
		};

		_options.Add( option );
		return option;
	}

	public void Clear()
	{
		_options.Clear();
		_hoveredIndex = -1;
	}

	public void OpenAtCursor()
	{
		OpenAt( Application.CursorPosition );
	}

	public void OpenAt( Vector2 position )
	{
		if ( _options.Count == 0 ) return;

		Paint.SetDefaultFont( 10, 500 );
		float maxButtonWidth = 0;
		foreach ( var option in _options )
		{
			var textSize = Paint.MeasureText( option.Text );
			float buttonWidth = textSize.x + ButtonPadding * 2;
			maxButtonWidth = Math.Max( maxButtonWidth, buttonWidth );
		}

		var size = (Radius + maxButtonWidth + 40) * 2;
		var widgetSize = new Vector2( size );

		Size = widgetSize;
		MinimumSize = widgetSize;
		MaximumSize = widgetSize;
		Position = position - (widgetSize / 2f);

		_centerPosition = widgetSize / 2;
		_hoveredIndex = -1;

		Show();
		Focus();
		MouseTracking = true;

		var localPos = Application.CursorPosition;
		UpdateHoveredOption( localPos );
	}

	protected override void OnPaint()
	{
		base.OnPaint();

		if ( _options.Count == 0 ) return;

		Paint.Antialiasing = true;

		var center = LocalRect.Center;
		int optionCount = _options.Count;
		float angleStep = 360f / optionCount;
		float startAngle = -90f;

		// Draw button boxes positioned around the circle
		for ( int i = 0; i < optionCount; i++ )
		{
			var option = _options[i];

			float angle = startAngle + (i * angleStep);
			float angleRad = angle * MathF.PI / 180f;

			// Calculate button position
			var buttonCenter = center + new Vector2(
				MathF.Cos( angleRad ) * Radius,
				MathF.Sin( angleRad ) * Radius
			);

			// Measure text width to size button
			Paint.SetDefaultFont( 10, 500 );
			var textSize = Paint.MeasureText( option.Text );

			// Button width = text width + padding
			float buttonWidth = textSize.x + ButtonPadding * 2;

			var buttonRect = new Rect(
				buttonCenter.x - buttonWidth / 2,
				buttonCenter.y - ButtonHeight / 2,
				buttonWidth,
				ButtonHeight
			);

			var buttonColor = i == _hoveredIndex ? ButtonHoverColor : ButtonColor;
			Paint.ClearPen();
			Paint.SetBrush( buttonColor );
			Paint.DrawRect( buttonRect, 3 );

			// Draw text - centered
			Paint.SetPen( Color.White );
			Paint.SetDefaultFont( 10, 500 );

			var textRect = buttonRect.Shrink( 10, 0 );
			Paint.DrawText( textRect, option.Text, TextFlag.Center );
		}

		// Draw center ring (donut shape)
		Paint.ClearPen();
		Paint.SetBrush( CenterRingColor );
		DrawRing( center, CenterRadius - CenterRingThickness, CenterRadius );

		if ( _hoveredIndex >= 0 && _hoveredIndex < _options.Count )
		{
			// Much faster interpolation for snappier feel
			float angleDiff = _targetIndicatorAngle - _currentIndicatorAngle;

			// Handle wraparound (shortest path)
			if ( angleDiff > 180f ) angleDiff -= 360f;
			if ( angleDiff < -180f ) angleDiff += 360f;

			_currentIndicatorAngle += angleDiff * 0.6f; // Increased from 0.3f for faster response

			// Normalize angle
			if ( _currentIndicatorAngle < 0 ) _currentIndicatorAngle += 360f;
			if ( _currentIndicatorAngle >= 360f ) _currentIndicatorAngle -= 360f;

			// Draw a colored arc segment on the ring
			float arcSpan = 40f;
			Paint.SetBrush( IndicatorColor );
			DrawArcSegment( center, CenterRadius - CenterRingThickness, CenterRadius, _currentIndicatorAngle - arcSpan / 2, _currentIndicatorAngle + arcSpan / 2 );
		}
	}

	/// <summary>
	/// Draw a ring (donut shape)
	/// </summary>
	private void DrawRing( Vector2 center, float innerRadius, float outerRadius )
	{
		const int segments = 64;
		var points = new List<Vector2>();

		// Outer circle
		for ( int i = 0; i <= segments; i++ )
		{
			float angle = (i / (float)segments) * 360f;
			float rad = angle * MathF.PI / 180f;
			points.Add( center + new Vector2( MathF.Cos( rad ), MathF.Sin( rad ) ) * outerRadius );
		}

		// Inner circle (reverse)
		for ( int i = segments; i >= 0; i-- )
		{
			float angle = (i / (float)segments) * 360f;
			float rad = angle * MathF.PI / 180f;
			points.Add( center + new Vector2( MathF.Cos( rad ), MathF.Sin( rad ) ) * innerRadius );
		}

		Paint.DrawPolygon( points.ToArray() );
	}

	/// <summary>
	/// Draw an arc segment on a ring
	/// </summary>
	private void DrawArcSegment( Vector2 center, float innerRadius, float outerRadius, float startAngle, float endAngle )
	{
		const int segments = 20;
		float angleRange = endAngle - startAngle;
		int segmentCount = Math.Max( 2, (int)(segments * angleRange / 360f) );

		var points = new List<Vector2>();

		// Inner arc
		for ( int i = 0; i <= segmentCount; i++ )
		{
			float angle = startAngle + (angleRange * i / segmentCount);
			float rad = angle * MathF.PI / 180f;
			points.Add( center + new Vector2( MathF.Cos( rad ), MathF.Sin( rad ) ) * innerRadius );
		}

		// Outer arc (reverse)
		for ( int i = segmentCount; i >= 0; i-- )
		{
			float angle = startAngle + (angleRange * i / segmentCount);
			float rad = angle * MathF.PI / 180f;
			points.Add( center + new Vector2( MathF.Cos( rad ), MathF.Sin( rad ) ) * outerRadius );
		}

		Paint.DrawPolygon( points.ToArray() );
	}

	protected override void OnMouseMove( MouseEvent e )
	{
		base.OnMouseMove( e );
		UpdateHoveredOption( e.LocalPosition );
	}

	protected override void OnMousePress( MouseEvent e )
	{
		base.OnMousePress( e );
		e.Accepted = true;
	}

	protected override void OnMouseReleased( MouseEvent e )
	{
		// Check if this is the button that opened this menu
		if ( e.Button == TriggerButton )
		{
			ExecuteHoveredOptionAndClose();
			e.Accepted = true;
			return;
		}

		base.OnMouseReleased( e );
	}

	/// <summary>
	/// Execute the currently hovered option and close the menu
	/// </summary>
	public void ExecuteHoveredOptionAndClose()
	{
		if ( _hoveredIndex >= 0 && _hoveredIndex < _options.Count )
		{
			var option = _options[_hoveredIndex];
			if ( option.Enabled )
			{
				option.Action?.Invoke();
			}
		}

		Close();
	}

	protected override void OnKeyPress( KeyEvent e )
	{
		base.OnKeyPress( e );

		if ( e.Key == KeyCode.Escape )
		{
			Close();
			e.Accepted = true;
		}
	}

	private void UpdateHoveredOption( Vector2 mousePos )
	{
		var center = LocalRect.Center;
		var offset = mousePos - center;
		float distance = offset.Length;

		// More forgiving selection area
		if ( distance < 10f || distance > Radius + 250 )
		{
			if ( _hoveredIndex != -1 )
			{
				_hoveredIndex = -1;
				Update();
			}
			return;
		}

		// Calculate angle from center to mouse
		float mouseAngle = MathF.Atan2( offset.y, offset.x ) * 180f / MathF.PI;

		// Find the closest button by angular distance
		int optionCount = _options.Count;
		float angleStep = 360f / optionCount;
		float startAngle = -90f;

		int closestIndex = -1;
		float closestDistance = float.MaxValue;

		for ( int i = 0; i < optionCount; i++ )
		{
			// Calculate the angle of this button's center
			float buttonAngle = startAngle + (i * angleStep);

			// Normalize both angles to 0-360
			float normMouseAngle = mouseAngle;
			if ( normMouseAngle < 0 ) normMouseAngle += 360f;
			float normButtonAngle = buttonAngle;
			if ( normButtonAngle < 0 ) normButtonAngle += 360f;

			// Calculate angular distance (shortest path around the circle)
			float angularDist = Math.Abs( normMouseAngle - normButtonAngle );
			if ( angularDist > 180f ) angularDist = 360f - angularDist;

			if ( angularDist < closestDistance )
			{
				closestDistance = angularDist;
				closestIndex = i;
			}
		}

		// Set target angle to the selected button's angle for smooth snapping
		if ( closestIndex != -1 )
		{
			_targetIndicatorAngle = startAngle + (closestIndex * angleStep);
		}

		_hoveredIndex = closestIndex;
		Update();
	}

	[Shortcut( "editor.paste.color", "CTRL+V", typeof( SceneViewWidget ) )]
	public static void PasteFromClipboard()
	{
		var clipboard = EditorUtility.Clipboard.Paste();

		if ( string.IsNullOrWhiteSpace( clipboard ) )
		{
			// Try GameObject paste if clipboard text is empty
			PasteGameObject();
			return;
		}

		// Try to parse as hex color first
		if ( clipboard.StartsWith( "#" ) )
		{
			if ( TryParseHexColor( clipboard, out Color color ) )
			{
				PasteColor( color );
				return;
			}
			else
			{
				Log.Warning( $"Failed to parse color from clipboard: {clipboard}" );
				return;
			}
		}

		// Try to handle GameObject paste
		PasteGameObject();
	}

	private static void PasteColor( Color color )
	{
		using var scope = SceneEditorSession.Scope();

		var selection = SceneEditorSession.Active.Selection;

		// Get all MeshComponents and ModelRenderers from selected GameObjects
		var meshComponents = selection.OfType<GameObject>()
			.Select( x => x.GetComponent<MeshComponent>() )
			.Where( x => x.IsValid() )
			.ToList();

		var modelRenderers = selection.OfType<GameObject>()
			.Select( x => x.GetComponent<ModelRenderer>() )
			.Where( x => x.IsValid() )
			.ToList();

		var totalCount = meshComponents.Count + modelRenderers.Count;

		if ( totalCount == 0 )
		{
			Log.Info( "No mesh or model renderer components selected" );
			return;
		}

		// Combine both lists for undo tracking
		var allComponents = meshComponents.Cast<Component>()
			.Concat( modelRenderers.Cast<Component>() )
			.ToList();

		using ( SceneEditorSession.Active.UndoScope( "Paste Color" )
			.WithComponentChanges( allComponents )
			.Push() )
		{
			// Apply color to MeshComponents
			foreach ( var component in meshComponents )
			{
				component.Color = color;
			}

			// Apply color to ModelRenderers
			foreach ( var component in modelRenderers )
			{
				component.Tint = color;
			}
		}

		Log.Info( $"Applied color to {meshComponents.Count} mesh component(s) and {modelRenderers.Count} model renderer(s)" );
	}

	private static void PasteGameObject()
	{
		var session = SceneEditorSession.Active;
		if ( session == null ) return;

		// Get the active scene viewport
		var sceneView = SceneViewWidget.Current;
		if ( sceneView?.LastSelectedViewportWidget == null ) return;

		var viewport = sceneView.LastSelectedViewportWidget;

		// First, paste the standard way
		EditorScene.Paste();

		// Get the pasted objects
		var pastedObjects = session.Selection.OfType<GameObject>().ToList();
		if ( pastedObjects.Count == 0 ) return;

		// Compute the average point of all pasted objects
		Vector3 middlePoint = Vector3.Zero;
		foreach ( var go in pastedObjects )
			middlePoint += go.WorldPosition;

		middlePoint /= pastedObjects.Count;

		// Get mouse position in viewport and trace
		var mousePosition = SceneViewportWidget.MousePosition;
		var camera = viewport.Renderer.Camera;

		if ( !camera.IsValid() ) return;

		// Create ray from mouse position
		var ray = camera.ScreenPixelToRay( mousePosition );

		// Trace to find world position
		var trace = session.Scene.Trace
			.Ray( ray, 10000f )
			.UseRenderMeshes( true )
			.UsePhysicsWorld( false )
			.Run();

		if ( trace.Hit )
		{
			using ( session.UndoScope( "Paste at Mouse" ).Push() )
			{
				// Reposition all game objects relative to new center
				foreach ( var go in pastedObjects )
				{
					Vector3 offset = go.WorldPosition - middlePoint;
					go.WorldPosition = trace.HitPosition + offset;
				}

				Log.Info( $"Pasted {pastedObjects.Count} GameObject(s) at mouse position" );
			}
		}
	}

	static bool TryParseHexColor( string hex, out Color color )
	{
		color = default;

		if ( string.IsNullOrEmpty( hex ) )
			return false;

		hex = hex.TrimStart( '#' );

		if ( hex.Length != 6 && hex.Length != 8 )
			return false;

		try
		{
			int r = Convert.ToInt32( hex.Substring( 0, 2 ), 16 );
			int g = Convert.ToInt32( hex.Substring( 2, 2 ), 16 );
			int b = Convert.ToInt32( hex.Substring( 4, 2 ), 16 );
			int a = hex.Length == 8 ? Convert.ToInt32( hex.Substring( 6, 2 ), 16 ) : 255;

			color = new Color( r / 255f, g / 255f, b / 255f, a / 255f );
			return true;
		}
		catch
		{
			return false;
		}
	}

	public IReadOnlyList<PieOption> Options => _options.AsReadOnly();
}
