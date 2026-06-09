using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public static class CustomMarket  {

	public static bool TryToBuy(int price, ref int money)
	{
		if (money >= price) {
			money = money - price;
			return true;
		} else
			return false;
	}
	public static bool TryToBuy(float price, ref float money)
	{
		if (money >= price) {
			money = money - price;
			return true;
		} else
			return false;
	}
	public static bool TryToBuy(Saves.ResourceData price, Saves.ResourceData money)
	{
		return money.TryToBuy (price);
	}

	public static bool CanBuy(int price, int money)
	{
		if (money >= price) {			
			return true;
		} else
			return false;
	}
	public static bool CanBuy(float price, float money)
	{
		if (money >= price) {			
			return true;
		} else
			return false;
	}
	public static bool CanBuy(Saves.ResourceData price, Saves.ResourceData money)
	{
		return money.CanBuy (price);
	}
}
