using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class RecourcePanel_UIScale : RecourcePanel {
	public Image ToScale;
	public float Value = 1f;
	public float Speed = 1f;
	public override void ShowRecource()
	{
		Value = spellSystem.Recource/spellSystem.MaxRecource;
	}
	void Update()
	{
		ShowRecource ();
		ToScale.fillAmount = Mathf.MoveTowards (ToScale.fillAmount, Value, Speed * Time.deltaTime);
	}
}
