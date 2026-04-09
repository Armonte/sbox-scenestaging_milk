using Sandbox;
using Sandbox.Rendering;

public enum ScreenProjectionMode
{
	Rectilinear = 0,
	Panini = 1,
	Equirectangular = 2,
	FisheyeStereographic = 3,
	FisheyeEquisolid = 4
}

[Title( "Screenspace Projection" )]
[Category( "Post Processing" )]
[Icon( "panorama_wide_angle" )]
public sealed class PaniniProjection : BasePostProcess<PaniniProjection>
{
	/// <summary>
	/// Projection model.
	/// Rectilinear: preserves straight lines.
	/// Panini: preserves vertical straight lines (and central horizontals).
	/// Equirectangular: uniform lat/lon sampling.
	/// Fisheye Stereographic: preserves angles (conformal).
	/// Fisheye Equisolid: preserves solid angle (equal area).
	/// </summary>
	[Property]
	public ScreenProjectionMode ProjectionMode { get; set; } = ScreenProjectionMode.Panini;

	/// <summary>
	/// Panini: D parameter (0..1). Other modes: blend with rectilinear (0 = rectilinear, 1 = full).
	/// </summary>
	[Property, Range( 0, 1 )]
	public float Strength { get; set; } = 0.5f;

	/// <summary>
	/// Auto-zoom to remove empty corners. 0 = none, 1 = minimal crop.
	/// </summary>
	[Property, Range( 0, 1 )]
	public float Fill { get; set; } = 1.0f;

	/// <summary>
	/// Panini vertical compensation for very wide angles.
	/// </summary>
	[Property, Range( -1, 1 )]
	public float PaniniVerticalCompensation { get; set; } = 0.0f;

	public override void Render()
	{
		float strength = GetWeighted( x => x.Strength );
		int mode = (int)ProjectionMode;

		// Rectilinear with any strength, or zero strength on other modes = no effect
		if ( mode == 0 || strength.AlmostEqual( 0.0f ) ) return;

		float fill = GetWeighted( x => x.Fill );
		float paniniS = GetWeighted( x => x.PaniniVerticalCompensation );

		// Get camera FOV
		float fovDeg = Camera?.FieldOfView ?? 90.0f;
		float halfTan = MathF.Tan( 0.5f * fovDeg * MathF.PI / 180.0f );

		// Compute aspect ratio and max rectilinear extents
		// S&Box uses vertical FOV by default
		float aspect = (Camera?.ScreenRect.Width ?? 1920f) / (Camera?.ScreenRect.Height ?? 1080f);
		float maxRectX = halfTan * aspect;
		float maxRectY = halfTan;

		// Compute fill zoom on CPU
		float screenZoom = ComputeFillZoom( mode, maxRectX, maxRectY, strength, paniniS, aspect, fill );

		Attributes.Set( "projection_mode", (float)mode );
		Attributes.Set( "strength", strength );
		Attributes.Set( "panini_s", paniniS );
		Attributes.Set( "screen_zoom", screenZoom );
		Attributes.Set( "max_rect_x", maxRectX );
		Attributes.Set( "max_rect_y", maxRectY );
		Attributes.Set( "aspect", aspect );

		var shader = Material.FromShader( "shaders/postprocess/panini_projection.shader" );
		if ( shader is null ) return;

		Blit( BlitMode.WithBackbuffer( shader, Stage.AfterPostProcess, 200, false ), "PaniniProjection" );
	}

	private float ComputeFillZoom( int mode, float maxRectX, float maxRectY, float strength, float paniniS, float aspect, float fill )
	{
		if ( fill <= 0.0f ) return 1.0f;

		if ( mode == 1 ) // Panini
		{
			float d = strength;
			if ( d <= 0.0f ) return 1.0f;

			float lonMax = MathF.Atan( maxRectX );
			float dp1 = d + 1.0f;
			float denom = d + MathF.Cos( lonMax );
			float k = dp1 / denom;
			float xEdgeP = MathF.Abs( k * MathF.Sin( lonMax ) );

			if ( xEdgeP <= 0.0f ) return 1.0f;
			float fitX = xEdgeP / maxRectX;
			return 1.0f / MathX.Lerp( 1.0f, fitX, fill );
		}
		else // Other modes: binary search
		{
			if ( CornerOobRatio( mode, maxRectX, maxRectY, strength, paniniS, aspect, 1.0f ) <= 1.0f )
				return 1.0f;

			float lo = 1.0f;
			float hi = 1.0f;

			for ( int i = 0; i < 6; i++ )
			{
				hi *= 2.0f;
				if ( CornerOobRatio( mode, maxRectX, maxRectY, strength, paniniS, aspect, hi ) <= 1.0f )
					break;
			}

			for ( int i = 0; i < 8; i++ )
			{
				float mid = 0.5f * (lo + hi);
				if ( CornerOobRatio( mode, maxRectX, maxRectY, strength, paniniS, aspect, mid ) > 1.0f )
					lo = mid;
				else
					hi = mid;
			}

			return MathX.Lerp( 1.0f, hi, fill );
		}
	}

	private float CornerOobRatio( int mode, float maxRectX, float maxRectY, float strength, float paniniS, float aspect, float zoom )
	{
		float worst = 1.0f;
		float invZ = 1.0f / MathF.Max( zoom, 1e-8f );

		Vector2[] corners = { new( -1, -1 ), new( 1, -1 ), new( -1, 1 ), new( 1, 1 ) };

		foreach ( var corner in corners )
		{
			var screenXY = corner * invZ;
			var ray = FinalViewRay( screenXY, mode, new Vector2( maxRectX, maxRectY ), strength, paniniS, aspect );

			if ( ray.z > 0.0f )
			{
				float rx = MathF.Abs( ray.x / ray.z ) / maxRectX;
				float ry = MathF.Abs( ray.y / ray.z ) / maxRectY;
				float ratio = MathF.Max( rx, ry );
				worst = MathF.Max( worst, ratio );
			}
			else
			{
				worst = MathF.Max( worst, 1e9f );
			}
		}

		return worst;
	}

	private Vector3 FinalViewRay( Vector2 screenXY, int mode, Vector2 maxRect, float strength, float paniniS, float aspect )
	{
		var projectedXY = screenXY * maxRect;
		var rect = new Vector3( projectedXY.x, projectedXY.y, 1.0f ).Normal;

		Vector3 proj;
		if ( mode == 1 ) proj = PaniniInverseS( projectedXY, strength, paniniS );
		else if ( mode == 2 ) proj = RayEquirectangular( screenXY, maxRect );
		else if ( mode == 3 ) proj = RayFisheye( screenXY, maxRect, aspect, true );
		else if ( mode == 4 ) proj = RayFisheye( screenXY, maxRect, aspect, false );
		else return rect;

		if ( mode == 2 )
		{
			// Equirectangular blends in UV space
			var uvR = new Vector2( rect.x, rect.y ) / MathF.Max( rect.z, 1e-8f );
			var uvP = new Vector2( proj.x, proj.y ) / MathF.Max( proj.z, 1e-8f );
			var blended = Vector2.Lerp( uvR, uvP, strength );
			return new Vector3( blended.x, blended.y, 1.0f );
		}

		if ( mode == 1 ) return proj;
		return Vector3.Lerp( rect, proj, strength );
	}

	private Vector3 PaniniInverseS( Vector2 projXY, float d, float s )
	{
		float dp1 = d + 1.0f;
		float k = (projXY.x * projXY.x) / (dp1 * dp1);
		float disc = k * k * d * d - (k + 1.0f) * (k * d * d - 1.0f);
		if ( disc <= 0.0f ) return new Vector3( projXY.x, projXY.y, 1.0f ).Normal;

		float cLon = (-k * d + MathF.Sqrt( disc )) / (k + 1.0f);
		float S = dp1 / (d + cLon);
		float lat = MathF.Atan2( projXY.y, S );
		float lon = MathF.Atan2( projXY.x, S * cLon );
		float cosLat = MathF.Cos( lat );
		var ray = new Vector3( cosLat * MathF.Sin( lon ), MathF.Sin( lat ), cosLat * MathF.Cos( lon ) );

		if ( ray.z > 0.0f )
		{
			float q = ray.x / ray.z;
			q = (q * q) / (dp1 * dp1);
			ray = ray.WithY( ray.y * MathX.Lerp( 1.0f, 1.0f / MathF.Sqrt( 1.0f + q ), s ) );
		}

		return ray;
	}

	private Vector3 RayEquirectangular( Vector2 screenXY, Vector2 maxRect )
	{
		float maxLat = MathF.Atan( maxRect.y );
		float maxLon = MathF.Atan( maxRect.x );
		float lat = screenXY.y * maxLat;
		float lon = screenXY.x * maxLon;
		float cosLat = MathF.Cos( lat );
		return new Vector3( cosLat * MathF.Sin( lon ), MathF.Sin( lat ), cosLat * MathF.Cos( lon ) );
	}

	private Vector3 RayFisheye( Vector2 screenXY, Vector2 maxRect, float aspect, bool stereographic )
	{
		var p = new Vector2( screenXY.x * aspect, screenXY.y );
		float r = p.Length;
		if ( r < 1e-8f ) return new Vector3( 0, 0, 1 );

		float thetaMax = MathF.Min( MathF.Atan( maxRect.x ), MathF.Atan( maxRect.y ) );
		float theta = stereographic
			? 2.0f * MathF.Atan( r * MathF.Tan( 0.5f * thetaMax ) )
			: 2.0f * MathF.Asin( Math.Clamp( r * MathF.Sin( 0.5f * thetaMax ), 0.0f, 1.0f ) );

		return new Vector3( (p.x / r) * MathF.Sin( theta ), (p.y / r) * MathF.Sin( theta ), MathF.Cos( theta ) ).Normal;
	}
}
