﻿using System;
using Vintagestory.API.Client;
using Vintagestory.API.Common;

#nullable disable

namespace Vintagestory.ServerMods
{
    public class LoadColorMaps : ModSystem
    {
        ICoreAPI api;
        public override double ExecuteOrder()
        {
            return 0.3; // After json patcher, after block/item loader
        }

        public override void Start(ICoreAPI api)
        {
            this.api = api;
        }

        public override void AssetsLoaded(ICoreAPI api)
        {
            if (api is ICoreClientAPI capi)
            {
                loadColorMaps();
            }
        }

        private void loadColorMaps()
        {
            try
            {
                IAsset asset = api.Assets.TryGet("config/colormaps.json");
                if (asset != null)
                {
                    var list = asset.ToObject<ColorMap[]>();
                    foreach (var val in list)
                    {
                        api.RegisterColorMap(val);
                    }

                }
            }
            catch (Exception e)
            {
                api.World.Logger.Error("Failed loading config/colormap.json. Will skip");
                api.World.Logger.Error(e);
            }
        }
    }
}
