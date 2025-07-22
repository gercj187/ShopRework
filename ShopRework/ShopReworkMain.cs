// Datei: ShopReworkMain.cs

using UnityModManagerNet;
using HarmonyLib;
using UnityEngine;
using System.Reflection;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System;
using DV.TimeKeeping;
using DV.Shops;
using TMPro;

namespace ShopRework
{
	public class Settings : UnityModManager.ModSettings, IDrawable
	{
		[Range(0, 50)] public int discountedItemsPerDay = 3;
		[Range(0, 50)] public int discountPercentage = 0;
		public bool UseWeeklyDiscounts = false;
		public bool EnableShopBlocking = true;
		public float ShopOpenHour = 6.0f;
		public float ShopCloseHour = 22.0f;
		public bool ShopClosedOnSunday = false;

		public int persistentDayCounter = 0;
		public DateTime lastKnownDate = DateTime.MinValue;

		public override void Save(UnityModManager.ModEntry modEntry) => Save(this, modEntry);
		public void OnChange() { }

		public void Draw()
		{
			GUILayout.Label("<b>Discount Configuration:</b>");
			UseWeeklyDiscounts = GUILayout.Toggle(UseWeeklyDiscounts, "Apply discounts weekly");
			GUILayout.Label($"Number of Discounted Items: {discountedItemsPerDay}");
			discountedItemsPerDay = (int)GUILayout.HorizontalSlider(discountedItemsPerDay, 0, 50);

			GUILayout.Label($"Discount in % (0 = random): {(discountPercentage == 0 ? "random" : discountPercentage + "%")}");
			discountPercentage = (int)GUILayout.HorizontalSlider(discountPercentage, 0, 50);

			GUILayout.Space(10);
			GUILayout.Label("<b>Shop Closure Settings:</b>");
			EnableShopBlocking = GUILayout.Toggle(EnableShopBlocking, "Enable store closing hours");

			if (EnableShopBlocking)
			{
				GUILayout.Label($"Opening time: {FormatHour(ShopOpenHour)}");
				ShopOpenHour = Mathf.Round(GUILayout.HorizontalSlider(ShopOpenHour, 0f, 23.9f) * 60f) / 60f;

				GUILayout.Label($"Closing time: {FormatHour(ShopCloseHour)}");
				ShopCloseHour = Mathf.Round(GUILayout.HorizontalSlider(ShopCloseHour, 0f, 23.9f) * 60f) / 60f;

				ShopClosedOnSunday = GUILayout.Toggle(ShopClosedOnSunday, "Keep shop closed on Sundays");
			}
		}

		private string FormatHour(float hourFloat)
		{
			int hour = Mathf.FloorToInt(hourFloat);
			int minute = Mathf.RoundToInt((hourFloat - hour) * 60f);
			return $"{hour:00}:{minute:00}";
		}
	}

	static class Main
	{
		public static Settings settings = null!;
		public static bool enabled;
		public static Harmony harmony = null!;
		public static UnityModManager.ModEntry mod = null!;
		public static int dayCounter = 0;
		public static DateTime lastKnownDate = DateTime.MinValue;
		public static readonly List<GameObject> spawnedBlockers = new();
		public static readonly HashSet<int> placedBlockerIndices = new();
		public static readonly Dictionary<int, Shop> mappedShops = new();
		public static readonly List<Shop> allShops = new();

		public static readonly Vector3[] shopPositions = {
			new Vector3(266.8f, 120.3f, 25.0f),			//CitySouthWest
			new Vector3(588.5f, 157.3f, 136.4f),		//MachineFactory
			new Vector3(689.8f, 138.2f, 462.4f),		//GoodsFactory
			new Vector3(256.3f, 111.1f, 322.0f),		//Harbor
			new Vector3(477.1f, 117.3f, 250.9f)			//FoodFactory
		};

		public static readonly Vector3[] shopRotations = {
			new Vector3(0f, 43.3f, 0f),			 		//CitySouthWest
			new Vector3(0f, 181.6f, 0f),				//MachineFactory
			new Vector3(0f, 241.8f, 0f),				//GoodsFactory
			new Vector3(0f, 273.0f, 0f),				//Harbor
			new Vector3(0f, 21.0f, 0f)					//FoodFactory
		};

		static bool Load(UnityModManager.ModEntry modEntry)
		{
			mod = modEntry;
			settings = UnityModManager.ModSettings.Load<Settings>(modEntry);
			dayCounter = settings.persistentDayCounter;
			lastKnownDate = settings.lastKnownDate;
			modEntry.OnGUI = _ => settings.Draw();
			modEntry.OnSaveGUI = _ => settings.Save(modEntry);

			harmony = new Harmony(modEntry.Info.Id);
			harmony.PatchAll(Assembly.GetExecutingAssembly());

			enabled = true;
			return true;
		}

		public static void SpawnBlockerAt(int index, bool active = true)
		{
			if (placedBlockerIndices.Contains(index))
			{
				//Debug.Log($"[ShopRework] Blocker für Shop {index} wurde bereits platziert – überspringe.");
				return;
			}

			//BLOCKER MAIN
			GameObject cube1 = GameObject.CreatePrimitive(PrimitiveType.Cube);
			cube1.name = $"ShopBlocker_{index}";
			cube1.transform.position = shopPositions[index] + Quaternion.Euler(shopRotations[index]) * Vector3.right;
			cube1.transform.rotation = Quaternion.Euler(shopRotations[index]);
			cube1.transform.localScale = new Vector3(8.8f, 90f, 6f);
			cube1.layer = LayerMask.NameToLayer("Default");
			cube1.isStatic = true;
			
			cube1.GetComponent<Renderer>().enabled = false;
			
			BoxCollider trigger = cube1.AddComponent<BoxCollider>();
			trigger.isTrigger = true;
			cube1.AddComponent<ShopBlockerPlayerFilter>();
			UnityEngine.Object.DontDestroyOnLoad(cube1);
			cube1.SetActive(true);

			spawnedBlockers.Add(cube1);
			placedBlockerIndices.Add(index);
			
			//BLOCKER FLOOR
			GameObject cube2 = GameObject.CreatePrimitive(PrimitiveType.Cube);
			cube2.name = $"ShopBlocker_{index}";
			cube2.transform.position = shopPositions[index] + Quaternion.Euler(shopRotations[index]) * Vector3.right;
			cube2.transform.rotation = Quaternion.Euler(shopRotations[index]);
			cube2.transform.localScale = new Vector3(8.8f, 4.2f, 6f);
			cube2.layer = LayerMask.NameToLayer("Default");
			cube2.isStatic = true;
			
			cube2.GetComponent<Renderer>().material.color = Color.black;
			
			cube2.AddComponent<BoxCollider>();
			cube2.AddComponent<SimpleZoneBlocker>();
			UnityEngine.Object.DontDestroyOnLoad(cube2);
			cube2.SetActive(true);

			spawnedBlockers.Add(cube2);
			placedBlockerIndices.Add(index);
			
			//BLOCKER LEFT
			GameObject cube3 = GameObject.CreatePrimitive(PrimitiveType.Cube);
			cube3.name = $"ShopBlocker_{index}";
			cube3.transform.position = cube1.transform.position + cube1.transform.forward * 2.25f - cube1.transform.right * 1.0f;;
			cube3.transform.rotation = Quaternion.Euler(shopRotations[index]);
			cube3.transform.localScale = new Vector3(6.9f, 9f, 1.5f);
			cube3.layer = LayerMask.NameToLayer("Default");
			cube3.isStatic = true;
			
			cube3.GetComponent<Renderer>().material.color = Color.black;
			
			cube3.AddComponent<BoxCollider>();
			cube3.AddComponent<SimpleZoneBlocker>();
			UnityEngine.Object.DontDestroyOnLoad(cube3);
			cube3.SetActive(true);

			spawnedBlockers.Add(cube3);
			placedBlockerIndices.Add(index);
			
			//BLOCKER MIDDLE
			GameObject cube4 = GameObject.CreatePrimitive(PrimitiveType.Cube);
			cube4.name = $"ShopBlocker_{index}";
			cube4.transform.position = shopPositions[index] + Quaternion.Euler(shopRotations[index]) * Vector3.right;
			cube4.transform.rotation = Quaternion.Euler(shopRotations[index]);
			cube4.transform.localScale = new Vector3(8.8f, 6.15f, 6f);
			cube4.layer = LayerMask.NameToLayer("Default");
			cube4.isStatic = true;
			
			cube4.GetComponent<Renderer>().material.color = Color.black;
			
			cube4.AddComponent<BoxCollider>();
			cube4.AddComponent<SimpleZoneBlocker>();
			UnityEngine.Object.DontDestroyOnLoad(cube4);
			cube4.SetActive(true);

			spawnedBlockers.Add(cube4);
			placedBlockerIndices.Add(index);
			
			//BLOCKER RIGHT
			GameObject cube5 = GameObject.CreatePrimitive(PrimitiveType.Cube);
			cube5.name = $"ShopBlocker_{index}";
			cube5.transform.position = cube1.transform.position - cube1.transform.forward * 1.25f - cube1.transform.right * 1.0f;
			cube5.transform.rotation = Quaternion.Euler(shopRotations[index]);
			cube5.transform.localScale = new Vector3(6.9f, 9f, 3.5f);
			cube5.layer = LayerMask.NameToLayer("Default");
			cube5.isStatic = true;
			
			cube5.GetComponent<Renderer>().material.color = Color.black;
			
			cube5.AddComponent<BoxCollider>();
			cube5.AddComponent<SimpleZoneBlocker>();
			UnityEngine.Object.DontDestroyOnLoad(cube5);
			cube5.SetActive(true);

			spawnedBlockers.Add(cube5);
			placedBlockerIndices.Add(index);
			
			//BLOCKER BACK
			GameObject cube6 = GameObject.CreatePrimitive(PrimitiveType.Cube);
			cube6.name = $"ShopBlocker_{index}";
			cube6.transform.position = cube1.transform.position + cube1.transform.right * 3.75f;
			cube6.transform.rotation = Quaternion.Euler(shopRotations[index]);
			cube6.transform.localScale = new Vector3(1.2f, 9f, 6f);
			cube6.layer = LayerMask.NameToLayer("Default");
			cube6.isStatic = true;
			
			cube6.GetComponent<Renderer>().material.color = Color.black;
			
			cube6.AddComponent<BoxCollider>();
			cube6.AddComponent<SimpleZoneBlocker>();
			UnityEngine.Object.DontDestroyOnLoad(cube6);
			cube6.SetActive(true);

			spawnedBlockers.Add(cube6);
			placedBlockerIndices.Add(index);
			
			//BLOCKER DOOR
			GameObject cube7 = GameObject.CreatePrimitive(PrimitiveType.Cube);
			cube7.name = $"ShopBlocker_{index}";
			cube7.transform.position = cube1.transform.position - cube1.transform.right * 1.0f;
			cube7.transform.rotation = Quaternion.Euler(shopRotations[index]);
			cube7.transform.localScale = new Vector3(6.0f, 9f, 6f);
			cube7.layer = LayerMask.NameToLayer("Default");
			cube7.isStatic = true;
			
			cube7.GetComponent<Renderer>().material.color = Color.black;
			
			cube7.AddComponent<BoxCollider>();
			cube7.AddComponent<SimpleZoneBlocker>();
			UnityEngine.Object.DontDestroyOnLoad(cube7);
			cube7.SetActive(true);

			spawnedBlockers.Add(cube7);
			placedBlockerIndices.Add(index);
			
			//BLOCKER FRONT
			GameObject cube8 = GameObject.CreatePrimitive(PrimitiveType.Cube);
			cube8.name = $"ShopBlocker_{index}";
			cube8.transform.position = cube1.transform.position - cube1.transform.right * 1.0f;
			cube8.transform.rotation = Quaternion.Euler(shopRotations[index]);
			cube8.transform.localScale = new Vector3(6.9f, 9f, 6f);
			cube8.layer = LayerMask.NameToLayer("Default");
			cube8.isStatic = true;
			
			cube8.GetComponent<Renderer>().material.color = Color.black;
			
			cube8.AddComponent<BoxCollider>();
			cube8.AddComponent<SimpleZoneBlocker>();
			UnityEngine.Object.DontDestroyOnLoad(cube8);
			cube8.SetActive(true);

			spawnedBlockers.Add(cube8);
			placedBlockerIndices.Add(index);
		}

		public static void EnableAllBlockers()
		{
			foreach (var obj in spawnedBlockers)
				if (obj != null) obj.SetActive(true);
		}

		public static void DisableAllBlockers()
		{
			foreach (var obj in spawnedBlockers)
				if (obj != null) obj.SetActive(false);
		}
		
		public static void SpawnAllBlockersInactive()
		{
			for (int i = 0; i < shopPositions.Length; i++)
			{
				if (!placedBlockerIndices.Contains(i))
					SpawnBlockerAt(i, false);
			}
		}
	}
	
	public class ShopBlockerPlayerFilter : MonoBehaviour
	{
		private void OnTriggerStay(Collider other)
		{
			if (other.CompareTag("Player"))
			{
				Transform player = other.transform;
				Vector3 pushDirection = (player.position - transform.position).normalized;

				// Spieler sanft herausbewegen
				player.position += pushDirection * 0.0f;

				//Debug.Log($"[ShopRework] Spieler aus ShopBlocker entfernt: {player.name}");
			}
		}

		private void OnTriggerEnter(Collider other)
		{
			// Für alle anderen Objekte sofortige Kollision deaktivieren
			if (!other.CompareTag("Player") && other.attachedRigidbody != null)
			{
				Physics.IgnoreCollision(other, GetComponent<Collider>(), true);
				//Debug.Log($"[ShopRework] Ignoriere Kollision für Item: {other.name}");
			}
		}
	}

	public class ShopReworkWatcher : MonoBehaviour
	{
		private DateTime lastGameDayCheck = DateTime.MinValue;
		private float checkInterval = 60f;
		private float timeSinceLastCheck = 0f;
		private GameObject? player;
		
		private int lastNearestShopIndex = -1;
		private bool lastShouldBeBlocked = false;
		
		void Start()
		{
			player = GameObject.FindWithTag("Player");
		}

		void Update()
		{
			timeSinceLastCheck += Time.deltaTime;
			if (timeSinceLastCheck < checkInterval) return;
			timeSinceLastCheck = 0f;

			var clock = FindObjectOfType<WorldClockController>();
			if (clock == null) return;

			DateTime now = clock.GetCurrentAnglesAndTimeOfDay().timeOfDay;
			float hour = now.Hour + now.Minute / 60f;

			// Tageswechsel prüfen
			if (now.Date != Main.lastKnownDate.Date)
			{
				Main.lastKnownDate = now.Date;
				Main.settings.lastKnownDate = Main.lastKnownDate;
				Main.dayCounter++;
				Main.settings.persistentDayCounter = Main.dayCounter;
				Main.settings.Save(Main.mod);

				DayOfWeek weekday = (DayOfWeek)(Main.dayCounter % 7);
				Debug.Log($"[ShopRework] Actual Ingame-Day: {weekday}");

				bool weekly = Main.settings.UseWeeklyDiscounts;
				if (!weekly || weekday == DayOfWeek.Monday)
				{
					Debug.Log($"[ShopRework] Discounds: ({(weekly ? "Weekly" : "Daily")})");
					ShopReworkManager.ApplyNewDiscounts();
				}
				else
				{
					//Debug.Log($"[ShopRework] Keine neuen Discounts heute ({weekday}) – Weekly-Modus aktiv");
				}
			}

			if (!Main.settings.EnableShopBlocking) return;

			// Bedingungen für Blockierung prüfen
			DayOfWeek day = (DayOfWeek)(Main.dayCounter % 7);
			bool isSunday = day == DayOfWeek.Sunday;
			float open = Main.settings.ShopOpenHour;
			float close = Main.settings.ShopCloseHour;
			bool isNight = close > open ? (hour >= close || hour < open) : (hour >= close && hour < open);
			bool shouldBeBlocked = (Main.settings.ShopClosedOnSunday && isSunday) || isNight;

			if (player == null)
			{
				player = GameObject.FindWithTag("Player");
				if (player == null) return;
				Debug.Log("[ShopRework] Player found.");
			}
			Vector3 playerPos = player.transform.position;

			// Nächstgelegenen Shop bestimmen
			int nearestBlockedShopIndex = -1;
			float nearestDistance = float.MaxValue;

			for (int i = 0; i < Main.shopPositions.Length; i++)
			{
				float dist = Vector3.Distance(playerPos, Main.shopPositions[i]);
				if (dist < nearestDistance)
				{
					nearestDistance = dist;
					nearestBlockedShopIndex = i;
				}
			}

			// Status nur vom Main-Blocker prüfen (spart massiv Performance)
			if (shouldBeBlocked != lastShouldBeBlocked || lastNearestShopIndex != nearestBlockedShopIndex)
			{
				for (int i = 0; i < Main.shopPositions.Length; i++)
				{
					string blockerName = $"ShopBlocker_{i}";
					GameObject? mainBlocker = Main.spawnedBlockers.FirstOrDefault(b => b != null && b.name == blockerName);

					bool shouldActivate = (i == nearestBlockedShopIndex) && shouldBeBlocked;

					if (mainBlocker != null && mainBlocker.activeSelf != shouldActivate)
					{
						foreach (var b in Main.spawnedBlockers.Where(b => b != null && b.name == blockerName))
							b.SetActive(shouldActivate);

						Debug.Log($"[ShopRework] Shop {i} is {(shouldActivate ? "closed" : "opened")}.");
					}
				}

				lastShouldBeBlocked = shouldBeBlocked;
				lastNearestShopIndex = nearestBlockedShopIndex;
			}
		}
	}

	public class SimpleZoneBlocker : ZoneBlocker
	{
		public override string GetHoverText()
		{
			float open = Main.settings?.ShopOpenHour ?? 6f;
			float close = Main.settings?.ShopCloseHour ?? 22f;
			string Format(float h) => $"{Mathf.FloorToInt(h):00}:{Mathf.RoundToInt((h % 1) * 60):00}";

			return Main.settings?.ShopClosedOnSunday == true
				? $"Shop closed!\n \nOpening hours:\nMon – Sat\n{Format(open)} - {Format(close)}\nSunday: Closed\n "
				: $"Shop closed!\n \nOpening hours:\n{Format(open)} - {Format(close)}";
		}
	}

	public static class ShopReworkManager
	{
		private static List<ScanItemCashRegisterModule> allItems = new();
		private static Dictionary<ScanItemCashRegisterModule, float> originalPrices = new();
		private static bool discountsAppliedOnce = false;
		public static int ItemCount => allItems.Count;

		public static void RegisterShopItem(ScanItemCashRegisterModule item)
		{
			if (item == null || allItems.Contains(item)) return;

			bool isFromShop = item.transform.GetComponentInParent<Shop>() != null;
			if (!isFromShop) return;

			allItems.Add(item);
			if (item.Data != null && !originalPrices.ContainsKey(item))
				originalPrices[item] = item.Data.pricePerUnit;

			//Debug.Log($"[ShopRework] Shop-Item registriert: {item.name} – Preis: {item.Data?.pricePerUnit}");

			if (!discountsAppliedOnce && Main.enabled)
			{
				//Debug.Log("[ShopRework] Erste gültige Shop-Items registriert – wende initiale Discounts an.");
				discountsAppliedOnce = true;
				GameObject go = new GameObject("ShopDiscountDelay");
				go.AddComponent<ShopReworkDiscountDelay>();
				UnityEngine.Object.DontDestroyOnLoad(go);
			}
		}		
		
		public static void ApplyNewDiscounts()
		{
			if (!Main.enabled) return;
			//Debug.Log($"[ShopRework] Beginne Rabattvergabe. Aktive Items: {allItems.Count}");

			foreach (var i in allItems) ResetDiscount(i);

			int n = Mathf.Min(Main.settings.discountedItemsPerDay, allItems.Count);
			float pct = Main.settings.discountPercentage;

			Debug.Log($"[ShopRework] SETTINGS: Number of Items = {n}, Discount = {(pct == 0 ? "random" : pct + "%")}");

			var selected = allItems.OrderBy(x => UnityEngine.Random.value).Take(n).ToList();
			Debug.Log($"[ShopRework] {selected.Count} Shop-Items are in Sale!");

			foreach (var i in selected)
			{
				if (i?.Data == null) continue;
				if (!originalPrices.ContainsKey(i)) originalPrices[i] = i.Data.pricePerUnit;

				float discount = pct > 0 ? pct : UnityEngine.Random.Range(5f, 50f);
				float original = originalPrices[i];
				float newPrice = Mathf.Round(original * (1f - discount / 100f));
				i.Data.pricePerUnit = newPrice;
				i.UpdateTexts();

				var text = AccessPrivateText(i);
				if (text != null) text.color = new Color32(153, 0, 0, 255);

				Debug.Log($"[ShopRework] Item: {i.name}, Price: {original}$ -{discount:0.#}% = {newPrice}$)");
			}
		}

		private static void ResetDiscount(ScanItemCashRegisterModule item)
		{
			if (item?.Data == null) return;
			if (originalPrices.TryGetValue(item, out float p)) item.Data.pricePerUnit = p;
			item.UpdateTexts();
			var t = AccessPrivateText(item); if (t != null) t.color = Color.black;
		}

		private static TextMeshPro? AccessPrivateText(ScanItemCashRegisterModule item)
		{
			var f = typeof(ScanItemCashRegisterModule).GetField("itemPriceText", BindingFlags.NonPublic | BindingFlags.Instance);
			return f?.GetValue(item) as TextMeshPro;
		}
	}
	
	public class ShopReworkDiscountDelay : MonoBehaviour
	{
		private IEnumerator Start()
		{
			yield return new WaitForSeconds(10.0f);

			//Debug.Log($"[ShopRework] Discount-Delay beendet – aktuelle Itemanzahl: {ShopReworkManager.ItemCount}");
			ShopReworkManager.ApplyNewDiscounts();
			Destroy(this.gameObject);
		}
	}

	[HarmonyPatch(typeof(WorldStreamingInit), "Awake")]
	static class InitPatch
	{
		static void Postfix()
		{
			GameObject go = new GameObject("ShopReworkController");
			go.AddComponent<ShopReworkWatcher>();
			UnityEngine.Object.DontDestroyOnLoad(go);
			
			// NEU: Alle Blocker unsichtbar vorbereiten
			Main.SpawnAllBlockersInactive();
			Debug.Log("[ShopRework] All Shops initialised.");
		}
	}

	[HarmonyPatch(typeof(ScanItemCashRegisterModule), "Awake")]
	static class ShopItemPatch
	{
		static void Postfix(ScanItemCashRegisterModule __instance) => ShopReworkManager.RegisterShopItem(__instance);
	}
	
	[HarmonyPatch(typeof(Shop), "Awake")]
	static class ShopAwakePatch
	{
		static void Postfix(Shop __instance)
		{
			Main.allShops.Add(__instance);
			//Debug.Log($"[ShopRework] Shop registriert: {__instance.name} @ {__instance.transform.position}");
			Vector3 pos = __instance.transform.position;

			for (int i = 0; i < Main.shopPositions.Length; i++)
			{
				if (Vector3.Distance(pos, Main.shopPositions[i]) < 20f)
				{
					Main.mappedShops[i] = __instance;
					break;
				}
			}
		}
	}
}
