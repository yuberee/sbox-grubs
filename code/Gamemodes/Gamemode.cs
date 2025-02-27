﻿namespace Grubs;

[Category( "Grubs" )]
public partial class Gamemode : Entity
{
	public enum State
	{
		MainMenu,
		Playing,
		GameOver
	}

	public virtual string GamemodeName => "";

	/// <summary>
	/// The players that are actively participating in the game.
	/// </summary>
	[Net]
	public IList<Player> Players { get; set; }

	/// <summary>
	/// Players who were created a pawn and then later disconnected.
	/// </summary>
	protected List<Player> DisconnectedPlayers { get; set; } = new();

	/// <summary>
	/// The CurrentState of the game.
	/// </summary>
	[Net]
	public State CurrentState { get; set; }

	/// <summary>
	/// The active player of the game (if one exists).
	/// </summary>
	[Net]
	public Player ActivePlayer { get; set; }

	/// <summary>
	/// The target we should be following with our camera (if one exists).
	/// </summary>
	[Net]
	public Entity CameraTarget { get; set; }

	/// <summary>
	/// The CSG game world.
	/// </summary>
	public Terrain Terrain => GrubsGame.Instance.Terrain;

	/// <summary>
	/// Whether or not the turn is changing between players.
	/// </summary>
	[Net]
	public bool TurnIsChanging { get; set; }

	/// <summary>
	/// If the <see cref="ActivePlayer"/> has used their turn.
	/// </summary>
	[Net]
	public bool UsedTurn { get; set; }

	/// <summary>
	/// Whether or not we should be allowing movement of players.
	/// </summary>
	[Net]
	public bool AllowMovement { get; set; }

	/// <summary>
	/// A queue containing the order of grubs damaged.
	/// </summary>
	public Queue<Grub> DamageQueue { get; set; } = new();

	/// <summary>
	/// The amount of wind steps that is currently being applied.
	/// </summary>
	[Net]
	public int ActiveWindSteps { get; set; }

	/// <summary>
	/// The calculated wind force we should apply to physics objects.
	/// </summary>
	public float ActiveWindForce => ActiveWindSteps * GrubsConfig.WindForce;

	/// <summary>
	/// The minimum players we need in order to start a game.
	/// </summary>
	public virtual int MinimumPlayers => 2;

	/// <summary>
	/// Is sudden death active?
	/// </summary>
	[Net]
	public bool SuddenDeath { get; set; } = false;

	/// <summary>
	/// How many full rotations of the player list until sudden death is activated?
	/// </summary>
	[Net]
	public int RoundsUntilSuddenDeath { get; set; }

	[Net]
	public int RoundsPassed { get; set; } = 0;

	public override void Spawn()
	{
		Transmit = TransmitType.Always;
	}

	public override void Simulate( IClient client )
	{
		foreach ( var disconnectedPlayer in DisconnectedPlayers )
		{
			disconnectedPlayer.Simulate( client );
		}
	}

	public virtual string GetGameStateLabel()
	{
		return string.Empty;
	}

	public virtual float GetTimeRemaining()
	{
		return -1;
	}

	internal virtual void Initialize() { }

	internal virtual void Start()
	{
		Event.Run( GrubsEvent.Game.Start );
	}

	[GrubsEvent.Game.End]
	internal virtual void IncrementGamesPlayedStat() { }

	internal virtual void OnClientJoined( IClient client )
	{
		var existingPlayer = DisconnectedPlayers.Where( p => p.SteamId == client.SteamId ).FirstOrDefault();
		if ( existingPlayer is not null )
		{
			client.Pawn = existingPlayer;
			DisconnectedPlayers.Remove( existingPlayer );
		}
		else
		{
			var player = new Player( client );
			client.Pawn = player;

			if ( CurrentState == State.Playing )
				OnPlayerJoinedLate( player );
		}
	}

	internal virtual void OnClientDisconnect( IClient cl, NetworkDisconnectionReason reason )
	{
		// We don't care about disconnected players outside of the playing state.
		if ( GamemodeSystem.Instance.CurrentState != Gamemode.State.Playing )
			return;

		if ( cl.Pawn is Player player )
			DisconnectedPlayers.Add( player );
	}

	internal virtual void OnPlayerKilled( Player player ) { }

	internal virtual void OnWeaponDropped( Player player, Weapon weapon ) { }

	internal virtual void MoveToSpawnpoint( IClient client ) { }

	internal virtual async Task SetupTurn()
	{
		Sound.FromScreen( To.Single( ActivePlayer ), "sounds/ui/ui_turn_indicator.sound" );
	}

	internal virtual void UseTurn( bool giveMovementGrace = false ) { }

	internal virtual void OnPlayerJoinedLate( Player player )
	{
		GrubsGame.Instance.TryAssignUnusedColor( player );
	}

	internal virtual Task OnRoundPassed()
	{
		Event.Run( GrubsEvent.Game.RoundPassed );
		RoundsPassed++;

		return Task.CompletedTask;
	}

	[GrubsEvent.Game.Start]
	internal virtual void InitializeSuddenDeath()
	{
		RoundsUntilSuddenDeath = GrubsConfig.SuddenDeathDelay;
		SuddenDeath = false;
	}

	internal async Task CheckSuddenDeath()
	{
		if ( !GrubsConfig.SuddenDeathEnabled )
			return;

		RoundsUntilSuddenDeath -= 1;
		SuddenDeath = RoundsUntilSuddenDeath <= 0;

		if ( !SuddenDeath )
			return;

		if ( RoundsUntilSuddenDeath == 0 )
		{
			UI.TextChat.AddInfoChatEntry( "The earth begins to rumble and shake..." );

			if ( GrubsConfig.SuddenDeathOneHealth )
			{
				foreach ( var grub in All.OfType<Grub>() )
				{
					grub.Health = 1;
				}
			}
		}

		Sound.FromScreen( "suddendeath_rumble" );
		await Terrain.LowerTerrain( GrubsConfig.SuddenDeathAggression );
	}
}
