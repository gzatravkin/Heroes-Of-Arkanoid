using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SO_Configuraciones : ScriptableObject {
	private static SO_Configuraciones Obj;
	public static SO_Configuraciones obj{
		get{
			if (Obj==null)
				Obj = (SO_Configuraciones)Resources.Load ("SO_Configuraciones", typeof(SO_Configuraciones)) ;
			return Obj;
		}
	}
	public SO_Hero[] Heroes;
	public List<SO_AbstractItem> Items = new List<SO_AbstractItem>();
	public Vector3 StartBarPosition;
	public List<SO_LocationData> Locationes;
	public Parameter_Grid PointsForLevel;
	[Header("Interface")]
	public float WidthPartOfBattle = 0.1375f;
	public GameObject StandartPallet;
	public GameObject NotificationPallet;
	public GameObject ItemPallet;
	public GameObject SkillPalet;
	public GameObject MissionPallet;
	public Timer timer;
	public Animator[] FadeScreenAnimations;
	public Sprite[] SkillLevelPanel;
	public Sprite LockedSpell;
	[Header("Managers")]
	public LifeManager lifeManager;
	public FonManager fonManager;
	[Header("Blocks")]
	public List<SO_BlocksCollector> BlockCollectors;
	public Color[] TeleporterColors;
}
