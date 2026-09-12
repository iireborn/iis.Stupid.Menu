using GorillaGameModes;
using iiMenu.Classes.Menu;
using iiMenu.Menu;
using iiMenu.Mods;
using System;
using System.Collections.Generic;
using System.Text;
using static iiMenu.DataTables.SuggestedGamemodeTypeTable;

namespace iiMenu.Managers
{
    internal class SuggestedModsManager
    {
        private static List<ButtonInfo> SuggestedMods;
        public static GameMode currentMode;
        public static void Init()
        {
            Buttons.CurrentCategoryName = "Suggested Mods";
            SuggestedMods = new List<ButtonInfo> { new ButtonInfo { buttonText = "Exit Suggested Mods", method = () => Buttons.CurrentCategoryName = "Main", isTogglable = false, toolTip = "Returns you back to the main page." } };
            SuggestFromGamemode();
        }

        public static void SuggestMod(ButtonInfo mod)
        {
            if (mod == null)
                return;

            SuggestedMods.Add(mod);
            Buttons.buttons[Buttons.GetCategory("Suggested Mods")] = SuggestedMods.ToArray();
        }

        public static void SuggestFromGamemode()
        {
            SuggestedGamemodeType gamemode = GetSuggestedCurrentMode();

            if (gamemode == SuggestedGamemodeType.None)
                return;

            int suggestedCategory = Buttons.GetCategory("Suggested Mods");

            for (int i = 0; i < Buttons.buttons.Length; i++)
            {
                if (i == suggestedCategory)
                    continue;

                foreach (ButtonInfo button in Buttons.buttons[i])
                {
                    if (button.suggestedGamemodeType != SuggestedGamemodeType.None && button.suggestedGamemodeType == gamemode)
                    {
                        SuggestMod(button);
                    }
                }
            }
        }


        public static SuggestedGamemodeType GetSuggestedCurrentMode()
        {
            string gamemode = NetworkSystem.Instance.GameModeString;
            if (gamemode.Contains("Infection"))
            {
                return SuggestedGamemodeType.Infection;
            }
            if (gamemode.Contains("Guardian"))
            {
                return SuggestedGamemodeType.Guardian;
            }
            if (gamemode.Contains("SuperInfect"))
            {
                return SuggestedGamemodeType.SuperInfect;
            }
            if (gamemode.Contains("Paintbrawl"))
            {
                return SuggestedGamemodeType.Paintbrawl;
            }
            return SuggestedGamemodeType.None;
        }
    }
}
