﻿using Observation.Cameras;
using Observation.UI;
using Sandbox.Audio;
using Sandbox.UI;

namespace Observation;

public class CameraManager : Component
{
	public static CameraManager? Instance { get; private set; }

	[Property, ReadOnly] public Camera? ActiveCamera { get; set; }
	[Property] public List<Camera> PossibleCameras { get; set; } = [];

	[Property, Category( "Inputs" ), InputAction] public string NextCameraInput { get; set; } = "nextcamera";
	[Property, Category( "Inputs" ), InputAction] public string LastCameraInput { get; set; } = "lastcamera";

	protected override void OnStart()
	{
		Instance = this;

		//foreach ( var camera in Scene.GetAllComponents<CameraComponent>() )
		//{
		//	camera.GameObject.Enabled = false;
		//}

		if ( PossibleCameras.Count > 0 )
		{
			var firstEnabledCamera = PossibleCameras.FirstOrDefault( x => GameObject.Enabled );
			if ( firstEnabledCamera.IsValid() )
			{
				SetActiveCamera( firstEnabledCamera );
			}
		}

		base.OnStart();
	}

	protected override void OnUpdate()
	{
		if ( Input.Pressed( NextCameraInput ) )
		{
			SetNextAvailableActive();
		}

		if ( Input.Pressed( LastCameraInput ) )
		{
			SetPreviousAvailableActive();
		}

		base.OnUpdate();
	}

	public void SetActiveCamera( Camera camera )
	{
		if ( ActiveCamera.IsValid() )
		{
			DisableCamera( ActiveCamera );
		}

		ActiveCamera = camera;
		camera.GameObject.Enabled = true;

		Hud.GetElement<AnomalyList>()?.Hide();
		var sound = Sound.Play( "ui.button.deny" );
		sound.TargetMixer = Mixer.FindMixerByName( "UI" );
	}

	public void DisableCamera( Camera camera )
	{
		camera.GameObject.Enabled = false;
	}

	public void SetNextAvailableActive()
	{
		if ( !ActiveCamera.IsValid() || PossibleCameras.Count == 0 ) return;

		var currentIndex = PossibleCameras.IndexOf( ActiveCamera );
		var nextIndex = (currentIndex + 1) % PossibleCameras.Count;
		SetActiveCamera( PossibleCameras[nextIndex] );
	}

	public void SetPreviousAvailableActive()
	{
		if ( !ActiveCamera.IsValid() || PossibleCameras.Count == 0 ) return;

		var currentIndex = PossibleCameras.IndexOf( ActiveCamera );
		var prevIndex = (currentIndex - 1 + PossibleCameras.Count) % PossibleCameras.Count;
		SetActiveCamera( PossibleCameras[prevIndex] );
	}
}
