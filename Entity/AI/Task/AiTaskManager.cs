﻿using System;
using System.Collections.Generic;
using System.Linq;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.API.Util;

#nullable disable

namespace Vintagestory.GameContent
{
    public static class ApiTaskAdditions
    {
        public static void RegisterAiTask(this ICoreServerAPI sapi, string code, Type type)
        {
            AiTaskRegistry.Register(code, type);
        }

        public static void RegisterAiTask<T>(this ICoreServerAPI sapi, string code) where T : AiTaskBase
        {
            AiTaskRegistry.Register<T>(code);
        }
    }


    public static class AiTaskRegistry
    {
        public static Dictionary<string, Type> TaskTypes = new Dictionary<string, Type>();
        public static Dictionary<Type, string> TaskCodes = new Dictionary<Type, string>();

        public static void Register(string code, Type type)
        {
            TaskTypes[code] = type;
            TaskCodes[type] = code;
        }

        public static void Register<T>(string code) where T : AiTaskBase
        {
            TaskTypes[code] = typeof(T);
            TaskCodes[typeof(T)] = code;
        }   

        static AiTaskRegistry()
        {
            Register("wander", typeof(AiTaskWander));
            Register("lookaround", typeof(AiTaskLookAround));
            Register("meleeattack", typeof(AiTaskMeleeAttack));
            Register("seekentity", typeof(AiTaskSeekEntity));
            Register("fleeentity", typeof(AiTaskFleeEntity));
            Register("stayclosetoentity", typeof(AiTaskStayCloseToEntity));
            Register("getoutofwater", typeof(AiTaskGetOutOfWater));
            Register("idle", typeof(AiTaskIdle));
            Register("seekfoodandeat", typeof(AiTaskSeekFoodAndEat));
            Register("seekblockandlay", typeof(AiTaskSeekBlockAndLay));
            Register("useinventory", typeof(AiTaskUseInventory));

            Register("meleeattacktargetingentity", typeof(AiTaskMeleeAttackTargetingEntity));
            Register("seektargetingentity", typeof(AiTaskSeekTargetingEntity));
            Register("stayclosetoguardedentity", typeof(AiTaskStayCloseToGuardedEntity));

            Register("jealousmeleeattack", typeof(AiTaskJealousMeleeAttack));
            Register("jealousseekentity", typeof(AiTaskJealousSeekEntity));

            Register("gotoentity", typeof(AiTaskGotoEntity));
            Register("lookatentity", typeof(AiTaskLookAtEntity));
        }
    }

    public class AiRuntimeConfig : ModSystem
    {
        public static bool RunAiTasks = true;
        public static bool RunAiActivities = true;
        ICoreServerAPI sapi;
        public override bool ShouldLoad(EnumAppSide forSide) => forSide == EnumAppSide.Server;

        public override void StartServerSide(ICoreServerAPI api)
        {
            sapi = api;
            api.Event.RegisterGameTickListener(onTick250ms, 250, 31);
        }

        private void onTick250ms(float obj)
        {
            RunAiTasks = sapi.World.Config.GetAsBool("runAiTasks", true);
            RunAiActivities = sapi.World.Config.GetAsBool("runAiActivities", true);
        }
    }


    public class AiTaskManager
    {
        public event Action<IAiTask> OnTaskStarted;
        public event Action<IAiTask> OnTaskStopped;
        /// <summary>
        /// All delegates must return true to execute the task
        /// </summary>
        public event ActionBoolReturn<IAiTask> OnShouldExecuteTask;

        Entity entity;
        List<IAiTask> tasks = new List<IAiTask>();
        IAiTask[] activeTasksBySlot = new IAiTask[8];
        public bool Shuffle;

        public IAiTask[] ActiveTasksBySlot => activeTasksBySlot;
        public List<IAiTask> AllTasks => tasks;

        public AiTaskManager(Entity entity)
        {
            this.entity = entity;
        }

        public void AddTask(IAiTask task)
        {
            tasks.Add(task);
            task.ProfilerName = "task-startexecute-" + AiTaskRegistry.TaskCodes[task.GetType()];
        }

        public void RemoveTask(IAiTask task)
        {
            tasks.Remove(task);
        }

        public void AfterInitialize()
        {
            foreach (IAiTask task in tasks)
            {
                task.AfterInitialize();
            }
        }

        public void ExecuteTask(IAiTask task, int slot)
        {
            task.StartExecute();
            activeTasksBySlot[slot] = task;

            if (entity.World.FrameProfiler.Enabled)
            {
                entity.World.FrameProfiler.Mark("task-startexecute-" + AiTaskRegistry.TaskCodes[task.GetType()]);
            }
        }

        public T GetTask<T>() where T : IAiTask
        {
            foreach (IAiTask task in tasks)
            {
                if (task is T)
                {
                    return (T)task;
                }
            }

            return default(T);
        }

        public IAiTask GetTask(string id)
        {
            return tasks.FirstOrDefault(t => t.Id == id);
        }

        public void ExecuteTask<T>() where T : IAiTask
        {
            foreach (IAiTask task in tasks)
            {
                if (task is T)
                {
                    int slot = task.Slot;
                    var activeTask = activeTasksBySlot[slot];
                    if (activeTask != null)
                    {
                        activeTask.FinishExecute(true);
                        OnTaskStopped?.Invoke(activeTask);
                    }

                    activeTasksBySlot[slot] = task;
                    task.StartExecute();
                    OnTaskStarted?.Invoke(task);

                    entity.World.FrameProfiler.Mark(task.ProfilerName);
                }
            }
        }

        

        public void StopTask(Type taskType)
        {
            foreach (IAiTask task in activeTasksBySlot)
            {
                if (task?.GetType() == taskType)
                {
                    task.FinishExecute(true);
                    OnTaskStopped?.Invoke(task);
                    activeTasksBySlot[task.Slot] = null;
                }
            }

            entity.World.FrameProfiler.Mark("finishexecute");
        }

        public void StopTasks()
        {
            foreach (IAiTask task in activeTasksBySlot)
            {
                if (task == null) continue;
                task.FinishExecute(true);
                OnTaskStopped?.Invoke(task);
                activeTasksBySlot[task.Slot] = null;
            }
        }

        bool wasRunAiTasks;
        public void OnGameTick(float dt)
        {
            if (!AiRuntimeConfig.RunAiTasks)
            {
                if (wasRunAiTasks)
                {
                    foreach (var task in activeTasksBySlot) task?.FinishExecute(true);
                }
                wasRunAiTasks = false;
                return;
            }
            wasRunAiTasks = AiRuntimeConfig.RunAiTasks;

            if (Shuffle)
            {
                tasks.Shuffle(entity.World.Rand);
            }

            foreach (IAiTask task in tasks)
            {
                if (task.Priority < 0) continue;

                int slot = task.Slot;
                IAiTask oldTask = activeTasksBySlot[slot];
                if ((oldTask == null || task.Priority > oldTask.PriorityForCancel) && task.ShouldExecute() && ShouldExecuteTask(task))
                {
                    oldTask?.FinishExecute(true);
                    if (oldTask != null) OnTaskStopped?.Invoke(oldTask);
                    activeTasksBySlot[slot] = task;
                    task.StartExecute();
                    OnTaskStarted?.Invoke(task);
                }

                if (entity.World.FrameProfiler.Enabled)
                {
                    entity.World.FrameProfiler.Mark(task.ProfilerName);
                }
            }


            for (int i = 0; i < activeTasksBySlot.Length; i++)
            {
                IAiTask task = activeTasksBySlot[i];
                if (task == null) continue;
                if (!task.CanContinueExecute()) continue;

                if (!task.ContinueExecute(dt))
                {
                    task.FinishExecute(false);
                    OnTaskStopped?.Invoke(task);
                    activeTasksBySlot[i] = null;
                }

                if (entity.World.FrameProfiler.Enabled)
                {
                    entity.World.FrameProfiler.Mark("task-continueexec-" + AiTaskRegistry.TaskCodes[task.GetType()]);
                }
            }


            if (entity.World.EntityDebugMode)
            {
                string tasks = "";
                int j = 0;
                for (int i = 0; i < activeTasksBySlot.Length; i++)
                {
                    IAiTask task = activeTasksBySlot[i];
                    if (task == null) continue;
                    if (j++ > 0) tasks += ", ";

                    AiTaskRegistry.TaskCodes.TryGetValue(task.GetType(), out string code);

                    tasks += code + "(p"+task.Priority+", pc"+task.PriorityForCancel+")";
#if DEBUG
                    // temporary for debugging
                    if (entity.Properties.Habitat == EnumHabitat.Underwater && task is AiTaskWander wand)
                    {
                        tasks += String.Format(" Heading to: {0:0.00},{1:0.00},{2:0.00}", wand.MainTarget.X - 500000, wand.MainTarget.Y, wand.MainTarget.Z - 500000);
                    }
#endif

                }
                entity.DebugAttributes.SetString("AI Tasks", tasks.Length > 0 ? tasks : "-");
            }
        }

        private bool ShouldExecuteTask(IAiTask task)
        {
            if (OnShouldExecuteTask == null) return true;
            bool exec = true;
            foreach (ActionBoolReturn<IAiTask> dele in OnShouldExecuteTask.GetInvocationList())
            {
                exec &= dele(task);
            }

            return exec;
        }

        public bool IsTaskActive(string id)
        {
            foreach (var val in activeTasksBySlot)
            {
                if (val != null && val.Id == id) return true;
            }

            return false;
        }

        internal void Notify(string key, object data)
        {
            if (key == "starttask")
            {
                if (activeTasksBySlot.FirstOrDefault(t => t?.Id == (string)data) != null) return;
                var task = GetTask((string)data);
                var activeTask = activeTasksBySlot[task.Slot];
                if (activeTask != null)
                {
                    activeTask.FinishExecute(true);
                    OnTaskStopped?.Invoke(activeTask);
                }
                activeTasksBySlot[task.Slot] = null;
                ExecuteTask(task, task.Slot);
                return;
            }

            if (key == "stoptask")
            {
                var task = activeTasksBySlot.FirstOrDefault(t => t?.Id == (string)data);
                if (task == null) return;

                task.FinishExecute(true);
                OnTaskStopped?.Invoke(task);
                activeTasksBySlot[task.Slot] = null;
                return;
            }

            for (int i = 0; i < tasks.Count; i++)
            {
                IAiTask task = tasks[i];

                if (task.Notify(key, data))
                {
                    int slot = tasks[i].Slot;

                    if ((activeTasksBySlot[slot] == null || task.Priority > activeTasksBySlot[slot].PriorityForCancel))
                    {
                        if (activeTasksBySlot[slot] != null)
                        {
                            activeTasksBySlot[slot].FinishExecute(true);
                            OnTaskStopped?.Invoke(activeTasksBySlot[slot]);
                        }

                        activeTasksBySlot[slot] = task;
                        task.StartExecute();
                        OnTaskStarted?.Invoke(task);
                    }
                }
            }
        }

        internal void OnStateChanged(EnumEntityState beforeState)
        {
            foreach (IAiTask task in tasks)
            {
                task.OnStateChanged(beforeState);
            }
        }

        internal void OnEntitySpawn()
        {
            foreach (IAiTask task in tasks)
            {
                task.OnEntitySpawn();
            }
        }

        internal void OnEntityLoaded()
        {
            foreach (IAiTask task in tasks)
            {
                task.OnEntityLoaded();
            }
        }

        internal void OnEntityDespawn(EntityDespawnData reason)
        {
            foreach (IAiTask task in tasks)
            {
                task.OnEntityDespawn(reason);
            }
        }


        internal void OnEntityHurt(DamageSource source, float damage)
        {
            foreach (IAiTask task in tasks)
            {
                task.OnEntityHurt(source, damage);
            }
        }

    }
}
