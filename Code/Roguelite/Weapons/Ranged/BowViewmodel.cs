/// <summary>
/// Scene-side marker component for the bow viewmodel. Lives on the GameObject
/// that holds the overlay-rendered bow mesh (typically a child of the main camera).
///
/// <see cref="BowWeapon"/> is dynamically created on the player at runtime, so
/// its [Property] slots can't hold scene references. Instead the weapon finds
/// this component via <c>Scene.GetAllComponents&lt;BowViewmodel&gt;()</c> on equip
/// and calls <see cref="SetVisible"/> / reads <see cref="NockPoint"/> from here.
/// </summary>
[Title( "Bow Viewmodel" )]
[Icon( "visibility" )]
public sealed class BowViewmodel : Component
{
	/// <summary>
	/// Child GameObject positioned at the arrow nock on the bowstring. Arrows
	/// spawn at this point's world position so their projectile flight starts
	/// past the visible overlay bow geometry — no more bow-on-top-of-arrow.
	/// </summary>
	[Property] public GameObject NockPoint { get; set; }

	/// <summary>
	/// Optional ready-arrow mesh visible while the bow is held and not firing.
	/// Scale-tweened from 0 → original over <see cref="NockFadeDuration"/> seconds
	/// after each shot, so the next arrow "pops in" instead of snapping on.
	/// </summary>
	[Property] public GameObject NockedArrowVisual { get; set; }

	/// <summary>
	/// How long the nocked-arrow scale-in tween takes. Typically driven by the
	/// weapon's fire cooldown via <see cref="StartNockedArrowFade"/>.
	/// </summary>
	[Property] public float NockFadeDuration { get; set; } = 0.3f;

	// --- Draw animation ---
	/// <summary>
	/// How far to pull the nocked arrow backward at full draw, in bow-viewmodel
	/// local space. Negative X = pulled toward the shooter if your bow is
	/// modeled pointing along +X. Tune per bow model in the inspector.
	/// </summary>
	[Property, Group( "Draw Animation" )]
	public Vector3 DrawPullOffset { get; set; } = new Vector3( -12f, 0f, 0f );

	/// <summary>
	/// Extra local rotation applied to the whole bow at full draw — adds a
	/// subtle tilt so the weapon feels like it's under tension. Zero by
	/// default because proper bone animation does most of the work; dial
	/// a small value in if you want extra camera-feel on top.
	/// </summary>
	[Property, Group( "Draw Animation" )]
	public Angles DrawBowTilt { get; set; } = Angles.Zero;

	/// <summary>
	/// Per-bone rotation offsets applied at full draw. Each entry names a bone
	/// on the bow's <see cref="SkinnedModelRenderer"/> and the LocalRotation
	/// offset to add when <c>_currentDrawProgress == 1</c>. Rest pose is
	/// captured at OnStart, so tune these in the inspector until the bend looks
	/// right — don't touch the model's own rest pose.
	/// </summary>
	// Yaw is the working axis for this rig's limbs — found empirically, per-bone
	// amplitudes tuned so the bow reads as "under tension" at full draw. If you
	// swap in a different bow model, re-tune these in the inspector.
	[Property, Group( "Draw Animation" )]
	public List<BoneDrawEntry> DrawBones { get; set; } = new()
	{
		// Inner limbs — the main visible bend toward the shooter
		new BoneDrawEntry { BoneName = "Bow_L_1",   DrawRotation = new Angles( 0f, -45f, 0f ) },
		new BoneDrawEntry { BoneName = "Bow_R_1",   DrawRotation = new Angles( 0f,  45f, 0f ) },
		// Outer limbs — smaller additive bend on the tip for a natural curve
		new BoneDrawEntry { BoneName = "Bow_L_2",   DrawRotation = new Angles( 0f, -15f, 0f ) },
		new BoneDrawEntry { BoneName = "Bow_R_2",   DrawRotation = new Angles( 0f,  15f, 0f ) },
		// String attachment points — usually follow the limbs for free
		new BoneDrawEntry { BoneName = "String_L_1", DrawRotation = Angles.Zero },
		new BoneDrawEntry { BoneName = "String_R_1", DrawRotation = Angles.Zero },
		// String mid-segments — tune these if the string doesn't visibly pull
		new BoneDrawEntry { BoneName = "String_L_2", DrawRotation = Angles.Zero },
		new BoneDrawEntry { BoneName = "String_R_2", DrawRotation = Angles.Zero },
	};

	/// <summary>
	/// Designer-facing entry for a single draw-animated bone.
	/// </summary>
	public class BoneDrawEntry
	{
		[Property] public string BoneName { get; set; } = "";
		[Property] public Angles DrawRotation { get; set; } = Angles.Zero;
	}

	// Cached list so SetVisible doesn't re-scan the hierarchy every frame.
	// Using the Renderer base class so we catch ModelRenderer, SkinnedModelRenderer,
	// and anything else derived. List is lazy-populated by EnsureRenderers() so this
	// still works if SetVisible fires before OnStart (e.g. weapon equip on scene load).
	private List<Renderer> _renderers;

	// Scale + position tween state for the nocked-arrow animations.
	private Vector3 _nockedArrowOriginalScale = Vector3.One;
	private Vector3 _nockedArrowOriginalPosition;
	private bool _nockedArrowCached;
	private bool _fadingNockedArrow;
	private float _nockFadeT;

	// Cached resting pose for the bow itself so DrawBowTilt can be layered
	// on top without permanently drifting from the edit-time orientation.
	private Rotation _bowOriginalLocalRotation;
	private bool _bowRotationCached;

	// Current draw progress (0..1), driven every frame by BowWeapon. Used by
	// OnUpdate so the draw pose holds steady instead of snapping each tick.
	private float _currentDrawProgress;

	// Skinned bow + cached bone rest poses. Populated in OnStart once the
	// renderer has been located and CreateBoneObjects has been enabled.
	private SkinnedModelRenderer _skinnedBow;
	private readonly Dictionary<string, CachedBone> _boneRestPoses = new();

	private struct CachedBone
	{
		public GameObject Bone;                    // GameObject wrapper (display-only on some rigs)
		public BoneCollection.Bone BoneRef;        // Authoritative bone reference for SetBoneTransform
		public Transform RestLocalTransform;       // Captured rest pose in local (parent-relative) space
	}

	protected override void OnStart()
	{
		EnsureRenderers();
		CacheNockedArrowTransform();
		CacheBowRotation();
		CacheDrawBones();

		// Start hidden — BowWeapon.OnEquip flips us on when the player actually
		// holds a bow. Keeping the GameObject itself enabled means the scene
		// lookup in BowWeapon can still find us.
		SetVisible( false );
	}

	private bool _cacheResultLogged;

	private void CacheDrawBones()
	{
		_boneRestPoses.Clear();

		// Find the skinned bow renderer. Multiple could theoretically exist
		// (viewmodel + decorative arrow) but the first one should be the bow.
		_skinnedBow = Components.Get<SkinnedModelRenderer>( FindMode.EverythingInSelfAndDescendants );
		if ( _skinnedBow is null ) return;

		// CRITICAL: disable the animgraph. When UseAnimGraph is true (the default),
		// the graph overwrites every bone's transform each frame AFTER we've
		// written our draw pose, so the bow never visibly bends. The bow viewmodel
		// has no real animation — we drive bones manually from code.
		_skinnedBow.UseAnimGraph = false;

		// CreateBoneObjects = true is what makes GetBoneObject(string) return a
		// real GameObject whose LocalRotation we can write to override the mesh.
		// Without this the call returns null and we can't drive bones from C#.
		_skinnedBow.CreateBoneObjects = true;

		if ( DrawBones is null ) return;

		foreach ( var entry in DrawBones )
		{
			if ( string.IsNullOrEmpty( entry.BoneName ) ) continue;

			// Two-pronged lookup: the official API first, then a scene graph fallback.
			// CreateBoneObjects spawns the skeleton as real child GameObjects under
			// the skinned renderer (you saw this as the "Armature" hierarchy in the
			// scene tree), so we can find them by name even if GetBoneObject returns
			// null for timing/initialization reasons.
			var boneGO = _skinnedBow.GetBoneObject( entry.BoneName );
			var source = "GetBoneObject";
			if ( !boneGO.IsValid() )
			{
				boneGO = FindChildByName( GameObject, entry.BoneName );
				source = "FindChildByName";
			}

			if ( !boneGO.IsValid() ) continue;

			// Grab the authoritative Bone reference. BoneCollection.Bone carries
			// the bind-pose LocalTransform from the model definition itself — this
			// is the static, artist-authored rest pose that the mesh was skinned
			// against. Using it (instead of a runtime-captured local) guarantees
			// the mesh renders identically to the editor at identity offset.
			if ( _skinnedBow.Model?.Bones is null )
				continue;
			var boneRef = _skinnedBow.Model.Bones.GetBone( entry.BoneName );
			if ( boneRef.Name is null )
				continue;

			var restLocal = boneRef.LocalTransform;

			// Guard against an uninitialized bone (scale would be zero).
			if ( restLocal.Scale.Length < 0.0001f )
				continue;

			_boneRestPoses[entry.BoneName] = new CachedBone
			{
				Bone = boneGO,
				BoneRef = boneRef,
				RestLocalTransform = restLocal,
			};

		}

		// Log a one-time summary when the cache first lands so the user sees
		// how many bones were matched. Warnings for unmatched entries are
		// emitted alongside so misconfigured bone names are obvious.
		if ( !_cacheResultLogged && _boneRestPoses.Count > 0 )
		{
			_cacheResultLogged = true;
			Log.Info( $"[BowViewmodel] Cached {_boneRestPoses.Count}/{DrawBones.Count} draw bones on {_skinnedBow.GameObject.Name}" );

			foreach ( var entry in DrawBones )
			{
				if ( string.IsNullOrEmpty( entry.BoneName ) ) continue;
				if ( !_boneRestPoses.ContainsKey( entry.BoneName ) )
					Log.Warning( $"[BowViewmodel]   missing bone: '{entry.BoneName}' — check spelling/case against the model's skeleton." );
			}
		}
	}

	protected override void OnUpdate()
	{
		// Retry the bone cache if OnStart caught the model before it was ready.
		// The model can take a frame or two to load after scene start, so we
		// keep trying silently until we actually get bones back.
		if ( _boneRestPoses.Count == 0 && DrawBones.Count > 0 )
			CacheDrawBones();

		// Drive the nocked-arrow fade-in tween. Runs on every client, since the
		// viewmodel is a purely visual object in the local scene.
		if ( _fadingNockedArrow && NockedArrowVisual.IsValid() )
		{
			var duration = MathF.Max( 0.05f, NockFadeDuration );
			_nockFadeT += Time.Delta / duration;

			if ( _nockFadeT >= 1f )
			{
				_nockFadeT = 1f;
				_fadingNockedArrow = false;
				NockedArrowVisual.LocalScale = _nockedArrowOriginalScale;
			}
			else
			{
				// Ease-out cubic — fast start, slow finish. Punchier than linear.
				var u = 1f - _nockFadeT;
				var eased = 1f - u * u * u;
				NockedArrowVisual.LocalScale = _nockedArrowOriginalScale * eased;
			}
		}

		// Apply the current draw pose every frame, unless the fade tween owns
		// the nocked arrow's transform right now. BowWeapon sets _currentDrawProgress
		// via SetDrawProgress; we apply it here so the pose is frame-coherent
		// with the bow's resting rotation layer.
		ApplyDrawPose();
	}

	private void ApplyDrawPose()
	{
		CacheBowRotation();

		// Whole-bow tilt: layered on top of whatever resting rotation the
		// viewmodel was placed at in the editor. Defaults to zero — tune up
		// if you want camera-feel on top of the bone animation.
		if ( _bowRotationCached )
		{
			var tiltRot = Rotation.From(
				DrawBowTilt.pitch * _currentDrawProgress,
				DrawBowTilt.yaw * _currentDrawProgress,
				DrawBowTilt.roll * _currentDrawProgress );
			GameObject.LocalRotation = _bowOriginalLocalRotation * tiltRot;
		}

		// Safety net: force UseAnimGraph = false every frame in case something
		// is re-enabling it. Cheap property set.
		if ( _skinnedBow is not null )
			_skinnedBow.UseAnimGraph = false;

		// Bone-level draw animation — override each configured bone's LOCAL
		// transform via SetBoneTransform. Working in local space means the
		// camera-parent hierarchy handles all world-space positioning for us;
		// we just rotate each bone in its parent frame.
		//
		// At progress=0 this writes the exact rest local transform (no-op),
		// so the bow should be visible at rest. At progress>0, we compose the
		// rotation offset on top of the cached rest.
		foreach ( var entry in DrawBones )
		{
			if ( string.IsNullOrEmpty( entry.BoneName ) ) continue;
			if ( !_boneRestPoses.TryGetValue( entry.BoneName, out var cached ) ) continue;
			if ( _skinnedBow is null ) break;

			var offset = Rotation.Slerp( Rotation.Identity, Rotation.From( entry.DrawRotation ), _currentDrawProgress );
			var newLocalRot = cached.RestLocalTransform.Rotation * offset;
			var newTransform = new Transform(
				cached.RestLocalTransform.Position,
				newLocalRot,
				cached.RestLocalTransform.Scale );

			_skinnedBow.SetBoneTransform( cached.BoneRef, newTransform );
		}

		// Draw-pose diagnostic removed now that the bone pipeline works — we'll
		// add it back if something regresses. Keep the frame-1 heartbeat and
		// cache-attempt logs since they're only a handful of lines total.

		// Nocked arrow pull-back: skip while the fade tween is running so we
		// don't fight its position writes. The fade only changes LocalScale
		// but if we also wrote LocalPosition from both places we'd stutter.
		if ( _fadingNockedArrow ) return;
		if ( !NockedArrowVisual.IsValid() ) return;

		CacheNockedArrowTransform();
		NockedArrowVisual.LocalPosition = _nockedArrowOriginalPosition + DrawPullOffset * _currentDrawProgress;
	}

	/// <summary>
	/// Called every frame by <see cref="BowWeapon"/> with the bow's current
	/// draw progress (0 = released, 1 = fully drawn). Zero when not drawing,
	/// so the arrow + bow snap back to rest automatically on release/fire.
	/// </summary>
	public void SetDrawProgress( float progress )
	{
		_currentDrawProgress = Math.Clamp( progress, 0f, 1f );
	}

	private void EnsureRenderers()
	{
		if ( _renderers is not null ) return;
		_renderers = Components.GetAll<Renderer>( FindMode.EverythingInSelfAndDescendants ).ToList();
	}

	private void CacheNockedArrowTransform()
	{
		if ( _nockedArrowCached ) return;
		if ( !NockedArrowVisual.IsValid() ) return;
		_nockedArrowOriginalScale = NockedArrowVisual.LocalScale;
		_nockedArrowOriginalPosition = NockedArrowVisual.LocalPosition;
		_nockedArrowCached = true;
	}

	private void CacheBowRotation()
	{
		if ( _bowRotationCached ) return;
		_bowOriginalLocalRotation = GameObject.LocalRotation;
		_bowRotationCached = true;
	}

	/// <summary>
	/// Recursive depth-first search for a GameObject with the given name in
	/// <paramref name="root"/>'s descendants. Used as a fallback for bone
	/// lookup when <see cref="SkinnedModelRenderer.GetBoneObject"/> can't
	/// find a bone but the skeleton has been materialized as scene objects.
	/// </summary>
	private static GameObject FindChildByName( GameObject root, string name )
	{
		if ( root is null ) return null;
		foreach ( var child in root.Children )
		{
			if ( child.Name == name ) return child;
			var found = FindChildByName( child, name );
			if ( found is not null ) return found;
		}
		return null;
	}

	private static string DescribePath( GameObject go )
	{
		if ( go is null ) return "(null)";
		var parts = new List<string>();
		var cur = go;
		while ( cur is not null )
		{
			parts.Insert( 0, cur.Name );
			cur = cur.Parent;
		}
		return string.Join( "/", parts );
	}

	public void SetVisible( bool visible )
	{
		EnsureRenderers();
		foreach ( var r in _renderers )
		{
			if ( r.IsValid() )
				r.Enabled = visible;
		}
	}

	/// <summary>
	/// Instantly show/hide the nocked arrow (used on equip/unequip). Also resets
	/// the scale to the cached original so a re-equip doesn't leave a zero-scale
	/// arrow invisible on the string from an in-flight fade tween.
	/// </summary>
	public void SetNockedArrowVisible( bool visible )
	{
		if ( !NockedArrowVisual.IsValid() )
		{
			Log.Info( $"[BowViewmodel] SetNockedArrowVisible({visible}) — 'Nocked Arrow Visual' slot is not wired in the inspector; nothing to toggle." );
			return;
		}

		var renderer = NockedArrowVisual.Components.Get<Renderer>( FindMode.EverythingInSelfAndDescendants );
		if ( renderer is null )
		{
			Log.Warning( $"[BowViewmodel] NockedArrowVisual '{NockedArrowVisual.Name}' has no Renderer component under it." );
			return;
		}

		renderer.Enabled = visible;
		_fadingNockedArrow = false;

		CacheNockedArrowTransform();
		NockedArrowVisual.LocalScale = visible ? _nockedArrowOriginalScale : Vector3.Zero;
	}

	/// <summary>
	/// Start the "next arrow scales in" tween. Called by the weapon immediately
	/// after firing — the renderer stays enabled, LocalScale jumps to zero, and
	/// <see cref="OnUpdate"/> grows it back to the cached original scale over
	/// <paramref name="duration"/> seconds with ease-out cubic.
	/// </summary>
	public void StartNockedArrowFade( float duration )
	{
		if ( !NockedArrowVisual.IsValid() ) return;

		CacheNockedArrowTransform();
		NockFadeDuration = duration;

		// Renderer stays enabled; we just shrink to zero so the tween has
		// somewhere to grow from. Much simpler than alpha fading (which would
		// require a translucent shader the arrow model doesn't have).
		var renderer = NockedArrowVisual.Components.Get<Renderer>( FindMode.EverythingInSelfAndDescendants );
		if ( renderer is not null )
			renderer.Enabled = true;

		NockedArrowVisual.LocalScale = Vector3.Zero;
		_nockFadeT = 0f;
		_fadingNockedArrow = true;
	}
}
