﻿using System;
using System.Collections.Generic;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.Util;

#nullable disable

namespace Vintagestory.GameContent
{
    public class DayTimeFrame
    {
        public double FromHour;
        public double ToHour;

        public bool Matches(double hourOfDay)
        {
            return FromHour <= hourOfDay && ToHour >= hourOfDay;
        }
    }

    public class AiTaskIdle : AiTaskBase
    {
        public AiTaskIdle(EntityAgent entity) : base(entity)
        {
        }

        public int minduration;
        public int maxduration;
        public float chance;
        public AssetLocation onBlockBelowCode;
        public long idleUntilMs;


        bool entityWasInRange;
        long lastEntityInRangeTestTotalMs;

        string[] stopOnNearbyEntityCodesExact = null;
        string[] stopOnNearbyEntityCodesBeginsWith = Array.Empty<string>();
        string targetEntityFirstLetters = "";
        float stopRange =0;
        bool stopOnHurt = false;
        EntityPartitioning partitionUtil;

        bool stopNow;

        float tamingGenerations = 10f;

        public override void LoadConfig(JsonObject taskConfig, JsonObject aiConfig)
        {
            partitionUtil = entity.Api.ModLoader.GetModSystem<EntityPartitioning>();

            this.minduration = taskConfig["minduration"].AsInt(2000);
            this.maxduration = taskConfig["maxduration"].AsInt(4000);
            this.chance = taskConfig["chance"].AsFloat(1.1f);
            string code = taskConfig["onBlockBelowCode"].AsString(null);

            tamingGenerations = taskConfig["tamingGenerations"].AsFloat(10f);

            if (code != null && code.Length > 0)
            {
                this.onBlockBelowCode = new AssetLocation(code);
            }

            stopRange = taskConfig["stopRange"].AsFloat(0f);
            stopOnHurt = taskConfig["stopOnHurt"].AsBool(false);


            string[] codes = taskConfig["stopOnNearbyEntityCodes"].AsArray<string>(new string[] { "player" });

            List<string> exact = new List<string>();
            List<string> beginswith = new List<string>();

            for (int i = 0; i < codes.Length; i++)
            {
                string ecode = codes[i];
                if (ecode.EndsWith('*')) beginswith.Add(ecode.Substring(0, ecode.Length - 1));
                else exact.Add(ecode);
            }

            stopOnNearbyEntityCodesExact = exact.ToArray();
            stopOnNearbyEntityCodesBeginsWith = beginswith.ToArray();
            foreach (string scode in stopOnNearbyEntityCodesExact)
            {
                if (scode.Length == 0) continue;
                char c = scode[0];
                if (targetEntityFirstLetters.IndexOf(c) < 0) targetEntityFirstLetters += c;
            }

            foreach (string scode in stopOnNearbyEntityCodesBeginsWith)
            {
                if (scode.Length == 0) continue;
                char c = scode[0];
                if (targetEntityFirstLetters.IndexOf(c) < 0) targetEntityFirstLetters += c;
            }


            if (maxduration < 0) idleUntilMs = -1;
            else idleUntilMs = entity.World.ElapsedMilliseconds + minduration + entity.World.Rand.Next(maxduration - minduration);

            int generation = entity.WatchedAttributes.GetInt("generation", 0);
            float fearReductionFactor = Math.Max(0f, (tamingGenerations - generation) / tamingGenerations);
            if (WhenInEmotionState != null) fearReductionFactor = 1;

            stopRange *= fearReductionFactor;

            base.LoadConfig(taskConfig, aiConfig);

            lastEntityInRangeTestTotalMs = entity.World.ElapsedMilliseconds - entity.World.Rand.Next(1500);   // randomise time for first expensive tick
        }

        public override bool ShouldExecute()
        {
            long ellapsedMs = entity.World.ElapsedMilliseconds;
            if (cooldownUntilMs < ellapsedMs && entity.World.Rand.NextDouble() < chance)
            {
                if (entity.Properties.Habitat == EnumHabitat.Land && entity.FeetInLiquid) return false;

                if (!PreconditionsSatisifed()) return false;

                // The entityInRange test is expensive. So we only test for it every 4 seconds
                // which should have zero impact on the behavior. It'll merely execute this task 4 seconds later
                if (ellapsedMs - lastEntityInRangeTestTotalMs > 2000)
                {
                    entityWasInRange = entityInRange();
                    lastEntityInRangeTestTotalMs = ellapsedMs;
                }

                if (entityWasInRange) return false;

                

                Block belowBlock = entity.World.BlockAccessor.GetBlockRaw((int)entity.ServerPos.X, (int)entity.ServerPos.InternalY - 1, (int)entity.ServerPos.Z, BlockLayersAccess.Solid);
                // Only with a solid block below (and here not lake ice: entities should not idle on lake ice!)
                if (!belowBlock.SideSolid[API.MathTools.BlockFacing.UP.Index]) return false;

                if (onBlockBelowCode == null) return true;
                Block block = entity.World.BlockAccessor.GetBlockRaw((int)entity.ServerPos.X, (int)entity.ServerPos.InternalY, (int)entity.ServerPos.Z);

                return block.WildCardMatch(onBlockBelowCode) || (block.Replaceable >= 6000 && belowBlock.WildCardMatch(onBlockBelowCode));
            }

            return false;
        }

        public override void StartExecute()
        {
            base.StartExecute();
            if (maxduration < 0) idleUntilMs = -1;
            else idleUntilMs = entity.World.ElapsedMilliseconds + minduration + entity.World.Rand.Next(maxduration - minduration);
            entity.IdleSoundChanceModifier = 0f;
            stopNow = false;
        }

        public override bool ContinueExecute(float dt)
        {
            if (rand.NextDouble() < 0.3f)
            {
                long ellapsedMs = entity.World.ElapsedMilliseconds;

                // The entityInRange test is expensive. So we only test for it every 1 second
                // which should have zero impact on the behavior. It'll merely execute this task 1 second later
                if (ellapsedMs - lastEntityInRangeTestTotalMs > 1500 && stopOnNearbyEntityCodesExact != null)
                {
                    entityWasInRange = entityInRange();
                    lastEntityInRangeTestTotalMs = ellapsedMs;
                }
                if (entityWasInRange) return false;


                //Check if time is still valid for task.
                if (!IsInValidDayTimeHours(false)) return false;

            }

            return !stopNow && (idleUntilMs < 0 || entity.World.ElapsedMilliseconds < idleUntilMs);
        }

        public override void FinishExecute(bool cancelled)
        {
            base.FinishExecute(cancelled);

            entity.IdleSoundChanceModifier = 1f;
        }


        bool entityInRange()
        {
            if (stopRange <= 0) return false;

            bool found = false;

            partitionUtil.WalkEntities(entity.ServerPos.XYZ, stopRange, (e) => {
                if (!e.Alive || e.EntityId == this.entity.EntityId || !e.IsInteractable) return true;

                string testPath = e.Code.Path;
                if (targetEntityFirstLetters.IndexOf(testPath[0]) < 0) return true;   // early exit if we don't have the first letter
                for (int i = 0; i < stopOnNearbyEntityCodesExact.Length; i++)
                {
                    if (testPath == stopOnNearbyEntityCodesExact[i])
                    {
                        if (e is EntityPlayer entityPlayer)
                        {
                            IPlayer player = entity.World.PlayerByUid(entityPlayer.PlayerUID);
                            if (player == null || (player.WorldData.CurrentGameMode != EnumGameMode.Creative && player.WorldData.CurrentGameMode != EnumGameMode.Spectator))
                            {
                                found = true;
                                return false;
                            }

                            return false;
                        }

                        found = true;
                        return false;
                    }
                }

                for (int i = 0; i < stopOnNearbyEntityCodesBeginsWith.Length; i++)
                {
                    if (testPath.StartsWithFast(stopOnNearbyEntityCodesBeginsWith[i]))
                    {
                        found = true;
                        return false;
                    }
                }

                return true;
            }, EnumEntitySearchType.Creatures);

            return found;
        }


        public override void OnEntityHurt(DamageSource source, float damage)
        {
            if (stopOnHurt)
            {
                stopNow = true;
            }
        }


    }
}
