using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BonusSize : AbstractBonus {
	public int Size = 1;
	public BonusSize(int size)
	{
		Size = size;
	}
	public BonusSize()
	{		
	}
	public override void Recalculation ()
	{
		var barController = (Player_BarController)player;
		barController.CurrentSize += Size;
	}
}
