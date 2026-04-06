/// <summary>
/// Simple runtime visual effects for combat feedback. Creates temporary GameObjects
/// that auto-destroy after a duration. Replace with proper VFX/particles later.
/// </summary>
public static class CombatVfx
{
	/// <summary>
	/// Draw a line between two points for a duration.
	/// </summary>
	public static void Line( Scene scene, Vector3 from, Vector3 to, Color color, float duration = 0.15f, float width = 1f )
	{
		var obj = scene.CreateObject();
		obj.Name = "vfx_line";
		obj.WorldPosition = from;

		var line = obj.Components.Create<LineRenderer>();
		line.UseVectorPoints = true;
		line.VectorPoints = new List<Vector3> { from, to };
		line.Color = new Gradient( new Gradient.ColorFrame( 0, color ), new Gradient.ColorFrame( 1, color ) );
		line.Width = new Curve( new Curve.Frame( 0, width ), new Curve.Frame( 1, width ) );
		line.Opaque = false;

		DestroyAfter( obj, duration );
	}

	/// <summary>
	/// Flash a sphere at a position.
	/// </summary>
	public static void Sphere( Scene scene, Vector3 position, float radius, Color color, float duration = 0.3f )
	{
		var obj = scene.CreateObject();
		obj.Name = "vfx_sphere";
		obj.WorldPosition = position;
		obj.LocalScale = Vector3.One * (radius / 16f); // dev sphere is ~32 units diameter

		var renderer = obj.Components.Create<ModelRenderer>();
		renderer.Model = Model.Load( "models/dev/sphere.vmdl" );
		renderer.Tint = color;

		DestroyAfter( obj, duration );
	}

	/// <summary>
	/// Draw a circle on the ground.
	/// </summary>
	public static void Circle( Scene scene, Vector3 center, float radius, Color color, float duration = 0f, int segments = 32 )
	{
		var obj = scene.CreateObject();
		obj.Name = "vfx_circle";
		obj.WorldPosition = center;

		var line = obj.Components.Create<LineRenderer>();
		line.UseVectorPoints = true;
		line.Opaque = false;

		var points = new List<Vector3>();
		for ( int i = 0; i <= segments; i++ )
		{
			var angle = (float)i / segments * MathF.PI * 2f;
			points.Add( center + new Vector3( MathF.Cos( angle ) * radius, MathF.Sin( angle ) * radius, 2f ) );
		}
		line.VectorPoints = points;
		line.Color = new Gradient( new Gradient.ColorFrame( 0, color ), new Gradient.ColorFrame( 1, color ) );
		line.Width = new Curve( new Curve.Frame( 0, 2f ), new Curve.Frame( 1, 2f ) );

		if ( duration > 0 )
			DestroyAfter( obj, duration );
	}

	/// <summary>
	/// Impact hit marker at a point.
	/// </summary>
	public static void Impact( Scene scene, Vector3 position, Color color, float size = 4f )
	{
		Sphere( scene, position, size, color, 0.2f );
	}

	private static async void DestroyAfter( GameObject obj, float seconds )
	{
		await GameTask.DelayRealtimeSeconds( seconds );
		if ( obj.IsValid() )
			obj.Destroy();
	}
}
