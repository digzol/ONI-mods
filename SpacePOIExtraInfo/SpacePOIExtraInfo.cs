using HarmonyLib;
using UnityEngine;
using TMPro;

namespace SpacePOIExtraInfo
{
    public class SpacePOIExtraInfoPatch
    {
        [HarmonyPatch(typeof(SpacePOISimpleInfoPanel))]
        [HarmonyPatch("RefreshMassHeader")]
        public class SpacePOIMassHeaderInfoPatch
        {
            public static LocString MaxCapacityLabel = "<b>Maximum Mass</b>";
            public static LocString RefillRateLabel = "<b>Refill Rate</b>";
            public static LocString TimeUntillFullLabel = "<b>Time Until Full</b>";

            public static SpacePOISimpleInfoPanel SpacePOIInfoPanel;
            public static GameObject MaxCapacityHeader;
            public static GameObject RefillRateHeader;
            public static GameObject TimeUntilFullHeader;

            public static void Postfix(
                ref SpacePOISimpleInfoPanel __instance,
                HarvestablePOIStates.Instance harvestable,
                //GameObject selectedTarget,
                CollapsibleDetailContentPanel spacePOIPanel)
            {
                // Instantiate our new UI elements if there are none or the info panel got changed somehow
                if (!ReferenceEquals(SpacePOIInfoPanel, __instance))
                {
                    SimpleInfoScreen simpleInfoScreen = Traverse.Create(__instance).Field("simpleInfoRoot").GetValue() as SimpleInfoScreen;
                    GameObject iconLabelRow = simpleInfoScreen.iconLabelRow;
                    GameObject parentContainer = spacePOIPanel.Content.gameObject;

                    SpacePOIInfoPanel = __instance;
                    MaxCapacityHeader = Util.KInstantiateUI(iconLabelRow, parentContainer);
                    TimeUntilFullHeader = Util.KInstantiateUI(iconLabelRow, parentContainer);
                    RefillRateHeader = Util.KInstantiateUI(iconLabelRow, parentContainer);
                }

                bool isHarvestable = (harvestable != null);

                MaxCapacityHeader.SetActive(isHarvestable);
                RefillRateHeader.SetActive(isHarvestable);
                TimeUntilFullHeader.SetActive(isHarvestable);

                if (!isHarvestable)
                    return;

                var harvestableConfig = harvestable.configuration;
                float maxCapacity = harvestableConfig.GetMaxCapacity();
                float refillRate = maxCapacity / harvestableConfig.GetRechargeTime();
                float timeUntilFull = Mathf.RoundToInt((maxCapacity - harvestable.poiCapacity) / refillRate);

                HierarchyReferences hierarchy;

                // Max capacity
                hierarchy = MaxCapacityHeader.GetComponent<HierarchyReferences>();
                hierarchy.GetReference<LocText>("NameLabel").text = MaxCapacityLabel;
                hierarchy.GetReference<LocText>("ValueLabel").text = GameUtil.GetFormattedMass(maxCapacity);
                hierarchy.GetReference<LocText>("ValueLabel").alignment = TextAlignmentOptions.MidlineRight;

                // Refill rate
                hierarchy = RefillRateHeader.GetComponent<HierarchyReferences>();
                hierarchy.GetReference<LocText>("NameLabel").text = RefillRateLabel;
                hierarchy.GetReference<LocText>("ValueLabel").text = GameUtil.GetFormattedMass(refillRate, GameUtil.TimeSlice.PerCycle);
                hierarchy.GetReference<LocText>("ValueLabel").alignment = TextAlignmentOptions.MidlineRight;

                // Time until full
                TimeUntilFullHeader.SetActive(timeUntilFull > 0);
                if (timeUntilFull > 0)
                {
                    hierarchy = TimeUntilFullHeader.GetComponent<HierarchyReferences>();
                    hierarchy.GetReference<LocText>("NameLabel").text = TimeUntillFullLabel;
                    hierarchy.GetReference<LocText>("ValueLabel").text = GameUtil.GetFormattedCycles(timeUntilFull);
                    hierarchy.GetReference<LocText>("ValueLabel").alignment = TextAlignmentOptions.MidlineRight;
                }
            }
        }
    }
}
