/// <summary>
/// Static utility for hit detection shapes. All methods return GameObjects that were hit,
/// with optional faction filtering. Use these instead of rolling your own traces.
/// </summary>
public static class HitDetection
{
	/// <summary>
	/// Find all GameObjects within a sphere. Filters out the attacker's hierarchy.
	/// </summary>
	public static List<GameObject> Sphere( Scene scene, Vector3 center, float radius,
		GameObject ignore = null, string[] excludeTags = null )
	{
		var results = new List<GameObject>();
		var seen = new HashSet<GameObject>();

		excludeTags ??= new[] { "trigger", "projectile" };

		// Use a series of traces in a sphere pattern
		// S&Box doesn't have a native overlap sphere, so we trace from center outward
		var directions = GetSphereDirections( 26 );
		foreach ( var dir in directions )
		{
			var tr = scene.Trace
				.Ray( center, center + dir * radius )
				.Size( radius * 0.3f )
				.WithoutTags( excludeTags )
				.Run();

			if ( ignore is not null )
			{
				if ( tr.GameObject is not null && tr.GameObject.IsDescendant( ignore ) )
					continue;
				if ( tr.GameObject == ignore )
					continue;
			}

			if ( tr.Hit && tr.GameObject is not null && seen.Add( tr.GameObject ) )
				results.Add( tr.GameObject );
		}

		// Also check all components in range directly
		foreach ( var health in scene.GetAllComponents<RogueliteHealthComponent>() )
		{
			if ( health.IsDead ) continue;
			var obj = health.GameObject;
			if ( obj == ignore ) continue;
			if ( obj.WorldPosition.Distance( center ) <= radius && seen.Add( obj ) )
				results.Add( obj );
		}

		return results;
	}

	/// <summary>
	/// Ray/capsule trace from origin in a direction. Standard projectile/melee hit.
	/// </summary>
	public static SceneTraceResult Ray( Scene scene, Vector3 from, Vector3 to,
		float radius = 0f, GameObject ignore = null )
	{
		var trace = scene.Trace
			.Ray( from, to )
			.WithoutTags( "trigger", "projectile" );

		if ( radius > 0 )
			trace = trace.Size( radius );

		if ( ignore is not null )
			trace = trace.IgnoreGameObjectHierarchy( ignore );

		return trace.Run();
	}

	/// <summary>
	/// Find all GameObjects in a cone from origin looking in a direction.
	/// </summary>
	public static List<GameObject> Cone( Scene scene, Vector3 origin, Vector3 direction,
		float range, float halfAngleDegrees, GameObject ignore = null )
	{
		var results = new List<GameObject>();
		var cosHalfAngle = MathF.Cos( halfAngleDegrees * MathF.PI / 180f );

		foreach ( var health in scene.GetAllComponents<RogueliteHealthComponent>() )
		{
			if ( health.IsDead ) continue;
			var obj = health.GameObject;
			if ( obj == ignore ) continue;

			var toTarget = obj.WorldPosition - origin;
			var dist = toTarget.Length;
			if ( dist > range || dist < 0.1f ) continue;

			var dot = Vector3.Dot( toTarget.Normal, direction.Normal );
			if ( dot >= cosHalfAngle )
				results.Add( obj );
		}

		return results;
	}

	/// <summary>
	/// Find all GameObjects in a box volume.
	/// </summary>
	public static List<GameObject> Box( Scene scene, BBox bounds, GameObject ignore = null )
	{
		var results = new List<GameObject>();

		foreach ( var health in scene.GetAllComponents<RogueliteHealthComponent>() )
		{
			if ( health.IsDead ) continue;
			var obj = health.GameObject;
			if ( obj == ignore ) continue;

			if ( bounds.Contains( obj.WorldPosition ) )
				results.Add( obj );
		}

		return results;
	}

	private static Vector3[] GetSphereDirections( int count )
	{
		// 26 directions: 6 axes + 12 edges + 8 corners
		return new[]
		{
			Vector3.Forward, Vector3.Backward, Vector3.Left, Vector3.Right, Vector3.Up, Vector3.Down,
			(Vector3.Forward + Vector3.Left).Normal, (Vector3.Forward + Vector3.Right).Normal,
			(Vector3.Backward + Vector3.Left).Normal, (Vector3.Backward + Vector3.Right).Normal,
			(Vector3.Forward + Vector3.Up).Normal, (Vector3.Forward + Vector3.Down).Normal,
			(Vector3.Backward + Vector3.Up).Normal, (Vector3.Backward + Vector3.Down).Normal,
			(Vector3.Left + Vector3.Up).Normal, (Vector3.Left + Vector3.Down).Normal,
			(Vector3.Right + Vector3.Up).Normal, (Vector3.Right + Vector3.Down).Normal,
			(Vector3.Forward + Vector3.Left + Vector3.Up).Normal, (Vector3.Forward + Vector3.Right + Vector3.Up).Normal,
			(Vector3.Forward + Vector3.Left + Vector3.Down).Normal, (Vector3.Forward + Vector3.Right + Vector3.Down).Normal,
			(Vector3.Backward + Vector3.Left + Vector3.Up).Normal, (Vector3.Backward + Vector3.Right + Vector3.Up).Normal,
			(Vector3.Backward + Vector3.Left + Vector3.Down).Normal, (Vector3.Backward + Vector3.Right + Vector3.Down).Normal,
		};
	}
}
