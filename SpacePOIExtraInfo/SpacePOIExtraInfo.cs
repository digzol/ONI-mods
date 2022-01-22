using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace SpacePOIExtraInfo
{
    public class SpacePOIExtraInfoPatches
    {
        // Hook into the method responsible for updating the info on the mass header (the label showing Mass Remaining)
        // Add our own headers here for info related to the POI as a whole (e.g. maximum mass, recharge rate, etc.)
        [HarmonyPatch(typeof(SpacePOISimpleInfoPanel))]
        [HarmonyPatch("RefreshMassHeader")]
        public class SpacePOIMassHeaderInfoPatch
        {
            public static LocString MaxCapacityLabel = "<b>Maximum Mass</b>";
            public static LocString RefillRateLabel = "<b>Refill Rate</b>";
            public static LocString TimeUntillFullLabel = "<b>Time Until Full</b>";

            private static SpacePOISimpleInfoPanel SpacePOIInfoPanel;
            private static GameObject MaxCapacityHeader;
            private static GameObject RefillRateHeader;
            private static GameObject TimeUntilFullHeader;

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

        // Hook into the method responsible for updating the info on the element list.
        // Go back and add our own tooltip to the UI elements created
        //  to show temperature, individual element capacity and refill rate.
        // What if this added collapisble panels instead of a tooltip?
        [HarmonyPatch(typeof(SpacePOISimpleInfoPanel))]
        [HarmonyPatch("RefreshElements")]
        public class SpacePOIElementsInfoPatch
        {
            public static LocString TemperatureTooltip = "Extractable {0} at {1}.";
            public static LocString CurrentMassTooltip = "Mass Remaining: {0}";
            public static LocString MassCapacityTooltip = "Maximum Mass: {0}";
            public static LocString MassRefillTooltip = "Refill Rate: {0}";

            public static LocString MassTooltipFormat = "{0}\n{1}\n{2}";
            public static LocString TooltipFormat = "{0}\n\n{1}";

            public static void Postfix(
                ref SpacePOISimpleInfoPanel __instance,
                HarvestablePOIStates.Instance harvestable)
            {
                var elementRows = Traverse.Create(__instance).Field("elementRows").GetValue() as Dictionary<Tag, GameObject>;

                if (harvestable == null || harvestable.configuration == null || elementRows == null)
                {
                    return;
                }

                Dictionary<SimHashes, float> elementWeights = harvestable.configuration.GetElementsWithWeights();

                float totalWeight = 0;
                foreach(KeyValuePair<SimHashes, float> entry in elementWeights)
                {
                    totalWeight += entry.Value;
                }

                foreach(KeyValuePair<SimHashes, float> entry in elementWeights)
                {
                    SimHashes elementHash = entry.Key;
                    float elementWeight = entry.Value;
                    Tag tag = elementHash.CreateTag();

                    if (elementRows.ContainsKey(tag) && elementRows[tag].activeInHierarchy)
                    {
                        Element elementDef = ElementLoader.FindElementByHash(elementHash);
                        float temperature = elementDef.defaultValues.temperature;
                        float ratio = elementWeight / totalWeight;
                        float currentMass = harvestable.poiCapacity * ratio;
                        float maxMass = harvestable.configuration.GetMaxCapacity() * ratio;
                        float refillRate = maxMass / harvestable.configuration.GetRechargeTime();

                        var TemperatureStr = string.Format(TemperatureTooltip, elementDef.name, GameUtil.GetFormattedTemperature(temperature));
                        var CurrentMassStr = string.Format(CurrentMassTooltip, GameUtil.GetFormattedMass(currentMass));
                        var MassCapacityStr = string.Format(MassCapacityTooltip, GameUtil.GetFormattedMass(maxMass));
                        var MassRefillStr = string.Format(MassRefillTooltip, GameUtil.GetFormattedMass(refillRate, GameUtil.TimeSlice.PerCycle));

                        var MassTooltipStr = string.Format(MassTooltipFormat, CurrentMassStr, MassCapacityStr, MassRefillStr);
                        var FullTooltipStr = string.Format(TooltipFormat, TemperatureStr, MassTooltipStr);

                        GameObject elementRow = elementRows[tag];
                        var tooltip = elementRow.GetComponent<ToolTip>();

                        if (tooltip == null)
                        {
                            tooltip = elementRow.AddComponent(typeof(ToolTip)) as ToolTip;
                        }

                        tooltip.SetSimpleTooltip(FullTooltipStr);
                    } else
                    {
                        // Something is wrong; these elements should definitely already be displayed in the UI
                        Debug.Log("[WARNING] SpacePOIExtraInfo couldn't add info to a space POI harvestable element. Please notify the author about this error.");
                    }
                }
            }
        }
    
        // Hook into the method responsible for refreshing artifact info at the POI and inject our own code:
        // * Edit the artifact row to display the actual artifact.
        // * Enable text-wrapping so long artifact names can fit without overlapping.
        // * Bump the time to recharge to a new line to make it more consistant and have the text fit.
        [HarmonyPatch(typeof(SpacePOISimpleInfoPanel))]
        [HarmonyPatch("RefreshArtifacts")]
        public class SpacePOIArtifactInfoPatch
        {
            public static LocString ARTIFACT_AVAILABLE = "Available";
            public static LocString ARTIFACT_UNAVAILABLE = "Unavailable";
            public static LocString ARTIFACT_RECHARGE_LABEL = "<b>Recharge Time</b>";

            private static SpacePOISimpleInfoPanel SpacePOIInfoPanel;
            private static GameObject RechargeTimeRow;

            public static void Postfix(ref SpacePOISimpleInfoPanel __instance,
                ArtifactPOIConfigurator artifactConfigurator,
                CollapsibleDetailContentPanel spacePOIPanel)
            {
                // Add UI changes if they weren't set yet or the info panel got set to a new instance somehow
                if (!ReferenceEquals(SpacePOIInfoPanel, __instance))
                {
                    SimpleInfoScreen simpleInfoScreen = Traverse.Create(__instance).Field("simpleInfoRoot").GetValue() as SimpleInfoScreen;
                    GameObject iconLabelRow = simpleInfoScreen.iconLabelRow;
                    GameObject parentContainer = spacePOIPanel.Content.gameObject;

                    SpacePOIInfoPanel = __instance;
                    RechargeTimeRow = Util.KInstantiateUI(iconLabelRow, parentContainer);

                    // Setup word wrapping - this could really be optimized
                    GameObject artifactRow = Traverse.Create(__instance).Field("artifactRow").GetValue() as GameObject;
                    HierarchyReferences refs = artifactRow.GetComponent<HierarchyReferences>();
                    refs.GetReference<LocText>("NameLabel").enableWordWrapping = true;
                    refs.GetReference<LocText>("NameLabel").gameObject.AddComponent<LayoutElement>().flexibleWidth = 1;
                    refs.GetReference<LocText>("ValueLabel").gameObject.GetComponent<LayoutElement>().ignoreLayout = false;
                }

                // Force the ui element to the bottom in case new element rows were added
                RechargeTimeRow.rectTransform().SetAsLastSibling();

                var smi = artifactConfigurator.GetSMI<ArtifactPOIStates.Instance>();

                string uniqueArtifactID = smi.configuration.GetArtifactID();
                bool isDestroyedOnHarvest = smi.configuration.DestroyOnHarvest();

                //int numHarvests = (int)Traverse.Create(smi).Field("numHarvests").GetValue();

                // Destroyable POI with a pre-assigned artifact (e.g. Russell's teapot)
                if (!string.IsNullOrEmpty(uniqueArtifactID) && isDestroyedOnHarvest)
                {
                    GameObject artifactRow = Traverse.Create(__instance).Field("artifactRow").GetValue() as GameObject;
                    GameObject artifactPrefab = Assets.GetPrefab(uniqueArtifactID);

                    if (artifactPrefab == null)
                        return;

                    HierarchyReferences refs = artifactRow.GetComponent<HierarchyReferences>();
                    refs.GetReference<LocText>("NameLabel").text = artifactPrefab.GetProperName();

                    var uisprite = Def.GetUISprite(uniqueArtifactID);
                    if (uisprite != null)
                    {
                        refs.GetReference<Image>("Icon").sprite = uisprite.first;
                        refs.GetReference<Image>("Icon").color = uisprite.second;
                    }
                }
                // Rechargeable POI with its next artifact assigned - a new artifact is generated each time one is harvested
                else if (!string.IsNullOrEmpty(smi.artifactToHarvest))
                {
                    GameObject artifactRow = Traverse.Create(__instance).Field("artifactRow").GetValue() as GameObject;
                    GameObject artifactPrefab = Assets.GetPrefab(smi.artifactToHarvest);

                    if (artifactPrefab == null)
                        return;

                    bool canHarvestArtifact = smi.CanHarvestArtifact();

                    HierarchyReferences refs = artifactRow.GetComponent<HierarchyReferences>();
                    refs.GetReference<LocText>("NameLabel").text = artifactPrefab.GetProperName();
                    refs.GetReference<LocText>("ValueLabel").text = (canHarvestArtifact) ? ARTIFACT_AVAILABLE : ARTIFACT_UNAVAILABLE;

                    var uisprite = Def.GetUISprite(smi.artifactToHarvest);
                    if (uisprite != null)
                    {
                        refs.GetReference<Image>("Icon").sprite = uisprite.first;
                        refs.GetReference<Image>("Icon").color = uisprite.second;
                    }

                    RechargeTimeRow.SetActive(!canHarvestArtifact);
                    if (!canHarvestArtifact)
                    {
                        refs = RechargeTimeRow.GetComponent<HierarchyReferences>();
                        refs.GetReference<LocText>("NameLabel").text = ARTIFACT_RECHARGE_LABEL;
                        refs.GetReference<LocText>("ValueLabel").text = GameUtil.GetFormattedCycles(smi.RechargeTimeRemaining(), forceCycles: true);
                        refs.GetReference<LocText>("ValueLabel").alignment = TextAlignmentOptions.MidlineRight;
                    }
                }
            }
        }
    }
}

