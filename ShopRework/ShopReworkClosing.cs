using UnityEngine;
using DV.TerrainSystem;
using DV.CashRegister;
using DV.CabControls;
using DV.TimeKeeping;
using DV.Shops;
using DV;
using HarmonyLib;
using System;
using System.IO;
using System.Reflection;
using System.Collections;
using System.Collections.Generic;

namespace ShopRework
{
    public class ShopReworkClosing : MonoBehaviour
    {
		public static ShopReworkClosing? Instance;
		
		void Awake()
		{
			if (Instance != null)
			{
				Destroy(this);
				return;
			}

			Instance = this;

			StartCoroutine(RegisterTerrainGrid());
		}
		
        private WorldClockController? clock;
		private Vector2Int? lastTerrainCoord;
        private bool subscribed = false;
        public  bool shopsClosed = false;
		private bool initialEvaluationScheduled = false;
		private bool visualsCached = false;

        private GameObject? closedShopPrefab;

        private readonly Vector3 objectScale =
            new Vector3(0.0985f, 0.0985f, 0.0985f);

        private readonly Dictionary<string, Vector3> shopPositions = new()
        {
            { "GoodsFactory", new Vector3(13034.82f,138.16f,11161.38f) },
            { "FoodFactory", new Vector3(9530.15f,117.26f,13418.86f) },
            { "MachineFactory", new Vector3(2234.49f,157.26f,10835.36f) },
            { "CityWest", new Vector3(1912.79f,120.26f,5786.02f) },
            { "Harbor", new Vector3(13424.3f,111.1f,3614.0f) }
        };

        private readonly Dictionary<string, Vector3> shopRotations = new()
        {
            { "GoodsFactory", new Vector3(0f,241.8f,0f) },
            { "FoodFactory", new Vector3(0f,21.0f,0f) },
            { "MachineFactory", new Vector3(0f,181.6f,0f) },
            { "CityWest", new Vector3(0f,43.3f,0f) },
            { "Harbor", new Vector3(0f,273.0f,0f) }
        };

        private readonly Dictionary<string, GameObject> spawnedObjects = new();
		private readonly Dictionary<string, GameObject> spawnedBlockers = new();
		private readonly Dictionary<string, GameObject> spawnedTriggers = new();
		public  readonly Dictionary<int, List<GameObject>> shopVisualObjects = new();
				
		private int GetShopIndexFromRoot(string root)
		{
			if (root.Contains("x1_z5")) return 0;   // CityWest
			if (root.Contains("x2_z10")) return 1;  // MachineFactory
			if (root.Contains("x12_z10")) return 2; // GoodsFactory
			if (root.Contains("x12_z2")) return 3;  // Harbor
			if (root.Contains("x9_z13")) return 4;  // FoodFactory

			return -1;
		}
		
		private static readonly HashSet<Vector2Int> shopStreamingTiles = new()
		{
			new Vector2Int(25,21), // GoodsFactory
			new Vector2Int(18,26), // FoodFactory
			new Vector2Int(4,21),  // MachineFactory
			new Vector2Int(3,11),  // CityWest
			new Vector2Int(26,7)   // Harbor
		};
		
		private IEnumerator RegisterTerrainGrid()
		{
			while (TerrainGrid.Instance == null || !TerrainGrid.Instance.IsInitialized)
				yield return null;

			TerrainGrid.Instance.TerrainsMoved += OnTerrainsMoved;

			if (Main.settings.DevDebug)
				Debug.Log("[ShopRework] Registered TerrainGrid event.");
		}
		
		private void OnTerrainsMoved()
		{
			var coord = TerrainGrid.Instance?.currentCenterCoord;

			if (!coord.HasValue)
				return;

			if (coord == lastTerrainCoord)
				return;

			lastTerrainCoord = coord;

			if (!shopStreamingTiles.Contains(coord.Value))
				return;

			if (Main.settings.DevDebug)
				Debug.Log("[ShopRework] Player entered shop terrain → " + coord.Value);

			CoroutineManager.Instance.Run(WaitForTerrainStreamingRebuild());
		}
		
		public  void SetShopVisualState(int index, bool visible)
		{
			if (!shopVisualObjects.TryGetValue(index, out var visuals))
				return;

			foreach (var go in visuals)
			{
				if (go != null && go.activeSelf != visible)
					go.SetActive(visible);
			}
		}

        void Update()
		{
			if (clock == null)
			{
				clock = UnityEngine.Object.FindObjectOfType<WorldClockController>();
				if (clock == null)
					return;

				clock.TimeChanged += OnTimeChanged;
				subscribed = true;

				if (!initialEvaluationScheduled)
				{
					initialEvaluationScheduled = true;
					StartCoroutine(DelayedInitialEvaluation());
				}
			}

			if (closedShopPrefab == null)
				LoadAssetBundle();
		}

        private void LoadAssetBundle()
        {
            string assemblyLocation = Assembly.GetExecutingAssembly().Location;
            string dllDirectory = Path.GetDirectoryName(assemblyLocation);
            string assetsFolder = Path.Combine(dllDirectory, "Assets");
            string bundlePath = Path.Combine(assetsFolder, "closedshop");

            if (!File.Exists(bundlePath))
            {
                Debug.Log("[ShopRework] closedshop bundle not found.");
                return;
            }

            var bundle = AssetBundle.LoadFromFile(bundlePath);

            if (bundle == null)
            {
                Debug.Log("[ShopRework] Failed to load AssetBundle.");
                return;
            }

            closedShopPrefab = bundle.LoadAsset<GameObject>("closedshop");

            if (closedShopPrefab != null)
                Debug.Log("[ShopRework] ClosedShop prefab loaded.");

            bundle.Unload(false);
        }

        private void SpawnClosedObjects()
        {
            if (closedShopPrefab == null)
                return;

            foreach (var shop in shopPositions)
            {
                if (spawnedObjects.ContainsKey(shop.Key))
                    continue;

                Vector3 worldOffset = WorldMover.currentMove;
                Vector3 unitySpawnPos = shop.Value + worldOffset;

                GameObject obj = Instantiate(closedShopPrefab);
                obj.name = "CLOSED_SHOP_" + shop.Key;

                obj.transform.SetParent(WorldMover.OriginShiftParent, false);
                obj.transform.position = unitySpawnPos;
                obj.transform.eulerAngles = shopRotations[shop.Key];
                obj.transform.localScale = objectScale;

                spawnedObjects.Add(shop.Key, obj);
				SpawnBlocker(shop.Key, unitySpawnPos);

                Debug.Log("[ShopRework] Shop CLOSED → " + shop.Key);
            }
        }

        private void DestroyClosedObjects()
        {
            foreach (var obj in spawnedObjects.Values)
            {
                if (obj != null)
                    Destroy(obj);
            }

            spawnedObjects.Clear();

			foreach (var blocker in spawnedBlockers.Values)
			{
				if (blocker != null)
					Destroy(blocker);
			}

			spawnedBlockers.Clear();

            Debug.Log("[ShopRework] Shops OPEN again.");
        }
		
		public class SimpleZoneBlocker : ZoneBlocker
		{
			public override string GetHoverText()
			{
				var s = Main.settings;

				string Format(string name, DayOfWeek day, Settings.DaySchedule d)
				{
					bool isToday = Main.CurrentDay == day;

					string prefix = isToday ? ">> " : "   ";
					string suffix = isToday ? " <<" : "";

					if (d.keepClosed)
						return $"{prefix}{name,-3} : Closed{suffix}";

					return $"{prefix}{name,-3} : {d.openTime}-{d.closeTime}{suffix}";
				}

				return
					"Shop opening times:\n\n" +
					Format("Mon", DayOfWeek.Monday, s.Monday) + "\n" +
					Format("Tue", DayOfWeek.Tuesday, s.Tuesday) + "\n" +
					Format("Wed", DayOfWeek.Wednesday, s.Wednesday) + "\n" +
					Format("Thu", DayOfWeek.Thursday, s.Thursday) + "\n" +
					Format("Fri", DayOfWeek.Friday, s.Friday) + "\n" +
					Format("Sat", DayOfWeek.Saturday, s.Saturday) + "\n" +
					Format("Sun", DayOfWeek.Sunday, s.Sunday);
			}
		}
		
		private void SpawnBlocker(string shopKey, Vector3 unitySpawnPos)
		{
			if (spawnedBlockers.ContainsKey(shopKey))
				return;

			GameObject blocker = GameObject.CreatePrimitive(PrimitiveType.Cube);

			blocker.name = "ShopBlocker_" + shopKey;

			UnityEngine.Object.Destroy(blocker.GetComponent<MeshRenderer>());
			UnityEngine.Object.Destroy(blocker.GetComponent<MeshFilter>());

			blocker.transform.SetParent(WorldMover.OriginShiftParent, false);

			Vector3 left = Quaternion.Euler(shopRotations[shopKey]) * Vector3.left;

			blocker.transform.position = unitySpawnPos - left * 1f;
			blocker.transform.rotation = Quaternion.Euler(shopRotations[shopKey]);
			blocker.transform.localScale = new Vector3(8.99f, 8f, 5.99f);

			var col = blocker.GetComponent<BoxCollider>();
			col.isTrigger = false;			
			blocker.tag = "NO_TELEPORT";

			spawnedBlockers.Add(shopKey, blocker);
		}		
		
		private void SpawnTrigger(string shopKey, Vector3 unitySpawnPos)
		{
			if (spawnedTriggers.ContainsKey(shopKey))
				return;

			GameObject trigger = GameObject.CreatePrimitive(PrimitiveType.Cube);

			trigger.name = "ShopTrigger_" + shopKey;

			UnityEngine.Object.Destroy(trigger.GetComponent<MeshRenderer>());
			UnityEngine.Object.Destroy(trigger.GetComponent<MeshFilter>());

			trigger.transform.SetParent(WorldMover.OriginShiftParent, false);

			Vector3 left = Quaternion.Euler(shopRotations[shopKey]) * Vector3.left;

			trigger.transform.position = unitySpawnPos - left * 1f;
			trigger.transform.rotation = Quaternion.Euler(shopRotations[shopKey]);
			trigger.transform.localScale = new Vector3(9f, 8f, 6f);

			var col = trigger.GetComponent<BoxCollider>();
			col.isTrigger = true;

			var zb = trigger.AddComponent<SimpleZoneBlocker>();
			zb.blockerObjectsParent = trigger;

			spawnedTriggers.Add(shopKey, trigger);
		}
		
		private void SetBlockerState(bool closed)
		{
			foreach (var blocker in spawnedBlockers.Values)
			{
				if (blocker == null)
					continue;

				blocker.SetActive(closed);
			}
		}
		
		private float ParseTime(string time)
		{
			if (TimeSpan.TryParse(time, out var ts))
				return ts.Hours + ts.Minutes / 60f;

			return 0f;
		}
		
		private void ForcePlayerOutOfClosedShops()
		{
			if (PlayerManager.PlayerTransform == null)
				return;

			Transform player = PlayerManager.PlayerTransform;
			Vector3 playerPos = player.position;

			foreach (var shop in shopPositions)
			{
				Vector3 worldOffset = WorldMover.currentMove;
				Vector3 shopPos = shop.Value + worldOffset;

				float dist = Vector3.Distance(playerPos, shopPos);

				if (dist > 6f)
					continue;

				Vector3 right = Quaternion.Euler(shopRotations[shop.Key]) * Vector3.right;

				Vector3 safePosition = shopPos - right * 6f;

				safePosition.y = playerPos.y;

				player.position = safePosition;
				player.position += Vector3.up * 0.05f;
				player.position -= Vector3.up * 0.05f;

				Debug.Log($"[ShopRework] Player moved out of closed shop {shop.Key}");
			}
		}

        private void OnTimeChanged(float hourAngle, float minuteAngle, DateTime currentTime)
        {
            Evaluate(currentTime);
        }

        public void Evaluate(DateTime now)
        {
            if (!Main.settings.EnableShopBlocking)
            {
                if (shopsClosed)
                {
                    shopsClosed = false;
                    DestroyClosedObjects();
                }
                return;
            }

            float hour = now.Hour + now.Minute / 60f;
			var schedule = Main.settings.GetSchedule(Main.CurrentDay);

			bool shouldClose;

			if (schedule.keepClosed)
			{
				shouldClose = true;
			}
			else
			{
				float open = ParseTime(schedule.openTime);
				float close = ParseTime(schedule.closeTime);

				bool isNight =
					close > open
					? (hour >= close || hour < open)
					: (hour >= close && hour < open);

				shouldClose = isNight;
			}

            if (shopsClosed == shouldClose)
                return;

            shopsClosed = shouldClose;

            if (shouldClose)
			{
				DelayedShopClose();
				SetBlockerState(true);

				if (visualsCached)
				{
					for (int i = 0; i < 5; i++)
						SetShopVisualState(i, false);
				}
			}
			else
			{
				DestroyClosedObjects();
				EnableRegisterPhysics();
				SetBlockerState(false);

				if (visualsCached)
				{
					for (int i = 0; i < 5; i++)
						SetShopVisualState(i, true);
				}
			}
            if (Main.settings.DevDebug)
                Debug.Log($"[ShopRework] Shops now {(shouldClose ? "CLOSED" : "OPEN")} at {hour:00.00}");
        }
		
		private void DisableRegisterPhysics()
		{
			foreach (var register in CashRegisterBase.allCashRegisters)
			{
				if (register == null)
					continue;

				if (!register.gameObject.scene.isLoaded)
					continue;

				var scanModule = register.GetComponentInChildren<ScanItemCashRegisterModule>(true);
				if (scanModule == null)
					continue;

				register.Cancel();
				register.IsProcessingTransaction = true;

				var buttons = register.GetComponentsInChildren<ButtonBase>(true);
				foreach (var b in buttons)
					b.enabled = false;
			}
		}
		
		private void EnableRegisterPhysics()
		{
			foreach (var register in CashRegisterBase.allCashRegisters)
			{
				if (register == null)
					continue;

				if (!register.gameObject.scene.isLoaded)
					continue;

				var scanModule = register.GetComponentInChildren<ScanItemCashRegisterModule>(true);
				if (scanModule == null)
					continue;

				register.IsProcessingTransaction = false;

				var buttons = register.GetComponentsInChildren<ButtonBase>(true);
				foreach (var b in buttons)
					b.enabled = true;
			}
		}
		
		public void ClearShopVisualCache()
		{
			shopVisualObjects.Clear();

			if (Main.settings.DevDebug)
				Debug.Log("[ShopRework] Shop visual cache cleared.");
		}
		
		public void RebuildAllShopVisuals()
		{
			for (int i = 0; i < 5; i++)
				SetShopVisualState(i, !shopsClosed);

			if (Main.settings.DevDebug)
				Debug.Log("[ShopRework] Shop visuals rebuilt.");
		}

        void OnDestroy()
		{
			if (clock != null && subscribed)
				clock.TimeChanged -= OnTimeChanged;

			if (TerrainGrid.Instance != null)
				TerrainGrid.Instance.TerrainsMoved -= OnTerrainsMoved;
		}
		
		private System.Collections.IEnumerator DelayedInitialEvaluation()
		{
			yield return new WaitForSeconds(0.5f);

			if (clock == null)
				yield break;

			var tuple = clock.GetCurrentAnglesAndTimeOfDay();

			if (tuple.validTime)
				Evaluate(tuple.timeOfDay);
		}
		
		private void ResetAllRegisters()
		{
			foreach (var register in CashRegisterBase.allCashRegisters)
			{
				if (register == null)
					continue;

				if (!register.gameObject.scene.isLoaded)
					continue;

				Debug.Log("[ShopRework] Resetting register: " + register.name);

				register.Cancel();

				var modules = register.GetComponentsInChildren<CashRegisterModule>(true);
				foreach (var m in modules)
				{
					m.ResetData();
				}

				var scanModules = register.GetComponentsInChildren<ScanItemCashRegisterModule>(true);
				foreach (var sm in scanModules)
				{
					sm.UpdateTexts();
				}
			}
		}

		private void DelayedShopClose()
		{
			SpawnClosedObjects();

			ResetAllRegisters();

			DisableRegisterPhysics();

			ForcePlayerOutOfClosedShops();

			for (int i = 0; i < 5; i++)
				SetShopVisualState(i, false);
		}
		
		public void CacheAllShopVisuals()
		{
			if (visualsCached)
			return;
		
			shopVisualObjects.Clear();

			var renderers = GameObject.FindObjectsOfType<MeshRenderer>();
			
			foreach (var r in renderers)
			{
				if (r == null)
					continue;

				var go = r.gameObject;

				if (go == null)
					continue;

				var scene = go.scene;

				if (!scene.IsValid())
					continue;

				if (!scene.isLoaded)
					continue;

				if (!r.name.Contains("ItemShop"))
					continue;

				var root = r.transform.root;

				if (root == null)
					continue;

				if (!root.name.StartsWith("Far__"))
					continue;

				int index = GetShopIndexFromRoot(root.name);

				if (index < 0)
					continue;

				if (!shopVisualObjects.ContainsKey(index))
					shopVisualObjects[index] = new List<GameObject>();

				shopVisualObjects[index].Add(go);
			}
			
			if (shopVisualObjects.Count > 0)
			{
				visualsCached = true;
			}
			
			if (Main.settings.DevDebug)
				Debug.Log("[ShopRework] Cached shop visuals: " + shopVisualObjects.Count);
		}
		
		private void SpawnAllTriggers()
		{
			foreach (var shop in shopPositions)
			{
				if (spawnedTriggers.ContainsKey(shop.Key))
					continue;

				Vector3 worldOffset = WorldMover.currentMove;
				Vector3 unitySpawnPos = shop.Value + worldOffset;

				SpawnTrigger(shop.Key, unitySpawnPos);
			}

			if (Main.settings.DevDebug)
				Debug.Log("[ShopRework] All shop triggers spawned (persistent).");
		}
		
		private void RebuildTriggersAfterStreaming()
		{
			foreach (var kvp in spawnedTriggers)
			{
				string shopKey = kvp.Key;
				GameObject trigger = kvp.Value;

				if (trigger == null)
					continue;

				Vector3 worldOffset = WorldMover.currentMove;
				Vector3 unitySpawnPos = shopPositions[shopKey] + worldOffset;

				Vector3 left = Quaternion.Euler(shopRotations[shopKey]) * Vector3.left;

				trigger.transform.position = unitySpawnPos - left * 1f;
				trigger.transform.rotation = Quaternion.Euler(shopRotations[shopKey]);
			}

			if (Main.settings.DevDebug)
				Debug.Log("[ShopRework] Triggers repositioned after streaming.");
		}
		
		public static IEnumerator WaitForWorldAndCacheShops()
		{
			while (!WorldStreamingInit.IsStreamingDone)
				yield return null;

			while (GlobalShopController.Instance == null)
				yield return null;

			yield return null;

			if (Instance != null)
			{
				Instance.CacheAllShopVisuals();
				Instance.SpawnAllTriggers();

				if (Instance.clock != null)
				{
					var tuple = Instance.clock.GetCurrentAnglesAndTimeOfDay();

					if (tuple.validTime)
						Instance.Evaluate(tuple.timeOfDay);
				}

				Instance.RebuildAllShopVisuals();

				if (Main.settings.DevDebug)
					Debug.Log("[ShopRework] Shop visuals cached after streaming.");
			}
		}
		
		public static IEnumerator WaitForFastTravelRebuild()
		{
			if (Main.settings.DevDebug)
				Debug.Log("[ShopRework] Waiting for streaming after fast travel...");

			yield return new WaitForSeconds(1f);

			while (!WorldStreamingInit.IsStreamingDone)
				yield return null;

			yield return new WaitForSeconds(0.5f);

			if (Instance != null)
			{
				if (Main.settings.DevDebug)
					Debug.Log("[ShopRework] FastTravel streaming finished → rebuilding shops");

				Instance.shopVisualObjects.Clear();
				Instance.visualsCached = false;

				yield return new WaitForSeconds(0.5f);

				Instance.CacheAllShopVisuals();

				if (!Instance.visualsCached)
				{
					yield return new WaitForSeconds(1f);
					Instance.CacheAllShopVisuals();
				}

				Instance.RebuildAllShopVisuals();
				Instance.RebuildTriggersAfterStreaming();
			}
		}
		
		public static IEnumerator WaitForTerrainStreamingRebuild()
		{
			yield return new WaitForSeconds(0.5f);

			while (!WorldStreamingInit.IsStreamingDone)
				yield return null;

			if (Instance == null)
				yield break;

			Instance.CacheAllShopVisuals();
			Instance.RebuildAllShopVisuals();
		}
    }
	
	[HarmonyPatch(typeof(WorldStreamingInit), "Awake")]
	public static class Patch_WorldStreamingInit
	{
		static void Postfix()
		{
			CoroutineManager.Instance.Run(ShopReworkClosing.WaitForWorldAndCacheShops());
		}
	}
	
	[HarmonyPatch(typeof(FastTravelController), "FastTravel")]
	public static class Patch_FastTravel
	{
		static void Postfix()
		{
			if (ShopReworkClosing.Instance == null)
				return;

			if (Main.settings.DevDebug)
				Debug.Log("[ShopRework] FastTravel detected → scheduling rebuild");

			CoroutineManager.Instance.Run(ShopReworkClosing.WaitForFastTravelRebuild());
		}
	}
}