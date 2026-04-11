/// <summary>
/// Sword weapon with 3-hit combo, parry, blade wave, and piercing dash.
/// Combo multipliers: 1x → 0.8x → 1.6x. Combo resets after ComboWindow expires.
/// </summary>
[Title( "Sword" )]
[Icon( "content_cut" )]
public sealed class SwordWeapon : WeaponBase
{
	[Property] public float ComboWindow { get; set; } = 0.8f;
	[Property] public float ParryDuration { get; set; } = 0.4f;
	[Property] public float MeleeRange { get; set; } = 120f;
	[Property] public float MeleeRadius { get; set; } = 16f;

	public int CurrentComboStep => _comboStep;
	private int _comboStep;
	private float _comboTimer;
	private bool _parryActive;

	public SwordWeapon()
	{
		BaseDamage = 35f;
		Category = WeaponCategory.Sword;
	}

	public override void PrimaryAttack()
	{
		if ( !IsCooldownReady( "swing" ) ) return;

		// Advance or reset combo
		_comboStep = _comboTimer > 0 ? (_comboStep + 1) % 3 : 0;
		_comboTimer = ComboWindow;

		var dmgMult = GetComboMultiplier( _comboStep );
		var knockback = GetComboKnockback( _comboStep );
		var attack = BuildAttack( dmgMult, DamageType.Slash,
			canKnockback: true, knockbackForce: knockback.Force );

		// Melee trace
		var results = DamageResolver.MeleeTrace( Owner, attack, MeleeRange, MeleeRadius );

		// Apply directional knockback per combo step
		foreach ( var result in results )
		{
			if ( result.FinalDamage <= 0 ) continue;

			var hitObj = result.Context.ImpactPoint != Vector3.Zero
				? result.Context.ImpactPoint
				: Vector3.Zero;

			// Find the enemy we hit and push them
			ApplyComboKnockback( _comboStep, result );
		}

		StartCooldown( "swing", GetComboDelay( _comboStep ) );

		BroadcastSwing( _comboStep );
	}

	public override void SecondaryAttack()
	{
		if ( Input.Down( "attack2" ) && !_parryActive )
		{
			StartParry();
		}
		else if ( !Input.Down( "attack2" ) && _parryActive )
		{
			EndParry();
		}
	}

	public override void WeaponAbility( int index )
	{
		switch ( index )
		{
			case 0 when IsCooldownReady( "blade_wave" ):
				FireBladeWave();
				break;
			case 1 when IsCooldownReady( "pierce_dash" ):
				PiercingDash();
				break;
		}
	}

	protected override void OnWeaponTick()
	{
		if ( _comboTimer > 0 )
			_comboTimer -= Time.Delta;
		else
			_comboStep = 0;
	}

	private void StartParry()
	{
		_parryActive = true;
		// TODO Phase 4: Add ParryStatus effect to Owner
	}

	private void EndParry()
	{
		_parryActive = false;
		// TODO Phase 4: Remove ParryStatus, optionally fire blade wave on perfect parry
	}

	private void FireBladeWave()
	{
		// TODO Phase 5: Spawn BladeWaveProjectile
		var attack = BuildAttack( 0.6f, DamageType.Slash );
		DamageResolver.MeleeTrace( Owner, attack, MeleeRange * 3f, MeleeRadius * 2f );
		StartCooldown( "blade_wave", 6f );
	}

	private void PiercingDash()
	{
		if ( Owner is null ) return;

		var attack = BuildAttack( 1.4f, DamageType.Pierce );

		Owner.DashForward( 400f, ( tr ) =>
		{
			if ( tr.GameObject is not null )
			{
				var ctx = HitContext.FromTrace( tr, Owner );
				DamageResolver.Resolve( attack, Owner, tr.GameObject, ctx );
			}
		} );

		StartCooldown( "pierce_dash", 8f );
	}

	[Rpc.Broadcast]
	private void BroadcastSwing( int comboStep )
	{
		// All clients play swing animation/sound for this combo step
		// TODO: Hook up animation and sound
	}

	/// <summary>
	/// Apply directional knockback based on combo step:
	/// Hit 1: slight push to the right
	/// Hit 2: slight push to the left
	/// Hit 3: big upward launch
	/// </summary>
	private void ApplyComboKnockback( int step, DamageResult result )
	{
		if ( Owner is null ) return;

		var lookRot = Owner.EyeAngles.ToRotation();
		var forward = lookRot.Forward;
		var right = lookRot.Right;

		Vector3 knockDir;
		if ( step == 2 )
		{
			// Big hit — up and back at a random angle within a half-cone
			var randomAngle = Random.Shared.NextSingle() * MathF.PI; // 0 to 180 degrees
			var randomSpread = Random.Shared.NextSingle() * 0.4f + 0.1f; // 0.1 to 0.5 lateral spread
			var lateral = right * MathF.Cos( randomAngle ) * randomSpread;
			knockDir = (forward * 0.7f + Vector3.Up * 0.7f + lateral).Normal;
		}
		else
		{
			knockDir = step switch
			{
				0 => (forward * 0.6f + right * 0.4f).Normal,    // slight right
				1 => (forward * 0.6f - right * 0.4f).Normal,    // slight left
				_ => forward
			};
		}

		var force = GetComboKnockback( step ).Force;

		// Find the enemy we hit and apply knockback through their component
		var eyePos = Owner.WorldPosition + Vector3.Up * 64f;
		var tr = Owner.Scene.Trace
			.Ray( eyePos, eyePos + lookRot.Forward * MeleeRange )
			.Size( MeleeRadius )
			.IgnoreGameObjectHierarchy( Owner.GameObject )
			.WithoutTags( "trigger" )
			.Run();

		if ( tr.Hit && tr.GameObject is not null )
		{
			var enemy = tr.GameObject.Components.Get<RogueliteEnemyBase>( FindMode.EverythingInSelfAndDescendants );
			enemy?.ApplyKnockback( knockDir, force );
		}
	}

	private (float Force, float) GetComboKnockback( int step ) => step switch
	{
		0 => (30f, 0f),    // light tap
		1 => (35f, 0f),    // light tap other direction
		2 => (40f, 0f),    // big hit (halved)
		_ => (20f, 0f)
	};

	private float GetComboMultiplier( int step ) => step switch
	{
		0 => 1f,
		1 => 0.8f,
		2 => 1.6f,
		_ => 1f
	};

	private float GetComboDelay( int step ) => step switch
	{
		0 => 0.4f,
		1 => 0.35f,
		2 => 0.7f,
		_ => 0.4f
	};
}
