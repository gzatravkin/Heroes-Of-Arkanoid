using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class CustomHitBar : MonoBehaviour {
	[SerializeField]
	private GameObject RootObj;
	public Image ScaleObj;
	public float HP, MaxHP;
	public void SetHP(float HP)
	{
		this.HP = HP;
		Refresh ();
	}
	public void SetMaxHP(float MaxHP)
	{
		this.MaxHP=MaxHP;
		Refresh ();
	}
	public void Refresh()
	{
		ScaleObj.fillAmount = HP / MaxHP;
	}
}
