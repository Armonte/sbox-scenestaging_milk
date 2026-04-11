/// <summary>
/// Geometric context about a hit — where it landed, angle, backstab/headshot detection.
/// Built from a SceneTraceResult via the static factory method.
/// </summary>
public readonly struct HitContext
{
	public readonly Vector3 Origin;
	public readonly Vector3 Direction;
	public readonly Vector3 ImpactPoint;
	public readonly Vector3 ImpactNormal;
	public readonly float Distance;
	public readonly bool IsBackstab;
	public readonly bool IsHeadshot;

	/// <summary>
	/// True when the trace hit a hitbox (ManualHitbox/ModelHitboxes), false when it
	/// hit a plain physics collider. Lets diagnostics distinguish the two cases
	/// without having to probe .Tags (which NREs on default-struct hitboxes).
	/// </summary>
	public readonly bool HitHitbox;

	/// <summary>
	/// Tag set from the hitbox that was hit, if the trace used hitboxes and hit one.
	/// Null when no hitbox was involved. Use <see cref="HasHitboxTag"/> for lookups.
	/// </summary>
	public readonly ITagSet HitboxTags;

	public HitContext(
		Vector3 origin,
		Vector3 direction,
		Vector3 impactPoint,
		Vector3 impactNormal,
		float distance,
		bool isBackstab,
		bool isHeadshot,
		ITagSet hitboxTags = null,
		bool hitHitbox = false )
	{
		Origin = origin;
		Direction = direction;
		ImpactPoint = impactPoint;
		ImpactNormal = impactNormal;
		Distance = distance;
		IsBackstab = isBackstab;
		IsHeadshot = isHeadshot;
		HitboxTags = hitboxTags;
		HitHitbox = hitHitbox;
	}

	public bool HasHitboxTag( string tag )
	{
		return HitboxTags is not null && HitboxTags.Has( tag );
	}

	/// <summary>
	/// Build a HitContext from a scene trace result. Automatically detects backstab and headshot.
	/// If the trace was run with <c>.UseHitboxes()</c>, also captures the hitbox tag set.
	/// </summary>
	public static HitContext FromTrace( SceneTraceResult tr, Component attacker )
	{
		var isBackstab = false;
		var isHeadshot = false;

		if ( tr.GameObject is not null )
		{
			// Backstab: attack direction aligns with target's forward (we're hitting their back)
			// Dot of (target forward) and (attack direction) > 0.5 means attacker is behind
			var targetForward = tr.GameObject.WorldRotation.Forward.WithZ( 0 ).Normal;
			var attackDir = tr.Direction.WithZ( 0 ).Normal;
			var dot = Vector3.Dot( targetForward, attackDir );
			isBackstab = dot > 0.5f; // Attacker's direction matches target's facing = hitting from behind

			// Headshot: impact point is near the top of the target
			var targetPos = tr.GameObject.WorldPosition;
			var heightAboveCenter = tr.HitPosition.z - targetPos.z;
			isHeadshot = heightAboveCenter > 60f;
		}

		// tr.Hitbox is a struct — when the trace hit geometry without a hitbox (world,
		// a collider with no ModelHitboxes/ManualHitbox attached, etc.) its .Tags getter
		// dereferences uninitialized state and NREs, so guard it. Non-hitbox hits just
		// end up with null tags and HasHitboxTag returns false cleanly.
		ITagSet hitboxTags = null;
		var hitHitbox = false;
		try
		{
			hitboxTags = tr.Hitbox.Tags;
			hitHitbox = hitboxTags is not null;
		}
		catch { /* trace didn't produce a hitbox — leave flags at default */ }

		return new HitContext(
			tr.StartPosition,
			tr.Direction,
			tr.HitPosition,
			tr.Normal,
			tr.Distance,
			isBackstab,
			isHeadshot,
			hitboxTags,
			hitHitbox );
	}
}
