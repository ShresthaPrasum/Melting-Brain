using System;
using System.Collections;
using UnityEngine;

public class LiquidSimulation : MonoBehaviour
{
	[SerializeField] private GameObject waterDropPrefap;
	[SerializeField] private Transform waterSpawnPoint;
	[SerializeField] private Transform spawnedWaterParent;
	
	[SerializeField] private string waterTagName = "Water";

	[SerializeField] private int totalDropsToSpawn = 120;
	[SerializeField] private float secondsBetweenDrops = 0.03f;
	[SerializeField] private float waitBeforeStarting = 0.1f;

	[SerializeField] private bool addRandomXOffset = true;

	[SerializeField] private float maxRandomXOffset = 0.05f;

	private Coroutine spawnRoutine;
	private int spawnedDropCount;

	public bool IsSpawningWater => spawnRoutine !=null;

	public int SpawnedDropCount => spawnedDropCount;

	public event Action FinishedSpawningWater;

	public void StartSpawningWater()
	{
		if(IsSpawningWater)
		{
			return;
		}

		if(waterDropPrefap == null || waterSpawnPoint == null)
		{
			Debug.LogWarning("BRO ASSIGN WATER DROP PREFAP");
			return;
		}

		spawnedDropCount = 0 ;
		spawnRoutine = StartCoroutine(SpawnWaterRoutine());
	}

	public void StopSpawningWater()
	{
		if(spawnRoutine == null)
		{
			return;
		}
		StopCoroutine(spawnRoutine);
		spawnRoutine = null;
	}

	public void ResetAllWaterDrops()
	{
		StopSpawningWater();
		spawnedDropCount = 0 ;

		if(spawnedWaterParent != null)
		{
			for(int i = spawnedWaterParent.childCount-1;i>=0;i--)
			{
				Destroy(spawnedWaterParent.GetChild(i).gameObject);
			}
			return;
		}
		if(string.IsNullOrWhiteSpace(waterTagName))
		{
			return;	
		}
		GameObject[] drops = GameObject.FindGameObjectsWithTag(waterTagName);

		for(int i = 0; i< drops.Length;i++)
		{
			Destroy(drops[i]);
		}
	}

	private IEnumerator SpawnWaterRoutine()
	{
		if(waitBeforeStarting>0f)
		{
			yield return new WaitForSeconds(waitBeforeStarting);
		}

		while(spawnedDropCount<totalDropsToSpawn)
		{
			SpawnOneDrop();
			spawnedDropCount++;

			if(secondsBetweenDrops>0f)
			{
				yield return new WaitForSeconds(secondsBetweenDrops);
			}
			else
			{
				yield return null;
			}
		}
		spawnRoutine = null;
		FinishedSpawningWater?.Invoke();
	}

	private void SpawnOneDrop()
	{
		Vector3 spawnPosition = waterSpawnPoint.position;

		if(addRandomXOffset && maxRandomXOffset > 0f)
		{
			spawnPosition.x += UnityEngine.Random.Range(-maxRandomXOffset,maxRandomXOffset);
		}

		if(spawnedWaterParent !=null)
		{
			Instantiate(waterDropPrefap, spawnPosition, Quaternion.identity, spawnedWaterParent);

		}

		else
		{
			Instantiate(waterDropPrefap, spawnPosition, Quaternion.identity);
		}
	}

	private void OnDisable()
	{
		StopSpawningWater();
	}
 }
