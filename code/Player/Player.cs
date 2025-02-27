﻿namespace Grubs;

public partial class Player : Entity
{
	[Net]
	public IList<Grub> Grubs { get; private set; }

	[Net]
	public Grub ActiveGrub { get; private set; }

	[Net]
	public IList<Gadget> Gadgets { get; private set; }

	[Net]
	public string SteamName { get; private set; }

	[Net]
	public long SteamId { get; private set; }

	/// <summary>
	/// The clothing the player joined the game with.
	/// </summary>
	[Net]
	public string AvatarClothingData { get; private set; }

	public int RoundsDisconnected { get; private set; }

	public bool IsDead => Grubs.All( grub => grub.LifeState == LifeState.Dead );
	public int GetTotalGrubHealth => (int)Grubs.Sum( g => g.Health );
	public int GetHealthPercentage => GetTotalGrubHealth / GrubsConfig.GrubCount;

	public bool IsAvailableForTurn => !IsDead && !IsDisconnected;

	[BindComponent]
	public Inventory Inventory { get; }

	public GrubsCamera GrubsCamera { get; } = new();

	public bool IsTurn
	{
		get
		{
			return GamemodeSystem.Instance.ActivePlayer == this && !GamemodeSystem.Instance.TurnIsChanging;
		}
	}

	public bool IsDisconnected => !Client.IsValid();

	public Player()
	{
		Transmit = TransmitType.Always;
	}

	public Player( IClient client ) : this()
	{
		SteamName = client.Name;
		SteamId = client.SteamId;
		SaveClientClothes( client );
	}

	public override void ClientSpawn()
	{
		if ( IsLocalPawn )
		{
			SelectedCosmeticIndex = -1;
			PopulateGrubNames();

			_ = new UI.TurnBobber();
		}
	}

	public override void Spawn()
	{
		Tags.Add( Tag.IgnoreReset );

		Components.Create<Inventory>();
	}

	public override void Simulate( IClient client )
	{
		Inventory.Simulate( client );
		Grubs.Simulate( client );
		Gadgets.Simulate( client );

		if ( IsTurn )
			ActiveGrub?.UpdateInputFromOwner( MoveInput, LookInput );
	}

	public override void FrameSimulate( IClient client )
	{
		GrubsCamera.FrameSimulate( client );

		foreach ( var grub in Grubs )
		{
			grub?.FrameSimulate( client );
		}
	}

	public void Respawn()
	{
		RoundsDisconnected = 0;
		Inventory.Clear();
		Inventory.GiveDefaultLoadout();

		Grubs.Clear();
		CreateGrubs();
	}

	public void HandleLateJoin()
	{
		if ( !GrubsConfig.SpawnLateJoiners )
			return;

		Inventory.Clear();
		Inventory.GiveDefaultLoadout();

		var grubNames = string.IsNullOrEmpty( GrubNames ) ? new List<string>() : System.Text.Json.JsonSerializer.Deserialize<List<string>>( GrubNames );
		var grub = new Grub( this )
		{
			Owner = this,
			Name = ParseGrubName( Game.Random.FromList( grubNames ) ),
			Position = GrubsGame.Instance.Terrain.FindSpawnLocation( traceDown: false )
		};

		grub.Components.Create<LateJoinMechanic>();
		Grubs.Add( grub );
		ActiveGrub = grub;
	}

	private void CreateGrubs()
	{
		var grubNames = string.IsNullOrEmpty( GrubNames ) ? new List<string>() : System.Text.Json.JsonSerializer.Deserialize<List<string>>( GrubNames );
		for ( int i = 0; i < GrubsConfig.GrubCount; i++ )
		{
			Grubs.Add( new Grub( this ) { Owner = this, Name = ParseGrubName( grubNames.ElementAtOrDefault( i ) ) } );
		}

		ActiveGrub = Grubs.First();
	}

	public void PickNextGrub()
	{
		RotateGrubs();

		while ( ActiveGrub.LifeState is LifeState.Dead or LifeState.Dying )
		{
			// hack
			if ( Grubs.All( grub => grub.LifeState == LifeState.Dead ) )
			{
				if ( GamemodeSystem.Instance is not FreeForAll gamemode )
				{
					return;
				}

				gamemode.RotateActivePlayer();
			}

			RotateGrubs();
		}
	}

	private void RotateGrubs()
	{
		var current = Grubs[0];
		current.EyeRotation = Rotation.Identity;
		Log.Info( $"Rotating Grub for {Client.Name} - Current: {current}" );

		Grubs.RemoveAt( 0 );
		Grubs.Add( current );

		ActiveGrub = Grubs[0];
	}

	public void EndTurn()
	{
		if ( !ActiveGrub.IsValid() || !ActiveGrub.ActiveWeapon.IsValid() )
			return;

		if ( Inventory.ActiveWeapon.IsCharging() )
			Inventory.ActiveWeapon.Fire();

		Inventory.SetActiveWeapon( null, true );
	}

	private void SaveClientClothes( IClient client )
	{
		var clothes = new ClothingContainer();
		clothes.LoadFromClient( client );

		clothes.Clothing.RemoveAll(
			c => c.Category is not Clothing.ClothingCategory.Hair
			and not Clothing.ClothingCategory.Hat
			and not Clothing.ClothingCategory.Facial
			and not Clothing.ClothingCategory.Skin
		);

		AvatarClothingData = clothes.Serialize();
	}

	private string ParseGrubName( string name )
	{
		return string.IsNullOrWhiteSpace( name ) ? Random.Shared.FromList( GrubNamePresets ) : name.Trim();
	}

	[GrubsEvent.Game.RoundPassed]
	private void OnRoundPass()
	{
		if ( IsDisconnected )
			RoundsDisconnected += 1;
	}
}
