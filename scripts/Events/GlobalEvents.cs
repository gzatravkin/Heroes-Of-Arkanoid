using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

public class GlobalEvents{

	public static UnityEvent ClassChanged = new UnityEvent();
	public static UnityEvent ResourceChanged = new UnityEvent();
	public static UnityEvent SkillsChanged = new UnityEvent();
	public static UnityEvent_Int ItemsChanged = new UnityEvent_Int();
	public static UnityEvent CurrentHeroChanged = new UnityEvent();

}
