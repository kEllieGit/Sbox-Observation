﻿using System;
using kEllie.Utils;

namespace Observation;

public sealed class GameTimer : Singleton<GameTimer>
{
	[Property] public float WinTime { get; set; } = 1080;

	public TimeSince SinceStart { get; set; }
	public TimeUntil UntilVictory { get; set; }

	protected override void OnStart()
	{
		SinceStart = 0;
		UntilVictory = WinTime;

		base.OnStart();
	}

	protected override void OnUpdate()
	{
		if ( UntilVictory )
		{
			GameManager.EndGameInWin();
			UntilVictory = int.MaxValue;
		}

		base.OnUpdate();
	}

	public string GetDisplayTime()
	{
		var timeScale = 21600f / WinTime;
		var totalInGameSeconds = (int)(SinceStart * timeScale);

		// Clamp the total seconds to max 21,600 (06:00)
		totalInGameSeconds = Math.Min( totalInGameSeconds, 21600 );

		var hours = totalInGameSeconds / 3600;
		var minutes = (totalInGameSeconds % 3600) / 60;

		return $"{hours:D2}:{minutes:D2}";
	}
}
