using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class GameField
{
	public static GameField objRef{ get { return BattleController.GetGameField (); } }

	private List<GameObject> LevelObjects;
	private List<BlockScript> Blocks;
	public Level LevelObject;
	public Vector2 PrefieredBattleSize;

	public List<GameObject> GetAllLevelObjects ()
	{
		LevelObjects.RemoveAll (x => x == null);
		return LevelObjects;
	}

	public List<GameObject> GetActiveLevelObjects ()
	{
		LevelObjects.RemoveAll (x => x == null);
		return LevelObjects.FindAll (x => x.activeInHierarchy);
	}

	public List<BlockScript> GetBlocks (bool getInactives = false)
	{	
		Blocks.RemoveAll (x => x == null || x.Killed == true);	
		if (getInactives)
			return Blocks;
		else
			return Blocks.FindAll (x => x.gameObject.activeInHierarchy);
	}

	public void SetPrefieredBattleSize (Vector2 size)
	{
		PrefieredBattleSize = size;
	}

	public void SetLevelObjects (List<GameObject> LevelObjects, GameObject LevelObject)
	{
		this.LevelObjects = LevelObjects;
		this.LevelObject = LevelObject.GetComponent<Level> ();
		Blocks = new List<BlockScript> ();
		Blocks.AddRange (LevelObject.GetComponentsInChildren<BlockScript> (true));
	}

	public static bool Between (float num, float lower, float upper, bool inclusive = false)
	{
		return inclusive
			? lower <= num && num <= upper
				: lower < num && num < upper;
	}

	public bool IsBlockExist (BlockScript block, bool CanBeInactive = false)
	{
		if (block == null || block.Killed)
			return false;
		if (CanBeInactive || block.IsActive ())
			return true;
		return false;
	}

	public BlockScript GetClosestBlock (BlockScript target, float maxDistance = 100f, float minDistance = 0f, bool canBeInactive = false)
	{
		float sqadMaxDistance = maxDistance * maxDistance;
		Vector2 posInicial = new Vector2 (target.transform.position.x, target.transform.position.y);
		float lastSqadDistance = sqadMaxDistance;
		float minSqadDistance = minDistance * minDistance;
		BlockScript lastBlock = null;
		for (int i = 0; i < Blocks.Count; i++) {			
			if (Blocks [i] != target) {
				if (!IsBlockExist (Blocks [i], canBeInactive))
					continue;
				float sqadDistance = Blocks [i].transform.position.sqadDistance2D (posInicial);
				if (sqadDistance <= lastSqadDistance && sqadDistance >= minSqadDistance) {
					lastSqadDistance = sqadDistance;
					lastBlock = Blocks [i];
				}
			}
		}
		return lastBlock;
	}

	public BlockScript GetClosestBlock (Vector3 point, float maxDistance = 100f, float minDistance = 0f, bool canBeInactive = false)
	{
		float sqadMaxDistance = maxDistance * maxDistance;
		Vector2 posInicial = point;
		float lastSqadDistance = sqadMaxDistance;
		float minSqadDistance = minDistance * minDistance;
		BlockScript lastBlock = null;
		for (int i = 0; i < Blocks.Count; i++) {						
			if (!IsBlockExist (Blocks [i], canBeInactive))
				continue;
			float sqadDistance = Blocks [i].transform.position.sqadDistance2D (posInicial);
			if (sqadDistance <= lastSqadDistance && sqadDistance >= minSqadDistance) {
				lastSqadDistance = sqadDistance;
				lastBlock = Blocks [i];
			}
		}
		return lastBlock;
	}

	public List<BlockScript> GetBlocksInArea (Vector3 point, float maxDistance, bool sorted = false, float minDistance = 0f, bool canBeInactive = false)
	{
		float sqadMaxDistance = maxDistance * maxDistance;
		float sqadMinDistance = minDistance * minDistance;
		var returnList = Blocks.FindAll (x => IsBlockExist (x, canBeInactive) && (Between (x.transform.position.sqadDistance2D (point), sqadMinDistance, sqadMaxDistance)));
		if (sorted)
			returnList.Sort ((x, y) => Comparer.floatSortFunction (x.transform.position.sqadDistance2D (point), y.transform.position.sqadDistance2D (point)));
		return returnList;

	}

}
