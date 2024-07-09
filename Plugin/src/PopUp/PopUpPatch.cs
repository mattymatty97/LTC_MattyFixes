using System.Text;
using HarmonyLib;
using TMPro;
using UnityEngine;

namespace MattyFixes.PopUp
{
    [HarmonyPatch]
    internal class PopUpPatch
    {
        [HarmonyPrefix]
        [HarmonyPatch(typeof(MenuManager), nameof(MenuManager.Awake))]
        private static void AddPopups(MenuManager __instance)
        {
            if (MattyFixes.FoundIncompatibilities.Count > 0)
            {
                StringBuilder sb = new StringBuilder($"{MattyFixes.NAME} was DISABLED!\nIncompatible:");
                foreach (var plugin in MattyFixes.FoundIncompatibilities)
                {
                    sb.Append("\n").Append(plugin.Metadata.Name);
                }
                AppendPopup("MF_Incompatibility", sb.ToString());
            }
        }
        
        
        private static void AppendPopup(string name, string text)
        {
            
            var menuContainer = GameObject.Find("/Canvas/MenuContainer/");
            var lanPopup = GameObject.Find("Canvas/MenuContainer/LANWarning/");
            if (lanPopup == null) 
                return;
            
            MattyFixes.Log.LogWarning("Cloning!");
            var newPopup = UnityEngine.Object.Instantiate(lanPopup, menuContainer.transform);
            newPopup.name = name;
            newPopup.SetActive(true);
            MattyFixes.Log.LogWarning("Finding text!");
            var textHolder = GameObject.Find($"Canvas/MenuContainer/{name}/Panel/NotificationText");
            MattyFixes.Log.LogWarning("Finding TextMeshPro!");
            var textMesh = textHolder.GetComponent<TextMeshProUGUI>();
            MattyFixes.Log.LogWarning("Changing text!");
            textMesh.text = text;
        }
    }
}