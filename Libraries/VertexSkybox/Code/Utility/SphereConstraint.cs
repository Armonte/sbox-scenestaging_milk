
namespace Sandbox;

/// <summary>
/// Utility for constraining points to a sphere surface
/// and performing ray-sphere intersection.
/// </summary>
public static class SphereConstraint
{
	/// <summary>
	/// Project a point onto the surface of a sphere centered at the origin.
	/// </summary>
	public static Vector3 ProjectOntoSphere( Vector3 point, float radius )
	{
		float len = point.Length;
		if ( len < 0.0001f )
			return new Vector3( 0, 0, radius );

		return point * (radius / len);
	}

	/// <summary>
	/// Intersect a ray with a sphere centered at the origin.
	/// Returns true if hit, with the nearest intersection point.
	/// </summary>
	public static bool RaySphereIntersect( Ray ray, float radius, out Vector3 hitPoint )
	{
		hitPoint = Vector3.Zero;

		var origin = ray.Position;
		var dir = ray.Forward.Normal;

		float a = Vector3.Dot( dir, dir );
		float b = 2f * Vector3.Dot( origin, dir );
		float c = Vector3.Dot( origin, origin ) - radius * radius;

		float discriminant = b * b - 4f * a * c;

		if ( discriminant < 0f )
			return false;

		float sqrtD = MathF.Sqrt( discriminant );
		float t1 = (-b - sqrtD) / (2f * a);
		float t2 = (-b + sqrtD) / (2f * a);

		// Take the nearest positive intersection
		float t;
		if ( t1 > 0.001f )
			t = t1;
		else if ( t2 > 0.001f )
			t = t2;
		else
			return false;

		hitPoint = origin + dir * t;
		return true;
	}

	/// <summary>
	/// Intersect a ray with a sphere centered at a given position.
	/// </summary>
	public static bool RaySphereIntersect( Ray ray, Vector3 sphereCenter, float radius, out Vector3 hitPoint )
	{
		// Transform ray to sphere-local space
		var localRay = new Ray( ray.Position - sphereCenter, ray.Forward );
		if ( RaySphereIntersect( localRay, radius, out hitPoint ) )
		{
			hitPoint += sphereCenter;
			return true;
		}
		return false;
	}
}
