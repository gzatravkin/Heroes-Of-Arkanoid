using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

public static class PalletsCreator
{
	public static GameObject GetPalletObj (PalletStyles style)
	{
		var config = ServiceLocator.GetConfiguraciones ();
		if (style == PalletStyles.Standart)
			return config.StandartPallet;
		if (style == PalletStyles.Notification)
			return config.NotificationPallet;
		if (style == PalletStyles.ItemGetted)
			return config.ItemPallet;
		if (style == PalletStyles.SkillOpenned)
			return config.SkillPalet;
		if (style == PalletStyles.MissionOpenned)
			return config.MissionPallet;
		return config.StandartPallet;
	}

	#region CustomPallets

	public static void CreateCustomPallet (string Name, string Descripcion, Sprite image, PalletStyles style,  params PalletButtonData[] data)
	{
		var sPalet = MonoBehaviour.Instantiate (GetPalletObj (style), Vector3.zero, Quaternion.identity) as GameObject;
		var PalletController = sPalet.GetComponent<PalletController> ();
		PalletController.Initialization (Name, Descripcion, image, data);
	}

	public static void CreateCustomPallet (TextTranslation Name, params PalletButtonData[] data)
	{
		CreateCustomPallet (Name, new TextTranslation (), null, PalletStyles.Standart, data);
	}

	public static void CreateCustomPallet (params PalletButtonData[] data)
	{
		CreateCustomPallet (new TextTranslation (), new TextTranslation (), null, PalletStyles.Standart, data);
	}

	#endregion

	#region PersonalizedPallets

	public static void CreatePallet_Notification (string Name, string Descripcion, Sprite image)
	{
		CreateCustomPallet  (Name, Descripcion, image, PalletStyles.Notification, new PalletButtonData ("Okay!"));
	}
	public static void CreatePallet_ItemOpen(SO_AbstractItem item, int level)
	{
		var sPalet = MonoBehaviour.Instantiate (GetPalletObj (PalletStyles.ItemGetted), Vector3.zero, Quaternion.identity) as GameObject;
		var PalletController = sPalet.GetComponent<PalletController_Item> ();
		MonoBehaviour.DontDestroyOnLoad (sPalet);
		PalletController.Initializar (item,level);
	}
	public static void CreatePallet_MissionOpen(Level level, Premy lastPremy = null)
	{
		var sPalet = MonoBehaviour.Instantiate (GetPalletObj (PalletStyles.MissionOpenned), Vector3.zero, Quaternion.identity) as GameObject;
		var PalletController = sPalet.GetComponent<PalletController_Mission> ();
		PalletController.Initializar (level,lastPremy);
	}
	public static void CreatePallet_SkillOpen(SO_AbstractSkill skill)
	{
		var sPalet = MonoBehaviour.Instantiate (GetPalletObj (PalletStyles.SkillOpenned), Vector3.zero, Quaternion.identity) as GameObject;
		var PalletController = sPalet.GetComponent<PalletController_Skill> ();
		PalletController.Initializar (skill);
	}
	public static void CreateSurePallet (UnityAction SureAction)
	{
		CreateCustomPallet (new PalletButtonData ("NO SURE", null), new PalletButtonData ("SURE", SureAction));
	}

	#endregion
}
