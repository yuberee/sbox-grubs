namespace Grubs;

public partial class EverybodyGrubs : Gamemode
{
	public override string GamemodeName => "Everybody Grubs";
	public override bool AllowFriendlyFire => true;
	public override int MinimumPlayers => GrubsConfig.MinimumPlayers;

	public enum GameState
	{
		Waiting,
		Playing,
		GameOver
	}

	public override string GetGameStateLabel()
	{
		return CurrentState switch
		{
			GameState.Waiting => "Waiting for game to begin",
			GameState.Playing => "Game in progress",
			GameState.GameOver => "Game is over",
			_ => null,
		};
	}

	[Net] public GameState CurrentState { get; set; }

	/// <summary>
	/// The amount of time before all players get their choice locked in forcefully
	/// </summary>
	[Net]
	public TimeUntil TimeUntilForcedLockIn { get; set; }

	/// <summary>
	/// Whether we have started the game or not.
	/// </summary>
	[Net]
	public bool Started { get; set; } = false;

	public override float GetTimeRemaining() => TimeUntilForcedLockIn;

	internal override void Initialize()
	{
		base.Initialize();

		CurrentState = GameState.Waiting;
	}

	/// <summary>
	/// Spawn a Player and its Grubs for each client.
	/// Then, set the active players.
	/// </summary>
	private void SpawnPlayers()
	{
		foreach ( var client in Game.Clients )
		{
			if ( client.Pawn is not Player player )
				continue;

			player.Respawn();
			Players.Add( player );

			MoveToSpawnpoint( client );

			ActivePlayers.Add( player );
		}
	}
}
