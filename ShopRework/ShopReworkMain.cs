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
using Newtonsoft.Json.Linq;
using TMPro;

namespace ShopRework
{
    public class Settings : UnityModManager.ModSettings, IDrawable
    {
        [Range(0, 50)] public int discountedItemsPerDay = 3;
        [Range(0, 50)] public int discountPercentage = 0;
		
		public bool AllowPaintCanDiscounts = false;
		public bool AllowModPaintCanDiscounts = false;
        public bool UseWeeklyDiscounts = false;

        public bool EnableShopBlocking = true;
        public float ShopOpenHour = 6.0f;
        public float ShopCloseHour = 22.0f;
        public bool ShopClosedOnSunday = false;

        public bool DevDebug = false;

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
			GUILayout.Label("<b>Paint Can Discount Settings:</b>");

			AllowPaintCanDiscounts = GUILayout.Toggle(
				AllowPaintCanDiscounts,
				"Allow PaintCan discounts"
			);
			// Wenn Hauptoption AUS → Suboption erzwingen auf false
			if (!AllowPaintCanDiscounts)
			{
				AllowModPaintCanDiscounts = false;
			}
			else
			{
				AllowModPaintCanDiscounts = GUILayout.Toggle(
					AllowModPaintCanDiscounts,
					"Allow MOD PaintCan discounts"
				);
			}

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

            GUILayout.Space(10);
            GUILayout.Label("<b>Developer:</b>");
            DevDebug = GUILayout.Toggle(DevDebug, "DEV-DEBUG (print everything in log file)");
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

        public static readonly string[] shopDisplayNames = {
            "City South West",
            "Machine Factory",
            "Goods Factory",
            "Harbor",
            "Food Factory"
        };

        public static readonly Vector3[] shopPositions = {
            new Vector3(266.8f, 120.3f, 25.0f),
            new Vector3(588.5f, 157.3f, 136.4f),
            new Vector3(689.8f, 138.2f, 462.4f),
            new Vector3(256.3f, 111.1f, 322.0f),
            new Vector3(477.1f, 117.3f, 250.9f)
        };

        public static readonly Vector3[] shopRotations = {
            new Vector3(0f, 43.3f, 0f),
            new Vector3(0f, 181.6f, 0f),
            new Vector3(0f, 241.8f, 0f),
            new Vector3(0f, 273.0f, 0f),
            new Vector3(0f, 21.0f, 0f)
        };

        public static readonly List<GameObject> spawnedBlockers = new();
        public static readonly HashSet<int> placedBlockerIndices = new();
        public static readonly Dictionary<int, Shop> mappedShops = new();
        public static readonly List<Shop> allShops = new();

        static bool Load(UnityModManager.ModEntry modEntry)
        {
            mod = modEntry;
            settings = UnityModManager.ModSettings.Load<Settings>(modEntry);
            modEntry.OnGUI = _ => settings.Draw();
            modEntry.OnSaveGUI = _ => settings.Save(modEntry);

            harmony = new Harmony(modEntry.Info.Id);
            harmony.PatchAll(Assembly.GetExecutingAssembly());

            enabled = true;
            return true;
        }

        public static void SpawnBlockerAt(int index, bool active = true)
        {
            if (placedBlockerIndices.Contains(index)) return;

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

            GameObject cube3 = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cube3.name = $"ShopBlocker_{index}";
            cube3.transform.position = cube1.transform.position + cube1.transform.forward * 2.25f - cube1.transform.right * 1.0f;
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

        public static string GetShopNameFromItem(ScanItemCashRegisterModule item)
        {
            if (item == null) return "Unknown";
            Transform t = item.transform;
            while (t != null)
            {
                var shop = t.GetComponent<Shop>();
                if (shop != null) return t.gameObject.name;
                t = t.parent;
            }
            return "Unknown";
        }

        public static string GetFriendlyShopName(Shop shop)
        {
            if (shop == null) return "Unknown";
            return shop.gameObject.name;
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
                player.position += pushDirection * 0.0f;
            }
        }

        private void OnTriggerEnter(Collider other)
        {
            if (!other.CompareTag("Player") && other.attachedRigidbody != null)
            {
                Physics.IgnoreCollision(other, GetComponent<Collider>(), true);
            }
        }
    }

    public class PersistentDiscountID : MonoBehaviour
    {
        [SerializeField] public string persistentID = System.Guid.NewGuid().ToString();
    }

    /// <summary>
    /// NEU: Batch‑Trigger, der beim Awake der Shop‑Items ein Reapply nach 2s auslöst.
    /// Jeder weitere Awake‑Call verschiebt das Fenster erneut (debounced).
    /// Läuft nur, wenn gespeicherte Discounts vorhanden sind.
    /// </summary>
    public class ShopItemAwakeBatchApplier : MonoBehaviour
    {
        private static ShopItemAwakeBatchApplier? _instance;
        private float _nextRunAt = -1f;
        private const float DELAY_SECONDS = 2.0f;

        public static void Touch()
        {
            if (_instance == null)
            {
                var go = new GameObject("ShopRework_BatchApplier");
                _instance = go.AddComponent<ShopItemAwakeBatchApplier>();
                DontDestroyOnLoad(go);
            }
            // Jedes Touch verschiebt den geplanten Zeitpunkt nach hinten
            _instance._nextRunAt = Time.realtimeSinceStartup + DELAY_SECONDS;
        }

        private void Update()
        {
            if (_nextRunAt < 0f) return;
            if (Time.realtimeSinceStartup < _nextRunAt) return;

            // Reset Zielzeitpunkt vor Ausführung
            _nextRunAt = -1f;

            if (ShopReworkManager.savedDiscountEntries.Count > 0)
            {
                if (Main.settings.DevDebug)
                    Debug.Log("[ShopRework] BatchApplier: Reapplying saved discounts after item-awake batch.");
                ShopReworkManager.ReapplySavedDiscounts();
            }
        }
    }

    public class ShopReworkWatcher : MonoBehaviour
    {
        private float checkInterval = 60f;
        private float timeSinceLastCheck = 0f;
        private bool? lastShopStatus = null;

        private int lastKnownItemCount = 0;
        private bool reapplyDoneAfterLoad = false;

        void Update()
        {
            timeSinceLastCheck += Time.deltaTime;
            if (timeSinceLastCheck < checkInterval) return;
            timeSinceLastCheck = 0f;

            if (!reapplyDoneAfterLoad)
            {
                ShopReworkManager.ReapplySavedDiscounts();
                lastKnownItemCount = ShopReworkManager.ItemCount;
                reapplyDoneAfterLoad = true;
            }
            else if (Main.settings.DevDebug && ShopReworkManager.ItemCount > lastKnownItemCount)
            {
                lastKnownItemCount = ShopReworkManager.ItemCount;
                ShopReworkManager.ReapplySavedDiscounts();
            }

            var clock = UnityEngine.Object.FindObjectOfType<WorldClockController>();
            if (clock != null)
            {
                DateTime now = clock.GetCurrentAnglesAndTimeOfDay().timeOfDay;
                DateTime today = now.Date;
                DateTime lastSaved = Main.settings.lastKnownDate.Date;

                if (today != lastSaved)
                {
                    Main.dayCounter++;
                    Main.settings.lastKnownDate = today;
                    Main.settings.Save(Main.mod);

                    DayOfWeek weekday = (DayOfWeek)(Main.dayCounter % 7);
                    bool weekly = Main.settings.UseWeeklyDiscounts;

                    if (!weekly || weekday == DayOfWeek.Monday)
                    {
                        Debug.Log($"[ShopRework] Discounts: ({(weekly ? "Weekly" : "Daily")})");
                        ShopReworkManager.ApplyNewDiscounts();
                    }
                }
            }

            HandleShopBlockers();
        }

        private void HandleShopBlockers()
        {
            var clock = UnityEngine.Object.FindObjectOfType<WorldClockController>();
            if (clock == null) return;

            DateTime now = clock.GetCurrentAnglesAndTimeOfDay().timeOfDay;
            float hour = now.Hour + now.Minute / 60f;
            DayOfWeek day = (DayOfWeek)(Main.dayCounter % 7);
            bool isSunday = day == DayOfWeek.Sunday;
            float open = Main.settings.ShopOpenHour;
            float close = Main.settings.ShopCloseHour;
            bool isNight = close > open ? (hour >= close || hour < open) : (hour >= close && hour < open);
            bool shouldBeBlocked = (Main.settings.ShopClosedOnSunday && isSunday) || isNight;

            if (!shouldBeBlocked)
            {
                foreach (var b in Main.spawnedBlockers)
                    if (b != null && b.activeSelf)
                        b.SetActive(false);

                if (lastShopStatus != false)
                {
                    Debug.Log("[ShopRework] All Shops are opened.");
                    lastShopStatus = false;
                }
                return;
            }

            var allShopItems = GameObject.FindObjectsOfType<ScanItemCashRegisterModule>();
            for (int i = 0; i < Main.shopPositions.Length; i++)
            {
                string blockerName = $"ShopBlocker_{i}";
                bool hasNearbyShopItem = allShopItems.Any(item =>
                    item != null && Vector3.Distance(item.transform.position, Main.shopPositions[i]) < 30f);

                foreach (var b in Main.spawnedBlockers.Where(b => b != null && b.name == blockerName))
                {
                    if (b.activeSelf != hasNearbyShopItem)
                        b.SetActive(hasNearbyShopItem);
                }
            }

            if (lastShopStatus != true)
            {
                Debug.Log("[ShopRework] All Shops are closed.");
                lastShopStatus = true;
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
        [Serializable]
        public class DiscountEntry
        {
            public int number;
            public string itemName = "";
            public string shopName = "";
            public float discount;
        }

        private static List<ScanItemCashRegisterModule> allItems = new();
        private static Dictionary<ScanItemCashRegisterModule, float> originalPrices = new();

        private static bool discountsAppliedOnce = false;
        private static bool discountsLoadedFromSavegame = false;

        private static bool saveDirty = false;

        private static readonly Dictionary<(string shop, string item, int number), ScanItemCashRegisterModule> sessionAssignment
            = new();

        public static readonly List<DiscountEntry> savedDiscountEntries = new();

        public static int ItemCount => allItems.Count;

		private static bool IsPaintCan(ScanItemCashRegisterModule item)
		{
			if (item == null) return false;

			return item.name == "PaintCan_ShelfItem"
				|| item.name == "PaintCan_ShelfItem(Clone)";
		}

        public static void RegisterShopItem(ScanItemCashRegisterModule item)
        {
            if (item == null || allItems.Contains(item)) return;

            string shopNameByParent = Main.GetShopNameFromItem(item);
            bool isFromShop = shopNameByParent != "Unknown";
            if (!isFromShop) return;

            allItems.Add(item);

            if (item.Data != null && !originalPrices.ContainsKey(item))
            {
                originalPrices[item] = item.Data.pricePerUnit;
                if (Main.settings.DevDebug)
                    Debug.Log($"[ShopRework] Registered shop item: {item.name} price {item.Data.pricePerUnit} at {shopNameByParent}");
            }
            else
            {
                if (Main.settings.DevDebug)
                    Debug.Log($"[ShopRework] Registered shop item: {item.name} no price data at {shopNameByParent}");
            }

            if (Main.settings.DevDebug && !discountsAppliedOnce && Main.enabled && !discountsLoadedFromSavegame)
            {
                discountsAppliedOnce = true;
                GameObject go = new GameObject("ShopDiscountDelay");
                go.AddComponent<ShopReworkDiscountDelay>();
                UnityEngine.Object.DontDestroyOnLoad(go);
            }
        }

        public static void ApplyNewDiscounts()
		{
			if (!Main.enabled) return;

			// Reset aller bestehenden Discounts
			foreach (var i in allItems)
				ResetDiscount(i);

			savedDiscountEntries.Clear();
			sessionAssignment.Clear();

			// ----------------------------------------------------
			// FILTER: PaintCan-Logik basierend auf Settings
			// ----------------------------------------------------
			var eligibleItems = allItems.Where(item =>
			{
				if (item == null || item.Data == null)
					return false;

				if (IsPaintCan(item))
				{
					// Hauptschalter AUS -> keinerlei PaintCan-Discounts
					if (!Main.settings.AllowPaintCanDiscounts)
						return false;

					// Mod-PaintCans (Clone)
					if (item.name.Contains("(Clone)") &&
						!Main.settings.AllowModPaintCanDiscounts)
						return false;
				}

				return true;
			}).ToList();

			int n = Mathf.Min(Main.settings.discountedItemsPerDay, eligibleItems.Count);
			float pct = Main.settings.discountPercentage;

			Debug.Log($"[ShopRework] SETTINGS: Number of Items = {n}, Discount = {(pct == 0 ? "random" : pct + "%")}");

			var selected = eligibleItems
				.OrderBy(x => UnityEngine.Random.value)
				.Take(n)
				.ToList();

			Debug.Log($"[ShopRework] {selected.Count} Shop-Items are in Sale!");

			int runningNumber = 0;

			// ----------------------------------------------------
			// APPLY DISCOUNTS
			// ----------------------------------------------------
			foreach (var i in selected)
			{
				if (i == null || i.Data == null)
					continue;

				if (!originalPrices.ContainsKey(i))
					originalPrices[i] = i.Data.pricePerUnit;

				string shopName = Main.GetShopNameFromItem(i);

				float discount = pct > 0 ? pct : UnityEngine.Random.Range(5f, 50f);
				float original = originalPrices.TryGetValue(i, out float p)
					? p
					: i.Data.pricePerUnit;

				float newPrice = Mathf.Round(original * (1f - discount / 100f));
				i.Data.pricePerUnit = newPrice;
				i.UpdateTexts();

				var text = AccessPrivateText(i);
				if (text != null)
					text.color = new Color32(153, 0, 0, 255);

				runningNumber++;

				savedDiscountEntries.Add(new DiscountEntry
				{
					number = runningNumber,
					itemName = i.name,
					shopName = shopName,
					discount = discount
				});

				sessionAssignment[(shopName, i.name, runningNumber)] = i;

				if (Main.settings.DevDebug)
				{
					string cleanName = CleanItemName(i.name);
					Debug.Log($"[ShopRework] Shop '{shopName}' discounted {cleanName}, Price: {original}$ -{discount:0.#}% = {newPrice}$");
				}
			}
			MarkSaveDirty();
		}


        public static void ReapplySavedDiscounts()
        {
            if (savedDiscountEntries.Count == 0) return;

            foreach (var item in allItems) ResetDiscount(item);

            var candidatesByKey = new Dictionary<(string shop, string item), List<ScanItemCashRegisterModule>>();

            foreach (var it in allItems.Where(x => x != null))
            {
                string shopName = Main.GetShopNameFromItem(it);
                var key = (shopName, it.name);
                if (!candidatesByKey.TryGetValue(key, out var list))
                {
                    list = new List<ScanItemCashRegisterModule>();
                    candidatesByKey[key] = list;
                }
                list.Add(it);
            }

            var assignedThisRun = new HashSet<ScanItemCashRegisterModule>();

            foreach (var entry in savedDiscountEntries.OrderBy(e => e.number))
            {
                var key = (entry.shopName, entry.itemName);
                ScanItemCashRegisterModule? chosen = null;

                if (sessionAssignment.TryGetValue((entry.shopName, entry.itemName, entry.number), out var fixedItem)
                    && fixedItem != null && candidatesByKey.TryGetValue(key, out var list1)
                    && list1.Contains(fixedItem))
                {
                    chosen = fixedItem;
                }
                else
                {
                    if (candidatesByKey.TryGetValue(key, out var list2))
                    {
                        var free = list2.Where(x => x != null && !assignedThisRun.Contains(x)).ToList();
                        if (free.Count > 0)
                        {
                            chosen = free[UnityEngine.Random.Range(0, free.Count)];
                            sessionAssignment[(entry.shopName, entry.itemName, entry.number)] = chosen;
                        }
                    }
                }

                if (chosen == null) continue;

                ApplyDiscountToItem(chosen, entry.discount);
                assignedThisRun.Add(chosen);

                if (Main.settings.DevDebug)
                {
                    string cleanName = CleanItemName(chosen.name);
                    float original = originalPrices.TryGetValue(chosen, out float p) ? p : chosen.Data.pricePerUnit;
                    float newPrice = chosen.Data.pricePerUnit;
                    Debug.Log($"[ShopRework] (Reapply) Shop '{entry.shopName}' discounted {cleanName}, Price: {original}$ -{entry.discount:0.#}% = {newPrice}$");
                }
            }

            if (Main.settings.DevDebug)
                Debug.Log("[ShopRework] Discounts loaded/reapplied from savegame.");
        }

        private static void ApplyDiscountToItem(ScanItemCashRegisterModule item, float discount)
        {
            if (item == null || item.Data == null) return;

            if (!originalPrices.ContainsKey(item))
                originalPrices[item] = item.Data.pricePerUnit;

            float original = originalPrices.TryGetValue(item, out float p) ? p : item.Data.pricePerUnit;
            float newPrice = Mathf.Round(original * (1f - discount / 100f));
            item.Data.pricePerUnit = newPrice;
            item.UpdateTexts();

            var text = AccessPrivateText(item);
            if (text != null) text.color = new Color32(153, 0, 0, 255);
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

        private static string CleanItemName(string raw)
        {
            return raw.Replace("_ShelfItem", "").Replace("(Clone)", "").Trim();
        }

        public static void LoadSavedDiscountsFromToken(JToken? token)
        {
            savedDiscountEntries.Clear();
            sessionAssignment.Clear();
            discountsLoadedFromSavegame = true;

            if (token == null || token.Type == JTokenType.Null)
            {
                Debug.Log("[ShopRework] No saved discounts found.");
            }
            else if (token.Type == JTokenType.Array)
            {
                try
                {
                    var arr = (JArray)token;
                    foreach (var t in arr)
                    {
                        var de = t.ToObject<DiscountEntry>();
                        if (de != null && !string.IsNullOrEmpty(de.itemName) && !string.IsNullOrEmpty(de.shopName))
                            savedDiscountEntries.Add(de);
                    }
                    Debug.Log($"[ShopRework] Discounts loaded from Savegame: {savedDiscountEntries.Count} entries.");
                }
                catch (Exception ex)
                {
                    Debug.Log($"[ShopRework] Failed to parse discounts: {ex}");
                }
            }
            else if (token.Type == JTokenType.Object)
            {
                Debug.Log("[ShopRework] Found legacy discount format (per-ID). Unsupported due to unstable IDs, ignoring.");
            }
            else
            {
                Debug.Log("[ShopRework] Unknown discount token format – ignoring.");
            }
        }

        public static JArray ToJsonArraySorted()
        {
            var arr = new JArray();
            if (savedDiscountEntries.Count == 0) return arr;

            savedDiscountEntries.Sort((a, b) => a.number.CompareTo(b.number));
            for (int i = 0; i < savedDiscountEntries.Count; i++)
            {
                var e = savedDiscountEntries[i];
                var o = new JObject
                {
                    ["number"] = e.number,
                    ["itemName"] = e.itemName,
                    ["shopName"] = e.shopName,
                    ["discount"] = e.discount
                };
                arr.Add(o);
            }
            return arr;
        }

        public static void MarkSaveDirty() => saveDirty = true;

        public static bool ShouldPersistNowAndReset()
        {
            if (Main.settings.DevDebug) return true;
            if (saveDirty) { saveDirty = false; return true; }
            return false;
        }
    }

    [HarmonyPatch(typeof(StartGameData_FromSaveGame), "MakeCurrent")]
    static class Patch_StartGameData_FromSaveGame_MakeCurrent
    {
        static void Postfix(StartGameData_FromSaveGame __instance)
        {
            var saveData = Traverse.Create(__instance).Field("saveGameData").GetValue<SaveGameData>();
            if (saveData == null) return;

            var dataObject = Traverse.Create(saveData).Field("dataObject").GetValue<JObject>();
            if (dataObject == null)
            {
                Debug.Log("[ShopRework] SaveGameData.dataObject is null – aborting load.");
                return;
            }

            if (!dataObject.ContainsKey("ShopRework_CurrentDay"))
            {
                Main.dayCounter = 1;
                Debug.Log("[ShopRework] Created new savegame entry 'ShopRework_CurrentDay' – starting at day 1 (Monday).");
            }
            else
            {
                var dayToken = dataObject["ShopRework_CurrentDay"];
                if (dayToken != null && dayToken.Type != JTokenType.Null)
                {
                    Main.dayCounter = dayToken.ToObject<int>();
                    DayOfWeek weekday = (DayOfWeek)(Main.dayCounter % 7);
                    Debug.Log($"[ShopRework] Savegame loaded, current day: {weekday}");
                }
                else
                {
                    Main.dayCounter = 1;
                    Debug.Log("[ShopRework] 'ShopRework_CurrentDay' was null – reset to 1.");
                }
            }

            if (dataObject.TryGetValue("ShopRework_Discounts", out JToken? token) && token != null && token.Type != JTokenType.Null)
            {
                ShopReworkManager.LoadSavedDiscountsFromToken(token);
            }
            else
            {
                Debug.Log("[ShopRework] No 'ShopRework_Discounts' key in savegame – nothing to load.");
            }
        }
    }

    [HarmonyPatch(typeof(SaveGameManager), "Save")]
    static class Patch_SaveGameSave
    {
        static void Prefix()
        {
            var saveData = SaveGameManager.Instance?.data;
            if (saveData == null) return;

            var trav = Traverse.Create(saveData).Field("dataObject");
            var dataObject = trav.GetValue<JObject>() ?? new JObject();
            if (trav.GetValue<JObject>() == null)
                trav.SetValue(dataObject);

            dataObject["ShopRework_CurrentDay"] = Main.dayCounter;

            if (ShopReworkManager.ShouldPersistNowAndReset())
            {
                dataObject["ShopRework_Discounts"] = ShopReworkManager.ToJsonArraySorted();
                if (Main.settings.DevDebug)
                    Debug.Log("[ShopRework] Save wrote discounts.");
            }
            else
            {
                if (Main.settings.DevDebug)
                    Debug.Log("[ShopRework] Save skipped writing discounts (not dirty).");
            }
        }
    }

    public class ShopReworkDiscountDelay : MonoBehaviour
    {
        private IEnumerator Start()
        {
            yield return new WaitForSeconds(10.0f);
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

            var allShopItems = GameObject.FindObjectsOfType<ScanItemCashRegisterModule>();
            for (int i = 0; i < Main.shopPositions.Length; i++)
            {
                bool hasNearbyShopItem = allShopItems.Any(item =>
                    item != null &&
                    Vector3.Distance(item.transform.position, Main.shopPositions[i]) < 30f);

                if (hasNearbyShopItem)
                    Main.SpawnBlockerAt(i, false);
            }

            Debug.Log("[ShopRework] Shops initialised.");
        }
    }

    [HarmonyPatch(typeof(ScanItemCashRegisterModule), "Awake")]
    static class ShopItemPatch
    {
        static void Postfix(ScanItemCashRegisterModule __instance)
        {
            ShopReworkManager.RegisterShopItem(__instance);

            // NEU: Nur wenn gespeicherte Discounts existieren, einen Batch-Reapply nach 2s planen.
            if (ShopReworkManager.savedDiscountEntries.Count > 0)
                ShopItemAwakeBatchApplier.Touch();
        }
    }

    [HarmonyPatch(typeof(Shop), "Awake")]
    static class ShopAwakePatch
    {
        static void Postfix(Shop __instance)
        {
            Main.allShops.Add(__instance);

            Vector3 pos = __instance.transform.position;
            for (int i = 0; i < Main.shopPositions.Length; i++)
            {
                if (Vector3.Distance(pos, Main.shopPositions[i]) < 20f)
                {
                    Main.mappedShops[i] = __instance;
                    break;
                }
            }
            ShopReworkManager.ReapplySavedDiscounts();
        }
    }

    [HarmonyPatch(typeof(StartGameData_NewCareer), "PrepareNewSaveData")]
    static class Patch_NewCareerPrepareNewSave
    {
        static void Postfix(ref SaveGameData saveGameData)
        {
            if (saveGameData == null) return;

            var trav = Traverse.Create(saveGameData).Field("dataObject");
            var dataObject = trav.GetValue<JObject>() ?? new JObject();
            if (trav.GetValue<JObject>() == null)
                trav.SetValue(dataObject);

            if (!dataObject.ContainsKey("ShopRework_CurrentDay"))
            {
                Main.dayCounter = 1;
                dataObject["ShopRework_CurrentDay"] = Main.dayCounter;
                Debug.Log("[ShopRework] New career started – 'ShopRework_CurrentDay' – starting at day 1 (Monday).");
            }
            if (!dataObject.ContainsKey("ShopRework_Discounts"))
            {
                UnityEngine.Object.DontDestroyOnLoad(new GameObject("ShopDiscountInit")
                    .AddComponent<ShopReworkDiscountDelay>());
            }
        }
    }
}
