using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class LiquidSimulation : MonoBehaviour
{
	[Header("Water Setup")]
	[SerializeField] private GameObject waterDropPrefap;
	[SerializeField] private Transform waterSpawnPoint;
	[SerializeField] private Transform spawnedWaterParent;
	
	[SerializeField] private string waterTagName = "Water";

	[Header("Spawn Tuning")]
	[SerializeField] private int totalDropsToSpawn = 120;
	[SerializeField] private float secondsBetweenDrops = 0.03f;
	[SerializeField] private float waitBeforeStarting = 0.1f;

	[SerializeField] private bool addRandomXOffset = true;
	[SerializeField] private float maxRandomXOffset = 0.05f;

	private Coroutine spawnRoutine;
	private int spawnedDropCount;
	private List<GameObject> dropPool = new List<GameObject>();

	public bool IsSpawningWater => spawnRoutine !=null;
	public int SpawnedDropCount => spawnedDropCount;
	public event Action FinishedSpawningWater;

	private void Start()
	{
		if (waterDropPrefap == null) return;
		
		for (int i = 0; i < totalDropsToSpawn; i++)
		{
			GameObject drop = Instantiate(waterDropPrefap, spawnedWaterParent != null ? spawnedWaterParent : transform);
			drop.SetActive(false);
			dropPool.Add(drop);
		}
	}

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

		spawnedDropCount = 0;
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

		for (int i = 0; i < dropPool.Count; i++)
		{
			if (dropPool[i] != null)
			{
				dropPool[i].SetActive(false);
				
				if (dropPool[i].TryGetComponent(out Rigidbody2D rb))
				{
					rb.linearVelocity = Vector2.zero;
					rb.angularVelocity = 0f;
				}
			}
		}
	}

	private IEnumerator SpawnWaterRoutine()
	{
		if(waitBeforeStarting>0f)
		{
			yield return new WaitForSeconds(waitBeforeStarting);
		}

		while(spawnedDropCount<totalDropsToSpawn && spawnedDropCount < dropPool.Count)
		{
			SpawnOneDrop(dropPool[spawnedDropCount]);
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

	private void SpawnOneDrop(GameObject dropToSpawn)
	{
		Vector3 spawnPosition = waterSpawnPoint.position;

		if(addRandomXOffset && maxRandomXOffset > 0f)
		{
			spawnPosition.x += UnityEngine.Random.Range(-maxRandomXOffset,maxRandomXOffset);
		}

		dropToSpawn.transform.position = spawnPosition;
		dropToSpawn.SetActive(true);
	}

	private void OnDisable()
	{
		StopSpawningWater();
	}
}
