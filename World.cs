using Conquord.Game;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Conquord.Network.GamePackets;
using System.Threading;
using System.Threading.Generic;
using Conquord.Network.Sockets;
using Conquord.Game.ConquerStructures;
using Conquord.Game.ConquerStructures.Society;
using Conquord.Client;
using System.Drawing;
using Conquord.Database;
using Conquord.Interfaces;
using Conquord.Network;
using Conquord.Game.Features.Tournaments;


namespace Conquord
{
    public class World
    {
        public ShaDow.DelayedTask DelayedTask;
        //public SteedRace SteedRace;
        public static Time32 ClanWarArenaStampScore;
        public Entity attacked;
        public CaptureTheFlag CTF;
        public Auction Auction;
        public static StaticPool GenericThreadPool;
        public static StaticPool ReceivePool, SendPool;
        public TimerRule<GameClient> /*Buffers,*/ Characters, AutoAttack, Companions, Prayer;
        public TimerRule<ClientWrapper> ConnectionReceive, ConnectionReview, ConnectionSend;
        public HeroOfGame HeroOfGame = new HeroOfGame();
        public World()
        {
            GenericThreadPool = new StaticPool(32).Run();
            ReceivePool = new StaticPool(32).Run();
            SendPool = new StaticPool(32).Run();
        }
        public void Init(bool onlylogin = false)
        {
            if (!onlylogin)
            {
                Characters = new TimerRule<GameClient>(CharactersCallback, 1000, ThreadPriority.BelowNormal);
                AutoAttack = new TimerRule<GameClient>(AutoAttackCallback, 1000, ThreadPriority.BelowNormal);
                ThunderCloud = new TimerRule<Entity>(ThunderCloudTimer, 250, ThreadPriority.Lowest);
                Companions = new TimerRule<GameClient>(CompanionsCallback, 1000, ThreadPriority.BelowNormal);
                Prayer = new TimerRule<GameClient>(PrayerCallback, 1000, ThreadPriority.BelowNormal);
                /////////////////////////////
                ConnectionReview = new TimerRule<ClientWrapper>(connectionReview, 60000, ThreadPriority.Lowest);
                ConnectionReceive = new TimerRule<ClientWrapper>(connectionReceive, 1);
                ConnectionSend = new TimerRule<ClientWrapper>(connectionSend, 1);
                ////////////////////////////
                Subscribe(ServerFunctions, 5000);
                Subscribe(WorldTournaments, 1000);
                Subscribe(ArenaFunctions, 1000, ThreadPriority.AboveNormal);
                Subscribe(TeamArenaFunctions, 1000, ThreadPriority.AboveNormal);
            }
            ConnectionSend = new TimerRule<ClientWrapper>(connectionSend, 1);
            ConnectionReceive = new TimerRule<ClientWrapper>(connectionReceive, 1);
            ConnectionReview = new TimerRule<ClientWrapper>(connectionReview, 60000, ThreadPriority.Lowest);
        }
        public bool Register(Entity ThunderCloudd)
        {
            if (ThunderCloudd.Owner.TimerSubscriptions == null)
            {
                ThunderCloudd.Owner.TimerSyncRoot = new object();
                ThunderCloudd.Owner.TimerSubscriptions = new IDisposable[]
                {
                    ThunderCloud.Add(ThunderCloudd)
                };
                return true;
            }
            return false;
        }
        public void Unregister(Entity Thundercloud)
        {
            if (Thundercloud.Owner == null || Thundercloud.Owner.TimerSubscriptions == null) return;
            lock (Thundercloud.Owner.TimerSyncRoot)
            {
                if (Thundercloud.Owner.TimerSubscriptions != null)
                {
                    foreach (var timer in Thundercloud.Owner.TimerSubscriptions)
                        timer.Dispose();
                    Thundercloud.Owner.TimerSubscriptions = null;
                }
            }
        }
        public void CreateTournaments()
        {
            //SteedRace = new SteedRace();
            ElitePKTournament.Create();
            Game.Features.Tournaments.TeamElitePk.TeamTournament.Create();
            Game.Features.Tournaments.TeamElitePk.SkillTeamTournament.Create();
            CTF = new CaptureTheFlag();
            DelayedTask = new ShaDow.DelayedTask();
            Auction = new Auction();
        }
        private void connectionSend(ClientWrapper wrapper, int time)
        {
            ClientWrapper.TrySend(wrapper);
        }
        private void connectionReview(ClientWrapper wrapper, int time)
        {
            ClientWrapper.TryReview(wrapper);
        }
        private void connectionReceive(ClientWrapper wrapper, int time)
        {
            ClientWrapper.TryReceive(wrapper);
        }
        public bool Register(GameClient client)
        {
            if (client.TimerSubscriptions == null)
            {
                client.TimerSyncRoot = new object();
                client.TimerSubscriptions = new IDisposable[]
                {
                    //Buffers.Add(client),
                    Characters.Add(client),
                    AutoAttack.Add(client),
                    Companions.Add(client),
                    Prayer.Add(client),
                };
                return true;
            }
            return false;
        }
        public void UnRegister(GameClient client)
        {
            if (client.TimerSubscriptions == null) return;
            lock (client.TimerSyncRoot)
            {
                if (client.TimerSubscriptions != null)
                {
                    foreach (var timer in client.TimerSubscriptions)
                        timer.Dispose();
                    client.TimerSubscriptions = null;
                }
            }
        }
        public static bool Valid(GameClient client)
        {
            if (!client.Socket.Alive || client.Entity == null)
            {
                client.Disconnect();
                return false;
            }
            return true;
        }
        private void CharactersCallback(GameClient client, int time)
        {
            Program.Save();
            //System.Console.Clear();
            if (!Valid(client)) return;
            Time32 Now = new Time32(time);
            DateTime Now64 = DateTime.Now;
            #region OnlinePoints
            if (Now >= client.Entity.OnlinePStamp.AddMinutes(5))
            {
                client.Entity.OnlinePoints += 10;
                client.Entity.OnlinePStamp = Time32.Now;
            }
            #endregion
            #region EpicMonk
            if (client.Spells != null)
            {
                if (client.Spells.ContainsKey(12550) ||
                    client.Spells.ContainsKey(12560) ||
                    client.Spells.ContainsKey(12570))
                {
                    if (!DataHolder.IsMonk(client.Entity.Class))
                    {

                        client.RemoveSpell(client.Spells[12550]);
                        client.RemoveSpell(client.Spells[12560]);
                        client.RemoveSpell(client.Spells[12570]);
                    }
                }
            }
            #endregion
            #region Gambleing
            if (client.Entity.Gambleing != null)
            {
                if (DateTime.Now >= client.Entity.Gambleing.StartTime.AddSeconds(50))
                {
                    var Random = new Random();
                    client.Entity.Gambleing.Seconds = 0;
                    client.Entity.Gambleing.Type = Gambleing.Gambl.EndGamble;
                    client.Send(client.Entity.Gambleing);
                    client.Entity.Gambleing.Seconds = 1;
                    client.Entity.Gambleing.Type = Gambleing.Gambl.ResultGamble;
                    client.Entity.Gambleing.Dice1 = (byte)Random.Next(1, 7);
                    client.Entity.Gambleing.Dice2 = (byte)Random.Next(1, 7);
                    client.Entity.Gambleing.Dice3 = (byte)Random.Next(1, 7);
                    client.Entity.Gambleing.Unknowen = (byte)Random.Next(1, 7);
                    client.Send(client.Entity.Gambleing);
                    byte sum = 0;
                    sum += client.Entity.Gambleing.Dice1;
                    sum += client.Entity.Gambleing.Dice2;
                    sum += client.Entity.Gambleing.Dice3;
                    if (sum <= 10)
                    {
                        if (client.Entity.Gambleing.Bet.ContainsKey(0))
                        {
                            client.Entity.Money += client.Entity.Gambleing.Bet[0].Amount *
                                                   client.Entity.Gambleing.Bet[0].Precent;
                        }
                    }
                    else
                    {
                        if (client.Entity.Gambleing.Bet.ContainsKey(1))
                        {
                            client.Entity.Money += client.Entity.Gambleing.Bet[1].Amount *
                                                   client.Entity.Gambleing.Bet[1].Precent;
                        }
                    }
                    if (client.Entity.Gambleing.Bet.ContainsKey(sum))
                    {
                        client.Entity.Money += client.Entity.Gambleing.Bet[sum].Amount *
                                               client.Entity.Gambleing.Bet[sum].Precent;
                    }
                    client.Entity.Gambleing.Seconds = 50;
                    client.Entity.Gambleing.StartTime = DateTime.Now;
                    client.Entity.Gambleing.Type = Gambleing.Gambl.BeginGamble;
                    client.Entity.Gambleing.Dice1 = 0;
                    client.Entity.Gambleing.Dice2 = 0;
                    client.Entity.Gambleing.Dice3 = 0;
                    client.Send(client.Entity.Gambleing);
                }
            }
            #endregion
            #region lacb
            if (client.Entity.lacb >= 10 & client.Entity.lacb <= 300)
            {//MenaMagice 
                client.Entity.Update((byte)Update.mantos, 1, true);
            }
            if (client.Entity.lacb >= 300 & client.Entity.lacb <= 600)
            {
                client.Entity.Update((byte)Update.mantos, 2, true);
            }
            if (client.Entity.lacb >= 600 & client.Entity.lacb <= 900)
            {
                client.Entity.Update((byte)Update.mantos, 3, true);
            }
            if (client.Entity.lacb >= 900 & client.Entity.lacb <= 1300)
            {
                client.Entity.Update((byte)Update.mantos, 4, true);
            }
            if (client.Entity.lacb >= 1300 & client.Entity.lacb <= 1600)
            {
                client.Entity.Update((byte)Update.mantos, 5, true);
            }
            if (client.Entity.lacb >= 1600 & client.Entity.lacb <= 1900)
            {
                client.Entity.Update((byte)Update.mantos, 6, true);
            }
            if (client.Entity.lacb >= 1900 & client.Entity.lacb <= 2200)
            {
                client.Entity.Update((byte)Update.mantos, 7, true);
            }
            if (client.Entity.lacb >= 2200 & client.Entity.lacb <= 2800)
            {
                client.Entity.Update((byte)Update.mantos, 8, true);
            }
            if (client.Entity.lacb >= 2800 & client.Entity.lacb <= 3400)
            {
                client.Entity.Update((byte)Update.mantos, 9, true);
            }
            if (client.Entity.lacb >= 3400 & client.Entity.lacb <= 4200)
            {
                client.Entity.Update((byte)Update.mantos, 10, true);
            }
            if (client.Entity.lacb >= 4200 & client.Entity.lacb <= 5400)
            {
                client.Entity.Update((byte)Update.mantos, 11, true);
            }
            if (client.Entity.lacb >= 5400 & client.Entity.lacb <= 6800)
            {
                client.Entity.Update((byte)Update.mantos, 12, true);
            }
            if (client.Entity.lacb >= 6800)
            {
                client.Entity.Update((byte)Update.mantos, 13, true);
            }
            #endregion  
            if (client.Entity.HandleTiming)
            {
                #region Titles
                if (client.Entity.Titles.Count > 0)
                {
                    foreach (var titles in client.Entity.Titles)
                    {
                        if (Now64 > titles.Value)
                        {
                            client.Entity.Titles.Remove(titles.Key);
                            if (client.Entity.MyTitle == titles.Key)
                                client.Entity.MyTitle = TitlePacket.Titles.None;
                            client.Entity.RemoveTopStatus((UInt64)titles.Key);
                        }
                    }
                }
                #endregion
                #region ClanWarArena
                if (DateTime.Now.Hour == 20 && DateTime.Now.Minute == 00 && DateTime.Now.Second == 01)
                {
                    Game.ClanWar.Start();
                    Kernel.SendWorldMessage(new Message(" Clan war has began!", System.Drawing.Color.Black, Message.Talk), Program.GamePool);
                    foreach (Client.GameClient GameClient in Kernel.GamePool.Values)
                        GameClient.MessageBox("Clan war has began! Wanna Join?",
                              (p) => { p.Entity.Teleport(1509, 82, 118); }, null, 60);
                    client.Send(new Network.GamePackets.Message(" Clan war has began!.", System.Drawing.Color.White, Network.GamePackets.Message.Center));
                }
                if (DateTime.Now.Hour == 20 && DateTime.Now.Minute == 59 && DateTime.Now.Second == 59)
                {
                    Game.ClanWar.End();
                }
                #endregion  
                #region Guildwar Start
                if (DateTime.Now.DayOfWeek == DayOfWeek.Friday && DateTime.Now.Hour == 20 && DateTime.Now.Minute == 00 && DateTime.Now.Second == 00)
                {
                    GuildWar.Start();
                    Kernel.SendWorldMessage(new Message(" Guild war start Now.", System.Drawing.Color.Black, Message.Talk), Program.GamePool);
                    foreach (Client.GameClient GameClient in Kernel.GamePool.Values)
                        GameClient.MessageBox("Guild War Start Wanna Join?",
                              (p) => { p.Entity.Teleport(1038, 86, 112); }, null, 60);
                    client.Send(new Network.GamePackets.Message(" Guild war start Now.", System.Drawing.Color.White, Network.GamePackets.Message.Center));
                }

                #endregion
                #region Guild War End
                if (DateTime.Now.DayOfWeek == DayOfWeek.Friday && DateTime.Now.Hour == 24 && DateTime.Now.Minute == 00 && DateTime.Now.Second == 00)
                {
                    GuildWar.End();
                    Kernel.SendWorldMessage(new Message(" Guild war has End.", System.Drawing.Color.Black, Message.Talk), Program.GamePool);
                    foreach (Client.GameClient GameClient in Kernel.GamePool.Values)
                        GameClient.MessageBox("Guild War Ended Wanna back to twin city?",
                              (p) => { p.Entity.Teleport(1002, 300, 278); }, null, 60);
                    client.Send(new Network.GamePackets.Message(" Guild war Ended.", System.Drawing.Color.White, Network.GamePackets.Message.Center));
                }

                #endregion
                #region OneHitPK
                if (client.Entity.MapID == 6000 || client.Entity.MapID == 6001 || client.Entity.MapID == 6002 || client.Entity.MapID == 6003 || client.Entity.MapID == 6004)
                    return;
                if (DateTime.Now.Minute == 50 && DateTime.Now.Second <= 02)
                {

                    client.Send(new Message("One Hit [PK] Event Began .. Come To Win More Cps !", System.Drawing.Color.White, Message.Center));
                    client.MessageBox("One Hit [PK] Start Wanna Join ..?",
                          (p) => { p.Entity.Teleport(18, 67, 52); }, null, 60);
                }
                #endregion
                #region LifePK
                if (client.Entity.MapID == 6000 || client.Entity.MapID == 6001 || client.Entity.MapID == 6002 || client.Entity.MapID == 6003 || client.Entity.MapID == 6004)
                    return;
                if (DateTime.Now.Minute == 21 && DateTime.Now.Second <= 10)
                {

                    client.Send(new Message("Life [PK] Event Began .. Come To Win More Cps !", System.Drawing.Color.White, Message.Center));
                    client.MessageBox("Life [PK] Start Wanna Join ..?",
                          (p) => { p.Entity.Teleport(18, 67, 59); }, null, 60);
                }
                #endregion
                #region BigBossPK
                if (client.Entity.MapID == 6000 || client.Entity.MapID == 6001 || client.Entity.MapID == 6002 || client.Entity.MapID == 6003 || client.Entity.MapID == 6004)
                    return;
                {
                    if (DateTime.Now.Minute == 10 && DateTime.Now.Second == 02)
                    {
                        client.Send(new Message("BigBoss [PK] Event Began .. Come To Win More Cps !", System.Drawing.Color.White, Message.Center));
                        client.MessageBox("BigBoss [PK] Has Start Wanna Join ..?",
                              (p) => { p.Entity.Teleport(18, 60, 46); }, null, 60);
                    }
                }
                #endregion
                #region GentleWarPK
                if (client.Entity.MapID == 6000 || client.Entity.MapID == 6001 || client.Entity.MapID == 6002 || client.Entity.MapID == 6003 || client.Entity.MapID == 6004)
                    return;
                {
                    if (DateTime.Now.Minute == 29 && DateTime.Now.Second == 02)
                    {
                        client.Send(new Message("GentleWar [PK] Event Began .. Come To Win More Cps !", System.Drawing.Color.White, Message.Center));
                        client.MessageBox("GentleWar [PK] Start Wanna Join ..?",
                              p => { p.Entity.Teleport(18, 66, 46); }, null, 60);
                    }
                }
                #endregion
                #region CrazyWarPK
                if (client.Entity.MapID == 6000 || client.Entity.MapID == 6001 || client.Entity.MapID == 6002 || client.Entity.MapID == 6003 || client.Entity.MapID == 6004)
                    return;
                if (DateTime.Now.Minute == 45 && DateTime.Now.Second <= 02)
                {

                    client.Send(new Message("CrazyWar [PK] Event Began .. Come To Win More Cps !", System.Drawing.Color.White, Message.Center));
                    client.MessageBox("CrazyWar [PK] Start Wanna Join ..?",
                          (p) => { p.Entity.Teleport(18, 54, 51); }, null, 60);
                }
                #endregion
                #region BuchterPK
                if (client.Entity.MapID == 6000 || client.Entity.MapID == 6001 || client.Entity.MapID == 6002 || client.Entity.MapID == 6003 || client.Entity.MapID == 6004)
                    return;
                if (DateTime.Now.Minute == 32 && DateTime.Now.Second <= 02)
                {

                    client.Send(new Message("ButcherWar [PK] Event Began .. Come To Win More Cps !", System.Drawing.Color.White, Message.Center));
                    client.MessageBox("ButcherWar [PK] Start Wanna Join ..?",
                          (p) => { p.Entity.Teleport(18, 52, 38); }, null, 60);
                }
                #endregion
                #region BlackNamePK
                if (DateTime.Now.Minute == 38 && DateTime.Now.Second <= 01)
                {
                    client.Send(new Message("BlackName [PK] Event Began .. Come To Win More Cps And [ToP] !", System.Drawing.Color.White, Message.Center));
                    client.MessageBox("BlackName [PK] Start Wanna Join ..?",
                              (p) => { p.Entity.Teleport(18, 66, 65); }, null, 60);
                }
                #endregion
                #region ChampionRacePK
                if (DateTime.Now.Minute == 5 && DateTime.Now.Second == 10)
                {
                    client.Send(new Message("ChampionRace [PK] Event Began .. Come To Win More Cps And [ToP] !", System.Drawing.Color.White, Message.Center));
                    client.MessageBox("ChampionRace [PK] Start Wanna Join ..?",
                       (p) => { p.Entity.Teleport(18, 60, 66); }, null, 60);
                }
                #endregion
                #region RedNamePK
                if (DateTime.Now.Minute == 13 && DateTime.Now.Second <= 01)
                {
                    client.Send(new Message("RedName [PK] Event Began .. Come To Win More Cps And [ToP] !", System.Drawing.Color.White, Message.Center));
                    client.MessageBox("RedName [PK] Start Wanna Join ..?",
                             (p) => { p.Entity.Teleport(18, 56, 66); }, null, 60);
                }
                #endregion
                #region DeadWorldPK
                if (DateTime.Now.Minute == 18 && DateTime.Now.Second <= 01)
                {
                    client.Send(new Message("DeadWorld [PK] Event Began .. Come To Win More Cps And [ToP] !", System.Drawing.Color.White, Message.Center));
                    client.MessageBox("DeadWorld [PK] Start Wanna Join ..?",
                              (p) => { p.Entity.Teleport(18, 56, 59); }, null, 60);
                }
                #endregion
                #region RevengerPK
                if (DateTime.Now.Minute == 25 && DateTime.Now.Second <= 02)
                {
                    client.Send(new Message("Revenger [PK] Event Began .. Come To Win More Cps And [ToP] !", System.Drawing.Color.White, Message.Center));
                    client.MessageBox("Revenger [PK] Start Wanna Join ..?",
                             (p) => { p.Entity.Teleport(18, 56, 46); }, null, 60);
                }
                #endregion
                #region FloorItems
                /* foreach (var flooritem in Database.FloorItemTable.FloorItemms)
            {
             //   if (Kernel.GetDistance(flooritem.X, flooritem.Y, client.Entity.X, client.Entity.Y) < 17)
                  //  client.SendScreenSpawn(flooritem, true);
            }*/
                #endregion
                #region Bloodshed~Sea item's
                if (client.Entity.IncreaseFinalMDamage)
                {
                    if (Time32.Now > client.Entity.IncreaseFinalMDamageStamp.AddSeconds(80))
                    {
                        client.Entity.IncreaseFinalMDamage = false;
                        client.LoadItemStats();
                    }
                }
                if (client.Entity.IncreaseFinalPDamage)
                {
                    if (Time32.Now > client.Entity.IncreaseFinalPDamageStamp.AddSeconds(80))
                    {
                        client.Entity.IncreaseFinalPDamage = false;
                        client.LoadItemStats();
                    }
                }
                if (client.Entity.IncreaseFinalMAttack)
                {
                    if (Time32.Now > client.Entity.IncreaseFinalMAttackStamp.AddSeconds(80))
                    {
                        client.Entity.IncreaseFinalMAttack = false;
                        client.LoadItemStats();
                    }
                }
                if (client.Entity.IncreaseFinalPAttack)
                {
                    if (Time32.Now > client.Entity.IncreaseFinalPAttackStamp.AddSeconds(80))
                    {
                        client.Entity.IncreaseFinalPAttack = false;
                        client.LoadItemStats();
                    }
                }
                if (client.Entity.IncreaseImunity)
                {
                    if (Time32.Now > client.Entity.IncreaseImunityStamp.AddSeconds(80))
                    {
                        client.Entity.IncreaseImunity = false;
                        client.LoadItemStats();
                    }
                }
                if (client.Entity.IncreaseAntiBreack)
                {
                    if (Time32.Now > client.Entity.IncreaseAntiBreackStamp.AddSeconds(80))
                    {
                        client.Entity.IncreaseAntiBreack = false;
                        client.LoadItemStats();
                    }
                }
                if (client.Entity.IncreasePStrike)
                {
                    if (Time32.Now > client.Entity.IncreasePStrikeStamp.AddSeconds(80))
                    {
                        client.Entity.IncreasePStrike = false;
                        client.LoadItemStats();
                    }
                }
                if (client.Entity.IncreaseBreack)
                {
                    if (Time32.Now > client.Entity.IncreaseBreackStamp.AddSeconds(80))
                    {
                        client.Entity.IncreaseBreack = false;
                        client.LoadItemStats();
                    }
                }
                if (client.Entity.GodlyShield)
                {
                    if (client.Entity.ContainsFlag((ulong)Network.GamePackets.Update.Flags.GodlyShield))
                    {
                        if (Time32.Now > client.Entity.GodlyShieldStamp.AddSeconds(80))
                        {
                            client.Entity.RemoveFlag((ulong)Network.GamePackets.Update.Flags.GodlyShield);
                            client.Entity.GodlyShield = false;
                        }
                    }
                }
                if (client.Entity.IncreaseAttribute)
                {
                    if (Time32.Now > client.Entity.IncreaseAttributeStamp.AddSeconds(80))
                    {
                        client.Entity.IncreaseAttribute = false;
                        client.LoadItemStats();
                    }
                }
                #endregion
                #region PowerArena
                if (((DateTime.Now.Hour == 12 && DateTime.Now.Minute == 55) || (DateTime.Now.Hour == 19 && DateTime.Now.Minute == 55)) && DateTime.Now.Second == 1)
                {
                    client.Send(new Message("Power Arena will be opened in 5 minutes. Please get ready for that!", Color.White, Message.Talk));
                }
                if (((DateTime.Now.Hour == 12 && DateTime.Now.Minute == 56) || (DateTime.Now.Hour == 19 && DateTime.Now.Minute == 56)) && DateTime.Now.Second == 1)
                {
                    client.Send(new Message("Power Arena will be opened in 4 minutes. Please get ready for that!", Color.White, Message.Talk));
                }
                if (((DateTime.Now.Hour == 12 && DateTime.Now.Minute == 57) || (DateTime.Now.Hour == 20 && DateTime.Now.Minute == 57)) && DateTime.Now.Second == 1)
                {
                    client.Send(new Message("Power Arena will be opened in 3 minutes. Please get ready for that!", Color.White, Message.Talk));
                }
                if (((DateTime.Now.Hour == 12 && DateTime.Now.Minute == 58) || (DateTime.Now.Hour == 20 && DateTime.Now.Minute == 58)) && DateTime.Now.Second == 1)
                {
                    client.Send(new Message("Power Arena will be opened in 2 minutes. Please get ready for that!", Color.White, Message.Talk));
                }
                if (((DateTime.Now.Hour == 12 && DateTime.Now.Minute == 59) || (DateTime.Now.Hour == 20 && DateTime.Now.Minute == 59)) && DateTime.Now.Second == 1)
                {
                    client.Send(new Message("Power Arena will be opened in 1 minutes. Please get ready for that!", Color.White, Message.Talk));
                }
                if (((DateTime.Now.Hour == 13 && DateTime.Now.Minute == 00) || (DateTime.Now.Hour == 21 && DateTime.Now.Minute == 00)) && DateTime.Now.Second == 1)
                {
                    client.Send(new Message("The Power Arena is open! Find Arena Manager Wang in Twin City (465,234) to sign up for the Arena.", Color.Red, Message.TopLeft));

                }
                if (((DateTime.Now.Hour == 13 && DateTime.Now.Minute == 55) || (DateTime.Now.Hour == 21 && DateTime.Now.Minute == 55)) && DateTime.Now.Second == 1)
                {
                    client.Send(new Message("Power Arena will be closed in 5 minutes. Go and claim your reward now!", Color.White, Message.Talk));

                }
                if (((DateTime.Now.Hour == 13 && DateTime.Now.Minute == 56) || (DateTime.Now.Hour == 21 && DateTime.Now.Minute == 56)) && DateTime.Now.Second == 1)
                {
                    client.Send(new Message("Power Arena will be closed in 4 minutes. Go and claim your reward now!", Color.White, Message.Talk));

                }
                if (((DateTime.Now.Hour == 13 && DateTime.Now.Minute == 57) || (DateTime.Now.Hour == 21 && DateTime.Now.Minute == 57)) && DateTime.Now.Second == 1)
                {
                    client.Send(new Message("Power Arena will be closed in 3 minutes. Go and claim your reward now!", Color.White, Message.Talk));

                }
                if (((DateTime.Now.Hour == 13 && DateTime.Now.Minute == 58) || (DateTime.Now.Hour == 21 && DateTime.Now.Minute == 58)) && DateTime.Now.Second == 1)
                {
                    client.Send(new Message("Power Arena will be closed in 2 minutes. Go and claim your reward now!", Color.White, Message.Talk));

                }
                if (((DateTime.Now.Hour == 13 && DateTime.Now.Minute == 59) || (DateTime.Now.Hour == 21 && DateTime.Now.Minute == 59)) && DateTime.Now.Second == 1)
                {
                    client.Send(new Message("Power Arena will be closed in 1 minutes. Go and claim your reward now!", Color.White, Message.Talk));

                }
                #endregion
                #region BroadCast
                if (DateTime.Now > Game.ConquerStructures.Broadcast.LastBroadcast.AddMinutes(2))
                {
                    if (Game.ConquerStructures.Broadcast.Broadcasts.Count > 0)
                    {
                        Game.ConquerStructures.Broadcast.CurrentBroadcast = Game.ConquerStructures.Broadcast.Broadcasts[0];
                        Game.ConquerStructures.Broadcast.Broadcasts.Remove(Game.ConquerStructures.Broadcast.CurrentBroadcast);
                        Game.ConquerStructures.Broadcast.LastBroadcast = DateTime.Now;
                        client.Send(new Network.GamePackets.Message(Game.ConquerStructures.Broadcast.CurrentBroadcast.Message, "ALLUSERS", Game.ConquerStructures.Broadcast.CurrentBroadcast.EntityName, System.Drawing.Color.Red, Network.GamePackets.Message.BroadcastMessage));
                    }
                    else
                        Game.ConquerStructures.Broadcast.CurrentBroadcast.EntityID = 1;
                }
                #endregion
                #region CTF
                if (DateTime.Now.DayOfWeek == DayOfWeek.Saturday)
                {
                    if (DateTime.Now.Hour == 20 && DateTime.Now.Minute == 00 && DateTime.Now.Second == 00)
                    {
                        if (!client.Entity.InJail())
                        {
                            Network.GamePackets.AutoInvite alert = new Network.GamePackets.AutoInvite
                            {
                                StrResID = 10535,
                                Countdown = 60,
                                Action = 1
                            };
                            client.Entity.StrResID = 10535;
                            client.Send(alert.ToArray());
                        }
                        if (client.Entity.GLCTF == 1)
                        {
                            client.Entity.GLCTF = 0;
                        }
                        using (var cmd = new MySqlCommand(MySqlCommandType.UPDATE))
                        {
                            cmd.Update("entities").Set("GLCTF", 0).Execute();
                        }

                    }
                }
                if (CaptureTheFlag.IsWar)
                {
                    if (client.Entity.MapID == CaptureTheFlag.MapID)
                    {
                        CaptureTheFlag.SortScoresJoining(client, out client.Guild);
                        CaptureTheFlag.SendScores();

                    }
                }
                #endregion
                #region Activeness
                if (client.Activenes != null)
                {
                    if (Time32.Now >= client.Activenes.HalfHourTask.AddMinutes(30))
                    {
                        client.Activenes.HalfHourTask = Time32.Now;
                        client.Entity.HoursTimes++;
                        client.Activenes.SendSinglePacket(client, Activeness.Types.HoursTask, (byte)(client.Entity.HoursTimes));
                    }
                }
                #endregion
                #region Team Pk
                if (DateTime.Now.DayOfWeek == DayOfWeek.Saturday && DateTime.Now.Hour == 18 && DateTime.Now.Minute == 55 && DateTime.Now.Second == 00)
                {
                    if (!client.Entity.InJail())
                    {
                        Network.GamePackets.AutoInvite alert = new Network.GamePackets.AutoInvite
                        {
                            StrResID = 10543,
                            Countdown = 60,
                            Action = 1
                        };
                        client.Entity.StrResID = 10543;
                        client.Send(alert.ToArray());
                    }
                }
                #endregion
                #region SkillTeamPk
                if (DateTime.Now.DayOfWeek == DayOfWeek.Wednesday && DateTime.Now.Hour == 19 && DateTime.Now.Minute == 40 && DateTime.Now.Second == 00)
                {
                    if (!client.Entity.InJail())
                    {
                        Network.GamePackets.AutoInvite alert = new Network.GamePackets.AutoInvite
                        {
                            StrResID = 10541,
                            Countdown = 60,
                            Action = 1
                        };
                        client.Entity.StrResID = 10541;
                        client.Send(alert.ToArray());
                    }
                }
                #endregion
                #region Roullet PlayerTimer
                Map map = Kernel.Maps[2807];
                if (map != null)
                {
                    foreach (Interfaces.IRoulette Table in map.Tables.Values)
                    {
                        if (Table == null) return;
                        Roulette.RoulettePacket.GetTablePlayerNumber(Table);
                        if (Table.PlayerNumber > 0)
                        {
                            if (Table.Time.AddSeconds(Table.Stamp) < Time32.Now)
                            {
                                Table.Recored.Clear();
                                FastRandom Rand = new FastRandom();
                                byte num = (byte)Rand.Next(0, 38);
                                Table.LotteryNumber = num;
                                if (client.RouletteID == Table.UID || client.RouletteWatchID == Table.UID)
                                {
                                    byte[] buffer = new byte[5 + 8];
                                    Network.Writer.Write(5, 0, buffer);
                                    Network.Writer.Write(2801, 2, buffer);
                                    if (num == 38)
                                        num = 37;
                                    Network.Writer.Write((byte)num, 4, buffer);
                                    client.Send(buffer);
                                    client.RoulletWinnigAmount = 0;
                                    if (client.RouletteWatchID == 0)
                                    {
                                        foreach (var item in client.RoulleteBet)
                                        {
                                            if (item.BetAttribute.Values.Contains(num))
                                            {
                                                if (Table.StackType == 1)
                                                {
                                                    client.RoulletWinnigAmount += item.BetAmount * item.BetAttribute.Profitability;
                                                }
                                                else if (Table.StackType == 2)
                                                {
                                                    client.RoulletWinnigAmount += item.BetAmount * item.BetAttribute.Profitability;
                                                }
                                            }
                                        }
                                        Table.Recored.Add(client);
                                        if (Table.StackType == 1)
                                        {
                                            client.Entity.Money += client.RoulletWinnigAmount;
                                        }
                                        else if (Table.StackType == 2)
                                        {
                                            client.Entity.ConquerPoints += client.RoulletWinnigAmount;
                                        }
                                        client.RoulleteBet.Clear();
                                    }

                                }
                                Table.Time = Time32.Now;
                                Table.Stamp = 35;
                            }
                        }
                    }
                }
                #endregion
                #region Elite PK Tournament
                if (((DateTime.Now.Hour == ElitePK.EventTime) && DateTime.Now.Minute >= 55) && !ElitePKTournament.TimersRegistered)
                {
                    ElitePKTournament.RegisterTimers();
                    ElitePKBrackets brackets = new ElitePKBrackets(true, 0);
                    brackets.Type = ElitePKBrackets.EPK_State;
                    brackets.OnGoing = true;
                    client.Send(brackets);
                    if (!client.Entity.InJail())
                    {
                        Network.GamePackets.AutoInvite alert = new Network.GamePackets.AutoInvite
                        {
                            StrResID = 10533,
                            Countdown = 60,
                            Action = 1
                        };
                        client.Entity.StrResID = 10533;
                        client.Send(alert.ToArray());
                    }
                    #region RemoveTopElite
                    var EliteChampion = Network.GamePackets.TitlePacket.Titles.ElitePKChamption_High;
                    var EliteSecond = Network.GamePackets.TitlePacket.Titles.ElitePK2ndPlace_High;
                    var EliteThird = Network.GamePackets.TitlePacket.Titles.ElitePK3ndPlace_High;
                    var EliteEightChampion = Network.GamePackets.TitlePacket.Titles.ElitePKChamption_Low;
                    var EliteEightSecond = Network.GamePackets.TitlePacket.Titles.ElitePK2ndPlace_Low;
                    var EliteEightThird = Network.GamePackets.TitlePacket.Titles.ElitePK3ndPlace_Low;
                    var EliteEight = Network.GamePackets.TitlePacket.Titles.ElitePKTopEight_Low;
                    if (client.Entity.Titles.ContainsKey(EliteChampion))
                        client.Entity.RemoveTopStatus((ulong)EliteChampion);
                    if (client.Entity.Titles.ContainsKey(EliteSecond))
                        client.Entity.RemoveTopStatus((ulong)EliteSecond);
                    if (client.Entity.Titles.ContainsKey(EliteThird))
                        client.Entity.RemoveTopStatus((ulong)EliteThird);
                    if (client.Entity.Titles.ContainsKey(EliteEightChampion))
                        client.Entity.RemoveTopStatus((ulong)EliteEightChampion);
                    if (client.Entity.Titles.ContainsKey(EliteEightSecond))
                        client.Entity.RemoveTopStatus((ulong)EliteEightSecond);
                    if (client.Entity.Titles.ContainsKey(EliteEightThird))
                        client.Entity.RemoveTopStatus((ulong)EliteEightThird);
                    if (client.Entity.Titles.ContainsKey(EliteEight))
                        client.Entity.RemoveTopStatus((ulong)EliteEight);
                    #endregion
                }
                if ((((DateTime.Now.Hour == ElitePK.EventTime + 1)) && DateTime.Now.Minute >= 10) && ElitePKTournament.TimersRegistered)
                {
                    bool done = true;
                    foreach (var epk in ElitePKTournament.Tournaments)
                        if (epk.Players.Count != 0)
                            done = false;
                    if (done)
                    {
                        ElitePKTournament.TimersRegistered = false;
                        ElitePKBrackets brackets = new ElitePKBrackets(true, 0);
                        brackets.Type = ElitePKBrackets.EPK_State;
                        brackets.OnGoing = false;
                        client.Send(brackets);
                    }
                }
                #endregion
                #region FlameLit
                if (DateTime.Now.DayOfWeek == DayOfWeek.Saturday && DateTime.Now.Hour == 14 && DateTime.Now.Minute == 30 && DateTime.Now.Second == 00)
                    client.Send(new Message("Let`s light up the flame to celebrate the Olympic Games! Find the Flame Taoist (353,325) to learn more!", Color.WhiteSmoke, 2007));
                #endregion
                #region MonthlyPk
                if (DateTime.Now.Day == 1 && DateTime.Now.Hour == 20 && DateTime.Now.Minute == 00 && DateTime.Now.Second == 00)
                {
                    if (!client.Entity.InJail())
                    {
                        Network.GamePackets.AutoInvite alert = new Network.GamePackets.AutoInvite
                        {
                            StrResID = 10523,
                            Countdown = 60,
                            Action = 1
                        };
                        client.Entity.StrResID = 10523;
                        client.Send(alert.ToArray());
                        client.Send(new Message("It's time for Pk War. Go to talk to General Bravely in Twin City (324,194) before 20:19.", Color.Red, Message.TopLeft));
                    }
                }
                #endregion
                #region BlackSpot
                if (Kernel.BlackSpoted.Values.Count > 0)
                {
                    foreach (var spot in Kernel.BlackSpoted.Values)
                    {
                        if (Time32.Now >= spot.BlackSpotStamp.AddSeconds(spot.BlackSpotStepSecs))
                        {
                            if (spot.Dead && spot.EntityFlag == EntityFlag.Player)
                            {
                                foreach (var h in Kernel.GamePool.Values)
                                {
                                    Network.GamePackets.BlackSpotPacket BlackSpotPacket = new Network.GamePackets.BlackSpotPacket();
                                    h.Send(BlackSpotPacket.ToArray(false, spot.UID));
                                }
                                Kernel.BlackSpoted.Remove(spot.UID);
                                continue;
                            }
                            foreach (var h in Kernel.GamePool.Values)
                            {
                                Network.GamePackets.BlackSpotPacket BlackSpotPacket = new Network.GamePackets.BlackSpotPacket();
                                h.Send(BlackSpotPacket.ToArray(false, spot.UID));
                            }
                            spot.IsBlackSpotted = false;
                            Kernel.BlackSpoted.Remove(spot.UID);
                        }
                    }
                }
                #endregion
                #region Jiang
                if (client.Entity.MyJiang != null)
                {
                    client.Entity.MyJiang.TheadTime(client);
                }
                #endregion
                #region WaveofBlood
                if (Time32.Now > client.Entity.WaveofBlood.AddSeconds(client.Entity.WaveofBloodXp))
                {
                    if (client.Spells.ContainsKey(12690))
                    {
                        client.XPCount += 15;
                    }
                    client.Entity.WaveofBlood = Time32.Now;
                    client.Entity.WaveofBloodXp = 8;
                }
                #endregion
                #region ToxicFog
                if (client.Entity.ToxicFogLeft > 0)
                {
                    if (Now >= client.Entity.ToxicFogStamp.AddSeconds(2))
                    {
                        float Percent = client.Entity.ToxicFogPercent;
                        Percent = Percent / 300 * (client.Entity.Immunity / 100F);
                        //Remove this line if you want it normal 
                        //Percent = Math.Min(0.1F, client.Entity.ToxicFogPercent); 
                        client.Entity.ToxicFogLeft--;
                        if (client.Entity.ToxicFogLeft == 0)
                        {
                            client.Entity.RemoveFlag(Update.Flags.Poisoned);
                            return;
                        }
                        client.Entity.ToxicFogStamp = Now;
                        if (client.Entity.Hitpoints > 1)
                        {
                            uint damage = Game.Attacking.Calculate.Percent(client.Entity, Percent);
                            uint value = 100;
                            if (client.Equipment.TotalPerfectionLevel >= 1) value -= 30;
                            if (client.Equipment.TotalPerfectionLevel >= 45) value -= 5;
                            if (client.Equipment.TotalPerfectionLevel >= 85) value -= 5;
                            if (client.Equipment.TotalPerfectionLevel >= 110) value -= 5;
                            if (client.Equipment.TotalPerfectionLevel >= 145) value -= 5;
                            if (client.Equipment.TotalPerfectionLevel >= 185) value -= 10;
                            if (client.Equipment.TotalPerfectionLevel >= 200) value -= 10;
                            if (client.Equipment.TotalPerfectionLevel >= 230) value -= 10;
                            if (client.Equipment.TotalPerfectionLevel >= 260) value -= 10;
                            if (client.Equipment.TotalPerfectionLevel >= 300) value -= 10;
                            client.Entity.Hitpoints -= damage;
                            Network.GamePackets.SpellUse suse = new Network.GamePackets.SpellUse(true);
                            suse.Attacker = client.Entity.UID;
                            suse.SpellID = 10010;
                            suse.AddTarget(client.Entity, damage, null);
                            client.SendScreen(suse, true);

                            if (client != null)
                            {
                                client.UpdateQualifier(damage);

                            }

                        }
                    }
                }
                else
                {
                    if (client.Entity.ContainsFlag(Update.Flags.Poisoned))
                        client.Entity.RemoveFlag(Update.Flags.Poisoned);
                }
                #endregion
                #region Flags
                #region ScurvyBomb
                if (client.Entity.OnScurvyBomb())
                {
                    if (Now > client.Entity.ScurbyBombStamp.AddSeconds(client.Entity.ScurbyBomb))
                    {
                        client.Entity.RemoveFlag2((ulong)Update.Flags2.ScurvyBomb);
                        Update upgrade = new Update(true);
                        upgrade.UID = client.Entity.UID;
                        upgrade.Append(Network.GamePackets.Update.Fatigue, 0, 0, 0, 0);
                        client.Send(upgrade.ToArray());
                    }
                    else if (Now > client.Entity.ScurbyBomb2Stamp.AddSeconds(2))
                    {
                        if (client.Entity.Stamina >= 5)
                        {
                            client.Entity.Stamina -= 5;
                            client.Entity.ScurbyBomb2Stamp = Time32.Now;
                            client.Entity.AddFlag2((ulong)Update.Flags2.ScurvyBomb);
                        }
                        client.Entity.Stamina = client.Entity.Stamina;
                        client.Entity.ScurbyBomb2Stamp = Time32.Now;
                    }
                }
                #endregion
                #region ManiacDance
                if (client.Entity.ContainsFlag3((ulong)1UL << 53))
                {
                    if (Time32.Now > client.Entity.ManiacDance.AddSeconds(15))
                    {
                        client.Entity.RemoveFlag3((ulong)1UL << 53);
                    }
                }
                #endregion
                #region XpBlueStamp
                if (client.Entity.ContainsFlag3(Update.Flags3.WarriorEpicShield))
                {
                    if (Time32.Now > client.Entity.XpBlueStamp.AddSeconds(33))
                    {
                        client.Entity.ShieldIncrease = 0;
                        client.Entity.ShieldTime = 0;
                        client.Entity.MagicShieldIncrease = 0;
                        client.Entity.MagicShieldTime = 0;
                        client.Entity.RemoveFlag3(Update.Flags3.WarriorEpicShield);
                    }
                }
                #endregion
                #region Backfire
                if (client.Entity.ContainsFlag3((ulong)1UL << 51))
                {
                    if (Time32.Now > client.Entity.BackfireStamp.AddSeconds(10))
                    {
                        if (client.Spells.ContainsKey(12680))
                        {
                            if (client.Entity.ContainsFlag3((ulong)1UL << 51))
                                client.Entity.RemoveFlag3((ulong)1UL << 51);
                        }
                        client.Entity.BackfireStamp = Time32.Now;
                    }
                }
                #endregion
                if (client.Entity.ContainsFlag3(Network.GamePackets.Update.Flags3.DivineGuard))
                {
                    if (Time32.Now >= client.Entity.DivineGuardStamp.AddSeconds(10))
                    {
                        client.Entity.RemoveFlag3(Network.GamePackets.Update.Flags3.DivineGuard);
                        client.LoadItemStats();
                    }
                }
                if (client.Entity.ContainsFlag3(Network.GamePackets.Update.Flags3.ShieldBreak))
                {
                    if (Time32.Now >= client.Entity.ShieldBreakStamp.AddSeconds(10))
                    {
                        client.Entity.RemoveFlag3(Network.GamePackets.Update.Flags3.ShieldBreak);
                        client.LoadItemStats();
                    }
                }
                if (client.Entity.HeavenBlessing > 0)
                {
                    if (Now > client.LastTrainingPointsUp.AddMinutes(1))
                    {
                        client.OnlineTrainingPoints += 3;
                        if (client.OnlineTrainingPoints >= 30)
                        {
                            client.OnlineTrainingPoints -= 30;
                            client.Entity.OnlineTraining += 2;
                            client.Entity.Update((byte)Update.OnlineTraining, OnlineTraining.ReceiveExperience, 0);
                        }
                        client.LastTrainingPointsUp = Now;
                        client.Entity.Update((byte)Update.OnlineTraining, OnlineTraining.IncreasePoints, 0);
                    }
                }
                if (client.Entity.HeavenBlessing > 0)
                {
                    if (Now > client.Entity.HeavenBlessingStamp.AddMilliseconds(1000))
                    {
                        client.Entity.HeavenBlessingStamp = Now;
                        client.Entity.HeavenBlessing--;
                    }
                }
                if (client.Entity.DoubleExperienceTime > 0)
                {
                    if (Now > client.Entity.DoubleExpStamp.AddMilliseconds(1000))
                    {
                        client.Entity.DoubleExpStamp = Now;
                        client.Entity.DoubleExperienceTime--;
                    }
                }
                if (client.Entity.EnlightmentTime > 0)
                {
                    if (Now >= client.Entity.EnlightmentStamp.AddMinutes(1))
                    {
                        client.Entity.EnlightmentStamp = Now;
                        client.Entity.EnlightmentTime--;
                        if (client.Entity.EnlightmentTime % 10 == 0 && client.Entity.EnlightmentTime > 0)
                            client.IncreaseExperience(Game.Attacking.Calculate.Percent((int)client.ExpBall, .10F), false);
                    }
                }
                if (Now >= client.Entity.PKPointDecreaseStamp.AddMinutes(5))
                {
                    client.Entity.PKPointDecreaseStamp = Now;
                    if (client.Entity.PKPoints > 0)
                    {
                        client.Entity.PKPoints--;
                    }
                    else
                        client.Entity.PKPoints = 0;
                }
                if (!client.Entity.ContainsFlag(Network.GamePackets.Update.Flags.XPList))
                {
                    if (Now > client.XPCountStamp.AddSeconds(3))
                    {
                        client.XPCountStamp = Now;
                        client.XPCount++;
                        if (client.XPCount >= 100)
                        {
                            client.Entity.AddFlag(Network.GamePackets.Update.Flags.XPList);
                            client.XPCount = 0;
                            client.XPListStamp = Now;
                        }
                    }
                }
                else
                {
                    if (Now > client.XPListStamp.AddSeconds(20))
                    {
                        client.Entity.RemoveFlag(Network.GamePackets.Update.Flags.XPList);
                    }
                }
                if (client.Entity.ContainsFlag((ulong)Update.Flags.FreezeSmall))
                {
                    if (client.RaceFrightened)
                    {
                        if (Time32.Now > client.FrightenStamp.AddSeconds(20))
                        {
                            client.RaceFrightened = false;
                            {
                                GameCharacterUpdates update = new GameCharacterUpdates(true);
                                update.UID = client.Entity.UID;
                                update.Remove(GameCharacterUpdates.Flustered);
                                client.SendScreen(update, true);
                            }
                            client.Entity.RemoveFlag((ulong)Update.Flags.FreezeSmall);
                        }
                        else
                        {
                            int rand;
                            ushort x, y;
                            do
                            {
                                rand = Kernel.Random.Next(Game.Map.XDir.Length);
                                x = (ushort)(client.Entity.X + Game.Map.XDir[rand]);
                                y = (ushort)(client.Entity.Y + Game.Map.YDir[rand]);
                            }
                            while (!client.Map.Floor[x, y, MapObjectType.Player]);
                            client.Entity.Facing = Kernel.GetAngle(client.Entity.X, client.Entity.Y, x, y);
                            client.Entity.X = x;
                            client.Entity.Y = y;
                            client.SendScreen(new TwoMovements()
                            {
                                EntityCount = 1,
                                Facing = client.Entity.Facing,
                                FirstEntity = client.Entity.UID,
                                WalkType = 9,
                                X = client.Entity.X,
                                Y = client.Entity.Y,
                                MovementType = TwoMovements.Walk
                            }, true);
                        }
                    }
                }
                if (client.Entity.ContainsFlag((ulong)Update.Flags.Freeze))
                {
                    if (Now > client.Entity.FrozenStamp.AddSeconds(client.Entity.FrozenTime))
                    {
                        client.Entity.FrozenTime = 0;
                        client.Entity.RemoveFlag((ulong)Update.Flags.Freeze);
                        GameCharacterUpdates update = new GameCharacterUpdates(true);
                        update.UID = client.Entity.UID;
                        update.Remove(GameCharacterUpdates.Freeze);
                        client.SendScreen(update, true);
                    }
                }
                if (client.Entity.ContainsFlag((ulong)Update.Flags.ChaosCycle))
                {
                    if (Time32.Now > client.FrightenStamp.AddSeconds(5))
                    {
                        client.RaceFrightened = false;
                        {
                            GameCharacterUpdates update = new GameCharacterUpdates(true);
                            update.UID = client.Entity.UID;
                            update.Remove(GameCharacterUpdates.Flustered);
                            client.SendScreen(update);
                        }
                        client.Entity.RemoveFlag((ulong)Update.Flags.ChaosCycle);
                    }
                }
                if (client.Entity.ContainsFlag((ulong)Update.Flags.FreezeSmall))
                {
                    if (Time32.Now > client.FrightenStamp.AddSeconds(client.Entity.Fright))
                    {
                        GameCharacterUpdates update = new GameCharacterUpdates(true);
                        update.UID = client.Entity.UID;
                        update.Remove(GameCharacterUpdates.Dizzy);
                        client.SendScreen(update, true);
                        client.Entity.RemoveFlag((ulong)Update.Flags.FreezeSmall);
                    }
                    else
                    {
                        int rand;
                        ushort x, y;
                        do
                        {
                            rand = Kernel.Random.Next(Game.Map.XDir.Length);
                            x = (ushort)(client.Entity.X + Game.Map.XDir[rand]);
                            y = (ushort)(client.Entity.Y + Game.Map.YDir[rand]);
                        }
                        while (!client.Map.Floor[x, y, MapObjectType.Player]);
                        client.Entity.Facing = Kernel.GetAngle(client.Entity.X, client.Entity.Y, x, y);
                        client.Entity.X = x;
                        client.Entity.Y = y;
                        client.SendScreen(new TwoMovements()
                        {
                            EntityCount = 1,
                            Facing = client.Entity.Facing,
                            FirstEntity = client.Entity.UID,
                            WalkType = 9,
                            X = client.Entity.X,
                            Y = client.Entity.Y,
                            MovementType = TwoMovements.Walk
                        }, true);
                    }
                }
                if (client.Entity.ContainsFlag(Network.GamePackets.Update.Flags.FlashingName))
                {
                    if (DateTime.Now > client.Entity.FlashingNameStamp.AddSeconds(client.Entity.FlashingNameTime))
                    {
                        client.Entity.RemoveFlag(Network.GamePackets.Update.Flags.FlashingName);
                    }
                }
                if (client.Entity.Aura_isActive)
                {
                    if (client.Entity.Aura_isActive)
                    {
                        if (Time32.Now >= client.Entity.AuraStamp.AddSeconds(client.Entity.AuraTime))
                        {
                            client.Entity.RemoveFlag2(client.Entity.Aura_actType);
                            client.removeAuraBonuses(client.Entity.Aura_actType, client.Entity.Aura_actPower, 1);
                            client.Entity.Aura_isActive = false;
                            client.Entity.AuraTime = 0;
                            client.Entity.Aura_actType = 0;
                            client.Entity.Aura_actPower = 0;
                        }
                    }
                }
                if (client.Entity.OnKOSpell())
                {
                    if (client.Entity.OnCyclone())
                    {
                        int Seconds = Now.AllSeconds() - client.Entity.CycloneStamp.AddSeconds(client.Entity.CycloneTime).AllSeconds();
                        if (Seconds >= 1)
                        {
                            client.Entity.RemoveFlag(Network.GamePackets.Update.Flags.Cyclone);
                        }
                    }
                    if (client.Entity.OnSuperman())
                    {
                        int Seconds = Now.AllSeconds() - client.Entity.SupermanStamp.AddSeconds(client.Entity.SupermanTime).AllSeconds();
                        if (Seconds >= 1)
                        {
                            client.Entity.RemoveFlag(Network.GamePackets.Update.Flags.Superman);
                        }
                    }
                    if (client.Entity.OnSuperCyclone())
                    {
                        int Seconds = Now.AllSeconds() - client.Entity.SuperCycloneStamp.AddSeconds(client.Entity.SuperCycloneTime).AllSeconds();
                        if (Seconds >= 1)
                        {
                            client.Entity.RemoveFlag3(Network.GamePackets.Update.Flags3.SuperCyclone);
                        }
                    }
                    if (!client.Entity.OnKOSpell())
                    {
                        client.Entity.KOCount = 0;
                    }
                }
                if (client.Entity.ContainsFlag(Network.GamePackets.Update.Flags.Fly))
                {
                    if (Now >= client.Entity.FlyStamp.AddSeconds(client.Entity.FlyTime))
                    {
                        client.Entity.RemoveFlag(Network.GamePackets.Update.Flags.Fly);
                        client.Entity.FlyTime = 0;
                    }
                }
                if (client.Entity.NoDrugsTime > 0)
                {
                    if (Now > client.Entity.NoDrugsStamp.AddSeconds(client.Entity.NoDrugsTime))
                    {
                        client.Entity.NoDrugsTime = 0;
                    }
                }
                if (client.Entity.OnFatalStrike())
                {
                    if (Now > client.Entity.FatalStrikeStamp.AddSeconds(client.Entity.FatalStrikeTime))
                    {
                        client.Entity.RemoveFlag(Network.GamePackets.Update.Flags.FatalStrike);
                    }
                }
                if (client.Entity.OnOblivion())
                {
                    if (Now > client.Entity.OblivionStamp.AddSeconds(client.Entity.OblivionTime))
                    {
                        client.Entity.RemoveFlag2(Network.GamePackets.Update.Flags2.Oblivion);
                    }
                }
                if (client.Entity.ContainsFlag(Network.GamePackets.Update.Flags.ShurikenVortex))
                {
                    if (Now > client.Entity.ShurikenVortexStamp.AddSeconds(client.Entity.ShurikenVortexTime))
                    {
                        client.Entity.RemoveFlag(Network.GamePackets.Update.Flags.ShurikenVortex);
                    }
                }
                if (client.Entity.Transformed)
                {
                    if (Now > client.Entity.TransformationStamp.AddSeconds(client.Entity.TransformationTime))
                    {
                        client.Entity.Untransform();
                    }
                }
                if (client.Entity.ContainsFlag2(Network.GamePackets.Update.Flags2.SoulShackle))
                {
                    if (Now > client.Entity.ShackleStamp.AddSeconds(client.Entity.ShackleTime))
                    {
                        client.Entity.RemoveFlag2(Network.GamePackets.Update.Flags2.SoulShackle);
                    }
                }
                if (client.Entity.ContainsFlag2(Network.GamePackets.Update.Flags2.AzureShield))
                {
                    if (Now > client.Entity.MagicShieldStamp.AddSeconds(client.Entity.MagicShieldTime))
                    {
                        client.Entity.RemoveFlag2(Network.GamePackets.Update.Flags2.AzureShield);
                    }
                }
                if (client.Entity.ContainsFlag3(Update.Flags3.BladeFlurry))
                {
                    if (Time32.Now > client.Entity.BladeFlurryStamp.AddSeconds(45))
                    {
                        client.Entity.RemoveFlag3(Update.Flags3.BladeFlurry);
                    }
                }
                if (client.Entity.ContainsFlag((ulong)Update.Flags.GodlyShield) && client.Entity.MapID != 3846)
                {
                    if (Time32.Now > client.GuardStamp.AddSeconds(10))
                    {
                        client.RaceGuard = false;
                        {
                            GameCharacterUpdates update = new GameCharacterUpdates(true);
                            update.UID = client.Entity.UID;
                            update.Remove(GameCharacterUpdates.DivineShield);
                            client.SendScreen(update);
                        }
                        client.Entity.RemoveFlag((ulong)Update.Flags.GodlyShield);
                    }
                }
                if (client.Entity.ContainsFlag3(Update.Flags3.SuperCyclone))
                {
                    if (Time32.Now > client.Entity.SuperCycloneStamp.AddSeconds(40))
                    {
                        client.Entity.RemoveFlag3(Update.Flags3.SuperCyclone);
                    }
                }
                if (client.Entity.ContainsFlag(Update.Flags.DivineShield) && client.Entity.MapID == 1950)
                {
                    if (Now > client.GuardStamp.AddSeconds(10))
                    {
                        client.RaceGuard = false;
                        {
                            GameCharacterUpdates update = new GameCharacterUpdates(true);
                            update.UID = client.Entity.UID;
                            update.Remove(GameCharacterUpdates.DivineShield);
                            client.SendScreen(update);
                        }
                        client.Entity.RemoveFlag(Update.Flags.DivineShield);
                    }
                }
                if (client.Entity.ContainsFlag(Update.Flags.OrangeSparkles) && !client.InQualifier() && client.Entity.MapID == 1950)
                {
                    if (Time32.Now > client.RaceExcitementStamp.AddSeconds(15))
                    {
                        var upd = new GameCharacterUpdates(true)
                        {
                            UID = client.Entity.UID
                        };
                        upd.Remove(GameCharacterUpdates.Accelerated);
                        client.SendScreen(upd);
                        client.SpeedChange = null;
                        client.Entity.RemoveFlag(Update.Flags.OrangeSparkles);
                    }
                }
                if (client.Entity.ContainsFlag((ulong)Update.Flags.OrangeSparkles))
                {
                    if (Time32.Now > client.RaceExcitementStamp.AddSeconds(15))
                    {
                        var upd = new GameCharacterUpdates(true)
                        {
                            UID = client.Entity.UID
                        };
                        upd.Remove(GameCharacterUpdates.Accelerated);
                        client.SendScreen(upd);
                        client.SpeedChange = null;
                        client.Entity.RemoveFlag((ulong)Update.Flags.SpeedIncreased);
                        client.Entity.RemoveFlag((ulong)Update.Flags.OrangeSparkles);
                    }
                }
                if (client.Entity.ContainsFlag((ulong)Update.Flags.PurpleSparkles))
                {
                    if (Time32.Now > client.DecelerateStamp.AddSeconds(10))
                    {
                        {
                            client.RaceDecelerated = false;
                            var upd = new GameCharacterUpdates(true)
                            {
                                UID = client.Entity.UID
                            };
                            upd.Remove(GameCharacterUpdates.Decelerated);
                            client.SendScreen(upd);
                            client.SpeedChange = null;
                        }
                        client.Entity.RemoveFlag((ulong)Update.Flags.PurpleSparkles);
                    }
                }
                if (client.Entity.ContainsFlag(Update.Flags.PurpleSparkles) && !client.InQualifier())
                {
                    if (Time32.Now > client.DecelerateStamp.AddSeconds(10))
                    {
                        {
                            client.RaceDecelerated = false;
                            var upd = new GameCharacterUpdates(true)
                            {
                                UID = client.Entity.UID
                            };
                            upd.Remove(GameCharacterUpdates.Decelerated);
                            client.SendScreen(upd);
                            client.SpeedChange = null;
                        }
                        client.Entity.RemoveFlag(Update.Flags.PurpleSparkles);
                    }
                }
                if (client.Entity.ContainsFlag(Update.Flags.Cursed))
                {
                    if (Time32.Now > client.Entity.Cursed.AddSeconds(300))
                    {
                        client.Entity.RemoveFlag(Update.Flags.Cursed);
                    }
                }
                if (!client.TeamAura)
                {
                    if (client.Team != null && !client.Entity.Dead && client.Team.Teammates != null)
                    {
                        foreach (Client.GameClient pClient in client.Team.Teammates)
                        {
                            if (client.Entity.UID != pClient.Entity.UID && Kernel.GetDistance(client.Entity.X, client.Entity.Y, pClient.Entity.X, pClient.Entity.Y) <= Constants.pScreenDistance)
                            {
                                if (pClient.Entity.Aura_isActive && pClient.Socket.Alive && pClient.Entity.UID != client.Entity.UID && pClient.Entity.MapID == client.Entity.MapID)
                                {
                                    if (pClient.Entity.Aura_actType == Update.Flags2.FendAura || pClient.Entity.Aura_actType == Update.Flags2.TyrantAura)
                                    {
                                        client.TeamAura = true;
                                        client.TeamAuraOwner = pClient;
                                        client.TeamAuraStatusFlag = pClient.Entity.Aura_actType;
                                        client.TeamAuraPower = pClient.Entity.Aura_actPower;
                                        client.Entity.AddFlag2(client.TeamAuraStatusFlag);
                                        string type = "Critial Strikes";
                                        if (client.Entity.Aura_actType == 100) type = "Immunity";
                                        client.Send(new Message(type + " increased By " + client.TeamAuraPower + " percent!", System.Drawing.Color.Red, Message.Agate));
                                        client.doAuraBonuses(client.TeamAuraStatusFlag, client.TeamAuraPower, 1);
                                        break;
                                    }
                                }
                            }
                        }
                    }
                }
                else
                {
                    var pClient = client.TeamAuraOwner;
                    string type = "Critial Strikes";
                    if (client.Entity.Aura_actType == 100) type = "Immunity";
                    if (pClient == null)
                    {
                        client.TeamAura = false;
                        client.removeAuraBonuses(client.TeamAuraStatusFlag, client.TeamAuraPower, 1);
                        client.Entity.RemoveFlag2(client.TeamAuraStatusFlag);
                        client.Send(new Message(type + " decreased by " + client.TeamAuraPower + " percent!", System.Drawing.Color.Red, Message.Agate));
                        client.TeamAuraStatusFlag = 0;
                        client.TeamAuraPower = 0;
                    }
                    else
                    {
                        if (!pClient.Entity.Aura_isActive || !pClient.Socket.Alive || pClient.Entity.Dead || pClient.Entity.MapID != client.Entity.MapID)
                        {
                            client.TeamAura = false;
                            client.removeAuraBonuses(client.TeamAuraStatusFlag, client.TeamAuraPower, 1);
                            client.Entity.RemoveFlag2(client.TeamAuraStatusFlag);
                            client.Send(new Message(type + " decreased by " + client.TeamAuraPower + " percent!", System.Drawing.Color.Red, Message.Agate));
                            client.TeamAuraStatusFlag = 0;
                            client.TeamAuraPower = 0;
                        }
                        else
                        {
                            if (client.Team == null || (pClient.Team == null || (pClient.Team != null && !pClient.Team.IsTeammate(client.Entity.UID))) || client.Entity.Dead || Kernel.GetDistance(client.Entity.X, client.Entity.Y, pClient.Entity.X, pClient.Entity.Y) > Constants.pScreenDistance)
                            {
                                client.TeamAura = false;
                                client.removeAuraBonuses(client.TeamAuraStatusFlag, client.TeamAuraPower, 1);
                                client.Entity.RemoveFlag2(client.TeamAuraStatusFlag);
                                client.Send(new Message(type + " decreased by " + client.TeamAuraPower + " percent!", System.Drawing.Color.Red, Message.Agate));
                                client.TeamAuraStatusFlag = 0;
                                client.TeamAuraPower = 0;
                            }
                        }
                    }
                }
                if (client.Entity.ContainsFlag(Update.Flags2.Congelado))
                {
                    if (DateTime.Now > client.Entity.CongeladoTimeStamp)
                    {
                        client.Entity.RemoveFlag(Update.Flags2.Congelado);
                    }
                }
                if (client.Entity.ContainsFlag(18014398509481984uL) && client.Entity.MapID == 1950 && client.RaceFrightened)
                {
                    if (Time32.Now > client.FrightenStamp.AddSeconds(60))
                    {
                        client.RaceFrightened = false;
                        GameCharacterUpdates gameCharacterUpdates = new GameCharacterUpdates(true);
                        gameCharacterUpdates.UID = client.Entity.UID;
                        gameCharacterUpdates.Remove(54u);
                        client.SendScreen(gameCharacterUpdates, true);
                        client.Entity.RemoveFlag(18014398509481984uL);
                    }
                }
                if (client.Entity.ContainsFlag3(Update.Flags3.DragonCyclone))
                {
                    if (Time32.Now > client.Entity.DragonCycloneStamp.AddSeconds(45))
                    {
                        client.Entity.RemoveFlag3(Update.Flags3.DragonCyclone);
                    }
                }
                if (client.Entity.ContainsFlag3(Update.Flags3.DragonFury))
                {
                    if (Time32.Now > client.Entity.DragonFuryStamp.AddSeconds(client.Entity.DragonFuryTime))
                    {
                        client.Entity.RemoveFlag3(Update.Flags3.DragonFury);

                        Network.GamePackets.Update upgrade = new Network.GamePackets.Update(true);
                        upgrade.UID = client.Entity.UID;
                        upgrade.Append(74
                            , 0
                            , 0, 0, 0);
                        client.Entity.Owner.Send(upgrade.ToArray());
                    }
                }
                if (client.Entity.ContainsFlag3(Update.Flags3.DragonFlow) && !client.Entity.ContainsFlag3(Update.Flags3.DragonCyclone))
                {
                    if (Time32.Now > client.Entity.DragonFlowStamp.AddSeconds(8))
                    {
                        if (client.Spells.ContainsKey(12270))
                        {
                            var spell = Database.SpellTable.GetSpell(client.Spells[12270].ID, client.Spells[12270].Level);
                            if (spell != null)
                            {
                                int stamina = 100;
                                if (client.Entity.HeavenBlessing > 0)
                                    stamina += 30;
                                if (client.Entity.Stamina != stamina)
                                {
                                    client.Entity.Stamina += 5;
                                    if (client.Entity.ContainsFlag3(Update.Flags3.DragonCyclone))
                                        if (client.Entity.Stamina != stamina)
                                            client.Entity.Stamina += 7;
                                    _String str = new _String(true);
                                    str.UID = client.Entity.UID;
                                    str.TextsCount = 1;
                                    str.Type = _String.Effect;
                                    str.Texts.Add("leedragonblood");
                                    client.SendScreen(str, true);
                                }
                            }
                        }
                        client.Entity.DragonFlowStamp = Time32.Now;
                    }
                }
                if (client.Entity.ContainsFlag3(Update.Flags3.DragonSwing))
                {
                    if (Time32.Now > client.Entity.DragonSwingStamp.AddSeconds(160))
                    {
                        client.Entity.RemoveFlag3(Update.Flags3.DragonSwing);
                        client.Entity.OnDragonSwing = false;
                        Update upgrade = new Update(true);
                        upgrade.UID = client.Entity.UID;
                        upgrade.Append(Update.DragonSwing, 0, 0, 0, 0);
                        client.Entity.Owner.Send(upgrade.ToArray());
                    }
                }
                if (client.Entity.AutoRev > 0)
                {
                    if (client.Entity.HeavenBlessing > 0)
                    {
                        if (Time32.Now >= client.Entity.AutoRevStamp.AddSeconds(client.Entity.AutoRev))
                        {
                            client.Entity.Action = Game.Enums.ConquerAction.None;
                            client.ReviveStamp = Time32.Now;
                            client.Attackable = false;
                            client.Entity.TransformationID = 0;
                            client.Entity.RemoveFlag(Update.Flags.Dead);
                            client.Entity.RemoveFlag(Update.Flags.Ghost);
                            client.Entity.Hitpoints = client.Entity.MaxHitpoints;
                            client.Entity.Mana = client.Entity.MaxMana;
                            client.Entity.AutoRev = 0;
                            AutoHunt AutoHunt = new AutoHunt();
                            AutoHunt.Action = AutoHunt.Mode.Start;
                            client.Entity.InAutoHunt = true;
                            // PacketHandler.HandlePacket(AutoHunt.ToArray(), client);   

                        }
                    }
                    else
                    {
                        client.Entity.RemoveFlag(Update.Flags3.AutoHunting);
                        client.Entity.InAutoHunt = false;
                    }
                }
                if (client.Entity.Hitpoints == 0 && client.Entity.ContainsFlag(Network.GamePackets.Update.Flags.Dead) && !client.Entity.ContainsFlag(Network.GamePackets.Update.Flags.Ghost))
                {
                    if (Now > client.Entity.DeathStamp.AddSeconds(2))
                    {
                        client.Entity.AddFlag(Network.GamePackets.Update.Flags.Ghost);
                        if (client.Entity.Body % 10 < 3)
                            client.Entity.TransformationID = 99;
                        else
                            client.Entity.TransformationID = 98;

                        client.SendScreenSpawn(client.Entity, true);
                    }
                }
                if (client.Entity.ContainsFlag2(Update.Flags2.ChainBoltActive))
                    if (Now > client.Entity.ChainboltStamp.AddSeconds(client.Entity.ChainboltTime))
                        client.Entity.RemoveFlag2(Update.Flags2.ChainBoltActive);
                if (client.Entity.HasMagicDefender && Now >= client.Entity.MagicDefenderStamp.AddSeconds(client.Entity.MagicDefenderSecs))
                {
                    client.Entity.RemoveMagicDefender();
                }
                if (Now >= client.Entity.BlackbeardsRageStamp.AddSeconds(60))
                {
                    client.Entity.RemoveFlag2(Conquord.Network.GamePackets.Update.Flags2.BlackbeardsRage);
                }
                if (Now >= client.Entity.CannonBarrageStamp.AddSeconds(60))
                {
                    client.Entity.RemoveFlag2(Conquord.Network.GamePackets.Update.Flags2.CannonBarrage);
                }
                if (Now >= client.Entity.SuperCycloneStamp.AddSeconds(40))
                {
                    client.Entity.RemoveFlag3(Conquord.Network.GamePackets.Update.Flags3.SuperCyclone);
                }
                if (Now >= client.Entity.FatigueStamp.AddSeconds(client.Entity.FatigueSecs))
                {
                    client.Entity.RemoveFlag2(Conquord.Network.GamePackets.Update.Flags2.Fatigue);
                    client.Entity.IsDefensiveStance = false;
                }
                if (client.Entity.ContainsFlag2(Network.GamePackets.Update.Flags2.TyrantAura) && !client.TeamAura)
                {
                    if (Now >= client.Entity.AuraStamp.AddSeconds(client.Entity.AuraTime))
                    {
                        client.Entity.AuraTime = 0;
                        client.Entity.Aura_isActive = false;
                        //client.Entity.StigmaIncrease = 0;
                        client.Entity.RemoveFlag2(Network.GamePackets.Update.Flags2.TyrantAura);
                    }
                }
                if (client.Entity.ContainsFlag2(Network.GamePackets.Update.Flags2.FendAura) && !client.TeamAura)
                {
                    if (Now >= client.Entity.AuraStamp.AddSeconds(client.Entity.AuraTime))
                    {
                        client.Entity.AuraTime = 0;
                        client.Entity.Aura_isActive = false;
                        //client.Entity.StigmaIncrease = 0;
                        client.Entity.RemoveFlag2(Network.GamePackets.Update.Flags2.FendAura);
                    }
                }
                if (client.Entity.ContainsFlag2(Network.GamePackets.Update.Flags2.MetalAura))
                {
                    if (Now >= client.Entity.AuraStamp.AddSeconds(client.Entity.AuraTime))
                    {
                        client.Entity.AuraTime = 0;
                        client.Entity.Aura_isActive = false;
                        //client.Entity.StigmaIncrease = 0;
                        client.Entity.RemoveFlag2(Network.GamePackets.Update.Flags2.MetalAura);
                    }
                }
                if (client.Entity.ContainsFlag2(Network.GamePackets.Update.Flags2.WoodAura))
                {
                    if (Now >= client.Entity.AuraStamp.AddSeconds(client.Entity.AuraTime))
                    {
                        client.Entity.AuraTime = 0;
                        client.Entity.Aura_isActive = false;
                        //client.Entity.StigmaIncrease = 0;
                        client.Entity.RemoveFlag2(Network.GamePackets.Update.Flags2.WoodAura);
                    }
                }
                if (client.Entity.ContainsFlag2(Network.GamePackets.Update.Flags2.WaterAura))
                {
                    if (Now >= client.Entity.AuraStamp.AddSeconds(client.Entity.AuraTime))
                    {
                        client.Entity.AuraTime = 0;
                        client.Entity.Aura_isActive = false;
                        //client.Entity.StigmaIncrease = 0;
                        client.Entity.RemoveFlag2(Network.GamePackets.Update.Flags2.WaterAura);
                    }
                }
                if (client.Entity.ContainsFlag2(Network.GamePackets.Update.Flags2.EarthAura))
                {
                    if (Now >= client.Entity.AuraStamp.AddSeconds(client.Entity.AuraTime))
                    {
                        client.Entity.AuraTime = 0;
                        client.Entity.Aura_isActive = false;
                        //client.Entity.StigmaIncrease = 0;
                        client.Entity.RemoveFlag2(Network.GamePackets.Update.Flags2.EarthAura);
                    }
                }
                if (client.Entity.ContainsFlag2(Network.GamePackets.Update.Flags2.FireAura))
                {
                    if (Now >= client.Entity.AuraStamp.AddSeconds(client.Entity.AuraTime))
                    {
                        client.Entity.AuraTime = 0;
                        client.Entity.Aura_isActive = false;
                        //client.Entity.StigmaIncrease = 0;
                        client.Entity.RemoveFlag2(Network.GamePackets.Update.Flags2.FireAura);
                    }
                }

                if (client.Entity.ContainsFlag(Network.GamePackets.Update.Flags.Stigma))
                {
                    if (Now >= client.Entity.StigmaStamp.AddSeconds(client.Entity.StigmaTime))
                    {
                        client.Entity.StigmaTime = 0;
                        client.Entity.StigmaIncrease = 0;
                        client.Entity.RemoveFlag(Network.GamePackets.Update.Flags.Stigma);
                    }
                }
                if (client.Entity.ContainsFlag(Network.GamePackets.Update.Flags.Dodge))
                {
                    if (Now >= client.Entity.DodgeStamp.AddSeconds(client.Entity.DodgeTime))
                    {
                        client.Entity.DodgeTime = 0;
                        client.Entity.DodgeIncrease = 0;
                        client.Entity.RemoveFlag(Network.GamePackets.Update.Flags.Dodge);
                    }
                }
                if (client.Entity.ContainsFlag(Network.GamePackets.Update.Flags.Invisibility))
                {
                    if (Now >= client.Entity.InvisibilityStamp.AddSeconds(client.Entity.InvisibilityTime))
                    {
                        client.Entity.RemoveFlag(Network.GamePackets.Update.Flags.Invisibility);
                    }
                }
                if (client.Entity.ContainsFlag(Network.GamePackets.Update.Flags.StarOfAccuracy))
                {
                    if (client.Entity.StarOfAccuracyTime != 0)
                    {
                        if (Now >= client.Entity.StarOfAccuracyStamp.AddSeconds(client.Entity.StarOfAccuracyTime))
                        {
                            client.Entity.RemoveFlag(Network.GamePackets.Update.Flags.StarOfAccuracy);
                        }
                    }
                    else
                    {
                        if (Now >= client.Entity.AccuracyStamp.AddSeconds(client.Entity.AccuracyTime))
                        {
                            client.Entity.RemoveFlag(Network.GamePackets.Update.Flags.StarOfAccuracy);
                        }
                    }
                }
                if (client.Entity.ContainsFlag(Network.GamePackets.Update.Flags.MagicShield))
                {
                    if (client.Entity.MagicShieldTime != 0)
                    {
                        if (Now >= client.Entity.MagicShieldStamp.AddSeconds(client.Entity.MagicShieldTime))
                        {
                            client.Entity.MagicShieldIncrease = 0;
                            client.Entity.MagicShieldTime = 0;
                            client.Entity.RemoveFlag(Network.GamePackets.Update.Flags.MagicShield);
                        }
                    }
                    else
                    {
                        if (Now >= client.Entity.ShieldStamp.AddSeconds(client.Entity.ShieldTime))
                        {
                            client.Entity.ShieldIncrease = 0;
                            client.Entity.ShieldTime = 0;
                            client.Entity.RemoveFlag(Network.GamePackets.Update.Flags.MagicShield);
                        }
                    }
                }
                if (client.Entity.EnlightenPoints >= 0.9)
                {
                    client.Entity.Update((byte)Update.EnlightPoints, client.Entity.EnlightenPoints, true);
                }
                else if ((client.Entity.EnlightenPoints < 1.0) && client.Entity.ContainsFlag((byte)Update.EnlightPoints))
                {
                    client.Entity.RemoveFlag((byte)Update.EnlightPoints);
                }
                if (client.Entity.ContainsFlag(Update.Flags.CastPray))
                {
                    if (client.BlessTime <= 7198500)
                        client.BlessTime += 1000;
                    else
                        client.BlessTime = 7200000;
                    client.Entity.Update(Update.LuckyTimeTimer, client.BlessTime, false);
                }
                if (client.Entity.ContainsFlag(Update.Flags.Praying))
                {
                    if (client.PrayLead != null)
                    {
                        if (client.PrayLead.Socket.Alive)
                        {
                            if (client.BlessTime <= 7199000)
                                client.BlessTime += 500;
                            else
                                client.BlessTime = 7200000;
                            client.Entity.Update(Update.LuckyTimeTimer, client.BlessTime, false);
                        }
                        else
                            client.Entity.RemoveFlag(Update.Flags.Praying);
                    }
                    else
                        client.Entity.RemoveFlag(Update.Flags.Praying);
                }
                if (!client.Entity.ContainsFlag(Update.Flags.Praying) && !client.Entity.ContainsFlag(Update.Flags.CastPray))
                {
                    if (client.BlessTime > 0)
                    {
                        if (client.BlessTime >= 500)
                            client.BlessTime -= 500;
                        else
                            client.BlessTime = 0;
                        client.Entity.Update(Update.LuckyTimeTimer, client.BlessTime, false);
                    }
                }
                #endregion
                #region Mentor
                client.ReviewMentor();
                #endregion
                #region BP Check

                if (client.Entity.NobilityRank == NobilityRank.King && client.Entity.BattlePower > 450)
                {

                    ConquerItem[] inventory = new ConquerItem[client.Inventory.Objects.Length];
                    client.Inventory.Objects.CopyTo(inventory, 0);

                    foreach (ConquerItem item in inventory)
                    {
                        client.Inventory.Remove(item, Game.Enums.ItemUse.Remove);
                    }

                    client.Equipment.Remove(1);
                    client.Equipment.Remove(2);
                    client.Equipment.Remove(3);
                    client.Equipment.Remove(4);
                    client.Equipment.Remove(5);
                    client.Equipment.Remove(6);
                    client.Equipment.Remove(7);
                    client.Equipment.Remove(8);
                    client.Equipment.Remove(9);
                    client.Equipment.Remove(10);
                    client.Equipment.Remove(11);
                    client.Equipment.Remove(12);
                    client.Equipment.Remove(18);
                    client.Equipment.Remove(19);
                    client.Disconnect();
                }
                if (client.Entity.NobilityRank == NobilityRank.Prince && client.Entity.BattlePower > 450)
                {

                    ConquerItem[] inventory = new ConquerItem[client.Inventory.Objects.Length];
                    client.Inventory.Objects.CopyTo(inventory, 0);

                    foreach (ConquerItem item in inventory)
                    {
                        client.Inventory.Remove(item, Game.Enums.ItemUse.Remove);
                    }

                    client.Equipment.Remove(1);
                    client.Equipment.Remove(2);
                    client.Equipment.Remove(3);
                    client.Equipment.Remove(4);
                    client.Equipment.Remove(5);
                    client.Equipment.Remove(6);
                    client.Equipment.Remove(7);
                    client.Equipment.Remove(8);
                    client.Equipment.Remove(9);
                    client.Equipment.Remove(10);
                    client.Equipment.Remove(11);
                    client.Equipment.Remove(12);
                    client.Equipment.Remove(18);
                    client.Equipment.Remove(19);
                    client.Disconnect();
                }
                if (client.Entity.NobilityRank == NobilityRank.Duke && client.Entity.BattlePower > 450)
                {

                    ConquerItem[] inventory = new ConquerItem[client.Inventory.Objects.Length];
                    client.Inventory.Objects.CopyTo(inventory, 0);

                    foreach (ConquerItem item in inventory)
                    {
                        client.Inventory.Remove(item, Game.Enums.ItemUse.Remove);
                    }

                    client.Equipment.Remove(1);
                    client.Equipment.Remove(2);
                    client.Equipment.Remove(3);
                    client.Equipment.Remove(4);
                    client.Equipment.Remove(5);
                    client.Equipment.Remove(6);
                    client.Equipment.Remove(7);
                    client.Equipment.Remove(8);
                    client.Equipment.Remove(9);
                    client.Equipment.Remove(10);
                    client.Equipment.Remove(11);
                    client.Equipment.Remove(12);
                    client.Equipment.Remove(18);
                    client.Equipment.Remove(19);
                    client.Disconnect();
                }
                if (client.Entity.NobilityRank == NobilityRank.Earl && client.Entity.BattlePower > 450)
                {

                    ConquerItem[] inventory = new ConquerItem[client.Inventory.Objects.Length];
                    client.Inventory.Objects.CopyTo(inventory, 0);

                    foreach (ConquerItem item in inventory)
                    {
                        client.Inventory.Remove(item, Game.Enums.ItemUse.Remove);
                    }

                    client.Equipment.Remove(1);
                    client.Equipment.Remove(2);
                    client.Equipment.Remove(3);
                    client.Equipment.Remove(4);
                    client.Equipment.Remove(5);
                    client.Equipment.Remove(6);
                    client.Equipment.Remove(7);
                    client.Equipment.Remove(8);
                    client.Equipment.Remove(9);
                    client.Equipment.Remove(10);
                    client.Equipment.Remove(11);
                    client.Equipment.Remove(12);
                    client.Equipment.Remove(18);
                    client.Equipment.Remove(19);
                    client.Disconnect();
                }
                if (client.Entity.NobilityRank == NobilityRank.Knight && client.Entity.BattlePower > 450)
                {

                    ConquerItem[] inventory = new ConquerItem[client.Inventory.Objects.Length];
                    client.Inventory.Objects.CopyTo(inventory, 0);

                    foreach (ConquerItem item in inventory)
                    {
                        client.Inventory.Remove(item, Game.Enums.ItemUse.Remove);
                    }

                    client.Equipment.Remove(1);
                    client.Equipment.Remove(2);
                    client.Equipment.Remove(3);
                    client.Equipment.Remove(4);
                    client.Equipment.Remove(5);
                    client.Equipment.Remove(6);
                    client.Equipment.Remove(7);
                    client.Equipment.Remove(8);
                    client.Equipment.Remove(9);
                    client.Equipment.Remove(10);
                    client.Equipment.Remove(11);
                    client.Equipment.Remove(12);
                    client.Equipment.Remove(18);
                    client.Equipment.Remove(19);
                    client.Disconnect();
                }
                if (client.Entity.NobilityRank == NobilityRank.Serf && client.Entity.BattlePower > 450)
                {

                    ConquerItem[] inventory = new ConquerItem[client.Inventory.Objects.Length];
                    client.Inventory.Objects.CopyTo(inventory, 0);

                    foreach (ConquerItem item in inventory)
                    {
                        client.Inventory.Remove(item, Game.Enums.ItemUse.Remove);
                    }

                    client.Equipment.Remove(1);
                    client.Equipment.Remove(2);
                    client.Equipment.Remove(3);
                    client.Equipment.Remove(4);
                    client.Equipment.Remove(5);
                    client.Equipment.Remove(6);
                    client.Equipment.Remove(7);
                    client.Equipment.Remove(8);
                    client.Equipment.Remove(9);
                    client.Equipment.Remove(10);
                    client.Equipment.Remove(11);
                    client.Equipment.Remove(12);
                    client.Equipment.Remove(18);
                    client.Equipment.Remove(19);
                    client.Disconnect();
                }

                #endregion
                #region Minning
                if (client.Mining && !client.Entity.Dead)
                {
                    if (Now >= client.MiningStamp.AddSeconds(2))
                    {
                        client.MiningStamp = Now;
                        Game.ConquerStructures.Mining.Mine(client);
                    }
                }
                #endregion
                #region Attackable
                if (client.JustLoggedOn)
                {
                    client.JustLoggedOn = false;
                    client.ReviveStamp = Now;
                }
                if (!client.Attackable)
                {
                    if (Now > client.ReviveStamp.AddSeconds(5))
                    {
                        client.Attackable = true;
                    }
                }
                #endregion
                #region OverHP
                if (client.Entity.FullyLoaded)
                {
                    if (client.Entity.Hitpoints > client.Entity.MaxHitpoints && client.Entity.MaxHitpoints > 1 && !client.Entity.Transformed)
                    {
                        client.Entity.Hitpoints = client.Entity.MaxHitpoints;
                    }
                }
                #endregion
                #region Auto Restore HP
                if (DateTime.Now.Second == 00 || DateTime.Now.Second == 10 || DateTime.Now.Second == 20 || DateTime.Now.Second == 30 || DateTime.Now.Second == 40 || DateTime.Now.Second == 50)
                {
                    if (client.Entity.Hitpoints < client.Entity.MaxHitpoints && !Constants.PKFreeMaps.Contains(client.Entity.MapID) && !client.Entity.Dead && client.Entity.MaxHitpoints > 1)
                    {
                        client.Entity.Hitpoints += (uint)Math.Min((uint)(client.Entity.MaxHitpoints - client.Entity.Hitpoints), (uint)6);
                    }
                }
                #endregion
                #region TreasureInTheBlue
                if (DateTime.Now.DayOfWeek != DayOfWeek.Sunday)
                {
                    if (!Kernel.TreasureInTheBlue)
                    {
                        if ((DateTime.Now.Hour == 12 && DateTime.Now.Minute >= 30 || DateTime.Now.Hour == 13 && DateTime.Now.Minute < 30) || (DateTime.Now.Hour == 20 && DateTime.Now.Minute >= 30 || DateTime.Now.Hour == 21 && DateTime.Now.Minute < 29) && DateTime.Now.Second == 0)
                        {
                            if (!client.Entity.InJail())
                            {
                                Network.GamePackets.AutoInvite alert = new Network.GamePackets.AutoInvite
                                {
                                    StrResID = 10552,
                                    Countdown = 60,
                                    Action = 1
                                };
                                client.Entity.StrResID = 10552;
                                client.Send(alert.ToArray());
                                Kernel.TreasureInTheBlue = true;
                            }
                        }
                    }
                    if (Kernel.TreasureInTheBlue && ((DateTime.Now.Hour == 13 && DateTime.Now.Minute == 19) || (DateTime.Now.Hour == 21 && DateTime.Now.Minute == 19)) && DateTime.Now.Second == 0)
                    {
                        client.Send(new Message("The ship of `Treasure In The Blue` event will return home after 10 minutes. Hurry to exchange your coins!", Color.Red, Message.TopLeft));
                    }
                    if (Kernel.TreasureInTheBlue && ((DateTime.Now.Hour == 13 && DateTime.Now.Minute == 29) || (DateTime.Now.Hour == 21 && DateTime.Now.Minute == 29)) && DateTime.Now.Second == 0)
                    {
                        Kernel.TreasureInTheBlue = false;
                        client.Send(new Message("This round of 'Treasure In The Blue' event has ended. Let's look forward to the next round!", Color.White, Message.Talk));
                        if (client.Entity.MapID == 3071 || client.Entity.MapID == 1068)
                        {
                            client.Entity.Teleport(1002, 300, 278);
                        }
                    }
                }
                #endregion
                #region GuildRequest
                if (Now > client.Entity.GuildRequest.AddSeconds(30))
                {
                    client.GuildJoinTarget = 0;
                }
                #endregion
                #region ClassPk
                if (DateTime.Now.DayOfWeek == DayOfWeek.Monday && DateTime.Now.Hour == 19 && DateTime.Now.Minute == 30 && DateTime.Now.Second <= 10)
                {
                    if (!client.Entity.InJail())
                    {
                        Network.GamePackets.AutoInvite alert = new Network.GamePackets.AutoInvite
                        {
                            StrResID = 10519,
                            Countdown = 60,
                            Action = 1
                        };
                        client.Entity.StrResID = 10519;
                        client.Send(alert.ToArray());
                    }
                }
                #endregion
                #region WeeklyPk
                if (DateTime.Now.DayOfWeek == DayOfWeek.Saturday && DateTime.Now.Hour == 20 && DateTime.Now.Minute == 00 && DateTime.Now.Second == 00)
                {
                    if (!client.Entity.InJail())
                    {
                        Network.GamePackets.AutoInvite alert = new Network.GamePackets.AutoInvite
                        {
                            StrResID = 10521,
                            Countdown = 60,
                            Action = 1
                        };
                        client.Entity.StrResID = 10521;
                        client.Send(alert.ToArray());
                        client.Send(new Message("It's time for Pk War. Go to talk to General Bravely in Twin City (324,194) before 20:19.", Color.Red, Message.TopLeft));
                    }
                }
                #endregion
                #region CrossServer
                if (Now64.DayOfWeek == DayOfWeek.Tuesday && Now64.Hour == CrossServer.hour && Now64.Minute == 00 && Now64.Second == 5 && !CrossServer.IsWar)
                {
                    CrossServer.Start();
                    foreach (var GameClient in Program.GamePool)
                        client.MessageBox("CrossServer CTF has begun! Would you like to join?",
                               p => { p.Entity.Teleport(1002, 225, 237); }, null);
                }
                if (CrossServer.IsWar)
                {
                    if (DateTime.Now > CrossServer.StartTime.AddHours(1.0))
                    {
                        CrossServer.End();
                    }
                }
                if (CrossServer.IsWar)
                {
                    if (Time32.Now > CrossServer.ScoreSendStamp.AddSeconds(3))
                    {
                        CrossServer.ScoreSendStamp = Time32.Now;
                        CrossServer.SendScores();
                    }
                }
                #endregion
                #region SnowBansheeSoul
                if (Kernel.SpawnBansheeSoul)
                {
                    if (Time32.Now > Kernel.SpawnBansheeSoulStamp.AddMinutes(30))
                    {
                        Kernel.SpawnBansheeSoul = false;
                    }
                }
                #endregion
                #region AlluringWitch&HisCrystals
                if (DateTime.Now.Minute == 00 && DateTime.Now.Second == 05 && Kernel.AlluringWitchHisCrystals == false)
                {
                    Conquord.Game.MonsterSpawn.AlluringWitchHisCrystals(client);
                }
                #endregion
                #region Ganoderma
                if (DateTime.Now.Minute == 11 && DateTime.Now.Second == 05 && Kernel.Ganoderma == false)
                {
                    Conquord.Game.MonsterSpawn.Ganoderma(client);
                }
                #endregion
                #region Titan
                if (DateTime.Now.Minute == 15 && DateTime.Now.Second == 05 && Kernel.Titan == false)
                {
                    Conquord.Game.MonsterSpawn.Titan(client);
                }
                #endregion
                #region Windwalker
                if (client.Entity.ContainsFlag4(Update.Flags4.Omnipotence))
                {
                    if (Time32.Now > client.Entity.OmnipotenceStamp.AddSeconds(20))
                    {
                        client.Entity.RemoveFlag4(Update.Flags4.Omnipotence);
                    }
                }
                if (client.Entity.ContainsFlag4(Update.Flags4.xChillingSnow))
                {
                    if (Time32.Now >= client.Entity.ChillingSnowStamp.AddSeconds(client.Entity.ChillingSnow))
                    {
                        client.Entity.RemoveFlag4(Update.Flags4.xChillingSnow);
                        client.Entity.ChillingSnow = 0;
                    }
                }
                if (client.Entity.ContainsFlag4(Update.Flags4.xFreezingPelter))
                {
                    if (Time32.Now >= client.Entity.FreezingPelterStamp.AddSeconds(client.Entity.FreezingPelter))
                    {
                        client.Entity.RemoveFlag4(Update.Flags4.xFreezingPelter);
                        client.Entity.FreezingPelter = 0;
                    }
                }
                if (client.Entity.ContainsFlag4(Update.Flags4.HealingSnow))
                {
                    if (Time32.Now > client.Entity.HealingSnowStamp.AddSeconds(5))
                    {
                        client.Entity.HealingSnowStamp = Time32.Now;
                        var spell = Database.SpellTable.GetSpell(12950, client);
                        client.Entity.Hitpoints += (uint)spell.FirstDamage;
                        if (client.Entity.Hitpoints > client.Entity.MaxHitpoints)
                            client.Entity.Hitpoints = client.Entity.MaxHitpoints;
                        client.Entity.Mana += (ushort)spell.SecondDamage;
                        if (client.Entity.Mana > client.Entity.MaxMana)
                            client.Entity.Mana = client.Entity.MaxMana;
                    }
                }
                if (client.Entity.ContainsFlag4(Network.GamePackets.Update.Flags4.RevengeTaill))
                {
                    if (Time32.Now >= client.Entity.RevengeTaillStamp.AddSeconds(10))
                    {
                        client.Entity.RemoveFlag4(Network.GamePackets.Update.Flags4.RevengeTaill);
                    }
                }
                #endregion
                #region SnowBanshee
                if (DateTime.Now.Minute == 27 && DateTime.Now.Second == 05 && Kernel.SpawnBanshee == false ||
                    DateTime.Now.Minute == 57 && DateTime.Now.Second == 05 && Kernel.SpawnBanshee == false)
                {
                    Conquord.Game.MonsterSpawn.StartSnowBanshee(client);
                }
                #endregion
                #region NemesisTyrant
                if (DateTime.Now.Minute == 15 && DateTime.Now.Second == 05 && Kernel.SpawnNemesis == false ||
                    DateTime.Now.Minute == 45 && DateTime.Now.Second == 05 && Kernel.SpawnNemesis == false)
                {
                    MonsterSpawn.StartNemesisTyrant(client);
                }
                #endregion
                #region Mr Joo
                if (DateTime.Now.Minute == 37 && DateTime.Now.Second <= 01)
                {
                    client.Send(new Message("Hi We Wish you Engoy with ZConquer[GM],please Vote us and like to the Page and Groub :D", System.Drawing.Color.White, Message.Center));
                    foreach (Client.GameClient GameClient in Kernel.GamePool.Values)
                        GameClient.MessageBox("Do You want To vote us ?",
                             (p) => { client.Send(new Message("https://www.facebook.com/StorMTQ/", System.Drawing.Color.Red, Network.GamePackets.Message.Website)); }, null, 60);
                }
                #endregion
            }
        }

        public TimerRule<Entity> ThunderCloud;
        private void ThunderCloudTimer(Entity ThunderCloud, int time)
        {
            if (ThunderCloud == null) return;
            if (!Kernel.Maps.ContainsKey(ThunderCloud.MapID))
            {
                Kernel.Maps[ThunderCloud.MapID].RemoveEntity(ThunderCloud);
                Data data = new Data(true);
                data.UID = ThunderCloud.UID;
                data.ID = Data.RemoveEntity;
                ThunderCloud.MonsterInfo.SendScreen(data);
                ThunderCloud.MonsterInfo.SendScreen(data);
                foreach (var client in Kernel.GamePool.Values)
                {
                    if (Kernel.GetDistance(ThunderCloud.X, ThunderCloud.Y, client.Entity.X, client.Entity.Y) > 16) continue;
                    client.RemoveScreenSpawn(ThunderCloud, true);
                }
                Unregister(ThunderCloud);
                return;
            }
            if (Time32.Now >= ThunderCloud.ThunderCloudStamp.AddSeconds(1))
            {
                ThunderCloud.ThunderCloudStamp = Time32.Now;
                if (ThunderCloud.Hitpoints > 400)
                    ThunderCloud.Hitpoints -= 400;
                else
                {
                    Kernel.Maps[ThunderCloud.MapID].RemoveEntity(ThunderCloud);
                    Data data = new Data(true);
                    data.UID = ThunderCloud.UID;
                    data.ID = Data.RemoveEntity;
                    ThunderCloud.MonsterInfo.SendScreen(data);
                    ThunderCloud.MonsterInfo.SendScreen(data);
                    foreach (var client in Kernel.GamePool.Values)
                    {
                        if (Kernel.GetDistance(ThunderCloud.X, ThunderCloud.Y, client.Entity.X, client.Entity.Y) > 16) continue;
                        client.RemoveScreenSpawn(ThunderCloud, true);
                    }
                    Unregister(ThunderCloud);
                    return;
                }
            }
            if ((ThunderCloud.SpawnPacket[50] == 0 && Time32.Now >= ThunderCloud.MonsterInfo.LastMove.AddMilliseconds(750)) || ThunderCloud.SpawnPacket[50] == 128)
            {
                ThunderCloud.MonsterInfo.LastMove = Time32.Now;
                if (ThunderCloud.MonsterInfo.InSight == 0)
                {
                    foreach (var one in Kernel.Maps[ThunderCloud.MapID].Entities.Values.Where(i => Kernel.GetDistance(ThunderCloud.X, ThunderCloud.Y, i.X, i.Y) <= ThunderCloud.MonsterInfo.AttackRange))
                    {
                        if (one == null || one.Dead || one.MonsterInfo.Guard || one.Companion) continue;
                        ThunderCloud.MonsterInfo.InSight = one.UID;
                        Entity insight = null;
                        if (Kernel.Maps[ThunderCloud.MapID].Entities.ContainsKey(ThunderCloud.MonsterInfo.InSight))
                            insight = Kernel.Maps[ThunderCloud.MapID].Entities[ThunderCloud.MonsterInfo.InSight];
                        else if (Kernel.GamePool.ContainsKey(ThunderCloud.MonsterInfo.InSight))
                            insight = Kernel.GamePool[ThunderCloud.MonsterInfo.InSight].Entity;
                        if (insight == null || insight.Dead || (insight.MonsterInfo != null && insight.MonsterInfo.Guard))
                        {
                            ThunderCloud.MonsterInfo.InSight = 0;
                            break;
                        }
                        new Game.Attacking.Handle(null, ThunderCloud, insight);
                        break;
                    }
                }
                else
                {
                    Entity insight = null;
                    if (Kernel.Maps[ThunderCloud.MapID].Entities.ContainsKey(ThunderCloud.MonsterInfo.InSight))
                        insight = Kernel.Maps[ThunderCloud.MapID].Entities[ThunderCloud.MonsterInfo.InSight];
                    else if (Kernel.GamePool.ContainsKey(ThunderCloud.MonsterInfo.InSight))
                        insight = Kernel.GamePool[ThunderCloud.MonsterInfo.InSight].Entity;
                    if (insight == null || insight.Dead || (insight.MonsterInfo != null && insight.MonsterInfo.Guard))
                    {
                        ThunderCloud.MonsterInfo.InSight = 0;
                        return;
                    }
                    new Game.Attacking.Handle(null, ThunderCloud, insight);
                }
            }

        }
        private void AutoAttackCallback(GameClient client, int time)
        {
            if (!Valid(client)) return;
            Time32 Now = new Time32(time);
            if (client.Entity.AttackPacket != null || client.Entity.VortexAttackStamp != null)
            {
                try
                {
                    if (client.Entity.ContainsFlag((ulong)Update.Flags.ShurikenVortex))
                    {
                        if (client.Entity.VortexPacket != null && client.Entity.VortexPacket.ToArray() != null)
                        {
                            if (Now > client.Entity.VortexAttackStamp.AddMilliseconds(1400))
                            {
                                client.Entity.VortexAttackStamp = Now;
                                new Game.Attacking.Handle(client.Entity.VortexPacket, client.Entity, null);
                            }
                        }
                    }
                    else
                    {
                        client.Entity.VortexPacket = null;
                        var AttackPacket = client.Entity.AttackPacket;
                        if (AttackPacket != null && AttackPacket.ToArray() != null)
                        {
                            uint AttackType = AttackPacket.AttackType;
                            if (AttackType == Attack.Magic || AttackType == Attack.Melee || AttackType == Attack.Ranged)
                            {
                                if (AttackType == Attack.Magic)
                                {
                                    if (Now > client.Entity.AttackStamp.AddSeconds(1))
                                    {
                                        if (AttackPacket.Damage != 12160 && AttackPacket.Damage != 12170 &&
                                            AttackPacket.Damage != 12120 && AttackPacket.Damage != 12130 &&
                                            AttackPacket.Damage != 12140 && AttackPacket.Damage != 12320 &&
                                            AttackPacket.Damage != 12330 && AttackPacket.Damage != 12340 &&
                                            AttackPacket.Damage != 12210 && AttackPacket.Damage != 12570)
                                            new Game.Attacking.Handle(AttackPacket, client.Entity, null);
                                    }
                                }
                                else
                                {
                                    int decrease = -300;
                                    if (client.Entity.OnCyclone())
                                        decrease = 700;
                                    if (client.Entity.OnSuperman())
                                        decrease = 200;
                                    if (Now > client.Entity.AttackStamp.AddMilliseconds((1000 - client.Entity.Agility - decrease) * (int)(AttackType == Attack.Ranged ? 1 : 1)))
                                    {
                                        new Game.Attacking.Handle(AttackPacket, client.Entity, null);
                                    }
                                }
                            }
                        }
                    }
                }
                catch (Exception)
                {
                    client.Entity.AttackPacket = null;
                    client.Entity.VortexPacket = null;
                }
            }
        }
        private void PrayerCallback(GameClient client, int time)
        {
            if (!Valid(client)) return;
            Time32 Now = new Time32(time);
            if (client.Entity.Reborn > 1) return;
            if (client.Entity.HandleTiming)
            {
                if (!client.Entity.ContainsFlag((ulong)Update.Flags.Praying))
                {
                    foreach (Interfaces.IMapObject ClientObj in client.Screen.Objects)
                    {
                        if (ClientObj != null)
                        {
                            if (ClientObj.MapObjType == Game.MapObjectType.Player)
                            {
                                var Client = ClientObj.Owner;
                                if (Client.Entity.ContainsFlag((ulong)Update.Flags.CastPray))
                                {
                                    if (Kernel.GetDistance(client.Entity.X, client.Entity.Y, ClientObj.X, ClientObj.Y) <= 3)
                                    {
                                        client.Entity.AddFlag((ulong)Update.Flags.Praying);
                                        client.PrayLead = Client;
                                        client.Entity.Action = Client.Entity.Action;
                                        Client.Prayers.Add(client);
                                        break;
                                    }
                                }
                            }
                        }
                    }
                }
                else if (client.PrayLead != null)
                {
                    if (Kernel.GetDistance(client.Entity.X, client.Entity.Y, client.PrayLead.Entity.X, client.PrayLead.Entity.Y) > 4)
                    {
                        client.Entity.RemoveFlag((ulong)Update.Flags.Praying);
                        client.PrayLead.Prayers.Remove(client);
                        client.PrayLead = null;
                    }
                }
            }
        }
        private void CompanionsCallback(GameClient client, int time)
        {
            if (!Valid(client)) return;
            Time32 Now = new Time32(time);
            if (client.Companion != null)
            {
                short distance = Kernel.GetDistance(client.Companion.X, client.Companion.Y, client.Entity.X, client.Entity.Y);
                if (distance >= 8)
                {
                    ushort X = (ushort)(client.Entity.X + Kernel.Random.Next(2));
                    ushort Y = (ushort)(client.Entity.Y + Kernel.Random.Next(2));
                    if (!client.Map.SelectCoordonates(ref X, ref Y))
                    {
                        X = client.Entity.X;
                        Y = client.Entity.Y;
                    }
                    client.Companion.X = X;
                    client.Companion.Y = Y;
                    Data data = new Data(true);
                    data.ID = Data.Jump;
                    data.dwParam = (uint)((Y << 16) | X);
                    data.wParam1 = X;
                    data.wParam2 = Y;
                    data.UID = client.Companion.UID;
                    client.Companion.MonsterInfo.SendScreen(data);
                }
                else if (distance > 4)
                {
                    Enums.ConquerAngle facing = Kernel.GetAngle(client.Companion.X, client.Companion.Y, client.Companion.Owner.Entity.X, client.Companion.Owner.Entity.Y);
                    if (!client.Companion.Move(facing))
                    {
                        facing = (Enums.ConquerAngle)Kernel.Random.Next(7);
                        if (client.Companion.Move(facing))
                        {
                            client.Companion.Facing = facing;
                            GroundMovement move = new GroundMovement(true);
                            move.Direction = facing;
                            move.UID = client.Companion.UID;
                            move.GroundMovementType = GroundMovement.Run;
                            client.Companion.MonsterInfo.SendScreen(move);
                        }
                    }
                    else
                    {
                        client.Companion.Facing = facing;
                        GroundMovement move = new GroundMovement(true);
                        move.Direction = facing;
                        move.UID = client.Companion.UID;
                        move.GroundMovementType = GroundMovement.Run;
                        client.Companion.MonsterInfo.SendScreen(move);
                    }
                }
                else
                {
                    var monster = client.Companion;
                    if (monster.MonsterInfo.InSight == 0)
                    {
                        if (client.Entity.AttackPacket != null)
                        {
                            if (client.Entity.AttackPacket.AttackType == Attack.Magic)
                            {
                                if (client.Entity.AttackPacket.Decoded)
                                {
                                    if (Database.SpellTable.SpellInformations.ContainsKey((ushort)client.Entity.AttackPacket.Damage))
                                    {
                                        var info = Database.SpellTable.SpellInformations[(ushort)client.Entity.AttackPacket.Damage].Values.ToArray()[client.Spells[(ushort)client.Entity.AttackPacket.Damage].Level];
                                        if (info.CanKill)
                                        {
                                            monster.MonsterInfo.InSight = client.Entity.AttackPacket.Attacked;
                                        }
                                    }
                                }
                            }
                            else
                            {
                                monster.MonsterInfo.InSight = client.Entity.AttackPacket.Attacked;
                            }
                        }
                    }
                    else
                    {
                        if (monster.MonsterInfo.InSight > 400000 && monster.MonsterInfo.InSight < 600000 || monster.MonsterInfo.InSight > 800000 && monster.MonsterInfo.InSight != monster.UID)
                        {
                            Entity attacked = null;

                            if (client.Screen.TryGetValue(monster.MonsterInfo.InSight, out attacked))
                            {
                                if (Now > monster.AttackStamp.AddMilliseconds(monster.MonsterInfo.AttackSpeed))
                                {
                                    monster.AttackStamp = Now;
                                    if (attacked.Dead)
                                    {
                                        monster.MonsterInfo.InSight = 0;
                                    }
                                    else new Game.Attacking.Handle(null, monster, attacked);
                                }
                            }
                            else monster.MonsterInfo.InSight = 0;
                        }
                    }
                }
            }
        }
        private void WorldTournaments(int time)
        {
            Time32 Now = new Time32(time);
            DateTime Now64 = DateTime.Now;
            #region PlunderWar
            if (DateTime.Now.Hour == 18 && DateTime.Now.Minute == 00 && DateTime.Now.Second == 00) Kernel.PlunderWar = true;
            if (DateTime.Now.Hour == 20 && DateTime.Now.Minute == 00 && DateTime.Now.Second == 00) { Kernel.PlunderWar = false; Network.GamePackets.Union.UnionClass.UpGradeUnion(); }
            #endregion
            #region Quiz Show
            if (DateTime.Now.DayOfWeek == DayOfWeek.Saturday || DateTime.Now.DayOfWeek == DayOfWeek.Sunday)
                if (DateTime.Now.Hour == 4 || DateTime.Now.Hour == 14 || DateTime.Now.Hour == 21)
                    if (DateTime.Now.Minute == 0 && DateTime.Now.Second <= 2)
                        Kernel.QuizShow.Start();
            #endregion
            #region CaptureTheFlag
            if (DateTime.Now.DayOfWeek == DayOfWeek.Saturday)
            {
                if (DateTime.Now.Hour == 20 && DateTime.Now.Minute == 00 && DateTime.Now.Second == 0 && !CaptureTheFlag.IsWar)
                {
                    CaptureTheFlag.IsWar = true;
                    CaptureTheFlag.StartTime = DateTime.Now;
                    CaptureTheFlag.ClearHistory();
                    foreach (var current in Kernel.Guilds.Values)
                    {
                        current.CTFFlagScore = 0;
                        current.Points = 0;
                        current.CTFdonationCPs = 0;
                        current.CTFdonationSilver = 0;
                        current.CalculateCTFRank(true);
                        foreach (var current2 in current.Members.Values)
                        {
                            current2.Exploits = 0;
                            current2.ExploitsRank = 0;
                            current2.CTFCpsReward = 0;
                            current2.CTFSilverReward = 0;
                        }
                        current.CalculateCTFRank(false);
                    }



                }
            }
            if (CaptureTheFlag.IsWar)
            {
                Program.World.CTF.SendUpdates();
                if (DateTime.Now >= CaptureTheFlag.StartTime.AddHours(1))
                {
                    CaptureTheFlag.IsWar = false;
                    CaptureTheFlag.Close();
                }
            }
            if (CTF != null)
                CTF.SpawnFlags();
            #endregion
            #region TeamPk
            if (DateTime.Now.DayOfWeek == DayOfWeek.Saturday && DateTime.Now.Hour == 18 && DateTime.Now.Minute == 55 && DateTime.Now.Second == 00)
                Game.Features.Tournaments.TeamElitePk.TeamTournament.Open();
            #endregion
            #region SkillTeamPk
            if (DateTime.Now.DayOfWeek == DayOfWeek.Wednesday && DateTime.Now.Hour == 19 && DateTime.Now.Minute == 40 && DateTime.Now.Second == 00)
                Game.Features.Tournaments.TeamElitePk.SkillTeamTournament.Open();
            #endregion
            #region GuildWar
            if (GuildWar.IsWar)
            {
                if (Time32.Now > GuildWar.ScoreSendStamp.AddSeconds(3))
                {
                    GuildWar.ScoreSendStamp = Time32.Now;
                    GuildWar.SendScores();
                }
                if (!GuildWar.Flame10th)
                {
                    if (DateTime.Now.DayOfWeek == DayOfWeek.Saturday && DateTime.Now.Hour == 14 && DateTime.Now.Minute == 30 && DateTime.Now.Second == 00)
                    {
                        GuildWar.Flame10th = true;
                    }
                }
                if (DateTime.Now.Hour == 15 && DateTime.Now.Minute == 00 && DateTime.Now.Second == 00)
                {
                    GuildWar.Flame10th = false;

                    GuildWar.End();
                }
            }
            #endregion
            #region Elite PK Tournament
            if (((DateTime.Now.Hour == ElitePK.EventTime) && DateTime.Now.Minute >= 55) && !ElitePKTournament.TimersRegistered)
            {
                ElitePK.EventTime = DateTime.Now.Hour;
                ElitePKTournament.RegisterTimers();
            }
            #endregion
            #region HeroOfGame
            if (DateTime.Now.Minute == 00 && DateTime.Now.Second == 01)
            {
                HeroOfGame.CheakUp();
            }
            #endregion
            #region Elite GW
            if (Now64.DayOfWeek == DayOfWeek.Saturday || Now64.DayOfWeek == DayOfWeek.Sunday || Now64.DayOfWeek == DayOfWeek.Monday || Now64.DayOfWeek == DayOfWeek.Tuesday || Now64.DayOfWeek == DayOfWeek.Wednesday || Now64.DayOfWeek == DayOfWeek.Thursday)
            {
                if (!Game.EliteGuildWar.IsWar)
                {
                    if (Program.Conquord_EliteGWTimes.Start.EliteGW && Now64.Minute >= 00)
                    {
                        Game.EliteGuildWar.Start();
                        foreach (var client in Program.Values)
                            //  if (client.Entity.GuildID != 0)
                            client.MessageBox("EliteGuildWar Begin Want Join [Prize : 20,000] CPs] ?",
                                p => { p.Entity.Teleport(1002, 286, 161); }, null);
                        foreach (var client in Program.Values)
                            //  if (client.Entity.GuildID != 0)
                            client.MessageBox("EliteGuildWar Begin Want Join [Prize : 20,000] CPs]",
                                   p => { p.Entity.Teleport(1002, 416, 260); }, null, 60);
                    }
                }
                if (Game.EliteGuildWar.IsWar)
                {
                    if (Time32.Now > Game.EliteGuildWar.ScoreSendStamp.AddSeconds(3))
                    {
                        Game.EliteGuildWar.ScoreSendStamp = Time32.Now;
                        Game.EliteGuildWar.SendScores();
                    }
                    if (Program.Conquord_EliteGWTimes.Start.EliteGW && Now64.Minute == 50 && Now64.Second == 2)
                    {
                        Kernel.SendWorldMessage(new Network.GamePackets.Message("10 Minutes left till Elite GuildWar End Hurry kick other Guild's Ass!.", System.Drawing.Color.White, Network.GamePackets.Message.System), Program.Values);
                    }
                }
                if (Game.EliteGuildWar.IsWar)
                {
                    if (Program.Conquord_EliteGWTimes.End.EliteGW && Now64.Minute >= 00)
                        Game.EliteGuildWar.End();
                }
            }
            #endregion

        }
        private void ServerFunctions(int time)
        {
            DateTime LastPerfectionSort = DateTime.Now;
            DateTime Now64 = DateTime.Now;
            if (DateTime.Now >= LastPerfectionSort.AddMinutes(1))
            {
                LastPerfectionSort = DateTime.Now;
                new MsgUserAbilityScore().GetRankingList();
                new MsgEquipRefineRank().UpdateRanking();
                PrestigeRank.LoadRanking();
            }
            if (DateTime.Now.Second == 00)
            {
                Program.Save();
            }
            if (DateTime.Now > Program.LastRandomReset.AddMinutes(30))
            {
                Program.LastRandomReset = DateTime.Now;
                Kernel.Random = new FastRandom(Program.RandomSeed);
            }
        }
        private void ArenaFunctions(int time)
        {
            Game.Arena.EngagePlayers();
            Game.Arena.CheckGroups();
            Game.Arena.VerifyAwaitingPeople();
            Game.Arena.Reset();
        }
        private void TeamArenaFunctions(int time)
        {
            Game.TeamArena.PickUpTeams();
            Game.TeamArena.EngagePlayers();
            Game.TeamArena.CheckGroups();
            Game.TeamArena.VerifyAwaitingPeople();
            Game.TeamArena.Reset();
        }
        #region Functions
        public static void Execute(Action<int> action, int timeOut = 0, ThreadPriority priority = ThreadPriority.Normal)
        {
            GenericThreadPool.Subscribe(new LazyDelegate(action, timeOut, priority));
        }
        public static void Execute<T>(Action<T, int> action, T param, int timeOut = 0, ThreadPriority priority = ThreadPriority.Normal)
        {
            GenericThreadPool.Subscribe<T>(new LazyDelegate<T>(action, timeOut, priority), param);
        }
        public static IDisposable Subscribe(Action<int> action, int period = 1, ThreadPriority priority = ThreadPriority.Normal)
        {
            return GenericThreadPool.Subscribe(new TimerRule(action, period, priority));
        }
        public static IDisposable Subscribe<T>(Action<T, int> action, T param, int timeOut = 0, ThreadPriority priority = ThreadPriority.Normal)
        {
            return GenericThreadPool.Subscribe<T>(new TimerRule<T>(action, timeOut, priority), param);
        }
        public static IDisposable Subscribe<T>(TimerRule<T> rule, T param, StandalonePool pool)
        {
            return pool.Subscribe<T>(rule, param);
        }
        public static IDisposable Subscribe<T>(TimerRule<T> rule, T param, StaticPool pool)
        {
            return pool.Subscribe<T>(rule, param);
        }
        public static IDisposable Subscribe<T>(TimerRule<T> rule, T param)
        {
            return GenericThreadPool.Subscribe<T>(rule, param);
        }
        #endregion
        
    }
}