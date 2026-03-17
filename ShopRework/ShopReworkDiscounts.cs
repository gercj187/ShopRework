using UnityEngine;
using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Newtonsoft.Json.Linq;
using TMPro;
using DV.Shops;

namespace ShopRework
{
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

        private static readonly List<ScanItemCashRegisterModule> allItems = new();
        private static readonly Dictionary<string, float> originalPricesByKey = new();
		private static readonly Dictionary<string, ScanItemCashRegisterModule> itemByKey = new();

        public static readonly List<DiscountEntry> savedDiscountEntries = new();

		public static void RegisterShopItem(ScanItemCashRegisterModule item)
		{
			allItems.RemoveAll(i => i == null);
			
			if (!item || allItems.Contains(item))
				return;

			string shopName = GetShopNameFromItem(item);
			if (shopName == "Unknown")
				return;

			allItems.Add(item);

			if (item.Data != null)
			{
				string key = $"{shopName}::{item.name}";

				if (!originalPricesByKey.ContainsKey(key))
					originalPricesByKey[key] = item.Data.pricePerUnit;

				itemByKey[key] = item;
			}
		}
		
		private static void CleanupDestroyedItems()
		{
			allItems.RemoveAll(i => i == null);
		}
		
		public static void ResetAllDiscounts()
		{
			CleanupDestroyedItems();

			Debug.Log("[ShopRework] Resetting all discounts.");

			foreach (var item in allItems)
			{
				if (item == null || item.Data == null)
					continue;

				string shopName = GetShopNameFromItem(item);
				string key = $"{shopName}::{item.name}";

				if (originalPricesByKey.TryGetValue(key, out float original))
				{
					item.Data.pricePerUnit = original;
					item.UpdateTexts();
				}

				var text = AccessPrivateText(item);
				if (text != null)
					text.color = Color.black;
			}

			savedDiscountEntries.Clear();

			Debug.Log("[ShopRework] All discounts reset.");
		}

        public static void ApplyNewDiscounts()
        {
			CleanupDestroyedItems();
			
			if (!Main.settings.EnableShopDiscounts)
			{
				Debug.Log("[ShopRework] Discounts disabled in settings. Skipping generation.");
				ResetAllDiscounts();
				return;
			}
			
            Debug.Log($"[ShopRework] Generating discounts. Target: {Main.settings.discountedItemsPerDay}");

            foreach (var i in allItems)
                ResetDiscount(i);

            savedDiscountEntries.Clear();

            var eligible = allItems.Where(i =>
            {
                if (i == null || i.Data == null) return false;

                if (IsPaintCan(i))
                {
                    if (!Main.settings.AllowPaintCanDiscounts)
                        return false;

                    if (i.name.Contains("(Clone)") &&
                        !Main.settings.AllowModPaintCanDiscounts)
                        return false;
                }

                return true;
            }).ToList();

            Debug.Log($"[ShopRework] Eligible items: {eligible.Count}");

            int n = Mathf.Min(Main.settings.discountedItemsPerDay, eligible.Count);
            float pct = Main.settings.discountPercentage;

            var selected = eligible
                .OrderBy(x => UnityEngine.Random.value)
                .Take(n)
                .ToList();

            int running = 0;

            foreach (var i in selected)
            {
                string shopName = GetShopNameFromItem(i);
                string key = $"{shopName}::{i.name}";

                if (!originalPricesByKey.TryGetValue(key, out float original))
                    continue;

                float discount = pct > 0 ? pct : UnityEngine.Random.Range(5f, 50f);
                float newPrice = Mathf.Round(original * (1f - discount / 100f));

                i.Data.pricePerUnit = newPrice;
                i.UpdateTexts();
                SetTextRed(i);

                running++;

                savedDiscountEntries.Add(new DiscountEntry
                {
                    number = running,
                    itemName = i.name,
                    shopName = shopName,
                    discount = discount
                });

                Debug.Log($"[ShopRework] Discounted: {i.name} in {shopName} → {discount:F2}%");
            }

            Debug.Log($"[ShopRework] Total discounted items: {savedDiscountEntries.Count}");
        }

        public static void ReapplySavedDiscounts()
        {
			CleanupDestroyedItems();
			
			if (!Main.settings.EnableShopDiscounts)
			{
				Debug.Log("[ShopRework] Discounts disabled. Saved discounts will not be applied.");
				ResetAllDiscounts();
				return;
			}
			
            Debug.Log($"[ShopRework] Reapplying {savedDiscountEntries.Count} saved discounts.");

            foreach (var item in allItems)
                ResetDiscount(item);

            foreach (var entry in savedDiscountEntries.OrderBy(e => e.number))
			{
				string key = $"{entry.shopName}::{entry.itemName}";

				if (!itemByKey.TryGetValue(key, out var match) || match == null)
					continue;

				if (!originalPricesByKey.TryGetValue(key, out float original))
					continue;

                float newPrice = Mathf.Round(original * (1f - entry.discount / 100f));

                match.Data.pricePerUnit = newPrice;
                match.UpdateTexts();
                SetTextRed(match);

                Debug.Log($"[ShopRework] Reapplied: {match.name} in {entry.shopName} → {entry.discount:F2}%");
            }
        }

        public static void LoadSavedDiscountsFromToken(JToken? token)
        {
            savedDiscountEntries.Clear();

            if (token == null || token.Type != JTokenType.Array)
                return;

            var arr = (JArray)token;

            foreach (var t in arr)
            {
                var de = t.ToObject<DiscountEntry>();
                if (de != null)
                    savedDiscountEntries.Add(de);
            }
        }
		
		public static void ReapplyDiscountIfExists(ScanItemCashRegisterModule item)
		{
			if (!Main.settings.EnableShopDiscounts)
				return;
	
			if (item == null || item.Data == null)
				return;

			string shopName = GetShopNameFromItem(item);

			var entry = savedDiscountEntries.FirstOrDefault(e =>
				e.itemName == item.name &&
				e.shopName == shopName);

			if (entry == null)
				return;

			string key = $"{shopName}::{item.name}";
			if (!originalPricesByKey.TryGetValue(key, out float original))
				return;

			float newPrice = Mathf.Round(original * (1f - entry.discount / 100f));

			item.Data.pricePerUnit = newPrice;
			item.UpdateTexts();
			SetTextRed(item);
		}

        public static JArray ToJsonArraySorted()
        {
            var arr = new JArray();

            foreach (var e in savedDiscountEntries.OrderBy(e => e.number))
            {
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

        private static void ResetDiscount(ScanItemCashRegisterModule item)
        {
            if (item?.Data == null) return;

            string shopName = GetShopNameFromItem(item);
            string key = $"{shopName}::{item.name}";

            if (originalPricesByKey.TryGetValue(key, out float original))
            {
                item.Data.pricePerUnit = original;
                item.UpdateTexts();
            }

            var t = AccessPrivateText(item);
            if (t != null) t.color = Color.black;
        }

        private static void SetTextRed(ScanItemCashRegisterModule item)
        {
            var text = AccessPrivateText(item);
            if (text != null)
                text.color = new Color32(153, 0, 0, 255);
        }

        private static TextMeshPro? AccessPrivateText(ScanItemCashRegisterModule item)
        {
            var f = typeof(ScanItemCashRegisterModule)
                .GetField("itemPriceText", BindingFlags.NonPublic | BindingFlags.Instance);

            return f?.GetValue(item) as TextMeshPro;
        }

        private static bool IsPaintCan(ScanItemCashRegisterModule item)
        {
            return item.name == "PaintCan_ShelfItem"
                || item.name == "PaintCan_ShelfItem(Clone)";
        }

        public static string GetShopNameFromItem(ScanItemCashRegisterModule item)
        {
            if (item == null) return "Unknown";

            Transform t = item.transform;

            while (t != null)
            {
                var shop = t.GetComponent<Shop>();
                if (shop != null)
                    return t.gameObject.name;

                t = t.parent;
            }

            return "Unknown";
        }
    }

    [HarmonyPatch(typeof(ScanItemCashRegisterModule), "Awake")]
    static class ShopItemPatch
    {
        static void Postfix(ScanItemCashRegisterModule __instance)
        {
            ShopReworkManager.RegisterShopItem(__instance);
        }
    }
	
	[HarmonyPatch(typeof(ScanItemCashRegisterModule), "InitializeData")]
	static class ScanItemInitializePatch
	{
		static void Postfix(ScanItemCashRegisterModule __instance)
		{
			ShopReworkManager.ReapplyDiscountIfExists(__instance);
		}
	}
}