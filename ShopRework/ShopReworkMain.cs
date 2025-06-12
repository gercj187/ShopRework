// ShopReworkCombined.cs
using UnityModManagerNet;
using HarmonyLib;
using UnityEngine;
using System.Reflection;
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
        public bool EnableShopBlocking = true;
        public float ShopOpenHour = 6.0f;
        public float ShopCloseHour = 22.0f;
        public bool ShopClosedOnSunday = false;

        public override void Save(UnityModManager.ModEntry modEntry) => Save(this, modEntry);
        public void OnChange() { }

        public void Draw()
        {
            GUILayout.Label("<b>Daily Discount Configuration:</b>");
            discountedItemsPerDay = (int)GUILayout.HorizontalSlider(discountedItemsPerDay, 0, 50);
            GUILayout.Label($"Number of Discounted Items per Day: {discountedItemsPerDay}");

            discountPercentage = (int)GUILayout.HorizontalSlider(discountPercentage, 0, 50);
            GUILayout.Label($"Discount in % (0 = random): {(discountPercentage == 0 ? "random" : discountPercentage + "%")}");

            GUILayout.Space(10);
            GUILayout.Label("<b>Shop Closure Settings:</b>");
            EnableShopBlocking = GUILayout.Toggle(EnableShopBlocking, "Enable store closing hours");

            if (EnableShopBlocking)
            {
                ShopOpenHour = Mathf.Round(GUILayout.HorizontalSlider(ShopOpenHour, 0f, 23.9f) * 60f) / 60f;
                GUILayout.Label($"Opening time: {FormatHour(ShopOpenHour)}");

                ShopCloseHour = Mathf.Round(GUILayout.HorizontalSlider(ShopCloseHour, 0f, 23.9f) * 60f) / 60f;
                GUILayout.Label($"Closing time: {FormatHour(ShopCloseHour)}");

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
        private static readonly List<GameObject> spawnedBlockers = new();

        static bool Load(UnityModManager.ModEntry modEntry)
        {
            mod = modEntry;
            settings = UnityModManager.ModSettings.Load<Settings>(modEntry);
            modEntry.OnGUI = _ => settings.Draw();
            modEntry.OnSaveGUI = _ => settings.Save(modEntry);

            harmony = new Harmony(modEntry.Info.Id);
            harmony.PatchAll(Assembly.GetExecutingAssembly());

            GameObject go = new GameObject("ShopReworkController");
            go.AddComponent<ShopReworkWatcher>();
            UnityEngine.Object.DontDestroyOnLoad(go);

            enabled = true;
            return true;
        }

        public static void DestroyAllBlockers()
        {
            foreach (GameObject obj in spawnedBlockers)
                if (obj != null) UnityEngine.Object.Destroy(obj);
            spawnedBlockers.Clear();
        }

        public static void PlaceAllBlockers()
        {
            string[] names = { "CityWest", "MachineFactory", "GoodsFactory", "HarborCity", "FoodFactory" };
            Vector3[] positions = {
                new Vector3(266.8f, 120.3f, 25.0f), new Vector3(588.5f, 157.3f, 136.4f),
                new Vector3(689.8f, 138.2f, 462.4f), new Vector3(256.3f, 111.1f, 322.0f), new Vector3(477.1f, 117.3f, 250.9f)
            };
            Vector3[] rotationsY = {
                new Vector3(0f, 43.3f, 0f), new Vector3(0f, 181.6f, 0f),
                new Vector3(0f, 241.8f, 0f), new Vector3(0f, 273.0f, 0f), new Vector3(0f, 21.0f, 0f)
            };

            for (int i = 0; i < names.Length; i++)
            {
                GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
                cube.name = $"ShopBlocker_{names[i]}";
                cube.transform.position = positions[i] + Quaternion.Euler(rotationsY[i]) * Vector3.right;
                cube.transform.rotation = Quaternion.Euler(rotationsY[i]);
                cube.transform.localScale = new Vector3(8.8f, 9f, 6f);
                cube.layer = LayerMask.NameToLayer("Default");
                cube.isStatic = true;

                Material blackMat = new Material(Shader.Find("Standard"));
                blackMat.color = Color.black;
                cube.GetComponent<Renderer>().material = blackMat;

                cube.AddComponent<BoxCollider>();
                cube.AddComponent<SimpleZoneBlocker>();
                UnityEngine.Object.DontDestroyOnLoad(cube);

                spawnedBlockers.Add(cube);
            }
        }
    }

    public class ShopReworkWatcher : MonoBehaviour
    {
        private DateTime lastGameDayCheck;
        private float checkInterval = 10f;
        private float timeSinceLastCheck = 0f;
        private bool blockersActive = false;

        void Start() => lastGameDayCheck = DateTime.MinValue;

        void Update()
        {
            timeSinceLastCheck += Time.deltaTime;
            if (timeSinceLastCheck < checkInterval) return;
            timeSinceLastCheck = 0f;

            var clock = FindObjectOfType<WorldClockController>();
            if (clock == null) return;

            DateTime now = clock.GetCurrentAnglesAndTimeOfDay().timeOfDay;
            float hour = now.Hour + now.Minute / 60f;

            if (now.Day != lastGameDayCheck.Day)
            {
                ShopReworkManager.ApplyNewDiscounts();
                lastGameDayCheck = now;
                Main.dayCounter++;
                Main.lastKnownDate = now.Date;
            }

            if (!Main.settings.EnableShopBlocking) return;

            bool isSunday = (DayOfWeek)(Main.dayCounter % 7) == DayOfWeek.Sunday;
            if (Main.settings.ShopClosedOnSunday && isSunday)
            {
                if (!blockersActive) { Main.PlaceAllBlockers(); blockersActive = true; }
                return;
            }

            float open = Main.settings.ShopOpenHour, close = Main.settings.ShopCloseHour;
            bool isNight = close > open ? (hour >= close || hour < open) : (hour >= close && hour < open);

            if (isNight && !blockersActive) { Main.PlaceAllBlockers(); blockersActive = true; }
            else if (!isNight && blockersActive) { Main.DestroyAllBlockers(); blockersActive = false; }
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
                ? $"Shop closed!\n \nOpening hours:\nMon – Sat\n{Format(open)} - {Format(close)}\nSunday: Closed"
                : $"Shop closed!\n \nOpening hours:\n{Format(open)} - {Format(close)}";
        }
    }

    public static class ShopReworkManager
    {
        private static List<ScanItemCashRegisterModule> allItems = new();
        private static Dictionary<ScanItemCashRegisterModule, float> originalPrices = new();

        public static void RegisterShopItem(ScanItemCashRegisterModule item)
        {
            if (item == null || allItems.Contains(item)) return;
            allItems.Add(item);
            if (item.Data != null && !originalPrices.ContainsKey(item))
                originalPrices[item] = item.Data.pricePerUnit;
        }

        public static void ApplyNewDiscounts()
        {
            if (!Main.enabled) return;

            foreach (var i in allItems) ResetDiscount(i);

            int n = Mathf.Min(Main.settings.discountedItemsPerDay, allItems.Count);
            float pct = Main.settings.discountPercentage;

            var selected = allItems.OrderBy(x => UnityEngine.Random.value).Take(n);
            foreach (var i in selected)
            {
                if (i?.Data == null) continue;
                if (!originalPrices.ContainsKey(i)) originalPrices[i] = i.Data.pricePerUnit;

                float discount = pct > 0 ? pct : UnityEngine.Random.Range(5f, 50f);
                float newPrice = Mathf.Round(originalPrices[i] * (1f - discount / 100f));
                i.Data.pricePerUnit = newPrice;
                i.UpdateTexts();

                var text = AccessPrivateText(i);
                if (text != null) text.color = new Color32(153, 0, 0, 255);
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

    [HarmonyPatch(typeof(WorldStreamingInit), "Awake")]
    static class InitPatch
    {
        static void Postfix()
        {
            GameObject go = new GameObject("ShopReworkController");
            go.AddComponent<ShopReworkWatcher>();
            UnityEngine.Object.DontDestroyOnLoad(go);
        }
    }

    [HarmonyPatch(typeof(ScanItemCashRegisterModule), "Awake")]
    static class ShopItemPatch
    {
        static void Postfix(ScanItemCashRegisterModule __instance) => ShopReworkManager.RegisterShopItem(__instance);
    }
}
