using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using Conquord.Network;
using Conquord.Database;
using Conquord.Network.Sockets;
using Conquord.Network.GamePackets.Union;
using Conquord.Network.AuthPackets;
using Conquord.Game.ConquerStructures.Society;
using Conquord.Game;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading;
using Conquord.Interfaces;
using System.Text;
using Conquord.Network.GamePackets;
using Conquord.Client;
using System.Threading.Tasks;
using Conquord.ShaDow;
namespace Conquord
{
    class Program
    {
        public static Encoding Encoding = ASCIIEncoding.Default;//Encoding.GetEncoding("iso-8859-1");
        [DllImport("user32.dll")]
        public static extern IntPtr FindWindow(string lpClassName, string lpWindowName);
        [DllImport("user32.dll")]
        public static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);
        public static DateTime LastRandomReset = DateTime.Now;
        private static Native.ConsoleEventHandler ConquordHandler;
        public static Client.GameClient[] GamePool = new Client.GameClient[0];
        public static Client.GameClient[] Values = new Client.GameClient[0];
        #region Poker

        public static void easteregg(string text)
        {

            try
            {

                String folderN = DateTime.Now.Year + "-" + DateTime.Now.Month,

                Path = "gmlogs\\PetApet\\",

                NewPath = System.IO.Path.Combine(Path, folderN);

                if (!File.Exists(NewPath + folderN))
                {

                    System.IO.Directory.CreateDirectory(System.IO.Path.Combine(Path, folderN));

                }

                if (!File.Exists(NewPath + "\\" + DateTime.Now.Day + ".txt"))
                {

                    using (System.IO.FileStream fs = System.IO.File.Create(NewPath + "\\" + DateTime.Now.Day + ".txt"))
                    {

                        fs.Close();

                    }

                }

                using (System.IO.StreamWriter file = new System.IO.StreamWriter(NewPath + "\\" + DateTime.Now.Day + ".txt", true))
                {

                    file.WriteLine(text);

                    file.Close();

                }

            }

            catch (Exception ex) { Console.WriteLine(ex); }

        }

        public static void AddpokerAllinCps(string text)
        {

            try
            {

                text = "[" + DateTime.Now.ToString("HH:mm:ss") + "]" + text;

                String folderN = DateTime.Now.Year + "-" + DateTime.Now.Month,

                Path = "gmlogs\\AddpokerAllinCps\\",

                NewPath = System.IO.Path.Combine(Path, folderN);

                if (!File.Exists(NewPath + folderN))
                {

                    System.IO.Directory.CreateDirectory(System.IO.Path.Combine(Path, folderN));

                }

                if (!File.Exists(NewPath + "\\" + DateTime.Now.Day + ".txt"))
                {

                    using (System.IO.FileStream fs = System.IO.File.Create(NewPath + "\\" + DateTime.Now.Day + ".txt"))
                    {

                        fs.Close();

                    }

                }

                using (System.IO.StreamWriter file = new System.IO.StreamWriter(NewPath + "\\" + DateTime.Now.Day + ".txt", true))
                {

                    file.WriteLine(text);

                    file.Close();

                }

            }

            catch (Exception ex) { Console.WriteLine(ex); }

        }

        public static void AddpokerCps(string text)
        {

            try
            {

                text = "[" + DateTime.Now.ToString("HH:mm:ss") + "]" + text;

                String folderN = DateTime.Now.Year + "-" + DateTime.Now.Month,

                Path = "gmlogs\\AddpokerCps\\",

                NewPath = System.IO.Path.Combine(Path, folderN);

                if (!File.Exists(NewPath + folderN))
                {

                    System.IO.Directory.CreateDirectory(System.IO.Path.Combine(Path, folderN));

                }

                if (!File.Exists(NewPath + "\\" + DateTime.Now.Day + ".txt"))
                {

                    using (System.IO.FileStream fs = System.IO.File.Create(NewPath + "\\" + DateTime.Now.Day + ".txt"))
                    {

                        fs.Close();

                    }

                }

                using (System.IO.StreamWriter file = new System.IO.StreamWriter(NewPath + "\\" + DateTime.Now.Day + ".txt", true))
                {

                    file.WriteLine(text);

                    file.Close();

                }

            }

            catch (Exception ex) { Console.WriteLine(ex); }

        }

        #endregion 
        public static void UpdateConsoleTitle()
        {
            if (Kernel.GamePool.Count > Program.MaxOn)
                Program.MaxOn = Kernel.GamePool.Count;
            if (Kernel.GamePool.Count != 0)
            {
                Console.Title = "Conquord - Online Players :  " + Kernel.GamePool.Count + "  Max Online :  " + Program.MaxOn + " ";
            }
            else if (Kernel.GamePool.Count == 0)
            {
                Console.Title = "Conquord - No Online Players Now !! But Max Online is :  " + Program.MaxOn + " ";
            }
        }
        private static bool ConquordConsole_CloseEvent(CtrlType sig)
        {
            if (MessageBox.Show("Do You Want To Close Console ...?", "Message", MessageBoxButtons.OKCancel, MessageBoxIcon.Question) == DialogResult.OK)
            {
                Save();
                foreach (Client.GameClient client in Kernel.GamePool.Values)
                    client.Disconnect();
                GameServer.Disable();
                AuthServer.Disable();
                if (GuildWar.IsWar)
                    GuildWar.End();
                if (EliteGuildWar.IsWar)
                    EliteGuildWar.End();
                if (ClanWar.IsWar)
                    ClanWar.End();
                if (CaptureTheFlag.IsWar)
                    CaptureTheFlag.Close();
                Application.Exit();
                Environment.Exit(0);
                return false;
            }
            else return true;
        }
        public static void Save()
        {
            try
            {
                using (var conn = Database.DataHolder.MySqlConnection)
                {
                    conn.Open();
                    foreach (Client.GameClient client in Kernel.GamePool.Values)
                    {
                        client.Account.Save(client);
                        Database.EntityTable.SaveEntity(client, conn);
                        Database.DailyQuestTable.Save(client);
                        Database.SkillTable.SaveProficiencies(client, conn);
                        Database.ActivenessTable.Save(client);
                        Database.ChiTable.Save(client);
                        Database.SkillTable.SaveSpells(client, conn);
                        Database.MailboxTable.Save(client);
                        Database.ArenaTable.SaveArenaStatistics(client.ArenaStatistic, client.CP, conn);
                        Database.TeamArenaTable.SaveArenaStatistics(client.TeamArenaStatistic, conn);
                    }
                }
                Conquord.Database.JiangHu.SaveJiangHu();
                AuctionBase.Save();
                Database.Flowers.LoadFlowers();
                Database.InnerPowerTable.Save();
                Database.EntityVariableTable.Save(0, Vars);
                using (MySqlCommand cmd = new MySqlCommand(MySqlCommandType.SELECT))
                {
                    cmd.Select("configuration");
                    using (MySqlReader r = new MySqlReader(cmd))
                    {
                        if (r.Read())
                        {
                            new Database.MySqlCommand(Database.MySqlCommandType.UPDATE).Update("configuration").Set("ServerKingdom", Kernel.ServerKingdom).Set("ItemUID", Network.GamePackets.ConquerItem.ItemUID.Now).Set("GuildID", Game.ConquerStructures.Society.Guild.GuildCounter.Now).Set("UnionID", Union.UnionCounter.Now).Execute();
                            if (r.ReadByte("LastDailySignReset") != DateTime.Now.Month) MsgSignIn.Reset();
                        }
                    }
                }
                using (var cmd = new MySqlCommand(MySqlCommandType.UPDATE).Update("configuration"))
                    cmd.Set("LastDailySignReset", DateTime.Now.Month).Execute();
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
            }
        }
        static void GameServer_OnClientReceive(byte[] buffer, int length, ClientWrapper obj)
        {
            if (obj.Connector == null)
            {
                obj.Disconnect();
                return;
            }

            Client.GameClient Client = obj.Connector as Client.GameClient;

            if (Client.Exchange)
            {
                Client.Exchange = false;
                Client.Action = 1;
                var crypto = new Network.Cryptography.GameCryptography(System.Text.Encoding.Default.GetBytes(Constants.GameCryptographyKey));
                byte[] otherData = new byte[length];
                Array.Copy(buffer, otherData, length);
                crypto.Decrypt(otherData, length);

                bool extra = false;
                int pos = 0;
                if (BitConverter.ToInt32(otherData, length - 140) == 128)//no extra packet
                {
                    pos = length - 140;
                    Client.Cryptography.Decrypt(buffer, length);
                }
                else if (BitConverter.ToInt32(otherData, length - 176) == 128)//extra packet
                {
                    pos = length - 176;
                    extra = true;
                    Client.Cryptography.Decrypt(buffer, length - 36);
                }
                int len = BitConverter.ToInt32(buffer, pos); pos += 4;
                if (len != 128)
                {
                    Client.Disconnect();
                    return;
                }
                byte[] pubKey = new byte[128];
                for (int x = 0; x < len; x++, pos++) pubKey[x] = buffer[pos];

                string PubKey = System.Text.Encoding.Default.GetString(pubKey);
                Client.Cryptography = Client.DHKeyExchange.HandleClientKeyPacket(PubKey, Client.Cryptography);

                if (extra)
                {
                    byte[] data = new byte[36];
                    Buffer.BlockCopy(buffer, length - 36, data, 0, 36);
                    processData(data, 36, Client);
                }
            }
            else
            {
                processData(buffer, length, Client);
            }
        }
        private static void processData(byte[] buffer, int length, Client.GameClient Client)
        {
            Client.Cryptography.Decrypt(buffer, length);
            Client.Queue.Enqueue(buffer, length);
            if (Client.Queue.CurrentLength > 1224)
            {
                Console.WriteLine("[ Disconnect ] Reason : The Packet Size Is Too Big. " + Client.Queue.CurrentLength);
                Client.Disconnect();
                return;
            }
            while (Client.Queue.CanDequeue())
            {
                byte[] data = Client.Queue.Dequeue();
                Network.PacketHandler.HandlePacket(data, Client);
            }
        }
        static void GameServer_OnClientConnect(ClientWrapper obj)
        {
            Client.GameClient client = new Client.GameClient(obj);
            client.Send(client.DHKeyExchange.CreateServerKeyPacket());
            obj.Connector = client;
        }
        static void GameServer_OnClientDisconnect(ClientWrapper obj)
        {
            if (obj.Connector != null)
                (obj.Connector as Client.GameClient).Disconnect();
            else
                obj.Disconnect();
        }
        static void AuthServer_OnClientReceive(byte[] buffer, int length, ClientWrapper arg3)
        {
            var player = arg3.Connector as Client.AuthClient;
            AuthClient authClient = arg3.Connector as AuthClient;
            player.Cryptographer.Decrypt(buffer, length);
            player.Queue.Enqueue(buffer, length);
            while (player.Queue.CanDequeue())
            {
                byte[] packet = player.Queue.Dequeue();

                ushort len = BitConverter.ToUInt16(packet, 0);
                ushort id = BitConverter.ToUInt16(packet, 2);

                if (len == 312)
                {
                    player.Info = new Authentication();
                    player.Info.Deserialize(packet);
                    player.Account = new AccountTable(player.Info.Username);
                    if (!BruteForceProtection.AcceptJoin(arg3.IP))
                    {
                        Console.WriteLine(string.Concat(new string[] { "Client > ", player.Info.Username, "was blocked address", arg3.IP, "!" }));
                        arg3.Disconnect();
                        break;
                    }
                    Forward Fw = new Forward();
                    if (player.Account.Password == player.Info.Password && player.Account.exists)
                    {
                        Fw.Type = Forward.ForwardType.Ready;
                    }
                    else
                    {
                        BruteForceProtection.ClientRegistred(arg3.IP);
                        Fw.Type = Forward.ForwardType.InvalidInfo;
                    }
                    if (IPBan.IsBanned(arg3.IP))
                    {
                        Fw.Type = Forward.ForwardType.Banned;
                        player.Send(Fw);
                        return;
                    }
                    if (Fw.Type == Network.AuthPackets.Forward.ForwardType.Ready)
                    {
                        Fw.Identifier = player.Account.GenerateKey();
                        Kernel.AwaitingPool[Fw.Identifier] = player.Account;
                        Fw.IP = ConquordIP;
                        Fw.Port = GamePort;
                    }
                    player.Send(Fw);
                }
            }
        }
        
        public static void SaveException(Exception e, bool dont = false)
        {
            if (e.TargetSite.Name == "ThrowInvalidOperationException") return;
            if (e.Message.Contains("String reference not set")) return;
            if (!dont)
                Console.WriteLine(e);
            var dt = DateTime.Now;
            string date = dt.Month + "-" + dt.Day + "//";
            if (!Directory.Exists(Application.StartupPath + Constants.UnhandledExceptionsPath))
                Directory.CreateDirectory(Application.StartupPath + "\\" + Constants.UnhandledExceptionsPath);
            if (!Directory.Exists(Application.StartupPath + "\\" + Constants.UnhandledExceptionsPath + date))
                Directory.CreateDirectory(Application.StartupPath + "\\" + Constants.UnhandledExceptionsPath + date);
            if (!Directory.Exists(Application.StartupPath + "\\" + Constants.UnhandledExceptionsPath + date + e.TargetSite.Name))
                Directory.CreateDirectory(Application.StartupPath + "\\" + Constants.UnhandledExceptionsPath + date + e.TargetSite.Name);
            string fullPath = Application.StartupPath + "\\" + Constants.UnhandledExceptionsPath + date + e.TargetSite.Name + "\\";
            string date2 = dt.Hour + "-" + dt.Minute;
            List<string> Lines = new List<string>();
            Lines.Add("----Exception message----");
            Lines.Add(e.Message);
            Lines.Add("----End of exception message----\r\n");
            Lines.Add("----Stack trace----");
            Lines.Add(e.StackTrace);
            Lines.Add("----End of stack trace----\r\n");
            File.WriteAllLines(fullPath + date2 + ".txt", Lines.ToArray());
        }
        public static void LoadServer(bool KnowConfig)
        {
            Time32 Start = Time32.Now;
            RandomSeed = Convert.ToInt32(DateTime.Now.Ticks.ToString().Remove(DateTime.Now.Ticks.ToString().Length / 2));
            Console.Title = "Conquord Is Loading...";
            Kernel.Random = new FastRandom(RandomSeed);
            if (!KnowConfig)
            {
                ConquordDBName = "test";
                ConquordDBPass = "123456";
                ConquordIP = "149.202.128.35";

            }
            System.Console.ForegroundColor = ConsoleColor.DarkGray;
            IntPtr hWnd = FindWindow(null, Console.Title);
            System.Console.WriteLine(@"      `       ______                                       __   `     `      ");
            System.Console.WriteLine(@"             / ____/___  ____  ____ ___  ______  _________/ /                ");
            System.Console.WriteLine(@"        `   / /   / __ \/ __ \/ __ `/ / / / __ \/ ___/ __  /  `      `    _  ");
            System.Console.WriteLine(@"          _/ /___/ /_/ / / / / /_/ / /_/ / /_/ / /  / /_/ /              | | ");
            System.Console.WriteLine(@"   `       \____/\____/_/ /_/\__, /\__,_/\____/_/   \__,_/        `   ___| | ");
            System.Console.WriteLine(@"         `                     /_/            `               `      (    .' ");
            System.Console.WriteLine(@" __        ...       _____ Michael Nashaat _____        ...       __  )  (   ");
            System.Console.WriteLine();
            System.Console.WriteLine(@"                  Copyright (c) Conquord Project 2015-2016.                  ");
            Console.BackgroundColor = ConsoleColor.Black;
            System.Console.ForegroundColor = ConsoleColor.DarkGray;
            Database.DataHolder.CreateConnection(ConquordDBName, ConquordDBPass);
            Database.EntityTable.EntityUID = new Counter(0);
            new MySqlCommand(MySqlCommandType.UPDATE).Update("entities").Set("Online", 0).Execute();
            using (MySqlCommand cmd = new MySqlCommand(MySqlCommandType.SELECT))
            {
                cmd.Select("configuration");
                using (MySqlReader r = new MySqlReader(cmd))
                {
                    if (r.Read())
                    {
                        if (!KnowConfig)
                        {
                            ConquordIP = r.ReadString("ConquordIP");
                            GamePort = 5816;
                            AuthPort = r.ReadUInt16("ConquordPort");
                        }
                        Database.EntityTable.EntityUID = new Counter(r.ReadUInt32("EntityID"));
                        if (Database.EntityTable.EntityUID.Now == 0)
                            Database.EntityTable.EntityUID.Now = 1;
                        Union.UnionCounter = new Counter(r.ReadUInt32("UnionID"));
                        Kernel.ServerKingdom = (r.ReadUInt32("ServerKingdom"));
                        if (r.ReadByte("LastDailySignReset") != DateTime.Now.Month) MsgSignIn.Reset();
                        Game.ConquerStructures.Society.Guild.GuildCounter = new Conquord.Counter(r.ReadUInt32("GuildID"));
                        Network.GamePackets.ConquerItem.ItemUID = new Conquord.Counter(r.ReadUInt32("ItemUID"));
                        Constants.ExtraExperienceRate = r.ReadUInt32("ExperienceRate");
                        Constants.ExtraSpellRate = r.ReadUInt32("SpellExperienceRate");
                        Constants.ExtraProficiencyRate = r.ReadUInt32("ProficiencyExperienceRate");
                        Constants.MoneyDropRate = r.ReadUInt32("MoneyDropRate");
                        Constants.ConquerPointsDropRate = r.ReadUInt32("ConquerPointsDropRate");
                        Constants.ItemDropRate = r.ReadUInt32("ItemDropRate");
                        Constants.ItemDropQualityRates = r.ReadString("ItemDropQualityString").Split('~');
                        Database.EntityVariableTable.Load(0, out Vars);
                    }
                }
            }
            using (var cmd = new MySqlCommand(MySqlCommandType.UPDATE).Update("configuration"))
                cmd.Set("LastDailySignReset", DateTime.Now.Month).Execute();
            Database.JiangHu.LoadStatus();
            Database.JiangHu.LoadJiangHu();
            Console.WriteLine("JiangHu Loaded.");
            Way2Heroes.Load();
            QuestInfo.Load();
            AuctionBase.Load();
            Database.DataHolder.ReadStats();
            Conquord.Soul.SoulProtection.Load();
            Database.PerfectionTable.Load();
            Console.WriteLine("Perfection Loaded.");
            Database.LotteryTable.Load();
            Database.ConquerItemTable.ClearNulledItems();
            Database.ConquerItemInformation.Load();
            Console.WriteLine("Items Loaded.");
            Database.GameUpdetess.LoadRates();
            Database.MonsterInformation.Load();
            Database.IPBan.Load();
            Database.SpellTable.Load();
            Database.ShopFile.Load();
            Database.HonorShop.Load();
            Database.RacePointShop.Load();
            Database.ChampionShop.Load();
            Database.EShopFile.Load();
            Database.EShopV2File.Load();
            Console.WriteLine("Shops Loaded.");
            Database.MapsTable.Load();
            Database.Flowers.LoadFlowers();
            Database.Flowers.SaveFlowers();
            Console.WriteLine("Flowers Systems Loaded.");
            Database.NobilityTable.Load();
            Database.ArenaTable.Load();
            Database.TeamArenaTable.Load();
            Database.GuildTable.Load();
            Database.ChiTable.LoadAllChi();
            Console.WriteLine("Social Systems Loaded.");
            Refinery.LoadItems();
            StorageManager.Load();
            UnionTable.Load();
            Console.WriteLine("Union Loaded.");
            World = new World();
            World.Init();
            Database.Statue.Load();
            Console.WriteLine("Tops And Quests Loaded.");
            Database.PoketTables.LoadTables();
            Database.InnerPowerTable.LoadDBInformation();
            Database.InnerPowerTable.Load();
            Console.WriteLine("InnerPower Loaded.");
            Map.CreateTimerFactories();
            Database.SignInTable.Load();
            Database.DMaps.Load();
            Console.WriteLine("Maps Loaded.");
            Game.Screen.CreateTimerFactories();
            World.CreateTournaments();
            Game.GuildWar.Initiate();
            Game.ClanWar.Initiate();
            Game.Tournaments.SkillTournament.LoadSkillTop8();
            Game.Tournaments.TeamTournament.LoadTeamTop8();
            Clan.LoadClans();
            Game.EliteGuildWar.EliteGwint();
            Console.WriteLine("Guilds and Clans loaded.");
            Booths.Load();
            Console.WriteLine("Booths Loaded.");
            
            Database.FloorItemTable.Load();
            Database.ReincarnationTable.Load();
            new MsgUserAbilityScore().GetRankingList();
            new MsgEquipRefineRank().UpdateRanking();
            PrestigeRank.LoadRanking();
            Console.WriteLine("Ranks Loaded.");
            BruteForceProtection.CreatePoll();
            Console.WriteLine("Protection System On.");
            {
                Client.GameClient gc = new Client.GameClient(new ClientWrapper());
                gc.Account = new AccountTable("NONE");
                gc.Socket.Alive = false;
                gc.Entity = new Entity(EntityFlag.Player, false) { Name = "NONE" };
                Npcs.GetDialog(new NpcRequest(), gc, true);
            }
            #region OpenSocket
            Network.Cryptography.AuthCryptography.PrepareAuthCryptography();
            AuthServer = new ServerSocket();
            AuthServer.OnClientConnect += AuthServer_OnClientConnect;
            AuthServer.OnClientReceive += AuthServer_OnClientReceive;
            AuthServer.OnClientDisconnect += AuthServer_OnClientDisconnect;
            AuthServer.Enable(AuthPort, "0.0.0.0");
            GameServer = new ServerSocket();
            GameServer.OnClientConnect += GameServer_OnClientConnect;
            GameServer.OnClientReceive += GameServer_OnClientReceive;
            GameServer.OnClientDisconnect += GameServer_OnClientDisconnect;
            GameServer.Enable(GamePort, "0.0.0.0");
            #endregion
            Console.WriteLine("Server loaded iN : " + (Time32.Now - Start) + " MilliSeconds.");
            ConquordHandler += ConquordConsole_CloseEvent;
            Native.SetConsoleCtrlHandler(ConquordHandler, true);
            GC.Collect();
            WorkConsole();
        }
        static void AuthServer_OnClientDisconnect(ClientWrapper obj)
        {
            obj.Disconnect();
        }
        static void AuthServer_OnClientConnect(ClientWrapper obj)
        {
            Client.AuthClient authState;
            obj.Connector = (authState = new Client.AuthClient(obj));
            authState.Cryptographer = new Network.Cryptography.AuthCryptography();
            Network.AuthPackets.PasswordCryptographySeed pcs = new PasswordCryptographySeed();
            pcs.Seed = Kernel.Random.Next();
            authState.PasswordSeed = pcs.Seed;
            authState.Send(pcs);
        }
        public static ServerSocket AuthServer;
        public static bool CpuUsageTimer = true;
        public static MemoryCompressor MCompressor = new MemoryCompressor();
        public static int CpuUse = 0;
        public static ServerSocket GameServer;
        public static string ConquordIP;
        public static ushort GamePort;
        public static string ConquordDBName;
        public static string ConquordDBPass;
        public static ushort AuthPort;
        public static World World;
        public static VariableVault Vars;
        public static int MaxOn = 0;
        public static int RandomSeed = 0;
        public static uint//KitMerchant's 
         Weapon, KitA, KitB = 100000;

        static void Main()
        {
            LoadServer(false);
        }
        public class Conquord_EliteGWTimes
        {
            public static DateTime now
            {
                get
                {
                    return DateTime.Now;
                }
            }

            public class Start
            {
                public static bool EliteGW
                {
                    get
                    {
                        return (now.Hour == 18 && now.Minute == 0);
                    }
                }
                
            }

            public class End
            {
                
                public static bool EliteGW
                {
                    get
                    {
                        return now.Hour == 19;
                    }
                }
                

            }

        }
        private static void WorkConsole()
        {
            while (true)
            {
                try
                {
                    CommandsAI(Console.ReadLine());
                }
                catch (Exception e) { Console.WriteLine(e); }
            }
        }
        public static void CommandsAI(string command) 
        { 
            try 
            { 
                if (command == null) 
                    return; 
                string[] data = command.Split(' '); 
                switch (data[0]) 
                { 
                    case "@clear": 
                        { 
                            System.Console.Clear(); 
                            Console.WriteLine("Consle And Program Cleared !!"); 
                            break; 
                        }
                    case "@Conquord":
                    case "@Eslam":
                        Console.WriteLine("Server will restart after 10 minutes.");
                        foreach (var client in Kernel.GamePool.Values)
                        {
                            client.Send(new Conquord.Network.GamePackets.Message("The server will be brought down for maintenance in 5 minute, Please exit the game now.", System.Drawing.Color.Red, Conquord.Network.GamePackets.Message.System));
                            System.Threading.Thread.Sleep(0x7530);
                            client.Send(new Conquord.Network.GamePackets.Message("The server will be brought down for maintenance in 4 minute 30 second, Please exit the game now.", System.Drawing.Color.Red, Conquord.Network.GamePackets.Message.System));
                            System.Threading.Thread.Sleep(0x7530);
                            client.Send(new Conquord.Network.GamePackets.Message("The server will be brought down for maintenance in 4 minute, Please exit the game now.", System.Drawing.Color.Red, Conquord.Network.GamePackets.Message.System));
                            System.Threading.Thread.Sleep(0x7530);
                            client.Send(new Conquord.Network.GamePackets.Message("The server will be brought down for maintenance in 3 minute 30 second, Please exit the game now.", System.Drawing.Color.Red, Conquord.Network.GamePackets.Message.System));
                            System.Threading.Thread.Sleep(0x7530);
                            client.Send(new Conquord.Network.GamePackets.Message("The server will be brought down for maintenance in 3 minute, Please exit the game now.", System.Drawing.Color.Red, Conquord.Network.GamePackets.Message.System));
                            System.Threading.Thread.Sleep(0x7530);
                            client.Send(new Conquord.Network.GamePackets.Message("The server will be brought down for maintenance in 2 minute 30 second, Please exit the game now.", System.Drawing.Color.Red, Conquord.Network.GamePackets.Message.System));
                            System.Threading.Thread.Sleep(0x7530);
                            client.Send(new Conquord.Network.GamePackets.Message("The server will be brought down for maintenance in 2 minute, Please exit the game now.", System.Drawing.Color.Red, Conquord.Network.GamePackets.Message.System));
                            System.Threading.Thread.Sleep(0x7530);
                            client.Send(new Conquord.Network.GamePackets.Message("The server will be brought down for maintenance in 1 minute 30 second, Please exit the game now.", System.Drawing.Color.Red, Conquord.Network.GamePackets.Message.System));
                            System.Threading.Thread.Sleep(0x7530);
                            client.Send(new Conquord.Network.GamePackets.Message("The server will be brought down for maintenance in 1 minute, Please exit the game now.", System.Drawing.Color.Red, Conquord.Network.GamePackets.Message.System));
                            System.Threading.Thread.Sleep(0x7530);
                            client.Send(new Conquord.Network.GamePackets.Message("The server will be brought down for maintenance in 30 second, Please exit the game now.", System.Drawing.Color.Red, Conquord.Network.GamePackets.Message.System));
                            Console.WriteLine("Server will exit after 1 minute.");
                            CommandsAI("@save");
                            System.Threading.Thread.Sleep(0x7530);
                            client.Send(new Conquord.Network.GamePackets.Message("The Server restarted, Please log in after 2 minutes! ", System.Drawing.Color.Red, Conquord.Network.GamePackets.Message.System));
                        }
                        try
                        {
                            CommandsAI("@restart");
                        }
                        catch
                        {
                            Console.WriteLine("Server Cannot Exit .");
                        }
                        break;  
                        try 
                        { 
                            CommandsAI("@restart"); 
                        } 
                        catch 
                        { 
                            Console.WriteLine("Server Cannot Exit"); 
                        } 
                        break; 
                    case "@flushbans": 
                        { 
                            Database.IPBan.Load(); 
                            break; 
                        }
                    case "@SNX":
                        {
                            ShaDow.ShaDowNpcControl ShaDow = new ShaDow.ShaDowNpcControl();
                            ShaDow.ShowDialog();
                            break;
                        }
                    case "@online": 
                        { 
                            Console.WriteLine("Online Players Count : " + Kernel.GamePool.Count); 
                            string line = ""; 
                            foreach (Client.GameClient pClient in Program.Values) 
                                line += pClient.Entity.Name + ","; 
                            if (line != "") 
                            { 
                                line = line.Remove(line.Length - 1); 
                                Console.WriteLine("Players : " + line); 
                            } 
                            break; 
                        } 
                    case "@memoryusage": 
                        { 
                            var proc = System.Diagnostics.Process.GetCurrentProcess(); 
                            Console.WriteLine("Thread count: " + proc.Threads.Count); 
                            Console.WriteLine("Memory set(MB): " + ((double)((double)proc.WorkingSet64 / 1024)) / 1024); 
                            proc.Close(); 
                            break; 
                        } 
                    case "@save": 
                        { 
                            Save(); 
                        } 
                        break; 
                    case "@skill": 
                        { 
                            Game.Features.Tournaments.TeamElitePk.SkillTeamTournament.Open(); 
                            foreach (var clien in Kernel.GamePool.Values) 
                            { 
                                if (clien.Team == null) 
                                    clien.Team = new Game.ConquerStructures.Team(clien); 
                                Game.Features.Tournaments.TeamElitePk.SkillTeamTournament.Join(clien, 3); 
                            } 
                            break; 
                        } 
                    case "@team": 
                        { 
                            Game.Features.Tournaments.TeamElitePk.TeamTournament.Open(); 
                            foreach (var clien in Kernel.GamePool.Values) 
                            { 
                                if (clien.Team == null) 
                                    clien.Team = new Game.ConquerStructures.Team(clien); 
                                Game.Features.Tournaments.TeamElitePk.TeamTournament.Join(clien, 3); 
                            } 
                            break; 
                        } 
                    case "@exit": 
                        { 
                            GameServer.Disable(); 
                                AuthServer.Disable(); 
                            Save(); 
                            Database.EntityVariableTable.Save(0, Vars); 

                            var WC = Program.Values.ToArray(); 
                            Parallel.ForEach(Program.Values, client => 
                            { 
                                client.Send("Server Will Exit For 5 Min to Fix Some Bugs, Please Be Paitent !"); 
                                client.Disconnect(); 
                            }); 

                            Kernel.SendWorldMessage(new Network.GamePackets.Message(string.Concat(new object[] { "Server Will Exit For 5 Min to Fix Some Bugs, Please Be Paitent" }), System.Drawing.Color.Black, 0x7db), Program.Values); 
                            CommandsAI("@save"); 

                            if (GuildWar.IsWar) 
                                GuildWar.End(); 
                            Save(); 
                            Environment.Exit(0); 
                        } 
                        break; 
                    case "@pressure": 
                        { 
                            Console.WriteLine("Genr: " + World.GenericThreadPool.ToString()); 
                            Console.WriteLine("Send: " + World.SendPool.ToString()); 
                            Console.WriteLine("Recv: " + World.ReceivePool.ToString()); 
                            break; 
                        } 
                    case "@restart": 
                        { 
                            try 
                            {
                                Kernel.SendWorldMessage(new Network.GamePackets.Message(string.Concat(new object[] { "Server Will Be Restart Now !" }), System.Drawing.Color.Black, 0x7db), Program.Values); 
                                CommandsAI("@save"); 

                                Save(); 

                                var WC = Program.Values.ToArray(); 
                                foreach (Client.GameClient client in WC) 
                                { 
                                    client.Send("Server Will Restart !"); 
                                    client.Disconnect(); 
                                } 
                                GameServer.Disable(); 
                                    AuthServer.Disable(); 
                                if (GuildWar.IsWar) 
                                    GuildWar.End(); 
                                Save(); 

                                Application.Restart(); 
                                Environment.Exit(0); 
                            } 
                            catch (Exception e) 
                            { 
                                Console.WriteLine(e); 
                                Console.ReadLine(); 
                            } 
                        } 
                        break; 
                    case "@account": 
                        { 
                            Database.AccountTable account = new AccountTable(data[1]); 
                            account.Password = data[2]; 
                            account.State = AccountTable.AccountState.Player; 
                            account.Save(); 
                        } 
                        break; 
                } 
            } 
            catch (Exception e) 
            { 
                Console.WriteLine(e.ToString()); 
            } 
        }
        public static void AddWarLog(string War, string CPs, string name)
        {
            String folderN = DateTime.Now.Year + "-" + DateTime.Now.Month,
                    Path = "gmlogs\\Warlogs\\",
                    NewPath = System.IO.Path.Combine(Path, folderN);
            if (!File.Exists(NewPath + folderN))
            {
                System.IO.Directory.CreateDirectory(System.IO.Path.Combine(Path, folderN));
            }
            if (!File.Exists(NewPath + "\\" + DateTime.Now.Day + ".txt"))
            {
                using (System.IO.FileStream fs = System.IO.File.Create(NewPath + "\\" + DateTime.Now.Day + ".txt"))
                {
                    fs.Close();
                }
            }

            using (System.IO.StreamWriter file = new System.IO.StreamWriter(NewPath + "\\" + DateTime.Now.Day + ".txt", true))
            {
                file.WriteLine(name + " got " + CPs + " CPs from the [" + War + "] as prize at " + DateTime.Now.Hour + ":" + DateTime.Now.Minute + ":" + DateTime.Now.Second);
            }
        }

        public static bool ALEXPC { get; set; }
    }
}