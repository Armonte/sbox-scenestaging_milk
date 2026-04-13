using Sandbox.Rendering;

/// <summary>
/// See-through portal with teleportation. Two portals linked together: looking at
/// one shows the view from the other, walking through teleports you.
///
/// <b>Scene setup:</b> place two GameObjects each with a <see cref="Portal"/> component,
/// a child <see cref="ModelRenderer"/> (plane mesh + portal_view material), and link
/// them via <see cref="LinkedPortal"/>. The rendering uses the same CommandList +
/// secondary camera pattern as <c>PlaneReflection.cs</c>.
///
/// <b>The math:</b> world → source-local → 180° yaw flip → destination-world.
/// Entering the front of source = exiting the back of destination.
/// </summary>
[Title( "Portal" )]
[Icon( "swap_horiz" )]
public sealed class Portal : Component
{
	// --- Configuration ---

	/// <summary>
	/// The portal this one is linked to. Each portal needs a reference to its pair.
	/// </summary>
	[Property] public Portal LinkedPortal { get; set; }

	/// <summary>
	/// Don't render the portal camera if the player is further than this.
	/// </summary>
	[Property] public float MaxRenderDistance { get; set; } = 2000f;

	/// <summary>
	/// Objects within this distance of the portal plane are tracked for teleportation.
	/// </summary>
	[Property] public float TeleportRadius { get; set; } = 100f;

	/// <summary>
	/// Minimum distance past the exit portal plane the player emerges. Acts as a
	/// safety buffer so the player isn't stuck exactly on the plane. The actual
	/// emerge distance is the larger of this value and how far past the entry
	/// plane the player was at teleport time — preserving entry depth keeps the
	/// view seamless (no "zoom" on crossing).
	/// </summary>
	[Property] public float EmergeOffset { get; set; } = 0f;

	/// <summary>
	/// Logs every step of the teleport pipeline: captured state, cyan directions,
	/// emerge position calculation, yaw rotation, velocity transformation, and
	/// final applied values. Turn on to debug portal behavior, off for production.
	/// </summary>
	[Property, Group( "Debug" )] public bool DebugSteps { get; set; } = false;

	// === TOGGLES FOR ISOLATING BUGS ===

	/// <summary>Rotate the player's EYE angles through the portal on teleport.</summary>
	[Property, Group( "Debug" )] public bool ApplyEyeRotation { get; set; } = true;

	/// <summary>Rotate the player's VELOCITY through the portal on teleport.</summary>
	[Property, Group( "Debug" )] public bool ApplyVelocityRotation { get; set; } = true;

	/// <summary>Preserve lateral offset (entry point) in the emerge position.</summary>
	[Property, Group( "Debug" )] public bool ApplyLateralOffset { get; set; } = true;

	/// <summary>Mirror the lateral offset (Portal-gun style). If false, pass-through.</summary>
	[Property, Group( "Debug" )] public bool MirrorLateral { get; set; } = true;

	/// <summary>Apply a counter-Punch to cancel CharacterController internal state.</summary>
	[Property, Group( "Debug" )] public bool ApplyCounterPunch { get; set; } = true;

	/// <summary>Snap body rotation immediately instead of letting it lerp.</summary>
	[Property, Group( "Debug" )] public bool SnapBodyRotation { get; set; } = true;

	/// <summary>Manual yaw offset added after portalYawDelta calculation. Try 180, -90, 90 etc. to find the right axis.</summary>
	[Property, Group( "Debug" ), Range( -360, 360 )] public float ExtraYawDelta { get; set; } = 0f;

	/// <summary>
	/// Width and height of the portal opening (used for gizmo visualization).
	/// X = width, Y = height.
	/// </summary>
	[Property] public Vector2 PortalSize { get; set; } = new Vector2( 100f, 160f );

	/// <summary>
	/// Render target resolution divider. 1 = full screen, 2 = half, 4 = quarter.
	/// Lower = cheaper but blurrier.
	/// </summary>
	[Property, Range( 1, 4 )] public int RenderResolution { get; set; } = 1;

	/// <summary>
	/// Override the portal camera's far clip plane. 0 = match the main camera.
	/// Crank this up if you can't see distant geometry through the portal.
	/// </summary>
	[Property] public float PortalZFar { get; set; } = 0f;

	/// <summary>
	/// Override the portal camera's near clip plane. 0 = match the main camera.
	/// Smaller = see things closer to the exit portal surface.
	/// </summary>
	[Property] public float PortalZNear { get; set; } = 0f;

	/// <summary>
	/// FOV offset added to the main camera's FOV for the portal view.
	/// Positive = wider (see more), negative = narrower (zoom in).
	/// </summary>
	[Property, Range( -30, 30 )] public float FovOffset { get; set; } = 0f;

	/// <summary>
	/// Tag for this portal's surface renderer. The linked portal's camera excludes
	/// this tag so it doesn't render its own surface (which would cause recursion).
	/// Set one portal to "portal_a" and the other to "portal_b".
	/// </summary>
	[Property] public string PortalTag { get; set; } = "portal_a";

	/// <summary>
	/// Which local axis points "through" the portal (the direction players walk
	/// through). Default is Forward (+X). If your portal surface is rotated and
	/// the view appears tilted, try changing this to Up or another axis.
	/// The portal transform and teleportation both use this as the plane normal.
	/// </summary>
	[Property] public Vector3 PortalNormal { get; set; } = Vector3.Forward;

	// --- Internal state ---

	private CameraComponent _portalCam;
	private Renderer _surface;
	private Texture _renderTarget;
	private readonly Dictionary<GameObject, float> _prevDist = new();
	private bool _cameraCreated;

	// ───────────────────────────────────────────────
	//  LIFECYCLE
	// ───────────────────────────────────────────────

	protected override void OnEnabled()
	{
		// Tag ourselves AND the surface GameObject so the linked portal's camera
		// actually excludes the surface (tags don't inherit to children in sbox).
		// Without the surface tag, the portal camera would see the opposite
		// portal's surface quad from behind, causing visual corruption at close range.
		Tags.Add( PortalTag );

		_surface = Components.Get<Renderer>( FindMode.EverythingInSelfAndDescendants );
		if ( _surface.IsValid() )
			_surface.GameObject.Tags.Add( PortalTag );

		CreatePortalCamera();
		CreateTriggerCollider();
	}

	/// <summary>
	/// Create a box trigger collider sized to the portal opening. Oriented along
	/// <see cref="PortalNormal"/> with depth = <see cref="TeleportRadius"/> so
	/// approaching objects are detected before crossing the plane.
	/// </summary>
	private void CreateTriggerCollider()
	{
		// Skip if one already exists (placed manually in the editor)
		if ( Components.Get<Collider>( FindMode.EverythingInSelfAndDescendants ) is not null )
			return;

		var col = Components.Create<BoxCollider>();
		col.IsTrigger = true;

		// Size: width × height × depth along the portal normal
		var normal = PortalNormal.Normal;
		var portalRot = Rotation.LookAt( normal, Vector3.Up );

		// BoxCollider scale is in local space of this GameObject.
		// We set Scale = (depth, width, height) rotated into the portal frame.
		col.Scale = new Vector3( TeleportRadius, PortalSize.x, PortalSize.y );

		// If PortalNormal isn't the default Forward, the box needs to be rotated
		// to match. Use a child or set center offset.
		col.Center = Vector3.Zero;

		col.Tags.Add( "trigger" );
		col.Tags.Add( PortalTag );
	}

	protected override void OnDisabled()
	{
		_prevDist.Clear();
	}

	/// <summary>
	/// Create a secondary camera as a child GameObject — same pattern as PlaneReflection.cs.
	/// This camera renders from the linked portal's perspective each frame.
	/// </summary>
	private void CreatePortalCamera()
	{
		if ( _cameraCreated ) return;
		_cameraCreated = true;

		var camGo = new GameObject( GameObject, true, "Portal Camera" )
		{
			Flags = GameObjectFlags.Absolute // world-space positioning, not local
		};

		_portalCam = camGo.AddComponent<CameraComponent>( true );
		_portalCam.Priority = -100;
		_portalCam.IsMainCamera = false;
		_portalCam.Enabled = false; // we drive it manually in OnPreRender

		// Exclude the linked portal's surface tag so we don't render it
		// (prevents infinite recursion: A renders B's view which renders A's view...)
		_portalCam.RenderExcludeTags.Add( "debugoverlay" );
	}

	// ───────────────────────────────────────────────
	//  RENDERING — runs every frame before the scene draws
	// ───────────────────────────────────────────────

	private bool _errorLogged;

	// Track the player across frames after a teleport to see if something moves them.
	private RoguelitePlayer _teleportedPlayer;
	private Vector3 _teleportedExpectedPos;
	private int _teleportedFrameCount = -1;

	// Track each tracked entity's position frame-to-frame so we can compute their
	// ACTUAL velocity (CC.Velocity lies). Updated every OnUpdate.
	private readonly Dictionary<GameObject, Vector3> _lastPositions = new();

	protected override void OnPreRender()
	{
		try
		{
			DoPreRender();
		}
		catch ( System.Exception ex )
		{
			if ( !_errorLogged )
			{
				_errorLogged = true;
				Log.Error( $"[Portal {GameObject.Name}] OnPreRender exception: {ex.Message}\n{ex.StackTrace}" );
			}
		}
	}

	private void DoPreRender()
	{
		if ( !LinkedPortal.IsValid() || !_surface.IsValid() || _portalCam is null )
			return;

		var mainCam = Scene.Camera;
		if ( mainCam is null ) return;

		// --- Visibility gate ---
		// Keep rendering while the player is close to the portal even if the
		// dot product briefly flips (happens during pass-through) — otherwise
		// the surface falls back to the void color for a frame and flickers.
		var toPortal = (WorldPosition - mainCam.WorldPosition).Normal;
		var dot = Vector3.Dot( mainCam.WorldRotation.Forward, toPortal );
		var dist = WorldPosition.Distance( mainCam.WorldPosition );
		var nearPortal = dist < TeleportRadius * 2f;

		if ( (dot < -0.1f && !nearPortal) || dist > MaxRenderDistance )
		{
			_surface.Attributes.Set( "HasReflectionTexture", false );
			return;
		}

		// --- Create or recreate render target when screen size changes ---
		// A stale render target (created at a different resolution) makes the
		// portal view look stretched because its aspect ratio no longer matches
		// the screen. Check each frame and recreate if the dimensions drift.
		var targetW = (int)(Screen.Size.x / RenderResolution.Clamp( 1, 4 ));
		var targetH = (int)(Screen.Size.y / RenderResolution.Clamp( 1, 4 ));
		if ( _renderTarget is null || _renderTarget.Width != targetW || _renderTarget.Height != targetH )
		{
			_renderTarget = Texture.CreateRenderTarget()
				.WithSize( targetW, targetH )
				.WithFormat( ImageFormat.RGBA16161616F )
				.Create( $"portal_rt_{GetHashCode()}" );
		}

		// --- Compute the portal camera's world transform ---
		var portalCamTransform = PortalTransform( mainCam.WorldTransform );
		_portalCam.WorldPosition = portalCamTransform.Position;
		_portalCam.WorldRotation = portalCamTransform.Rotation;

		// Match the main camera's perspective, with optional overrides for tuning
		_portalCam.FieldOfView = mainCam.FieldOfView + FovOffset;
		// Pull znear in aggressively so the scene behind the exit portal still
		// renders correctly when the player is right up against the entry portal.
		// Without this, the portal view can clip to empty for a frame during
		// pass-through and flicker.
		_portalCam.ZNear = PortalZNear > 0f ? PortalZNear : MathF.Min( mainCam.ZNear, 1f );
		_portalCam.ZFar = PortalZFar > 0f ? PortalZFar : mainCam.ZFar;

		// Exclude BOTH portals' surface tags so the portal camera never renders
		// any portal surface — prevents the camera from seeing the destination
		// portal's back face (it's right in front of the cam at B's location)
		// and also prevents feedback loops.
		_portalCam.RenderExcludeTags.Set( LinkedPortal.PortalTag, true );
		_portalCam.RenderExcludeTags.Set( PortalTag, true );

		// Keep the camera DISABLED so it's not part of the main render pipeline.
		// Use RenderToTexture for an explicit, off-pipeline capture that only
		// writes to our texture without taking over the main display.
		_portalCam.Enabled = false;

		// Build ViewSetup from the directly computed transform rather than reading
		// back from _portalCam.WorldTransform — that read can be stale if transform
		// updates are batched, causing "wrong camera perspective" snapping.
		var viewSetup = new Sandbox.Rendering.ViewSetup
		{
			Transform = portalCamTransform,
			FieldOfView = _portalCam.FieldOfView,
			ZNear = _portalCam.ZNear,
			ZFar = _portalCam.ZFar,
			EnablePostprocessing = true,
		};

		_portalCam.RenderToTexture( _renderTarget, viewSetup );

		// Pipe the texture to the renderer. Screen-space UV in the shader samples
		// this render target 1:1 with the portal's screen position.
		_surface.Attributes.Set( "HasReflectionTexture", true );
		_surface.Attributes.Set( "ReflectionTexture", _renderTarget );
	}

	// ───────────────────────────────────────────────
	//  TELEPORTATION — plane-crossing detection
	// ───────────────────────────────────────────────

	protected override void OnFixedUpdate()
	{
		if ( !Networking.IsHost ) return;
		if ( !LinkedPortal.IsValid() ) return;

		// Teleport detection runs in FixedUpdate so it's in lockstep with the
		// CharacterController's physics tick (which runs in FixedUpdate too).
		// Doing it in OnUpdate causes timing mismatch — velocity writes don't
		// stick because the CC processes motion between our write and the next
		// render frame.
		DoTeleportCheck();
	}

	private void DoTeleportCheck()
	{

		// Post-teleport tracking: log player position for 3 frames after teleport
		if ( _teleportedFrameCount >= 0 && _teleportedPlayer.IsValid() )
		{
			var actualPos = _teleportedPlayer.WorldPosition;
			var cc = _teleportedPlayer.Components.Get<CharacterController>();
			var vel = cc.IsValid() ? cc.Velocity : Vector3.Zero;
			var eyeYaw = _teleportedPlayer.EyeAngles.yaw;
			var eyeForward = _teleportedPlayer.EyeAngles.ToRotation().Forward;
			var analogMove = Input.AnalogMove;

			var wishVel = _teleportedPlayer.WishVelocity;
			Log.Info( $"[Portal POST-TELEPORT frame {_teleportedFrameCount}] pos={actualPos} vel={vel} wishVel={wishVel} eyeYaw={eyeYaw:F2} eyeFwd={eyeForward} AnalogMove={analogMove} delta={actualPos - _teleportedExpectedPos}" );

			_teleportedFrameCount++;
			if ( _teleportedFrameCount > 3 )
			{
				_teleportedFrameCount = -1;
				_teleportedPlayer = null;
			}
		}

		// Build the portal's local basis so we can test position within the
		// RECTANGULAR opening (not just a sphere). This prevents lateral triggers
		// when the player walks past the side of the portal without going through.
		var planeTransform = GetPortalPlaneTransform();

		// Check all players within teleport radius
		foreach ( var player in Scene.GetAllComponents<RoguelitePlayer>() )
		{
			if ( player.IsDead ) continue;

			// Track position frame-over-frame so we can compute actual velocity
			// at teleport time. cc.Velocity lies; position deltas don't.
			_lastPositions.TryGetValue( player.GameObject, out var prevPos );
			_lastPositions[player.GameObject] = player.WorldPosition;

			if ( !TryGetPortalLocalOffset( planeTransform, player.WorldPosition, out var localOffset ) )
			{
				_prevDist.Remove( player.GameObject );
				continue;
			}

			// local.x = signed distance along the portal normal (through direction)
			// local.y = right along portal, local.z = up along portal
			var signedDist = localOffset.x;

			if ( _prevDist.TryGetValue( player.GameObject, out var prev ) )
			{
				if ( prev > 0f && signedDist <= 0f )
				{
					// Measured velocity from last frame's position delta — this is
					// the REAL velocity the player is moving at, regardless of
					// what cc.Velocity reports.
					var measuredVel = (prevPos != Vector3.Zero)
						? (player.WorldPosition - prevPos) / Time.Delta
						: Vector3.Zero;
					TeleportPlayer( player, measuredVel );
					continue;
				}
			}

			_prevDist[player.GameObject] = signedDist;
		}

		// Projectiles: same rectangle-clamped plane-crossing check
		foreach ( var proj in Scene.GetAllComponents<ProjectileBase>() )
		{
			if ( !TryGetPortalLocalOffset( planeTransform, proj.WorldPosition, out var localOffset ) )
			{
				_prevDist.Remove( proj.GameObject );
				continue;
			}

			var signedDist = localOffset.x;

			if ( _prevDist.TryGetValue( proj.GameObject, out var prev ) )
			{
				if ( prev > 0f && signedDist <= 0f )
				{
					TeleportObject( proj.GameObject );
					_prevDist.Remove( proj.GameObject );
					LinkedPortal._prevDist[proj.GameObject] = -TeleportRadius;
					continue;
				}
			}

			_prevDist[proj.GameObject] = signedDist;
		}
	}

	/// <summary>
	/// Tests whether a world-space point is within the portal's rectangular
	/// activation volume. Returns the portal-local offset if so: x = through,
	/// y = right, z = up (all in portal local coordinates).
	/// </summary>
	private bool TryGetPortalLocalOffset( Transform planeTransform, Vector3 worldPos, out Vector3 localOffset )
	{
		localOffset = planeTransform.PointToLocal( worldPos );

		// x = along portal normal (through direction): must be within TeleportRadius depth
		if ( MathF.Abs( localOffset.x ) > TeleportRadius ) return false;

		// y/z = within the portal opening rectangle (half-width, half-height)
		if ( MathF.Abs( localOffset.y ) > PortalSize.x * 0.5f ) return false;
		if ( MathF.Abs( localOffset.z ) > PortalSize.y * 0.5f ) return false;

		return true;
	}

	private void TeleportPlayer( RoguelitePlayer player, Vector3 measuredVel )
	{
		// ====================================================================
		// STEP-BY-STEP PORTAL TRANSFER — toggle DebugSteps on the component to
		// log each intermediate value so you can verify the math visually.
		// ====================================================================

		// === STEP 1: Capture current player state ===
		// measuredVel is the TRUE velocity (computed from position delta last frame),
		// because cc.Velocity lies about the CC's internal motion state.
		var oldPos = player.WorldPosition;
		var oldAngles = player.EyeAngles;
		var cc0 = player.Components.Get<CharacterController>();
		var ccReportedVel = cc0.IsValid() ? cc0.Velocity : Vector3.Zero;
		var oldWishVel = player.WishVelocity;
		if ( DebugSteps ) Log.Info( $"[STEP 1 CAPTURE] pos={oldPos}, eye={oldAngles}, ccVel={ccReportedVel}, measuredVel={measuredVel}, wishVel={oldWishVel}" );

		// === STEP 2: Compute source & destination cyan directions ===
		var srcNormalWorld = (WorldRotation * PortalNormal.Normal).Normal;
		var dstNormalWorld = (LinkedPortal.WorldRotation * LinkedPortal.PortalNormal.Normal).Normal;
		var horizontalCyan = dstNormalWorld.WithZ( 0f );
		if ( horizontalCyan.LengthSquared < 0.01f )
			horizontalCyan = Vector3.Forward;
		horizontalCyan = horizontalCyan.Normal;
		if ( DebugSteps ) Log.Info( $"[STEP 2 CYAN] srcNormal={srcNormalWorld}, dstNormal={dstNormalWorld}, horizontalCyan={horizontalCyan}" );

		// === STEP 3: Compute emerge position (lateral offset + entry depth push) ===
		var srcPlane = GetPortalPlaneTransform();
		var dstPlane = LinkedPortal.GetPortalPlaneTransform();
		var localOffset = srcPlane.PointToLocal( player.WorldPosition );
		// Toggleable: lateral offset preservation (if off, emerge at dst center).
		// Toggleable: mirror Y (Portal-gun style) vs pass-through.
		var lateralLocalY = ApplyLateralOffset ? (MirrorLateral ? -localOffset.y : localOffset.y) : 0f;
		var lateralLocalZ = ApplyLateralOffset ? localOffset.z : 0f;
		var lateralWorld = dstPlane.Rotation * new Vector3( 0f, lateralLocalY, lateralLocalZ );
		// Emerge at the SAME depth we entered — if we were 3 units past A's plane,
		// pop out 3 units past B's plane. This keeps the portal camera's rendered
		// view (implicitly computed from ~A's-plane) visually consistent with
		// where we end up. EmergeOffset is the minimum safety buffer.
		var entryDepth = MathF.Max( MathF.Abs( localOffset.x ), EmergeOffset );
		var emergePosition = new Vector3(
			LinkedPortal.WorldPosition.x + horizontalCyan.x * entryDepth + lateralWorld.x,
			LinkedPortal.WorldPosition.y + horizontalCyan.y * entryDepth + lateralWorld.y,
			player.WorldPosition.z
		);
		if ( DebugSteps ) Log.Info( $"[STEP 3 POSITION] localOffset={localOffset}, entryDepth={entryDepth}, lateralWorld={lateralWorld}, emergePos={emergePosition}" );

		// === STEP 4: Compute yaw rotation delta ===
		var srcCyan = srcNormalWorld.WithZ( 0f );
		if ( srcCyan.LengthSquared < 0.01f ) srcCyan = Vector3.Forward;
		srcCyan = srcCyan.Normal;
		var srcYawDeg = MathF.Atan2( srcCyan.y, srcCyan.x ) * 180f / MathF.PI;
		var dstYawDeg = MathF.Atan2( horizontalCyan.y, horizontalCyan.x ) * 180f / MathF.PI;
		// Toggleable: apply the standard portal yaw delta + user-tunable extra offset
		var portalYawDelta = (dstYawDeg - srcYawDeg - 180f) + ExtraYawDelta;
		var newYaw = ApplyEyeRotation ? oldAngles.yaw + portalYawDelta : oldAngles.yaw;
		while ( newYaw > 180f ) newYaw -= 360f;
		while ( newYaw < -180f ) newYaw += 360f;
		var newAngles = new Angles( oldAngles.pitch, newYaw, 0f );
		if ( DebugSteps ) Log.Info( $"[STEP 4 YAW] srcYaw={srcYawDeg:F1}°, dstYaw={dstYawDeg:F1}°, portalYawDelta={portalYawDelta:F1}° (incl ExtraYaw={ExtraYawDelta}), newEye={newAngles}" );

		// === STEP 5: Compute rotated velocity (what SHOULD happen to momentum) ===
		// Use MEASURED velocity (from position delta), not cc.Velocity which lies.
		var velRot = Rotation.FromYaw( portalYawDelta );
		var redirectedVel = ApplyVelocityRotation ? (velRot * measuredVel) : measuredVel;
		if ( DebugSteps ) Log.Info( $"[STEP 5 VELOCITY] measuredVel={measuredVel}, velRot yaw={portalYawDelta:F1}°, redirectedVel={redirectedVel} (length={redirectedVel.Length:F1})" );

		// === STEP 6: Apply everything to the player ===
		player.WorldPosition = emergePosition;
		player.EyeAngles = newAngles;

		var cc = player.Components.Get<CharacterController>();
		if ( cc.IsValid() )
		{
			// Cancel the CC's hidden internal motion by Punching the NEGATIVE of
			// the measured velocity (the real motion the player has). Then Punch
			// in the redirected direction to inject new momentum.
			if ( ApplyCounterPunch )
			{
				cc.Punch( -measuredVel );
				cc.Velocity = Vector3.Zero;
			}
			if ( redirectedVel.Length > 1f )
				cc.Punch( redirectedVel );
			if ( DebugSteps ) Log.Info( $"[STEP 6 APPLY] Punched counter={-measuredVel}, Punched redirect={redirectedVel}, finalVel={cc.Velocity}" );
		}

		// Toggleable: snap body rotation immediately
		if ( SnapBodyRotation )
		{
			var body = player.Components.Get<SkinnedModelRenderer>( FindMode.EverythingInSelfAndDescendants );
			if ( body.IsValid() )
				body.WorldRotation = new Angles( 0, newAngles.yaw, 0 ).ToRotation();
		}

		// Force the main camera to snap to the new eye/rotation IMMEDIATELY so
		// the upcoming OnPreRender (portal texture rendering) and final scene
		// render use the post-teleport camera. Without this, if OnPreRender or
		// the main render read Scene.Camera BEFORE RoguelitePlayer.OnUpdate runs
		// its UpdateCamera, they'd see the camera at the pre-teleport position
		// for one frame — which is exactly "previous view for a frame."
		var mainCam = Scene.Camera;
		if ( mainCam.IsValid() )
		{
			var eyeHeight = player.EyeHeight;
			mainCam.WorldPosition = emergePosition + Vector3.Up * eyeHeight;
			mainCam.WorldRotation = newAngles.ToRotation();
		}

		// Seed linked portal's tracking so the emerge doesn't re-trigger next frame
		LinkedPortal._prevDist[player.GameObject] = -TeleportRadius;

		// Track player for post-teleport frame diagnostics
		_teleportedPlayer = player;
		_teleportedExpectedPos = emergePosition;
		_teleportedFrameCount = 0;

		Log.Info( $"[Portal TELEPORT] {GameObject.Name} → {LinkedPortal.GameObject.Name}" );
	}

	private void TeleportObject( GameObject obj )
	{
		var destTransform = PortalTransform( obj.WorldTransform );
		var destNormal = (LinkedPortal.WorldRotation * LinkedPortal.PortalNormal.Normal).Normal;

		// Push past the exit plane so the object doesn't clip into the destination
		obj.WorldPosition = destTransform.Position - destNormal * EmergeOffset;
		obj.WorldRotation = destTransform.Rotation;

		// Transform rigidbody velocity if present
		var rb = obj.Components.Get<Rigidbody>();
		if ( rb.IsValid() )
			rb.Velocity = PortalDirection( rb.Velocity );
	}

	// ───────────────────────────────────────────────
	//  PORTAL TRANSFORM MATH
	// ───────────────────────────────────────────────

	/// <summary>
	/// Build the effective world transform for this portal using <see cref="PortalNormal"/>
	/// as the forward direction. This decouples the portal's "through" direction from the
	/// root GameObject's orientation — so the user can set PortalNormal to match however
	/// their surface mesh is oriented (e.g. Vector3.Up for a horizontal plane model).
	///
	/// Critical: avoids the Rotation.LookAt singularity when PortalNormal is parallel to
	/// world Up (e.g. a floor/ceiling portal). When the normal is nearly vertical we use
	/// Vector3.Forward as the up-reference instead, which keeps the rotation stable as
	/// the player moves.
	/// </summary>
	private Transform GetPortalPlaneTransform()
	{
		var worldNormal = (WorldRotation * PortalNormal.Normal).Normal;

		// Pick an up-reference that isn't parallel to the normal. When the portal
		// is roughly horizontal (floor/ceiling), world Up collapses with normal —
		// fall back to world Forward so the rotation stays numerically stable.
		var upRef = MathF.Abs( worldNormal.z ) > 0.99f ? Vector3.Forward : Vector3.Up;

		var rot = Rotation.LookAt( worldNormal, upRef );
		return new Transform( WorldPosition, rot, 1f );
	}

	/// <summary>
	/// Transform a world-space Transform through this portal to the linked portal.
	/// World → source-local → 180° yaw flip → destination-world.
	///
	/// The flip is the key: "what's in front of source appears behind destination."
	/// Entering the front of this portal = exiting the back of the linked one.
	/// </summary>
	public Transform PortalTransform( Transform input )
	{
		var srcPlane = GetPortalPlaneTransform();
		var dstPlane = LinkedPortal.GetPortalPlaneTransform();

		// 1. World → source-local
		var local = srcPlane.ToLocal( input );

		// 2. 180° yaw flip — negate X (forward) and Y (right), keep Z (up).
		//    This is equivalent to Rotation.FromYaw(180) applied to position + rotation.
		local.Position = new Vector3( -local.Position.x, -local.Position.y, local.Position.z );
		local.Rotation = Rotation.FromYaw( 180f ) * local.Rotation;

		// 3. Destination-local → world
		return dstPlane.ToWorld( local );
	}

	/// <summary>
	/// Transform a world-space direction vector through the portal. Same flip as
	/// <see cref="PortalTransform"/> but for directions (no position component).
	/// Used for velocity, look direction, etc.
	/// </summary>
	public Vector3 PortalDirection( Vector3 worldDir )
	{
		var srcPlane = GetPortalPlaneTransform();
		var dstPlane = LinkedPortal.GetPortalPlaneTransform();

		// To local direction
		var localDir = srcPlane.NormalToLocal( worldDir );

		// Flip
		localDir = new Vector3( -localDir.x, -localDir.y, localDir.z );

		// To world direction at destination
		return dstPlane.NormalToWorld( localDir );
	}

	// ───────────────────────────────────────────────
	//  EDITOR GIZMOS
	// ───────────────────────────────────────────────

	protected override void DrawGizmos()
	{
		var normal = PortalNormal.Normal;
		var portalRot = Rotation.LookAt( normal, Vector3.Up );
		var hw = PortalSize.x * 0.5f;
		var hh = PortalSize.y * 0.5f;

		// Draw the portal opening as a thin box oriented perpendicular to the normal.
		// The box is in local space, so we rotate the gizmo transform to match.
		Gizmo.Draw.Color = new Color( 0.3f, 0.1f, 0.8f ).WithAlpha( Gizmo.IsSelected ? 0.5f : 0.15f );

		// Build corners in portal-local space (forward = normal, right = width, up = height)
		var r = portalRot.Right * hw;
		var u = portalRot.Up * hh;
		var thickness = normal * 2f;

		// Solid quad face
		Gizmo.Draw.SolidBox( new BBox(
			-r - u - thickness,
			r + u + thickness
		) );

		// Wireframe outline when selected
		if ( Gizmo.IsSelected )
		{
			Gizmo.Draw.Color = new Color( 0.5f, 0.2f, 1f );
			Gizmo.Draw.LineThickness = 2f;
			Gizmo.Draw.LineBBox( new BBox( -r - u - thickness, r + u + thickness ) );
		}

		// Draw the "through" direction arrow
		Gizmo.Draw.Color = Color.Cyan;
		Gizmo.Draw.Arrow( Vector3.Zero, normal * 60f, 8f, 4f );

		// Draw link line to the paired portal
		if ( LinkedPortal.IsValid() && Gizmo.IsSelected )
		{
			Gizmo.Transform = global::Transform.Zero;
			Gizmo.Draw.Color = Color.Yellow.WithAlpha( 0.4f );
			Gizmo.Draw.Line( WorldPosition, LinkedPortal.WorldPosition );
		}
	}
}
