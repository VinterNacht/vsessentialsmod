﻿using Vintagestory.API.Client;

#nullable disable

namespace Vintagestory.GameContent
{
    public class GuiDialogLogViewer : GuiDialogGeneric
    {
        public GuiDialogLogViewer(string text, ICoreClientAPI capi) : base("Log Viewer", capi)
        {
            ElementBounds topTextBounds = ElementBounds.Fixed(GuiStyle.ElementToDialogPadding, 40, 900, 30);

            ElementBounds logtextBounds = ElementBounds.Fixed(0, 0, 900, 300).FixedUnder(topTextBounds, 5);

            // Clipping bounds for textarea
            ElementBounds clippingBounds = logtextBounds.ForkBoundingParent();

            ElementBounds insetBounds = logtextBounds.FlatCopy().FixedGrow(6).WithFixedOffset(-3, -3);

            ElementBounds scrollbarBounds = insetBounds.CopyOffsetedSibling(logtextBounds.fixedWidth + 7).WithFixedWidth(20);

            ElementBounds closeButtonBounds = ElementBounds
                .FixedSize(0, 0)
                .FixedUnder(clippingBounds, 2 * 5)
                .WithAlignment(EnumDialogArea.RightFixed)
                .WithFixedPadding(20, 4)
            ;
            

            // 2. Around all that is 10 pixel padding
            ElementBounds bgBounds = ElementBounds.Fill.WithFixedPadding(GuiStyle.ElementToDialogPadding);
            bgBounds.BothSizing = ElementSizing.FitToChildren;
            bgBounds.WithChildren(insetBounds, clippingBounds, scrollbarBounds, closeButtonBounds); 

            // 3. Finally Dialog
            ElementBounds dialogBounds = ElementStdBounds.AutosizedMainDialog.WithAlignment(EnumDialogArea.CenterMiddle);


            SingleComposer = capi.Gui
                .CreateCompo("dialogviewer", dialogBounds)
                .AddShadedDialogBG(bgBounds, true)
                .AddDialogTitleBar(DialogTitle, OnTitleBarClose)
                .AddStaticText("The following warnings and errors were reported during startup:", CairoFont.WhiteDetailText(), topTextBounds)
                .BeginChildElements(bgBounds)
                    .BeginClip(clippingBounds)
                    .AddInset(insetBounds, 3)
                    .AddDynamicText("", CairoFont.WhiteDetailText(), logtextBounds, "text")
                    .EndClip()
                    .AddVerticalScrollbar(OnNewScrollbarvalue, scrollbarBounds, "scrollbar")
                    .AddSmallButton("Close", OnButtonClose, closeButtonBounds)
                .EndChildElements()
                .Compose()
            ;

            GuiElementDynamicText logtextElem = SingleComposer.GetDynamicText("text");
            logtextElem.AutoHeight();
            logtextElem.SetNewText(text);

            SingleComposer.GetScrollbar("scrollbar").SetHeights(
                (float)300, (float)logtextBounds.fixedHeight
            );            
        }


        private void OnNewScrollbarvalue(float value)
        {
            GuiElementDynamicText logtextElem = SingleComposer.GetDynamicText("text");
            logtextElem.Bounds.fixedY = 3 - value;
            logtextElem.Bounds.CalcWorldBounds();
        }


        private void OnTitleBarClose()
        {
            OnButtonClose();
        }

        private bool OnButtonClose()
        {
            TryClose();
            return true;
        }
    }
}
