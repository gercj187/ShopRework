using UnityModManagerNet;
using HarmonyLib;
using UnityEngine;
using System;
using System.Reflection;
using Newtonsoft.Json.Linq;
using DV.Utils;
using DV.TimeKeeping;

namespace ShopRework
{
    public class Settings : UnityModManager.ModSettings, IDrawable
    {
        [Range(1, 50)] public int discountedItemsPerDay = 5;
        [Range(0, 50)] public int discountPercentage = 0;

        public bool AllowPaintCanDiscounts = false;
        public bool AllowModPaintCanDiscounts = false;

        public bool UseDailyDiscounts = true;
        public bool UseWeeklyDiscounts = false;
		
		public bool EnableShopDiscounts = true;
		public bool EnableShopBlocking = true;
		
		private float ParseHour(string time)
		{
			if (TimeSpan.TryParse(time, out var ts))
				return ts.Hours + ts.Minutes / 60f;

			return 0f;
		}
		
		private float RoundToFiveMinutes(float hour)
		{
			float step = 5f / 60f;
			float rounded = Mathf.Round(hour / step) * step;

			if (rounded >= 23.999f)
				return 24f;

			return Mathf.Clamp(rounded, 0f, 24f);
		}
		
		[Serializable]
		public class DaySchedule
		{
			public bool keepClosed = false;
			public string openTime = "06:00";
			public string closeTime = "22:00";
		}

		public DaySchedule Monday = new DaySchedule
		{
			openTime = "06:00",
			closeTime = "22:00"
		};

		public DaySchedule Tuesday = new DaySchedule
		{
			openTime = "06:00",
			closeTime = "22:00"
		};

		public DaySchedule Wednesday = new DaySchedule
		{
			openTime = "06:00",
			closeTime = "22:00"
		};

		public DaySchedule Thursday = new DaySchedule
		{
			openTime = "06:00",
			closeTime = "22:00"
		};

		public DaySchedule Friday = new DaySchedule
		{
			openTime = "06:00",
			closeTime = "22:00"
		};

		public DaySchedule Saturday = new DaySchedule
		{
			openTime = "10:00",
			closeTime = "18:00"
		};

		public DaySchedule Sunday = new DaySchedule
		{
			keepClosed = true,
			openTime = "00:00",
			closeTime = "00:00"
		};
		
		public DaySchedule GetSchedule(DayOfWeek day)
		{
			switch (day)
			{
				case DayOfWeek.Monday: return Monday;
				case DayOfWeek.Tuesday: return Tuesday;
				case DayOfWeek.Wednesday: return Wednesday;
				case DayOfWeek.Thursday: return Thursday;
				case DayOfWeek.Friday: return Friday;
				case DayOfWeek.Saturday: return Saturday;
				case DayOfWeek.Sunday: return Sunday;
				default: return Monday;
			}
		}

        public bool DevDebug = false;

        public override void Save(UnityModManager.ModEntry modEntry)
            => Save(this, modEntry);

        public void OnChange() { }

        public void Draw()
        {
            GUILayout.Label("<b>Discount Configuration:</b>");
            GUILayout.Space(2);
			GUILayout.BeginVertical(GUI.skin.box);
			GUILayout.Space(5);
			EnableShopDiscounts = GUILayout.Toggle(EnableShopDiscounts, "Enable shop discounts");
			if (EnableShopDiscounts)
			{
				bool dailyBefore = UseDailyDiscounts;
				bool dailyAfter = GUILayout.Toggle(UseDailyDiscounts, "Apply daily discounts");
				if (dailyAfter && !dailyBefore)
				{
					UseDailyDiscounts = true;
					UseWeeklyDiscounts = false;
				}

				bool weeklyBefore = UseWeeklyDiscounts;
				bool weeklyAfter = GUILayout.Toggle(UseWeeklyDiscounts, "Apply weekly discounts");
				if (weeklyAfter && !weeklyBefore)
				{
					UseWeeklyDiscounts = true;
					UseDailyDiscounts = false;
				}

				if (!UseDailyDiscounts && !UseWeeklyDiscounts)
					UseDailyDiscounts = true;

				GUILayout.Label($"Number of Discounted Items: {discountedItemsPerDay}");
				discountedItemsPerDay = (int)GUILayout.HorizontalSlider(discountedItemsPerDay, 1, 50,GUILayout.Width(500));

				GUILayout.Label($"Discount in % (0 = random): {(discountPercentage == 0 ? "random" : discountPercentage + "%")}");
				discountPercentage = (int)GUILayout.HorizontalSlider(discountPercentage, 0, 50,GUILayout.Width(500));

				GUILayout.Space(5);
				GUILayout.Label("<b>Paint Can Discount Settings:</b>");

				AllowPaintCanDiscounts = GUILayout.Toggle(
					AllowPaintCanDiscounts,
					"Allow PaintCan discounts"
				);

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
			}
            GUILayout.Space(5);
			GUILayout.EndVertical();	
            GUILayout.Space(2);
			GUILayout.Label("<b>Shop Closure Settings:</b>");
            GUILayout.Space(2);
			GUILayout.BeginVertical(GUI.skin.box);
			EnableShopBlocking = GUILayout.Toggle(EnableShopBlocking, "Enable store closing hours");

			if (EnableShopBlocking)
			{
				GUILayout.Space(5);
				GUILayout.Label("<b>Shop Opening Hours</b>");

				DrawDay("Mon", Monday);
				DrawDay("Tue", Tuesday);
				DrawDay("Wed", Wednesday);
				DrawDay("Thu", Thursday);
				DrawDay("Fri", Friday);
				DrawDay("Sat", Saturday);
				DrawDay("Sun", Sunday);
			}
            GUILayout.Space(5);
			GUILayout.EndVertical();	
            GUILayout.Space(2);
        }			
		private void DrawDay(string label, DaySchedule day)
		{
			GUILayout.BeginHorizontal();

			GUILayout.Label(label + " :", GUILayout.Width(45));

			day.keepClosed = GUILayout.Toggle(day.keepClosed, "keep closed", GUILayout.Width(100));

			if (!day.keepClosed)
			{
				float open = ParseHour(day.openTime);
				float close = ParseHour(day.closeTime);

				GUILayout.Label($"Open: {FormatHour(open)}", GUILayout.Width(90));
				open = RoundToFiveMinutes(GUILayout.HorizontalSlider(open, 0f, 24f, GUILayout.Width(250)));
				close = RoundToFiveMinutes(GUILayout.HorizontalSlider(close, 0f, 24f, GUILayout.Width(250)));
				GUILayout.Label($"Close: {FormatHour(close)}", GUILayout.Width(90));

				day.openTime = FormatHour(open);
				day.closeTime = FormatHour(close);
			}
			else
			{
				GUILayout.Label("Closed!");
			}

			GUILayout.EndHorizontal();
		}
		private string FormatHour(float hourFloat)
		{
			if (hourFloat >= 24f)
				return "24:00";

			int hour = Mathf.FloorToInt(hourFloat);
			int minute = Mathf.RoundToInt((hourFloat - hour) * 60f);

			return $"{hour:00}:{minute:00}";
		}
    }

    static class Main
    {
        public static Settings settings = null!;
        public static Harmony harmony = null!;
        public static UnityModManager.ModEntry mod = null!;
        public static bool enabled;

        public static DayOfWeek CurrentDay = DayOfWeek.Monday;
        private static DateTime lastCheckedDate = DateTime.MinValue;

        static bool Load(UnityModManager.ModEntry modEntry)
        {
            mod = modEntry;
            settings = UnityModManager.ModSettings.Load<Settings>(modEntry);

            modEntry.OnGUI = _ => settings.Draw();
            modEntry.OnSaveGUI = _ => settings.Save(modEntry);

            harmony = new Harmony(modEntry.Info.Id);
            harmony.PatchAll(Assembly.GetExecutingAssembly());

            enabled = true;

            Debug.Log("[ShopRework] Mod loaded.");
            Debug.Log($"[ShopRework] Mode: {(settings.UseDailyDiscounts ? "Daily" : "Weekly")}");
            Debug.Log($"[ShopRework] Config: {settings.discountedItemsPerDay} items, {(settings.discountPercentage == 0 ? "random %" : settings.discountPercentage + "%")}");

            return true;
        }

        public static void AdvanceDay()
        {
            CurrentDay = (DayOfWeek)(((int)CurrentDay + 1) % 7);
            Debug.Log($"[ShopRework] Day advanced. New day: {CurrentDay}");
        }

        public static void CheckMidnight(DateTime now)
        {
            if (lastCheckedDate == DateTime.MinValue)
            {
                lastCheckedDate = now.Date;
                return;
            }

            if (now.Date > lastCheckedDate)
            {
                lastCheckedDate = now.Date;
                AdvanceDay();

                bool shouldApply =
                    settings.UseDailyDiscounts ||
                    (settings.UseWeeklyDiscounts && CurrentDay == DayOfWeek.Monday);

                if (shouldApply)
                {
                    Debug.Log("[ShopRework] Applying new discounts due to day change.");
                    ShopReworkManager.ApplyNewDiscounts();
                }
                else
                {
                    Debug.Log("[ShopRework] No discounts today (weekly mode, not Monday).");
                }
            }
        }
    }

    public class ShopReworkWatcher : MonoBehaviour
	{
		private WorldClockController? clock;
		private bool initialized = false;

		void Update()
		{
			if (clock == null)
			{
				clock = UnityEngine.Object.FindObjectOfType<WorldClockController>();
				if (clock == null)
					return;

				clock.TimeChanged += OnTimeChanged;
				Debug.Log("[ShopRework] Clock connected.");
			}
		}

		private void OnTimeChanged(float hourAngle, float minuteAngle, DateTime currentTime)
		{
			if (!initialized)
			{
				initialized = true;

				Debug.Log($"[ShopRework] Initializing. Current day: {Main.CurrentDay}");

				if (ShopReworkManager.savedDiscountEntries.Count > 0)
				{
					Debug.Log("[ShopRework] Savegame detected. Reapplying discounts.");
					ShopReworkManager.ReapplySavedDiscounts();
				}
				else
				{
					Debug.Log("[ShopRework] Fresh session. Generating new discounts.");
					ShopReworkManager.ApplyNewDiscounts();
				}
			}

			Main.CheckMidnight(currentTime);
		}

		void OnDestroy()
		{
			if (clock != null)
				clock.TimeChanged -= OnTimeChanged;
		}
	}

    [HarmonyPatch(typeof(WorldStreamingInit), "Awake")]
    static class InitPatch
    {
        static void Postfix()
        {
            GameObject go = new GameObject("ShopReworkController");
            go.AddComponent<ShopReworkWatcher>();
			go.AddComponent<ShopReworkClosing>();
            UnityEngine.Object.DontDestroyOnLoad(go);
        }
    }

    [HarmonyPatch(typeof(StartGameData_FromSaveGame), "MakeCurrent")]
    static class Patch_LoadSave
    {
        static void Postfix(StartGameData_FromSaveGame __instance)
        {
            var saveData = Traverse.Create(__instance)
                .Field("saveGameData")
                .GetValue<SaveGameData>();

            var dataObject = Traverse.Create(saveData)
                .Field("dataObject")
                .GetValue<JObject>();

            if (dataObject == null) return;

            if (dataObject.TryGetValue("ShopReworkMod", out JToken? block))
            {
                var obj = (JObject)block;

                if (obj.TryGetValue("ShopRework_Day", out JToken? dayToken))
                {
                    if (Enum.TryParse(dayToken.ToString(), out DayOfWeek parsed))
                        Main.CurrentDay = parsed;
                }

                if (obj.TryGetValue("ShopRework_Discounts", out JToken? discToken))
                {
                    ShopReworkManager.LoadSavedDiscountsFromToken(discToken);
                }

                Debug.Log($"[ShopRework] Loaded from savegame. Current day: {Main.CurrentDay}");
                Debug.Log($"[ShopRework] Loaded {ShopReworkManager.savedDiscountEntries.Count} saved discounts.");
            }
            else
            {
                Debug.Log("[ShopRework] No previous save data found.");
            }
        }
    }

    [HarmonyPatch(typeof(SaveGameManager), "Save")]
    static class Patch_Save
    {
        static void Prefix()
        {
            var saveData = SaveGameManager.Instance?.data;
            if (saveData == null) return;

            var trav = Traverse.Create(saveData).Field("dataObject");
            var dataObject = trav.GetValue<JObject>() ?? new JObject();
            if (trav.GetValue<JObject>() == null)
                trav.SetValue(dataObject);

            var block = new JObject
            {
                ["ShopRework_Day"] = Main.CurrentDay.ToString(),
                ["ShopRework_Discounts"] = ShopReworkManager.ToJsonArraySorted()
            };

            dataObject["ShopReworkMod"] = block;
        }
    }
}