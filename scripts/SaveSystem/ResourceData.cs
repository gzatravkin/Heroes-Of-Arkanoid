using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Saves {
	[System.Serializable]
	public class ResourceData {
		public int SpellPoints;
		public CrystalResource crystalResource;
		public ResourceData(int SpellPoints, CrystalResource crystales)
		{
			this.SpellPoints = SpellPoints;
			this.crystalResource= crystales;
		}
		public ResourceData()
		{			
		}
		public bool CanBuy(ResourceData price)
		{
			if (SpellPoints > price.SpellPoints &&			    
			    price.crystalResource.IsPartOf (crystalResource))
				return true;
			else
				return false;

		}
		public bool TryToBuy(ResourceData price)
		{
			if (CanBuy (price)) {
				SpellPoints += -price.SpellPoints;
				crystalResource = crystalResource-price.crystalResource;
				GlobalEvents.ResourceChanged.Invoke ();
				return true;
			} else
				return false;
		}
	}
}
