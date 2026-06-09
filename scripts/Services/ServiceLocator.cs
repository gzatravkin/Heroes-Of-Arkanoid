using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

public class ServiceLocator : MonoBehaviour {
	private static SO_Configuraciones configuraciones;
	public static LifeManager lifeManager;
	public static SpellPanel_List spellPanel;
	public static MissionWinController winController;
	public static SO_SpellController spellController;
	public static SO_Configuraciones GetConfiguraciones()
	{			
			return SO_Configuraciones.obj;
	}
	/*	public static void CreateChoisePalet(UnityAction Choise1,UnityAction Choise2)
	{
		var sPalet = Instantiate (GetConfiguraciones ().SurePalet, Vector3.zero, Quaternion.identity) as GameObject;
		var surePalletController = sPalet.GetComponent<SurePalletController> ();
		surePalletController.ADD_OK_ACTION (Choise2);
		surePalletController.ADD_OK_ACTION (()=>{Destroy(sPalet);});
		surePalletController.ADD_CANCEL_ACTION (()=>{Destroy(sPalet);});
	}
*/
}
