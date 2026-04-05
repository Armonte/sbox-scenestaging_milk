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

	public HitContext(
		Vector3 origin,
		Vector3 direction,
		Vector3 impactPoint,
		Vector3 impactNormal,
		float distance,
		bool isBackstab,
		bool isHeadshot )
	{
		Origin = origin;
		Direction = direction;
		ImpactPoint = impactPoint;
		ImpactNormal = impactNormal;
		Distance = distance;
		IsBackstab = isBackstab;
		IsHeadshot = isHeadshot;
	}

	/// <summary>
	/// Build a HitContext from a scene trace result. Automatically detects backstab and headshot.
	/// </summary>
	public static HitContext FromTrace( SceneTraceResult tr, Component attacker )
	{
		var isBackstab = false;
		var isHeadshot = false;

		if ( tr.GameObject is not null )
		{
			// Backstab: only if attacker is clearly behind the target (dot > 0.7)
			// Disabled for now — enemies always face the player so it triggers randomly
			isBackstab = false;

			// Headshot: impact point is near the top of the target
			var targetPos = tr.GameObject.WorldPosition;
			var heightAboveCenter = tr.HitPosition.z - targetPos.z;
			isHeadshot = heightAboveCenter > 60f;
		}

		return new HitContext(
			tr.StartPosition,
			tr.Direction,
			tr.HitPosition,
			tr.Normal,
			tr.Distance,
			isBackstab,
			isHeadshot );
	}
}
