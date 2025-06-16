﻿using System;
using System.Text;
using System.Text.RegularExpressions;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;

#nullable disable
using static Vintagestory.API.Client.GuiDialog;

namespace Vintagestory.GameContent
{
    public class CharacterExtraDialogs : ModSystem
    {
        DlgComposers Composers => dlg.Composers;
        ICoreClientAPI capi;
        GuiDialogCharacterBase dlg;

        public event System.Action<StringBuilder> OnEnvText;

        public override bool ShouldLoad(EnumAppSide forSide)
        {
            return forSide == EnumAppSide.Client;
        }

        bool IsOpened()
        {
            return dlg.IsOpened();
        }

        public override void StartClientSide(ICoreClientAPI api)
        {
            capi = api;

            base.StartClientSide(api);

            dlg = api.Gui.LoadedGuis.Find(dlg => dlg is GuiDialogCharacterBase) as GuiDialogCharacterBase;

            dlg.OnOpened += Dlg_OnOpened;
            dlg.OnClosed += Dlg_OnClosed;
            dlg.TabClicked += Dlg_TabClicked;
            dlg.ComposeExtraGuis += Dlg_ComposeExtraGuis;

            

            api.Event.RegisterGameTickListener(On2sTick, 2000);
        }

        private void Dlg_TabClicked(int tabIndex)
        {
            if (tabIndex != 0) Dlg_OnClosed();
            if (tabIndex == 0) Dlg_OnOpened();
        }

        private void Dlg_ComposeExtraGuis()
        {
            ComposeEnvGui();
            ComposeStatsGui();
        }

        private void On2sTick(float dt)
        {
            if (IsOpened())
            {
                updateEnvText();
            }
        }

        private void Dlg_OnClosed()
        {
            capi.World.Player.Entity.WatchedAttributes.UnregisterListener(UpdateStatBars);
            capi.World.Player.Entity.WatchedAttributes.UnregisterListener(UpdateStats);
        }

        private void Dlg_OnOpened()
        {
            capi.World.Player.Entity.WatchedAttributes.RegisterModifiedListener("hunger", UpdateStatBars);
            capi.World.Player.Entity.WatchedAttributes.RegisterModifiedListener("stats", UpdateStats);
            capi.World.Player.Entity.WatchedAttributes.RegisterModifiedListener("bodyTemp", UpdateStats);
        }

        public virtual void ComposeEnvGui()
        {
            ElementBounds leftDlgBounds = Composers["playercharacter"].Bounds;
            CairoFont font = CairoFont.WhiteSmallText().WithLineHeightMultiplier(1.2);
            
            string envText = getEnvText();
            int cntlines = 1 + Regex.Matches(envText, "\n").Count;
            double height = font.GetFontExtents().Height * font.LineHeightMultiplier * cntlines / RuntimeEnv.GUIScale;

            ElementBounds textBounds = ElementBounds.Fixed(0, 25, (int)(leftDlgBounds.InnerWidth / RuntimeEnv.GUIScale - 40), height);
            textBounds.Name = "textbounds";

            ElementBounds bgBounds = ElementBounds.Fill.WithFixedPadding(GuiStyle.ElementToDialogPadding);
            bgBounds.Name = "bgbounds";
            bgBounds.BothSizing = ElementSizing.FitToChildren;
            bgBounds.WithChildren(textBounds);

            ElementBounds dialogBounds = ElementStdBounds.AutosizedMainDialog.WithAlignment(EnumDialogArea.None).WithFixedPosition(leftDlgBounds.renderX / RuntimeEnv.GUIScale, leftDlgBounds.renderY / RuntimeEnv.GUIScale + leftDlgBounds.OuterHeight / RuntimeEnv.GUIScale + 10);
            dialogBounds.Name = "dialogbounds";

            Composers["environment"] = capi.Gui
                .CreateCompo("environment", dialogBounds)
                .AddShadedDialogBG(bgBounds, true)
                .AddDialogTitleBar(Lang.Get("Environment"), () => dlg.OnTitleBarClose())
                .BeginChildElements(bgBounds)
                    .AddDynamicText(envText, font, textBounds, "dyntext")
                .EndChildElements()
                .Compose()
            ;
        }

        void updateEnvText()
        {
            if (!IsOpened() || Composers?["environment"] == null) return;

            Composers["environment"].GetDynamicText("dyntext").SetNewTextAsync(getEnvText());
        }

        string getEnvText()
        {
            string date = capi.World.Calendar.PrettyDate();
            var conds = capi.World.BlockAccessor.GetClimateAt(capi.World.Player.Entity.Pos.AsBlockPos, EnumGetClimateMode.NowValues);
            string temp = "?";
            string rainfallfreq = "?";

            if (conds != null)
            {
                temp = (int)conds.Temperature + "°C";
                rainfallfreq = Lang.Get("freq-veryrare");

                if (conds.WorldgenRainfall > 0.9)
                {
                    rainfallfreq = Lang.Get("freq-allthetime");
                }
                else
                if (conds.WorldgenRainfall > 0.7)
                {
                    rainfallfreq = Lang.Get("freq-verycommon");
                }
                else
                if (conds.WorldgenRainfall > 0.45)
                {
                    rainfallfreq = Lang.Get("freq-common");
                }
                else
                if (conds.WorldgenRainfall > 0.3)
                {
                    rainfallfreq = Lang.Get("freq-uncommon");
                }
                else
                if (conds.WorldgenRainfall > 0.15)
                {
                    rainfallfreq = Lang.Get("freq-rarely");
                }
            }
            

            StringBuilder sb = new StringBuilder();
            sb.Append(Lang.Get("character-envtext", date, temp, rainfallfreq));

            OnEnvText?.Invoke(sb);

            return sb.ToString();
        }

        public virtual void ComposeStatsGui()
        {
            ElementBounds leftDlgBounds = Composers["playercharacter"].Bounds;
            ElementBounds botDlgBounds = Composers["environment"].Bounds;

            ElementBounds leftColumnBounds = ElementBounds.Fixed(0, 25, 90, 20);
            ElementBounds rightColumnBounds = ElementBounds.Fixed(120, 30, 120, 8);

            ElementBounds leftColumnBoundsW = ElementBounds.Fixed(0, 0, 140, 20);
            ElementBounds rightColumnBoundsW = ElementBounds.Fixed(165, 0, 120, 20);

            EntityPlayer entity = capi.World.Player.Entity;


            double b = botDlgBounds.InnerHeight / RuntimeEnv.GUIScale + 10;

            ElementBounds bgBounds = ElementBounds
                .Fixed(0, 0, 130 + 100 + 5, leftDlgBounds.InnerHeight / RuntimeEnv.GUIScale - GuiStyle.ElementToDialogPadding - 20 + b)
                .WithFixedPadding(GuiStyle.ElementToDialogPadding)
            ;


            ElementBounds dialogBounds =
                bgBounds.ForkBoundingParent()
                .WithAlignment(EnumDialogArea.LeftMiddle)
                .WithFixedAlignmentOffset((leftDlgBounds.renderX + leftDlgBounds.OuterWidth + 10) / RuntimeEnv.GUIScale, b / 2)
            ;

            getHealthSat(out float? health, out float? maxhealth, out float? saturation, out float? maxsaturation);

            float walkspeed = entity.Stats.GetBlended("walkspeed");
            float healingEffectivness = entity.Stats.GetBlended("healingeffectivness");
            float hungerRate = entity.Stats.GetBlended("hungerrate");
            float rangedWeaponAcc = entity.Stats.GetBlended("rangedWeaponsAcc");
            float rangedWeaponSpeed = entity.Stats.GetBlended("rangedWeaponsSpeed");
            ITreeAttribute tempTree = entity.WatchedAttributes.GetTreeAttribute("bodyTemp");

            float wetness = entity.WatchedAttributes.GetFloat("wetness");
            string wetnessString = "";
            if (wetness > 0.7) wetnessString = Lang.Get("wetness_soakingwet");
            else if (wetness > 0.4) wetnessString = Lang.Get("wetness_wet");
            else if (wetness > 0.1) wetnessString = Lang.Get("wetness_slightlywet");


            Composers["playerstats"] = capi.Gui
                .CreateCompo("playerstats", dialogBounds)
                .AddShadedDialogBG(bgBounds, true)
                .AddDialogTitleBar(Lang.Get("Stats"), () => dlg.OnTitleBarClose())
                .BeginChildElements(bgBounds)
            ;

            if (saturation != null)
            {
                Composers["playerstats"]
                    .AddStaticText(Lang.Get("playerinfo-nutrition"), CairoFont.WhiteSmallText().WithWeight(Cairo.FontWeight.Bold), leftColumnBounds.WithFixedWidth(200))
                    .AddStaticText(Lang.Get("playerinfo-nutrition-Freeza"), CairoFont.WhiteDetailText(), leftColumnBounds = leftColumnBounds.BelowCopy().WithFixedWidth(90))
                    .AddStaticText(Lang.Get("playerinfo-nutrition-Vegita"), CairoFont.WhiteDetailText(), leftColumnBounds = leftColumnBounds.BelowCopy())
                    .AddStaticText(Lang.Get("playerinfo-nutrition-Krillin"), CairoFont.WhiteDetailText(), leftColumnBounds = leftColumnBounds.BelowCopy())
                    .AddStaticText(Lang.Get("playerinfo-nutrition-Cell"), CairoFont.WhiteDetailText(), leftColumnBounds = leftColumnBounds.BelowCopy())
                    .AddStaticText(Lang.Get("playerinfo-nutrition-Dairy"), CairoFont.WhiteDetailText(), leftColumnBounds = leftColumnBounds.BelowCopy())

                    .AddStatbar(rightColumnBounds = rightColumnBounds.BelowCopy(0, 16), GuiStyle.FoodBarColor, "fruitBar")
                    .AddStatbar(rightColumnBounds = rightColumnBounds.BelowCopy(0, 12), GuiStyle.FoodBarColor, "vegetableBar")
                    .AddStatbar(rightColumnBounds = rightColumnBounds.BelowCopy(0, 12), GuiStyle.FoodBarColor, "grainBar")
                    .AddStatbar(rightColumnBounds = rightColumnBounds.BelowCopy(0, 12), GuiStyle.FoodBarColor, "proteinBar")
                    .AddStatbar(rightColumnBounds = rightColumnBounds.BelowCopy(0, 12), GuiStyle.FoodBarColor, "dairyBar")
                ;

                leftColumnBoundsW = leftColumnBoundsW.FixedUnder(leftColumnBounds, -5);
            }

            Composers["playerstats"]
                    .AddStaticText(Lang.Get("Physical"), CairoFont.WhiteSmallText().WithWeight(Cairo.FontWeight.Bold), leftColumnBoundsW.WithFixedWidth(200).WithFixedOffset(0, 23))
                    .Execute(() => {
                        leftColumnBoundsW = leftColumnBoundsW.FlatCopy();
                        leftColumnBoundsW.fixedY += 5;
                    })
                ;

            if (health != null)
            {
                Composers["playerstats"]
                    .AddStaticText(Lang.Get("Health Points"), CairoFont.WhiteDetailText(), leftColumnBoundsW = leftColumnBoundsW.BelowCopy())
                    .AddDynamicText(health + " / " + maxhealth, CairoFont.WhiteDetailText(), rightColumnBoundsW = rightColumnBoundsW.FlatCopy().WithFixedPosition(rightColumnBoundsW.fixedX, leftColumnBoundsW.fixedY).WithFixedHeight(30), "health")
                ;
            }

            if (saturation != null) {
                Composers["playerstats"]
                    .AddStaticText(Lang.Get("Satiety"), CairoFont.WhiteDetailText(), leftColumnBoundsW = leftColumnBoundsW.BelowCopy())
                    .AddDynamicText((int)saturation + " / " + (int)maxsaturation, CairoFont.WhiteDetailText(), rightColumnBoundsW = rightColumnBoundsW.FlatCopy().WithFixedPosition(rightColumnBoundsW.fixedX, leftColumnBoundsW.fixedY), "satiety")
                ;
            }

            if (tempTree != null)
            {
                Composers["playerstats"]
                    .AddStaticText(Lang.Get("Body Temperature"), CairoFont.WhiteDetailText(), leftColumnBoundsW = leftColumnBoundsW.BelowCopy())
                    .AddRichtext(tempTree == null ? "-" : getBodyTempText(tempTree), CairoFont.WhiteDetailText(), rightColumnBoundsW = rightColumnBoundsW.FlatCopy().WithFixedPosition(rightColumnBoundsW.fixedX, leftColumnBoundsW.fixedY), "bodytemp")
                ;
            }

            if (wetnessString.Length > 0)
            {
                Composers["playerstats"]
                    .AddRichtext(wetnessString, CairoFont.WhiteDetailText(), leftColumnBoundsW = leftColumnBoundsW.BelowCopy())
                ;
            }

            Composers["playerstats"]
                .AddStaticText(Lang.Get("Walk speed"), CairoFont.WhiteDetailText(), leftColumnBoundsW = leftColumnBoundsW.BelowCopy())
                .AddDynamicText((int)Math.Round(100 * walkspeed) + "%", CairoFont.WhiteDetailText(), rightColumnBoundsW = rightColumnBoundsW.FlatCopy().WithFixedPosition(rightColumnBoundsW.fixedX, leftColumnBoundsW.fixedY), "walkspeed")

                .AddStaticText(Lang.Get("Healing effectivness"), CairoFont.WhiteDetailText(), leftColumnBoundsW = leftColumnBoundsW.BelowCopy())
                .AddDynamicText((int)Math.Round(100 * healingEffectivness) + "%", CairoFont.WhiteDetailText(), rightColumnBoundsW = rightColumnBoundsW.FlatCopy().WithFixedPosition(rightColumnBoundsW.fixedX, leftColumnBoundsW.fixedY), "healeffectiveness")
            ;

            if (saturation != null) 
            { 
                Composers["playerstats"]
                    .AddStaticText(Lang.Get("Hunger rate"), CairoFont.WhiteDetailText(), leftColumnBoundsW = leftColumnBoundsW.BelowCopy())
                    .AddDynamicText((int)Math.Round(100 * hungerRate) + "%", CairoFont.WhiteDetailText(), rightColumnBoundsW = rightColumnBoundsW.FlatCopy().WithFixedPosition(rightColumnBoundsW.fixedX, leftColumnBoundsW.fixedY), "hungerrate")
                ;
            }

            Composers["playerstats"]
                .AddStaticText(Lang.Get("Ranged Accuracy"), CairoFont.WhiteDetailText(), leftColumnBoundsW = leftColumnBoundsW.BelowCopy())
                .AddDynamicText((int)Math.Round(100 * rangedWeaponAcc) + "%", CairoFont.WhiteDetailText(), rightColumnBoundsW = rightColumnBoundsW.FlatCopy().WithFixedPosition(rightColumnBoundsW.fixedX, leftColumnBoundsW.fixedY), "rangedweaponacc")

                .AddStaticText(Lang.Get("Ranged Charge Speed"), CairoFont.WhiteDetailText(), leftColumnBoundsW = leftColumnBoundsW.BelowCopy())
                .AddDynamicText((int)Math.Round(100 * rangedWeaponSpeed) + "%", CairoFont.WhiteDetailText(), rightColumnBoundsW = rightColumnBoundsW.FlatCopy().WithFixedPosition(rightColumnBoundsW.fixedX, leftColumnBoundsW.fixedY), "rangedweaponchargespeed")

                .EndChildElements()
                .Compose()
            ;

            UpdateStatBars();
        }


        string getBodyTempText(ITreeAttribute tempTree)
        {
            float baseTemp = tempTree.GetFloat("bodytemp");
            if (baseTemp > 37f) baseTemp = 37f + (baseTemp - 37f) / 10f;  //Prevent it displaying values greater than around 38 degrees: warm from fire temperatures shown as e.g. 37.3 not 40
            return string.Format("{0:0.#}°C", baseTemp);
        }

        void getHealthSat(out float? health, out float? maxHealth, out float? saturation, out float? maxSaturation)
        {
            health = null;
            maxHealth = null;
            saturation = null;
            maxSaturation = null;

            ITreeAttribute healthTree = capi.World.Player.Entity.WatchedAttributes.GetTreeAttribute("health");
            if (healthTree != null)
            {
                health = healthTree.TryGetFloat("currenthealth");
                maxHealth = healthTree.TryGetFloat("maxhealth");
            }

            if (health != null) health = (float)Math.Round((float)health, 1);
            if (maxHealth != null) maxHealth = (float)Math.Round((float)maxHealth, 1);

            ITreeAttribute hungerTree = capi.World.Player.Entity.WatchedAttributes.GetTreeAttribute("hunger");
            if (hungerTree != null)
            {
                saturation = hungerTree.TryGetFloat("currentsaturation");
                maxSaturation = hungerTree.TryGetFloat("maxsaturation");
            }
            if (saturation != null) saturation = (int)saturation;
        }

        private void UpdateStats()
        {
            EntityPlayer entity = capi.World.Player.Entity;
            GuiComposer compo = Composers["playerstats"];
            if (compo == null || !IsOpened()) return;

            getHealthSat(out float? health, out float? maxhealth, out float? saturation, out float? maxsaturation);

            float walkspeed = entity.Stats.GetBlended("walkspeed");
            float healingEffectivness = entity.Stats.GetBlended("healingeffectivness");
            float hungerRate = entity.Stats.GetBlended("hungerrate");
            float rangedWeaponAcc = entity.Stats.GetBlended("rangedWeaponsAcc");
            float rangedWeaponSpeed = entity.Stats.GetBlended("rangedWeaponsSpeed");


            if (health != null) compo.GetDynamicText("health").SetNewText((health + " / " + maxhealth));
            if (saturation != null) compo.GetDynamicText("satiety").SetNewText((int)saturation + " / " + (int)maxsaturation);

            compo.GetDynamicText("walkspeed").SetNewText((int)Math.Round(100 * walkspeed) + "%");
            compo.GetDynamicText("healeffectiveness").SetNewText((int)Math.Round(100 * healingEffectivness) + "%");
            compo.GetDynamicText("hungerrate")?.SetNewText((int)Math.Round(100 * hungerRate) + "%");
            compo.GetDynamicText("rangedweaponacc").SetNewText((int)Math.Round(100 * rangedWeaponAcc) + "%");
            compo.GetDynamicText("rangedweaponchargespeed").SetNewText((int)Math.Round(100 * rangedWeaponSpeed) + "%");

            ITreeAttribute tempTree = entity.WatchedAttributes.GetTreeAttribute("bodyTemp");
            compo.GetRichtext("bodytemp").SetNewText(getBodyTempText(tempTree), CairoFont.WhiteDetailText());
        }

        private void UpdateStatBars()
        {
            GuiComposer compo = Composers["playerstats"];
            if (compo == null || !IsOpened()) return;

            ITreeAttribute hungerTree = capi.World.Player.Entity.WatchedAttributes.GetTreeAttribute("hunger");

            if (hungerTree != null)
            {
                float saturation = hungerTree.GetFloat("currentsaturation");
                float maxSaturation = hungerTree.GetFloat("maxsaturation");
                float fruitLevel = hungerTree.GetFloat("fruitLevel");
                float vegetableLevel = hungerTree.GetFloat("vegetableLevel");
                float grainLevel = hungerTree.GetFloat("grainLevel");
                float proteinLevel = hungerTree.GetFloat("proteinLevel");
                float dairyLevel = hungerTree.GetFloat("dairyLevel");

                compo.GetDynamicText("satiety").SetNewText((int)saturation + " / " + maxSaturation);

                Composers["playerstats"].GetStatbar("fruitBar").SetLineInterval(maxSaturation / 10);
                Composers["playerstats"].GetStatbar("vegetableBar").SetLineInterval(maxSaturation / 10);
                Composers["playerstats"].GetStatbar("grainBar").SetLineInterval(maxSaturation / 10);
                Composers["playerstats"].GetStatbar("proteinBar").SetLineInterval(maxSaturation / 10);
                Composers["playerstats"].GetStatbar("dairyBar").SetLineInterval(maxSaturation / 10);


                Composers["playerstats"].GetStatbar("fruitBar").SetValues(fruitLevel, 0, maxSaturation);
                Composers["playerstats"].GetStatbar("vegetableBar").SetValues(vegetableLevel, 0, maxSaturation);
                Composers["playerstats"].GetStatbar("grainBar").SetValues(grainLevel, 0, maxSaturation);
                Composers["playerstats"].GetStatbar("proteinBar").SetValues(proteinLevel, 0, maxSaturation);
                Composers["playerstats"].GetStatbar("dairyBar").SetValues(dairyLevel, 0, maxSaturation);
            }


        }
    }
}
