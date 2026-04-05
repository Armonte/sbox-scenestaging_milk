public enum PlayerClass
{
	Warrior,
	Archer,
	Wizard,
	Necromancer,
	Summoner,
	Bard
}

public record ClassProfile( float MaxHp, float Armor, float Speed, int AbilitySlots );

public static class ClassStats
{
	public static ClassProfile Get( PlayerClass c ) => c switch
	{
		PlayerClass.Warrior     => new( MaxHp: 200, Armor: 40, Speed: 260, AbilitySlots: 4 ),
		PlayerClass.Archer      => new( MaxHp: 130, Armor: 10, Speed: 340, AbilitySlots: 4 ),
		PlayerClass.Wizard      => new( MaxHp: 110, Armor: 5,  Speed: 280, AbilitySlots: 4 ),
		PlayerClass.Necromancer => new( MaxHp: 140, Armor: 15, Speed: 270, AbilitySlots: 4 ),
		PlayerClass.Summoner    => new( MaxHp: 120, Armor: 10, Speed: 280, AbilitySlots: 4 ),
		PlayerClass.Bard        => new( MaxHp: 150, Armor: 20, Speed: 310, AbilitySlots: 4 ),
		_ => throw new ArgumentOutOfRangeException( nameof( c ) )
	};
}
