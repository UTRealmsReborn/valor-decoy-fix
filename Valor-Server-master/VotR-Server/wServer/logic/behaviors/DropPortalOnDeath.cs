﻿using System;
using wServer.realm;

namespace wServer.logic.behaviors
{
    class DropPortalOnDeath : Behavior
    {
        private readonly ushort _target;
        private readonly float _probability;
        private readonly int? _timeout;
        private readonly float xAdjustment;
        private readonly float yAdjustment;

        public DropPortalOnDeath(string target, double probability = 1, int? timeout = null, float XAdjustment = 0, float YAdjustment = 0)
        {
            _target = GetObjType(target);
            _probability = (float) probability;
            _timeout = timeout;
            xAdjustment = XAdjustment;
            yAdjustment = YAdjustment;
            // a value of 0 means never timeout, 
            // null means use xml timeout, 
            // a value means override xml timeout with that value (in seconds)
        }

        protected internal override void Resolve(State parent)
        {
            parent.Death += (sender, e) =>
            {
                var owner = e.Host.Owner;

                if (owner.Name.Contains("DeathArena") || e.Host.Spawned) 
                    return;
                
                if (e.Host.CurrentState.Is(parent) &&
                    Random.NextDouble() < _probability)
                {
                    var manager = e.Host.Manager;
                    var gameData = manager.Resources.GameData;
                    var timeoutTime = (_timeout == null)
                       ? gameData.Portals[_target].Timeout
                       : _timeout.Value;

                    var entity = Entity.Resolve(manager, _target);
                    entity.Move(e.Host.X + + xAdjustment, e.Host.Y + + yAdjustment);
                    owner.EnterWorld(entity);

                    if (timeoutTime != 0)
                        owner.Timers.Add(new WorldTimer(timeoutTime*1000, (world, t) => //default portal close time * 1000
                        {
                            try
                            {
                                world.LeaveWorld(entity);
                            }
                            catch
                                //couldn't remove portal, Owner became null. Should be fixed with RealmManager implementation
                            {
                                Console.WriteLine("Couldn't despawn portal.");
                            }
                        }));
                }
            };
        }

        protected override void TickCore(Entity host, RealmTime time, ref object state)
        {
        }
    }
}