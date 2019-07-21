using System;
using System.Linq;
using System.Collections.Generic;
using Mono.Game;
using common.resources;
using wServer.networking.packets.outgoing;
using wServer.networking.packets;


namespace wServer.realm.entities
{
    class Decoy : StaticObject, IPlayer
    {
        static Random rand = new Random();

        Player player;
        int duration;
        bool explode;
        int numShots;
        Item item;
        Vector2 direction;
        float speed;

        Vector2 GetRandDirection()
        {
            double angle = rand.NextDouble() * 2 * Math.PI;
            return new Vector2(
                (float)Math.Cos(angle),
                (float)Math.Sin(angle)
            );
        }
        public Decoy(Player player, int duration, float tps, bool explode, int numShots, Item item)
            : base(player.Manager, 0x0715, duration, true, true, true)
        {
            this.player = player;
            this.duration = duration;
            this.speed = tps;
            this.explode = explode;
            this.numShots = numShots;
            this.item = item;

            var history = player.TryGetHistory(1);
            if (history == null)
                direction = GetRandDirection();
            else
            {
                direction = new Vector2(player.X - history.Value.X, player.Y - history.Value.Y);
                if (direction.LengthSquared() == 0)
                    direction = GetRandDirection();
                else
                    direction.Normalize();
            }
        }

        protected override void ExportStats(IDictionary<StatsType, object> stats)
        {
            stats[StatsType.Texture1] = player.Texture1;
            stats[StatsType.Texture2] = player.Texture2;
            base.ExportStats(stats);
        }

        private long JoinedWorld { get; set; }
        private bool IsJoinedWorld { get; set; }
        
        public override void Tick(RealmTime time)
        {
            if (!IsJoinedWorld) {
                IsJoinedWorld = true;
                JoinedWorld = time.TotalElapsedMs;
            } else {
                if (duration > time.TotalElapsedMs - JoinedWorld) {
                    this.ValidateAndMove(
                        X + direction.X * speed * time.ElapsedMsDelta / 1000,
                        Y + direction.Y * speed * time.ElapsedMsDelta / 1000
                    );
                } else {
                    if (explode) {
                        Projectile[] prjs = new Projectile[numShots];
                        ProjectileDesc prjDesc = item.Projectiles[0];
                        var batch = new Packet[numShots];

                        Position decoyPos = new Position() { X = X, Y = Y };
                        float _angle = 360 / numShots;
                        wRandom rando = new wRandom();
                        for (var i = 0; i < numShots; i++)
                        {
                            int dmg = (int)rando.NextIntRange((uint)prjDesc.MinDamage, (uint)prjDesc.MaxDamage);
                            Projectile proj = CreateProjectile(prjDesc, item.ObjectType, dmg, time.TotalElapsedMs, decoyPos, i * _angle);

                            Owner.EnterWorld(proj);

                            batch[i] = new ServerPlayerShoot()
                            {
                                BulletId = proj.ProjectileId,
                                OwnerId = player.Id,
                                ContainerType = item.ObjectType,
                                StartingPos = decoyPos,
                                Angle = proj.Angle,
                                Damage = (short)proj.Damage
                            };
                            prjs[i] = proj;
                        }

                        foreach (Player plr in Owner?.Players.Values.Where(p => p?.DistSqr(this) < Player.RadiusSqr))
                            plr?.Client.SendPackets(batch);
                    }
                    
                    Owner.BroadcastPacketNearby(new ShowEffect()
                    {
                        EffectType = EffectType.AreaBlast,
                        Color = new ARGB(0xffff0000),
                        TargetObjectId = Id,
                        Pos1 = new Position() { X = 1 }
                    }, this, null, PacketPriority.Low);

                    this.HP = -1;
                }
            }

            base.Tick(time);
        }

        public void Damage(int dmg, Entity src) { }

        public bool IsVisibleToEnemy() { return true; }
    }
}
