using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class AbstractMissionWinCondicion : MonoBehaviour  {	
	private MissionCondicionState state = MissionCondicionState.Playing;
	public TextTranslation Descripcion = new TextTranslation();
	[HideInInspector]
	protected MissionWinController winController;
	public virtual string GetDescripcion()
	{
		return Descripcion;
	}
	protected void SetState(MissionCondicionState state)
	{
		if (state != this.state) {
			this.state = state;
			winController.CondicionStateChanged (this, state);
			if (state != MissionCondicionState.Playing)
				Finishing ();
		}
	}
	protected virtual void ControllerInitizliation()
	{
	}
	public MissionCondicionState GetState()
	{
		return state;
	}
	protected void Finishing()
	{
	}	
	public void Initialization(MissionWinController winController)
	{
		state = MissionCondicionState.Playing;
		this.winController = winController;
		ControllerInitizliation ();
	}
		
}
