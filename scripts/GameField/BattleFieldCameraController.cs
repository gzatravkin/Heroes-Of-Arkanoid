using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BattleFieldCameraController : MonoBehaviour {
	public float minBattleXSize;
	public float minBattleYSize;
	public static float FinalWidthOfInterface;
	public Rect GameFieldRect;
	public bool SetSizeInFirstUpdate=true;

	public void SetSize(Vector2 BattleSize, float PartWidthInterface)
	{				
		//BattleSize.x=Mathf.Max(minBattleXSize,BattleSize.x);
		//BattleSize.y=Mathf.Max(minBattleYSize,BattleSize.y);
		var cam = GetComponent<Camera> ();
		float WidthPart = PartWidthInterface;
		float FinalWIdthInterface = (BattleSize.x / (1 - WidthPart))*WidthPart;
		FinalWidthOfInterface = FinalWIdthInterface;
		cam.orthographicSize = (BattleSize.x+FinalWIdthInterface)/(2*cam.aspect) + 2;
		gameObject.SetX (-0.5f * FinalWIdthInterface); //центирование игрового поля
		gameObject.SetY (BattleSize.y-cam.orthographicSize);//установка полотка камеры как высота игрового поля
		GameFieldRect = new Rect( transform.position.x-(BattleSize.x*(1-PartWidthInterface)*0.5f),transform.position.y-cam.orthographicSize,BattleSize.x,2*cam.orthographicSize);
	}
	public bool IsInBattlePos(Vector2 pos)
	{
		return GameFieldRect.Contains (pos);
	}

	public void Initialization()
	{
		var BattleSize = new Vector2 ();
		if (BattleController.GetGameField()!=null)
			BattleSize=BattleController.GetGameField().PrefieredBattleSize;
		BattleSize.x = Mathf.Max (BattleSize.x, minBattleXSize);
		BattleSize.y = Mathf.Max (BattleSize.y, minBattleYSize);
		float PartWidthInterface = 0;
		if (ServiceLocator.spellPanel!=null)
			PartWidthInterface = SpellPanel_List.GetWidthPart ();		
		SetSize (BattleSize,PartWidthInterface);
		GameWallsManager.Initialization ();
	}
	void Update()
	{		
		if (SetSizeInFirstUpdate) {
			SetSizeInFirstUpdate = false;
			Initialization ();
		}
	}
	void OnValidate()
	{
	}
}
