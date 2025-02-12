﻿using System;
using Sandbox.UI;
using Observation.Platform;
using Observation.UI;
using Achievement=Observation.Platform.Achievement;

namespace Observation;

public class GameManager : Component
{
	public static GameManager? Instance { get; private set; }
	[Property] public SceneFile? MenuScene { get; set; }

	private static readonly Dictionary<Rank, int> Thresholds = [];

	protected override void OnStart()
	{
		Instance = this;
		InitializeRankThresholds();
		InitializePlayerData();
		ConfigureAnomalySettings();
		base.OnStart();
	}

	private static void InitializeRankThresholds()
	{
		Thresholds.Clear();
		foreach ( var rank in Enum.GetValues<Rank>() )
		{
			var threshold = rank.GetAttributeOfType<ThresholdAttribute>();
			Thresholds.Add( rank, threshold.MinimumValue );
		}
	}

	private static void InitializePlayerData()
	{
		var playerData = PlayerData.Data;

		if ( playerData is null )
		{
			FileSystem.Data.WriteJson( PlayerData.FileName, new PlayerData() );
			playerData = PlayerData.Data;
		}

		if ( playerData is null )
		{
			return;
		}
		playerData.FirstTime = false;
		playerData.Save();
	}

	public void ConfigureAnomalySettings()
	{
		if ( MapManager.Instance?.ActiveMap is not {} activeMap || AnomalyManager.Instance is not {} anomalyManager )
			return;

		anomalyManager.SetFailReportLimits( activeMap.Difficulty );
	}

	public void EndGameInLoss( LoseReason reason )
	{
		Log.Info( "Game Lost!" );

		GameStatistics.RecordLoss( reason, MapManager.Instance?.ActiveMap, AnomalyManager.Instance );
		ShowGameOverScreen( false, reason );
		PauseGame();
	}

	public void EndGameInWin()
	{
		Log.Info( "Game Win!" );
		var activeMap = MapManager.Instance?.ActiveMap;
		var anomalyManager = AnomalyManager.Instance;

		GameStatistics.RecordWin( activeMap, anomalyManager );
		UnlockWinAchievements( activeMap, anomalyManager );
		ShowGameOverScreen( true );
		PauseGame();
	}

	private static void UnlockWinAchievements( Map? activeMap, AnomalyManager? anomalyManager )
	{
		if ( activeMap is null ) return;

		var mapData = MapData.Data;

		if ( mapData is null )
		{
			FileSystem.Data.WriteJson( MapData.FileName, new MapData() );
			mapData = MapData.Data;
		}

		if ( mapData is not null )
		{
			if ( !mapData.MapsWon.Contains( activeMap.Ident ) )
			{
				mapData.MapsWon.Add( activeMap.Ident );
			}

			if ( mapData.MapsWon.Count == MapData.MapAmount )
			{
				Platform.Achievement.WinAllMaps.Unlock();
			}
		}

		switch ( activeMap.Difficulty )
		{
			case Difficulty.Easy or Difficulty.Normal:
				activeMap.WinAchievement?.Unlock();
				break;
			case Difficulty.Hard:
				activeMap.HardWinAchievement?.Unlock();
				break;
			default:
				throw new ArgumentOutOfRangeException();
		}

		if ( anomalyManager?.Rank != Rank.S )
		{
			return;
		}

		switch ( activeMap.Difficulty )
		{
			case Difficulty.Easy or Difficulty.Normal:
				activeMap.SRankAchievement?.Unlock();
				break;
			case Difficulty.Hard:
				activeMap.SRankHardAchievement?.Unlock();
				break;
		}

		if ( mapData is null )
		{
			return;
		}
		
		if ( !mapData.SRanks.Contains( activeMap.Ident ) )
		{
			mapData.SRanks.Add( activeMap.Ident );
		}

		if ( mapData.SRanks.Count == MapData.MapAmount )
		{
			Platform.Achievement.SRankAllMaps.Unlock();
		}
	}

	private static void ShowGameOverScreen( bool isWin, LoseReason? reason = null )
	{
		var menu = Hud.GetElement<GameOver>();
		if ( isWin )
			menu?.OnGameEnd( true );
		else if ( reason is not null )
			menu?.OnGameLose( reason.Value );

		menu?.Show();
	}

	private static void PauseGame() => Game.ActiveScene.Scene.TimeScale = 0;

	public void ToMenu()
	{
		Scene.DestroyPersistentObjects();
		Scene.Load( MenuScene );
	}

	public static Rank GetRank( int successRate )
	{
		if ( successRate is < 0 or > 100 )
			throw new ArgumentOutOfRangeException( nameof( successRate ),
				"Success rate must be between 0 and 100" );

		return Thresholds
			.OrderByDescending( x => x.Value )
			.First( x => successRate >= x.Value )
			.Key;
	}

	public static (Rank rank, int percentage) GetRank( int successes, int total )
	{
		if ( total == 0 ) return (Rank.F, 0);

		var percentage = (int)Math.Floor( (double)successes / total * 100 );
		percentage = Math.Min( 100, Math.Max( 0, percentage ) );

		return (GetRank( percentage ), percentage);
	}

	public enum LoseReason
	{
		[Description( "Too many failed reports" )]
		FailReports,
		[Description( "Too many active anomalies" )]
		TooManyAnomalies
	}
}

internal class GameStatistics
{
	internal static void RecordLoss( GameManager.LoseReason reason, Map? activeMap, AnomalyManager? anomalyManager )
	{
		Sandbox.Services.Stats.Increment( "Losses", 1 );
		Sandbox.Services.Stats.Increment( $"Losses_due_to_{reason}", 1 );

		if ( activeMap is null ) return;

		Sandbox.Services.Stats.Increment( $"Losses_on_map_{activeMap.Ident}", 1 );
		Sandbox.Services.Stats.Increment( $"Losses_on_map_{activeMap.Ident}_with_difficulty_{activeMap.Difficulty}", 1 );

		if ( !anomalyManager.IsValid() )
		{
			return;
		}

		RecordGameStats( anomalyManager.Rank, activeMap );
		RecordMapStats( activeMap, anomalyManager );
		RecordSuccessRate( activeMap, anomalyManager );
	}

	internal static void RecordWin( Map? activeMap, AnomalyManager? anomalyManager )
	{
		Sandbox.Services.Stats.Increment( "Wins", 1 );

		if ( activeMap is null ) return;

		Sandbox.Services.Stats.Increment( $"Wins_on_map_{activeMap.Ident}", 1 );
		Sandbox.Services.Stats.Increment( $"Wins_on_map_{activeMap.Ident}_with_difficulty_{activeMap.Difficulty}", 1 );

		if ( anomalyManager.IsValid() )
		{
			RecordGameStats( anomalyManager.Rank, activeMap );
			RecordMapStats( activeMap, anomalyManager );
			Sandbox.Services.Stats.Increment( $"Wins_with_rank_{anomalyManager.Rank}", 1 );
			RecordSuccessRate( activeMap, anomalyManager );
		}
	}

	private static void RecordGameStats( Rank rank, Map map )
	{
		Sandbox.Services.Stats.Increment( $"Game_over_with_difficulty_{map.Difficulty}", 1 );
		Sandbox.Services.Stats.Increment( $"Game_over_with_rank_{rank}", 1 );
		Sandbox.Services.Stats.Increment( $"Game_over_with_rank_{rank}_with_difficulty_{map.Difficulty}", 1 );
		Sandbox.Services.Stats.Increment( $"Game_over_on_map_{map.Ident}_with_rank_{rank}", 1 );
		Sandbox.Services.Stats.Increment( $"Game_over_on_map_{map.Ident}_with_rank_{rank}_with_difficulty_{map.Difficulty}", 1 );
	}

	private static void RecordMapStats( Map map, AnomalyManager anomalyManager )
	{
		Sandbox.Services.Stats.Increment( $"Losses_on_map_{map.Ident}_with_rank_{anomalyManager.Rank}", 1 );
		Sandbox.Services.Stats.Increment( $"Losses_on_map_{map.Ident}_with_rank_{anomalyManager.Rank}_with_difficulty_{map.Difficulty}", 1 );
	}

	private static void RecordSuccessRate( Map map, AnomalyManager anomalyManager )
	{
		Sandbox.Services.Stats.Increment( "Success_rate", anomalyManager.SuccessRate );
		Sandbox.Services.Stats.Increment( $"Success_rate_on_map_{map.Ident}", anomalyManager.SuccessRate );
	}
}

public static class LoseReasonExtensions
{
	public static string GetDescription( this GameManager.LoseReason reason )
	{
		var description = reason.GetAttributeOfType<DescriptionAttribute>();
		return description.Value ?? reason.ToString();
	}
}

public static class AnomalyManagerExtensions
{
	public static void SetFailReportLimits( this AnomalyManager anomalyManager, Difficulty difficulty )
	{
		switch ( difficulty )
		{
			case Difficulty.Easy:
			case Difficulty.Normal:
				anomalyManager.FailReportsTilWarning = int.MaxValue;
				anomalyManager.FailReportsTilGameOver = int.MaxValue;
				break;
			case Difficulty.Hard:
				anomalyManager.FailReportsTilWarning = 3;
				anomalyManager.FailReportsTilGameOver = 6;
				break;
			default:
				throw new ArgumentOutOfRangeException( nameof( difficulty ) );
		}
	}
}
