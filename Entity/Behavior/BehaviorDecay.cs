﻿using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;

#nullable disable

namespace Vintagestory.GameContent
{
    public class EntityBehaviorDeadDecay : EntityBehavior
    {
        ITreeAttribute decayTree;
        JsonObject typeAttributes;
        

        public float HoursToDecay { get; set; }

        public double TotalHoursDead
        {
            get { return decayTree.GetDouble("totalHoursDead"); }
            set { decayTree.SetDouble("totalHoursDead", value); }
        }


        public EntityBehaviorDeadDecay(Entity entity) : base(entity)
        {
        }

        public override void Initialize(EntityProperties properties, JsonObject typeAttributes)
        {
            base.Initialize(properties, typeAttributes);

            (entity as EntityAgent).AllowDespawn = false;

            this.typeAttributes = typeAttributes;
            HoursToDecay = typeAttributes["hoursToDecay"].AsFloat(96);

            decayTree = entity.WatchedAttributes.GetTreeAttribute("decay");

            if (decayTree == null)
            {
                entity.WatchedAttributes.SetAttribute("decay", decayTree = new TreeAttribute());
                TotalHoursDead = entity.World.Calendar.TotalHours;
            }
        }


        public override void OnGameTick(float deltaTime)
        {
            if (!entity.Alive && TotalHoursDead + HoursToDecay < entity.World.Calendar.TotalHours)
            {
                DecayNow();
            }

            base.OnGameTick(deltaTime);
        }


        public void DecayNow()
        {
            if ((entity as EntityAgent).AllowDespawn) return;

            (entity as EntityAgent).AllowDespawn = true;

            if (entity.DespawnReason == null)
            {
                entity.DespawnReason = new EntityDespawnData()
                {
                    DamageSourceForDeath = null,
                    Reason = EnumDespawnReason.Death
                };
            }

            if (typeAttributes["decayedBlock"].Exists)
            {
                AssetLocation blockcode = new AssetLocation(typeAttributes["decayedBlock"].AsString());
                Block decblock = entity.World.GetBlock(blockcode);

                double x = entity.ServerPos.X + entity.SelectionBox.X1 - entity.OriginSelectionBox.X1;
                double y = entity.ServerPos.Y + entity.SelectionBox.Y1 - entity.OriginSelectionBox.Y1;
                double z = entity.ServerPos.Z + entity.SelectionBox.Z1 - entity.OriginSelectionBox.Z1;

                BlockPos bonepos = new BlockPos((int)x, (int)y, (int)z);
                var bl = entity.World.BlockAccessor;
                Block exblock = bl.GetBlock(bonepos);

                if (exblock.IsReplacableBy(decblock))
                {
                    bl.SetBlock(decblock.BlockId, bonepos);
                    bl.MarkBlockDirty(bonepos);
                } else
                {
                    foreach (BlockFacing facing in BlockFacing.HORIZONTALS)
                    {
                        facing.IterateThruFacingOffsets(bonepos);
                        exblock = entity.World.BlockAccessor.GetBlock(bonepos);
                        if (exblock.IsReplacableBy(decblock))
                        {
                            entity.World.BlockAccessor.SetBlock(decblock.BlockId, bonepos);
                            break;
                        }
                    }
                }
                
            }

            Vec3d pos = entity.SidedPos.XYZ + entity.CollisionBox.Center - entity.OriginCollisionBox.Center;
            pos.Y += entity.Properties.DeadCollisionBoxSize.Y / 2;

            entity.World.SpawnParticles(new EntityCubeParticles(
                entity.World,
                entity.EntityId,
                pos, 0.15f, (int)(40 + entity.Properties.DeadCollisionBoxSize.X * 60), 0.4f, 1f
            ));
        }

        public override void OnEntityDeath(DamageSource damageSourceForDeath)
        {
            base.OnEntityDeath(damageSourceForDeath);

            TotalHoursDead = entity.World.Calendar.TotalHours;

            if (damageSourceForDeath?.Source == EnumDamageSource.Void)
            {
                (entity as EntityAgent).AllowDespawn = true;
            }
        }


        public override string PropertyName()
        {
            return "deaddecay";
        }
    }
}
