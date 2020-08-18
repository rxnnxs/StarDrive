﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Remoting.Messaging;
using System.Threading;
using Ship_Game.Gameplay;
using Ship_Game.Utils;

namespace Ship_Game.Threading
{
    /// <summary>
    /// There are two sync points to the main game thread.
    /// 1. the spacemanager quadtree. QuadTree Finds must not run during quadtree update.
    ///     locked by public GameThreadLocker
    /// 2. moving actions to be processed to the processing queue.
    ///     locked by private ActionProcessorLock
    ///     used by method MoveItemsToThread
    /// To avoid locking on the SpaceManager update try to run MoveItemsToThread just after Universe_SpaceManager Update.
    /// </summary>
    public class ActionPool 
    {
        Thread Worker;
        static readonly Array<Action> EmptyArray = new Array<Action>(0);
        Array<Action> ActionProcessor            = EmptyArray;
        Array<Action> ActionAccumulator;
        readonly object ActionProcessorLock      = new object();
        public object GameThreadLocker           = new object();
        bool Initialized;
        int DefaultAccumulatorSize               = 1000;
        
        public int ActionsProcessedThisTurn {get; private set;}
        public float AvgActionsProcessed {get; private set;}
        public bool IsProcessing {get; private set;}
        
        public void Add(Action itemToThread) => ActionAccumulator.Add(itemToThread);
        int MoveResetTime = 10;
        int MoveTimeDelay =0;

        readonly int CoreCount;
        
        public ActionPool()
        {
            Worker            = new Thread(ProcessQueuedItems);
            ActionAccumulator = new Array<Action>(DefaultAccumulatorSize);
            CoreCount         = Parallel.NumPhysicalCores;
        }

        /// <summary>
        /// Run On game thread
        /// this will steal the Actions Accumulated and run them on the game thread. 
        /// </summary>
        /// <param name="runTillEmpty"></param>
        public void ManualUpdate()
        {
            lock (ActionProcessorLock)
            {
                for (int i = 0; i < ActionAccumulator.Count; i++)
                {
                    var action = ActionAccumulator[i];
                    // exceptions will be handled on the game thread.
                    try
                    {
                        action?.Invoke();
                    }
                    catch (Exception ex)
                    {
                        Log.Error(ex);
                    }
                }
            }
            ActionAccumulator = new Array<Action>(DefaultAccumulatorSize);
        }

        /// <summary>
        /// Run after SpaceManager update is done. Run Only from thread generating actions adds
        /// moves all queued actions to thread processing pool. 
        /// </summary>
        public ThreadState MoveItemsToThread()
        {
            if (IsProcessing)
            {
                if (Empire.Universe?.DebugWin != null)
                    Log.Warning($"Action Pool Unable to process all items. Processed: " +
                                $"{ActionsProcessedThisTurn} Waiting {ActionAccumulator.Count} " +
                                $"AVGProcessed: {AvgActionsProcessed} " +
                                $" ThreadState {Worker?.ThreadState}");
                return Worker?.ThreadState ?? ThreadState.Stopped;
            }
            if (ActionProcessor.IsEmpty && ActionAccumulator.Count > 0)// && --MoveTimeDelay < 0)
            {
                if (Empire.Universe?.DebugWin != null)
                {
                    Log.Warning($"Action Pool ActionsProcess Last Update = {ActionsProcessedThisTurn} : AVG {AvgActionsProcessed} ");
                }
                lock(ActionProcessorLock)
                {
                    ActionProcessor = new Array<Action>(ActionAccumulator);
                    ActionAccumulator = new Array<Action>(1000);
                }
            }
            return Worker?.ThreadState ?? ThreadState.Stopped;
        }

        public void Initialize() 
        {
            if (Worker == null || Worker.ThreadState == ThreadState.Stopped)
            {
                Log.Warning($"Async data collector lost its thread? what'd you do!?");
                Worker      = new Thread(ProcessQueuedItems); 
                Initialized = false;
            }
            if (!Initialized)
                Worker.Start();
            Initialized = true;
        }

        void ProcessQueuedItems()
        {
            while (true)
            {
                if (ActionProcessor.Count > 0)
                {
                    IsProcessing = true;
                    int processedLastTurn = ActionsProcessedThisTurn;
                    ActionsProcessedThisTurn =0;

                    Array<Action> localActionQueue;
                    lock (ActionProcessorLock)
                    {
                        localActionQueue = new Array<Action>(ActionProcessor);
                        ActionProcessor = EmptyArray;
                    }

                    lock (GameThreadLocker)
                        Parallel.ForEach(localActionQueue, action =>
                        {
                            try
                            {
                                action?.Invoke();
                                ActionsProcessedThisTurn++;
                            }
                            catch (Exception ex)
                            {
                                Log.Error(ex, $"ActionProcessor Threw in parallel foreach");
                            }
                        });
                    AvgActionsProcessed = (ActionsProcessedThisTurn + processedLastTurn) /2f;
                    IsProcessing = false;       
                }
                
                {
                    Thread.Sleep(1);
                }
            }
        }
    }
}