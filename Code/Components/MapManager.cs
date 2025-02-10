﻿using System;
namespace Observation;

public class MapManager : Component
{
	public static MapManager? Instance { get; private set; }

	[Property, InlineEditor, ReadOnly] public Map? ActiveMap { get; private set; }

	protected override void OnStart()
	{
		Instance = this;
		GameObject.Flags = GameObjectFlags.DontDestroyOnLoad;
		base.OnStart();
	}

	public void SetMap( Map map )
	{
		ActiveMap = map;
		Sandbox.Services.Stats.Increment( $"Plays_{map.Ident}", 1 );
	}

	public void Restart()
	{
		if ( ActiveMap is null )
		{
			return;
		}

		var scene = ActiveMap.Scene;
		if ( scene.IsValid() )
			Scene.Load( scene );

		Sandbox.Services.Stats.Increment( "Restarts", 1 );
		Sandbox.Services.Stats.Increment( $"Restarts_on_map_{ActiveMap.Ident}", 1 );
	}
}

public class Map
{
	public string Ident { get; set; } = string.Empty;
	public string Name { get; set; } = string.Empty;
	public string Description { get; set; } = string.Empty;
	public Difficulty Difficulty { get; set; } = Difficulty.Normal;
	public SceneFile? Scene { get; set; }
	public Observation.Platform.Achievement? WinAchievement { get; set; }
	[Title( "S Rank Achievement" )] public Observation.Platform.Achievement? SRankAchievement { get; set; }

	public Rank? HighestRankAchieved => GetHighestRankAchieved() ?? Rank.F;
	public double? HighestPercentageAchieved => GetHighestPercentageAchieved() ?? 0.0;

	private const string GameIdent = "spoonstuff.observation";

	private Rank? GetHighestRankAchieved()
	{
		var stats = Sandbox.Services.Stats.GetLocalPlayerStats( GameIdent );

		foreach ( var rank in Enum.GetValues<Rank>().OrderBy( r => r ) )
		{
			var statName = $"Wins_on_map_{Ident}_with_rank_{rank}";
			if ( stats.TryGet( statName, out var stat ) && stat.Value > 0 )
			{
				return rank;
			}
		}

		return null;
	}

	private double? GetHighestPercentageAchieved()
	{
		var stats = Sandbox.Services.Stats.GetLocalPlayerStats( GameIdent );

		var statName = $"Success_rate_on_map_{Ident}";
		if ( stats.TryGet( statName, out var stat ) )
		{
			return stat.Max;
		}

		return null;
	}
}
