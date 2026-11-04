namespace PlayerModels.Core.Script;

public static partial class PresetScripts
{
	/// <summary>A streak behind each foot as you run, and sparks kicked up while moving.</summary>
	public const string SpeedTrail = """
		-- Speed trail: a streak behind each foot as you run, and sparks kicked up while moving.

		[Parameter( "Trail color", Group = "Look", Tip = "The colour a streak starts as" )]
		local trailColor = Color.Parse( "#8af4ff" )

		[Parameter( "Trail length", Min = 0.1, Max = 5, Step = 0.05, Group = "Look", Tip = "How long a streak stays behind, in seconds" )]
		local trailLength = 0.7

		Effect.Name = "Speed trail"
		Effect.Joint = Joint.Hips

		-- A streak from each foot, showing only as much as the foot moves
		local leftTrail = Trail()
		leftTrail.Joint = Joint.LeftFoot
		leftTrail.Offset = Vector3( 0, 0, 1 )
		leftTrail.Color = trailColor
		leftTrail.ColorEnd = Color.Parse( "#2a5aff" )
		leftTrail.Size = 5
		leftTrail.SizeEnd = 1
		leftTrail.Lifetime = trailLength
		leftTrail.FadeWithSpeed = true

		local rightTrail = Trail()
		rightTrail.Joint = Joint.RightFoot
		rightTrail.Offset = Vector3( 0, 0, 1 )
		rightTrail.Color = trailColor
		rightTrail.ColorEnd = Color.Parse( "#2a5aff" )
		rightTrail.Size = 5
		rightTrail.SizeEnd = 1
		rightTrail.Lifetime = trailLength
		rightTrail.FadeWithSpeed = true

		-- Sparks kicked up from each foot, only while moving
		local leftSparks = Sprite()
		leftSparks.Joint = Joint.LeftFoot
		leftSparks.ComesFrom = ComesFrom.Sphere
		leftSparks.Spread = 2
		leftSparks.LooksLike = Picture.Spark
		leftSparks.Color = Color.Parse( "#d8fcff" )
		leftSparks.ColorEnd = Color.Parse( "#3a8aff" )
		leftSparks.Size = 1.6
		leftSparks.SizeEnd = 0.4
		leftSparks.Speed = 30
		leftSparks.Gravity = 60
		leftSparks.Lifetime = 0.35
		leftSparks.Amount = 30
		leftSparks.When = When.Moving

		local rightSparks = Sprite()
		rightSparks.Joint = Joint.RightFoot
		rightSparks.ComesFrom = ComesFrom.Sphere
		rightSparks.Spread = 2
		rightSparks.LooksLike = Picture.Spark
		rightSparks.Color = Color.Parse( "#d8fcff" )
		rightSparks.ColorEnd = Color.Parse( "#3a8aff" )
		rightSparks.Size = 1.6
		rightSparks.SizeEnd = 0.4
		rightSparks.Speed = 30
		rightSparks.Gravity = 60
		rightSparks.Lifetime = 0.35
		rightSparks.Amount = 30
		rightSparks.When = When.Moving
		""";
}
