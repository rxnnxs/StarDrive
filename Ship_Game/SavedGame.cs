using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Ship_Game.Gameplay;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Xml.Serialization;
using System.Globalization;
using System.Configuration;
using Newtonsoft.Json;

namespace Ship_Game
{
    public class SerializeAttribute : Attribute
    {
        public int Id { get; set; } = -1;

        public SerializeAttribute()
        {
        }
        public SerializeAttribute(int id)
        {
            Id = id;
        }
    }

	public sealed class SavedGame
	{
        public static bool NewFormat = true; // use new save format ?
        public const string NewExt = ".sav";
        public const string OldExt = ".xml";
        public const string NewZipExt = ".sav.gz";
        public const string OldZipExt = ".xml.gz";

        private readonly UniverseSaveData SaveData = new UniverseSaveData();
		private static Thread SaveThread;

        public static bool IsSaving  => SaveThread != null && SaveThread.IsAlive;
        public static bool NotSaving => SaveThread == null || !SaveThread.IsAlive;

		public SavedGame(UniverseScreen screenToSave, string saveAs)
		{
		    SaveData.RemnantKills        = GlobalStats.RemnantKills;
            SaveData.RemnantActivation   = GlobalStats.RemnantActivation;
            SaveData.RemnantArmageddon   = GlobalStats.RemnantArmageddon;
			SaveData.gameDifficulty      = screenToSave.GameDifficulty;
			SaveData.AutoColonize        = EmpireManager.Player.AutoColonize;
			SaveData.AutoExplore         = EmpireManager.Player.AutoExplore;
			SaveData.AutoFreighters      = EmpireManager.Player.AutoFreighters;
			SaveData.AutoProjectors      = EmpireManager.Player.AutoBuild;
			SaveData.GamePacing          = UniverseScreen.GamePaceStatic;
			SaveData.GameScale           = UniverseScreen.GameScaleStatic;
			SaveData.StarDate            = screenToSave.StarDate;
			SaveData.FTLModifier         = screenToSave.FTLModifier;
            SaveData.EnemyFTLModifier    = screenToSave.EnemyFTLModifier;
			SaveData.GravityWells        = screenToSave.GravityWells;
			SaveData.PlayerLoyalty       = screenToSave.PlayerLoyalty;
			SaveData.RandomEvent         = RandomEventManager.ActiveEvent;
			SaveData.campos              = new Vector2(screenToSave.camPos.X, screenToSave.camPos.Y);
			SaveData.camheight           = screenToSave.camHeight;
            SaveData.MinimumWarpRange    = GlobalStats.MinimumWarpRange;
            SaveData.TurnTimer           = (byte)GlobalStats.TurnTimer;
            SaveData.IconSize            = GlobalStats.IconSize;
            SaveData.preventFederations  = GlobalStats.PreventFederations;
            SaveData.GravityWellRange    = GlobalStats.GravityWellRange;
            SaveData.EliminationMode     = GlobalStats.EliminationMode;
			SaveData.EmpireDataList      = new List<EmpireSaveData>();
			SaveData.SolarSystemDataList = new List<SolarSystemSaveData>();
            SaveData.OptionIncreaseShipMaintenance = GlobalStats.ShipMaintenanceMulti;
            

			foreach (SolarSystem system in UniverseScreen.SolarSystemList)
			{
				SolarSystemSaveData sysSave = new SolarSystemSaveData
				{
					Name = system.Name,
					Position = system.Position,
					SunPath = system.SunPath,
					AsteroidsList = new List<Asteroid>(),
                    Moons = new List<Moon>(),
				};
				foreach (Asteroid roid in system.AsteroidsList)
				{
					sysSave.AsteroidsList.Add(roid);
				}
                foreach (Moon moon in system.MoonList)
                    sysSave.Moons.Add(moon);
				sysSave.guid = system.guid;
				sysSave.RingList = new List<RingSave>();
				foreach (SolarSystem.Ring ring in system.RingList)
				{
					RingSave rsave = new RingSave
					{
						Asteroids = ring.Asteroids,
						OrbitalDistance = ring.Distance
					};
					if (ring.planet == null)
					{
						sysSave.RingList.Add(rsave);
					}
					else
					{
						PlanetSaveData pdata = new PlanetSaveData
						{
							Crippled_Turns       = ring.planet.Crippled_Turns,
							guid                 = ring.planet.guid,
							FoodState            = ring.planet.fs,
							ProdState            = ring.planet.ps,
							FoodLock             = ring.planet.FoodLocked,
							ProdLock             = ring.planet.ProdLocked,
							ResLock              = ring.planet.ResLocked,
							Name                 = ring.planet.Name,
                            Scale                = ring.planet.scale,
							ShieldStrength       = ring.planet.ShieldStrengthCurrent,
							Population           = ring.planet.Population,
							PopulationMax        = ring.planet.MaxPopulation,
							Fertility            = ring.planet.Fertility,
							Richness             = ring.planet.MineralRichness,
							Owner                = ring.planet.Owner?.data.Traits.Name ?? "",
							WhichPlanet          = ring.planet.planetType,
							OrbitalAngle         = ring.planet.OrbitalAngle,
							OrbitalDistance      = ring.planet.OrbitalRadius,
							HasRings             = ring.planet.hasRings,
							Radius               = ring.planet.ObjectRadius,
							farmerPercentage     = ring.planet.FarmerPercentage,
							workerPercentage     = ring.planet.WorkerPercentage,
							researcherPercentage = ring.planet.ResearcherPercentage,
							foodHere             = ring.planet.FoodHere,
							TerraformPoints      = ring.planet.TerraformPoints,
							prodHere             = ring.planet.ProductionHere,
							GovernorOn           = ring.planet.GovernorOn,
							ColonyType           = ring.planet.colonyType,
							StationsList         = new List<Guid>(),
                            SpecialDescription = ring.planet.SpecialDescription
						};
						foreach (var station in ring.planet.Shipyards)
						{
							if (station.Value.Active) pdata.StationsList.Add(station.Key);
						}
						pdata.QISaveList = new List<SavedGame.QueueItemSave>();
						if (ring.planet.Owner != null)
						{
							foreach (QueueItem item in ring.planet.ConstructionQueue)
							{
								QueueItemSave qi = new QueueItemSave()
								{
									isBuilding = item.isBuilding,
									IsRefit = item.isRefit
								};
								if (qi.IsRefit)
								{
									qi.RefitCost = item.Cost;
								}
								if (qi.isBuilding)
								{
									qi.UID = item.Building.Name;
								}
								qi.isShip = item.isShip;
								qi.DisplayName = item.DisplayName;
								if (qi.isShip)
								{
									qi.UID = item.sData.Name;
								}
								qi.isTroop = item.isTroop;
								if (qi.isTroop)
								{
									qi.UID = item.troop.Name;
								}
								qi.ProgressTowards = item.productionTowards;
								if (item.Goal != null)
								{
									qi.GoalGUID = item.Goal.guid;
								}
								if (item.pgs != null)
								{
									qi.pgsVector = new Vector2(item.pgs.x, item.pgs.y);
								}
                                qi.isPlayerAdded = item.IsPlayerAdded;
								pdata.QISaveList.Add(qi);
							}
						}
						pdata.PGSList = new List<SavedGame.PGSData>();
						foreach (PlanetGridSquare tile in ring.planet.TilesList)
						{
						    PGSData pgs = new PGSData
						    {
						        x          = tile.x,
						        y          = tile.y,
						        resbonus   = tile.resbonus,
						        prodbonus  = tile.prodbonus,
						        Habitable  = tile.Habitable,
						        foodbonus  = tile.foodbonus,
						        Biosphere  = tile.Biosphere,
						        building   = tile.building,
						        TroopsHere = tile.TroopsHere
						    };
						    pdata.PGSList.Add(pgs);
						}
						pdata.EmpiresThatKnowThisPlanet = new List<string>();
						foreach (var explored in system.ExploredDict)
						{
							if (explored.Value)
							    pdata.EmpiresThatKnowThisPlanet.Add(explored.Key.data.Traits.Name);
						}
						rsave.Planet = pdata;
						sysSave.RingList.Add(rsave);
					}
					sysSave.EmpiresThatKnowThisSystem = new List<string>();
					foreach (var explored in system.ExploredDict)
					{
						if (explored.Value)
						    sysSave.EmpiresThatKnowThisSystem.Add(explored.Key.data.Traits.Name); // @todo This is a duplicate??
					}
				}
				SaveData.SolarSystemDataList.Add(sysSave);
			}
			
            foreach (Empire e in EmpireManager.Empires)
			{
				EmpireSaveData empireToSave = new EmpireSaveData
				{
					IsFaction   = e.isFaction,
                    isMinorRace = e.MinorRace,
					Relations   = new List<Relationship>()
				};
				foreach (KeyValuePair<Empire, Relationship> relation in e.AllRelations)
				{
					empireToSave.Relations.Add(relation.Value);
				}
				empireToSave.Name                 = e.data.Traits.Name;
				empireToSave.empireData           = e.data.GetClone();
				empireToSave.Traits               = e.data.Traits;
				empireToSave.Research             = e.Research;
				empireToSave.ResearchTopic        = e.ResearchTopic;
				empireToSave.Money                = e.Money;
                empireToSave.CurrentAutoScout     = e.data.CurrentAutoScout;
                empireToSave.CurrentAutoFreighter = e.data.CurrentAutoFreighter;
                empireToSave.CurrentAutoColony    = e.data.CurrentAutoColony;
                empireToSave.CurrentConstructor   = e.data.CurrentConstructor;
				empireToSave.OwnedShips           = new List<ShipSaveData>();
				empireToSave.TechTree             = new List<TechEntry>();
				foreach (AO area in e.GetGSAI().AreasOfOperations)
				{
					area.PrepareForSave();
				}
				empireToSave.AOs = e.GetGSAI().AreasOfOperations;
				empireToSave.FleetsList = new List<FleetSave>();
				foreach (KeyValuePair<int, Fleet> fleet in e.GetFleetsDict())
				{
					FleetSave fs = new FleetSave()
					{
						Name        = fleet.Value.Name,
						IsCoreFleet = fleet.Value.IsCoreFleet,
						TaskStep    = fleet.Value.TaskStep,
						Key         = fleet.Key,
						facing      = fleet.Value.facing,
						FleetGuid   = fleet.Value.guid,
						Position    = fleet.Value.Position,
						ShipsInFleet = new List<FleetShipSave>()
					};
					foreach (FleetDataNode node in fleet.Value.DataNodes)
					{
						if (node.Ship== null)
						{
							continue;
						}
						node.ShipGuid = node.Ship.guid;
					}
					fs.DataNodes = fleet.Value.DataNodes;
					foreach (Ship ship in fleet.Value.Ships)
					{
						FleetShipSave ssave = new FleetShipSave()
						{
							fleetOffset = ship.RelativeFleetOffset,
							shipGuid = ship.guid
						};
						fs.ShipsInFleet.Add(ssave);
					}
					empireToSave.FleetsList.Add(fs);
				}
				empireToSave.SpaceRoadData = new List<SpaceRoadSave>();
				foreach (SpaceRoad road in e.SpaceRoadsList)
				{
					SpaceRoadSave rdata = new SpaceRoadSave()
					{
						OriginGUID = road.GetOrigin().guid,
						DestGUID = road.GetDestination().guid,
						RoadNodes = new List<RoadNodeSave>()
					};
					foreach (RoadNode node in road.RoadNodesList)
					{
						RoadNodeSave ndata = new RoadNodeSave()
						{
							Position = node.Position
						};
						if (node.Platform != null)
						{
							ndata.Guid_Platform = node.Platform.guid;
						}
						rdata.RoadNodes.Add(ndata);
					}
					empireToSave.SpaceRoadData.Add(rdata);
				}
				GSAISAVE gsaidata = new GSAISAVE()
				{
					UsedFleets = e.GetGSAI().UsedFleets,
					Goals      = new List<GoalSave>(),
					PinGuids   = new List<Guid>(),
					PinList    = new List<ThreatMatrix.Pin>()
				};
				foreach (KeyValuePair<Guid, ThreatMatrix.Pin> guid in e.GetGSAI().ThreatMatrix.Pins)
				{
                    gsaidata.PinGuids.Add(guid.Key);
					gsaidata.PinList.Add(guid.Value);
				}
				gsaidata.MilitaryTaskList = new List<MilitaryTask>();
				foreach (MilitaryTask task in e.GetGSAI().TaskList)
				{
					gsaidata.MilitaryTaskList.Add(task);
					if (task.GetTargetPlanet() == null)
					{
						continue;
					}
					task.TargetPlanetGuid = task.GetTargetPlanet().guid;
				}
				foreach (Goal g in e.GetGSAI().Goals)
				{
				    GoalSave gdata = new GoalSave()
				    {
				        BuildPosition = g.BuildPosition
				    };
				    if (g.GetColonyShip() != null)
				    {
				        gdata.colonyShipGuid = g.GetColonyShip().guid;
				    }
				    gdata.GoalStep = g.Step;
				    if (g.GetMarkedPlanet() != null)
				    {
				        gdata.markedPlanetGuid = g.GetMarkedPlanet().guid;
				    }
				    gdata.ToBuildUID = g.ToBuildUID;
				    gdata.type = g.type;
				    if (g.GetPlanetWhereBuilding() != null)
				    {
				        gdata.planetWhereBuildingAtGuid = g.GetPlanetWhereBuilding().guid;
				    }
				    if (g.GetFleet() != null)
				    {
				        gdata.fleetGuid = g.GetFleet().guid;
				    }
				    gdata.GoalGuid = g.guid;
				    gdata.GoalName = g.GoalName;
				    if (g.beingBuilt != null)
				    {
				        gdata.beingBuiltGUID = g.beingBuilt.guid;
				    }
				    gsaidata.Goals.Add(gdata);
				}
				empireToSave.GSAIData = gsaidata;
				foreach (KeyValuePair<string, TechEntry> tech in e.GetTDict())
				{
					empireToSave.TechTree.Add(tech.Value);
				}

                foreach (Ship ship in e.GetShips())
				{
					ShipSaveData sdata = new ShipSaveData()
					{
						guid       = ship.guid,
						data       = ship.ToShipData(),
						Position   = ship.Position,
						experience = ship.experience,
						kills      = ship.kills,
						Velocity   = ship.Velocity,
                        
					};
					if (ship.GetTether() != null)
					{
						sdata.TetheredTo = ship.GetTether().guid;
						sdata.TetherOffset = ship.TetherOffset;
					}
					sdata.Name = ship.Name;
                    sdata.VanityName = ship.VanityName;
					if (ship.PlayerShip)
					{
						sdata.IsPlayerShip = true;
					}
					sdata.Hull          = ship.GetShipData().Hull;
					sdata.Power         = ship.PowerCurrent;
					sdata.Ordnance      = ship.Ordinance;
					sdata.yRotation     = ship.yRotation;
					sdata.Rotation      = ship.Rotation;
					sdata.InCombatTimer = ship.InCombatTimer;
					if (ship.GetCargo().ContainsKey("Food"))
					{
						sdata.FoodCount = ship.GetCargo()["Food"];
					}
					if (ship.GetCargo().ContainsKey("Production"))
					{
						sdata.ProdCount = ship.GetCargo()["Production"];
					}
					if (ship.GetCargo().ContainsKey("Colonists_1000"))
					{
						sdata.PopCount = ship.GetCargo()["Colonists_1000"];
					}
					sdata.TroopList = ship.TroopList;

                    sdata.AreaOfOperation = ship.AreaOfOperation;
               
					sdata.AISave = new ShipAISave()
					{
						FoodOrProd = ship.GetAI().FoodOrProd,
						state      = ship.GetAI().State
					};
					if (ship.GetAI().Target != null && ship.GetAI().Target is Ship)
					{
						sdata.AISave.AttackTarget = (ship.GetAI().Target as Ship).guid;
					}
					sdata.AISave.defaultstate = ship.GetAI().DefaultAIState;
					if (ship.GetAI().start != null)
					{
						sdata.AISave.startGuid = ship.GetAI().start.guid;
					}
					if (ship.GetAI().end != null)
					{
						sdata.AISave.endGuid = ship.GetAI().end.guid;
					}
					sdata.AISave.GoToStep = ship.GetAI().GotoStep;
					sdata.AISave.MovePosition = ship.GetAI().MovePosition;
					sdata.AISave.ActiveWayPoints = new List<Vector2>();
					foreach (Vector2 waypoint in ship.GetAI().ActiveWayPoints)
					{
						sdata.AISave.ActiveWayPoints.Add(waypoint);
					}
					sdata.AISave.ShipGoalsList = new List<ShipGoalSave>();
					foreach (ArtificialIntelligence.ShipGoal sgoal in ship.GetAI().OrderQueue)
					{
						ShipGoalSave gsave = new ShipGoalSave()
						{
							DesiredFacing = sgoal.DesiredFacing
						};
						if (sgoal.fleet != null)
						{
							gsave.fleetGuid = sgoal.fleet.guid;
						}
						gsave.FacingVector = sgoal.FacingVector;
						if (sgoal.goal != null)
						{
							gsave.goalGuid = sgoal.goal.guid;
						}
						gsave.MovePosition = sgoal.MovePosition;
						gsave.Plan = sgoal.Plan;
						if (sgoal.TargetPlanet != null)
						{
							gsave.TargetPlanetGuid = sgoal.TargetPlanet.guid;
						}
						gsave.VariableString = sgoal.VariableString;
						gsave.SpeedLimit = sgoal.SpeedLimit;
						sdata.AISave.ShipGoalsList.Add(gsave);
					}
					if (ship.GetAI().OrbitTarget != null)
					{
						sdata.AISave.OrbitTarget = ship.GetAI().OrbitTarget.guid;
					}
					if (ship.GetAI().ColonizeTarget != null)
					{
						sdata.AISave.ColonizeTarget = ship.GetAI().ColonizeTarget.guid;
					}
					if (ship.GetAI().SystemToDefend != null)
					{
						sdata.AISave.SystemToDefend = ship.GetAI().SystemToDefend.guid;
					}
					if (ship.GetAI().EscortTarget != null)
					{
						sdata.AISave.EscortTarget = ship.GetAI().EscortTarget.guid;
					}
					sdata.Projectiles = new List<ProjectileSaveData>();
					foreach (Projectile p in ship.Projectiles)
					{
					    ProjectileSaveData pdata = new ProjectileSaveData()
					    {
					        Velocity = p.Velocity,
					        Rotation = p.Rotation,
					        Weapon   = p.weapon.UID,
					        Position = p.Center,
					        Duration = p.duration
					    };
					    sdata.Projectiles.Add(pdata);
					}
					empireToSave.OwnedShips.Add(sdata);
				}

                foreach (Ship ship in e.GetProjectors())  //fbedard
                {
                    ShipSaveData sdata = new ShipSaveData()
                    {
                        guid       = ship.guid,
                        data       = ship.ToShipData(),
                        Position   = ship.Position,
                        experience = ship.experience,
                        kills      = ship.kills,
                        Velocity   = ship.Velocity,

                    };
                    if (ship.GetTether() != null)
                    {
                        sdata.TetheredTo = ship.GetTether().guid;
                        sdata.TetherOffset = ship.TetherOffset;
                    }
                    sdata.Name = ship.Name;
                    sdata.VanityName = ship.VanityName;
                    if (ship.PlayerShip)
                    {
                        sdata.IsPlayerShip = true;
                    }
                    sdata.Hull          = ship.GetShipData().Hull;
                    sdata.Power         = ship.PowerCurrent;
                    sdata.Ordnance      = ship.Ordinance;
                    sdata.yRotation     = ship.yRotation;
                    sdata.Rotation      = ship.Rotation;
                    sdata.InCombatTimer = ship.InCombatTimer;
                    sdata.AISave = new ShipAISave
                    {
                        FoodOrProd      = ship.GetAI().FoodOrProd,
                        state           = ship.GetAI().State,
                        defaultstate    = ship.GetAI().DefaultAIState,
                        GoToStep        = ship.GetAI().GotoStep,
                        MovePosition    = ship.GetAI().MovePosition,
                        ActiveWayPoints = new List<Vector2>(),
                        ShipGoalsList   = new List<ShipGoalSave>(),
                    };
                    sdata.Projectiles = new List<ProjectileSaveData>();
                    empireToSave.OwnedShips.Add(sdata);
                }

				SaveData.EmpireDataList.Add(empireToSave);
			}
			SaveData.Snapshots = new SerializableDictionary<string, SerializableDictionary<int, Snapshot>>();
			foreach (var e in StatTracker.SnapshotsDict)
			{
				SaveData.Snapshots.Add(e.Key, e.Value);
			}
			string path = Dir.ApplicationData;
			SaveData.path       = path;
			SaveData.SaveAs     = saveAs;
			SaveData.Size       = screenToSave.Size;
			SaveData.FogMapName = saveAs + "fog";
			screenToSave.FogMap.Save(path + "/StarDrive/Saved Games/Fog Maps/" + saveAs + "fog.png", ImageFileFormat.Png);
		    SaveThread = new Thread(SaveUniverseDataAsync) {Name = "Save Thread: " + saveAs};
		    SaveThread.Start(SaveData);
		}

        private static void SaveUniverseDataAsync(object universeSaveData)
		{
			UniverseSaveData data = (UniverseSaveData)universeSaveData;
            try
            {
                string ext = NewFormat ? NewExt : OldExt;
                FileInfo info = new FileInfo(data.path + "/StarDrive/Saved Games/" + data.SaveAs + ext);
                using (FileStream writeStream = info.OpenWrite())
                {
                    var t = PerfTimer.StartNew();
                    if (NewFormat)
                    {
                        using (var textWriter = new StreamWriter(writeStream))
                        {
                            var ser = new JsonSerializer
                            {
                                NullValueHandling = NullValueHandling.Ignore,
                                DefaultValueHandling = DefaultValueHandling.Ignore,
                            };
                            ser.Serialize(textWriter, data);
                        }
                        Log.Warning("JSON Total Save elapsed: {0}s", t.Elapsed);
                    }
                    else
                    {
                        var ser = new XmlSerializer(typeof(UniverseSaveData));
                        ser.Serialize(writeStream, data);
                        Log.Warning("XML Total Save elapsed: {0}s", t.Elapsed);
                    }
                }
                HelperFunctions.Compress(info);
                info.Delete();
            }
            catch
            {
            }

            DateTime now = DateTime.Now;
			HeaderData header = new HeaderData
			{
				PlayerName = data.PlayerLoyalty,
				StarDate   = data.StarDate.ToString("#.0"),
				Time       = now,
                SaveName   = data.SaveAs,
                RealDate   = now.ToString("M/d/yyyy") + " " + now.ToString("t", CultureInfo.CreateSpecificCulture("en-US").DateTimeFormat),
                ModPath    = GlobalStats.ActiveMod?.ModPath ?? "",
                ModName    = GlobalStats.ActiveMod?.mi.ModName ?? "",
                Version    = Convert.ToInt32(ConfigurationManager.AppSettings["SaveVersion"])
			};
            using (var wf = new StreamWriter(data.path + "/StarDrive/Saved Games/Headers/" + data.SaveAs + ".xml"))
                new XmlSerializer(typeof(HeaderData)).Serialize(wf, header);

            HelperFunctions.CollectMemory();
        }

        public static UniverseSaveData DeserializeFromCompressedSave(FileInfo compressedSave)
        {
            UniverseSaveData usData;
            FileInfo decompressed = new FileInfo(HelperFunctions.Decompress(compressedSave));

            var t = PerfTimer.StartNew();
            if (decompressed.Extension == NewExt) // new save format
            {
                using (FileStream stream = decompressed.OpenRead())
                using (var reader = new JsonTextReader(new StreamReader(stream)))
                {
                    var ser = new JsonSerializer
                    {
                        NullValueHandling = NullValueHandling.Ignore,
                        DefaultValueHandling = DefaultValueHandling.Ignore,
                    };
                    usData = ser.Deserialize<UniverseSaveData>(reader);
                }

                Log.Warning("JSON Total Load elapsed: {0}s  ", t.Elapsed);
            }
            else // old 100MB XML savegame format (haha)
            {
                long mem = GC.GetTotalMemory(false);

                XmlSerializer serializer1;
                try
                {
                    serializer1 = new XmlSerializer(typeof(UniverseSaveData));
                }
                catch
                {
                    var attrOpts = new XmlAttributeOverrides();
                    attrOpts.Add(typeof(SolarSystemSaveData), "MoonList", new XmlAttributes { XmlIgnore = true });
                    attrOpts.Add(typeof(EmpireSaveData), "MoonList", new XmlAttributes { XmlIgnore = true });
                    serializer1 = new XmlSerializer(typeof(UniverseSaveData), attrOpts);
                }

                long serSize = GC.GetTotalMemory(false) - mem;

                using (FileStream stream = decompressed.OpenRead())
                    usData = (UniverseSaveData)serializer1.Deserialize(stream);

                Log.Warning("XML Total Load elapsed: {0}s  mem: {1}MB", t.Elapsed, serSize / (1024f * 1024f));
            }
            decompressed.Delete();

            HelperFunctions.CollectMemory();
            return usData;
        }

        public struct EmpireSaveData
		{
			[Serialize(0)] public string Name;
			[Serialize(1)] public List<Relationship> Relations;
			[Serialize(2)] public List<SpaceRoadSave> SpaceRoadData;
			[Serialize(3)] public bool IsFaction;
            [Serialize(4)] public bool isMinorRace;
			[Serialize(5)] public RacialTrait Traits;
			[Serialize(6)] public EmpireData empireData;
			[Serialize(7)] public List<ShipSaveData> OwnedShips;
			[Serialize(8)] public float Research;
			[Serialize(9)] public float Money;
			[Serialize(10)] public List<TechEntry> TechTree;
			[Serialize(11)] public GSAISAVE GSAIData;
			[Serialize(12)] public string ResearchTopic;
			[Serialize(13)] public List<AO> AOs;
			[Serialize(14)] public List<FleetSave> FleetsList;
            [Serialize(15)] public string CurrentAutoFreighter;
            [Serialize(16)] public string CurrentAutoColony;
            [Serialize(17)] public string CurrentAutoScout;
            [Serialize(18)] public string CurrentConstructor;
		}

		public struct FleetSave
		{
            [Serialize(0)] public bool IsCoreFleet;
            [Serialize(1)] public string Name;
            [Serialize(2)] public int TaskStep;
            [Serialize(3)] public Vector2 Position;
            [Serialize(4)] public Guid FleetGuid;
            [Serialize(5)] public float facing;
            [Serialize(6)] public int Key;
            [Serialize(7)] public List<FleetShipSave> ShipsInFleet;
            [Serialize(8)] public List<FleetDataNode> DataNodes;
		}

		public struct FleetShipSave
		{
			[Serialize(0)] public Guid shipGuid;
			[Serialize(1)] public Vector2 fleetOffset;
		}

		public struct GoalSave
		{
			[Serialize(0)] public GoalType type;
			[Serialize(1)] public int GoalStep;
			[Serialize(2)] public Guid markedPlanetGuid;
			[Serialize(3)] public Guid colonyShipGuid;
			[Serialize(4)] public Vector2 BuildPosition;
			[Serialize(5)] public string ToBuildUID;
			[Serialize(6)] public Guid planetWhereBuildingAtGuid;
			[Serialize(7)] public string GoalName;
			[Serialize(8)] public Guid beingBuiltGUID;
			[Serialize(9)] public Guid fleetGuid;
			[Serialize(10)] public Guid GoalGuid;
		}

		public class GSAISAVE
		{
            [Serialize(0)] public List<int> UsedFleets;
			[Serialize(1)] public List<GoalSave> Goals;
			[Serialize(2)] public List<MilitaryTask> MilitaryTaskList;
			[Serialize(3)] public List<Guid> PinGuids;
			[Serialize(4)] public List<ThreatMatrix.Pin> PinList;
		}

		public struct PGSData
		{
			[Serialize(0)] public int x;
			[Serialize(1)] public int y;
			[Serialize(2)] public List<Troop> TroopsHere;
			[Serialize(3)] public bool Biosphere;
			[Serialize(4)] public Building building;
			[Serialize(5)] public bool Habitable;
			[Serialize(6)] public int foodbonus;
			[Serialize(7)] public int resbonus;
			[Serialize(8)] public int prodbonus;
		}

		public struct PlanetSaveData
		{
			[Serialize(0)] public Guid guid;
            [Serialize(1)] public string SpecialDescription;
			[Serialize(2)] public string Name;
            [Serialize(3)] public float Scale;
			[Serialize(4)] public string Owner;
			[Serialize(5)] public float Population;
			[Serialize(6)] public float PopulationMax;
			[Serialize(7)] public float Fertility;
			[Serialize(8)] public float Richness;
			[Serialize(9)] public int WhichPlanet;
			[Serialize(10)] public float OrbitalAngle;
			[Serialize(11)] public float OrbitalDistance;
			[Serialize(12)] public float Radius;
			[Serialize(13)] public bool HasRings;
			[Serialize(14)] public float farmerPercentage;
			[Serialize(15)] public float workerPercentage;
			[Serialize(16)] public float researcherPercentage;
			[Serialize(17)] public float foodHere;
			[Serialize(18)] public float prodHere;
			[Serialize(19)] public List<PGSData> PGSList;
			[Serialize(20)] public bool GovernorOn;
			[Serialize(21)] public List<QueueItemSave> QISaveList;
			[Serialize(22)] public Planet.ColonyType ColonyType;
			[Serialize(23)] public Planet.GoodState FoodState;
			[Serialize(24)] public int Crippled_Turns;
			[Serialize(25)] public Planet.GoodState ProdState;
			[Serialize(26)] public List<string> EmpiresThatKnowThisPlanet;
			[Serialize(27)] public float TerraformPoints;
			[Serialize(28)] public List<Guid> StationsList;
			[Serialize(29)] public bool FoodLock;
			[Serialize(30)] public bool ResLock;
			[Serialize(31)] public bool ProdLock;
			[Serialize(32)] public float ShieldStrength;
		}

		public struct ProjectileSaveData
		{
			[Serialize(0)] public string Weapon;
			[Serialize(1)] public float Duration;
			[Serialize(2)] public float Rotation;
			[Serialize(3)] public Vector2 Velocity;
			[Serialize(4)] public Vector2 Position;
		}

		public struct QueueItemSave
		{
			[Serialize(0)] public string UID;
			[Serialize(1)] public Guid GoalGUID;
			[Serialize(2)] public float ProgressTowards;
			[Serialize(3)] public bool isBuilding;
			[Serialize(4)] public bool isTroop;
			[Serialize(5)] public bool isShip;
			[Serialize(6)] public string DisplayName;
			[Serialize(7)] public bool IsRefit;
			[Serialize(8)] public float RefitCost;
			[Serialize(9)] public Vector2 pgsVector;
            [Serialize(10)] public bool isPlayerAdded;
		}

		public struct RingSave
		{
			[Serialize(0)] public PlanetSaveData Planet;
			[Serialize(1)] public bool Asteroids;
			[Serialize(2)] public float OrbitalDistance;
		}

		public struct RoadNodeSave
		{
			[Serialize(0)] public Vector2 Position;
			[Serialize(1)] public Guid Guid_Platform;
		}

		public struct ShipAISave
		{
			[Serialize(0)] public AIState state;
			[Serialize(1)] public int numFood;
			[Serialize(2)] public int numProd;
			[Serialize(3)] public string FoodOrProd;
			[Serialize(4)] public AIState defaultstate;
			[Serialize(5)] public List<ShipGoalSave> ShipGoalsList;
			[Serialize(6)] public List<Vector2> ActiveWayPoints;
			[Serialize(7)] public Guid startGuid;
			[Serialize(8)] public Guid endGuid;
			[Serialize(9)] public int GoToStep;
			[Serialize(10)] public Vector2 MovePosition;
			[Serialize(11)] public Guid OrbitTarget;
			[Serialize(12)] public Guid ColonizeTarget;
			[Serialize(13)] public Guid SystemToDefend;
			[Serialize(14)] public Guid AttackTarget;
			[Serialize(15)] public Guid EscortTarget;
		}

		public struct ShipGoalSave
		{
			[Serialize(0)] public ArtificialIntelligence.Plan Plan;
			[Serialize(1)] public Guid goalGuid;
			[Serialize(2)] public string VariableString;
			[Serialize(3)] public Guid fleetGuid;
			[Serialize(4)] public float SpeedLimit;
			[Serialize(5)] public Vector2 MovePosition;
			[Serialize(6)] public float DesiredFacing;
			[Serialize(7)] public float FacingVector;
			[Serialize(8)] public Guid TargetPlanetGuid;
		}

		public struct ShipSaveData
		{
			[Serialize(0)] public Guid guid;
			[Serialize(1)] public bool AfterBurnerOn;
			[Serialize(2)] public ShipAISave AISave;
			[Serialize(3)] public Vector2 Position;
			[Serialize(4)] public Vector2 Velocity;
			[Serialize(5)] public float Rotation;
			[Serialize(6)] public ShipData data;
			[Serialize(7)] public string Hull;
			[Serialize(8)] public string Name;
            [Serialize(9)] public string VanityName;
			[Serialize(10)] public bool IsPlayerShip;
			[Serialize(11)] public float yRotation;
			[Serialize(12)] public float Power;
			[Serialize(13)] public float Ordnance;
			[Serialize(14)] public float InCombatTimer;
			[Serialize(15)] public float experience;
			[Serialize(16)] public int kills;
			[Serialize(17)] public List<Troop> TroopList;
            [Serialize(18)] public List<Rectangle> AreaOfOperation;
			[Serialize(19)] public float FoodCount;
			[Serialize(20)] public float ProdCount;
			[Serialize(21)] public float PopCount;
			[Serialize(22)] public Guid TetheredTo;
			[Serialize(23)] public Vector2 TetherOffset;
			[Serialize(24)] public List<ProjectileSaveData> Projectiles;
		}

		public struct SolarSystemSaveData
		{
			[Serialize(0)] public Guid guid;
			[Serialize(1)] public string SunPath;
			[Serialize(2)] public string Name;
			[Serialize(3)] public Vector2 Position;
			[Serialize(4)] public List<RingSave> RingList;
			[Serialize(5)] public List<Asteroid> AsteroidsList;
            [Serialize(6)] public List<Moon> Moons;
			[Serialize(7)] public List<string> EmpiresThatKnowThisSystem;
		}

		public struct SpaceRoadSave
		{
			[Serialize(0)] public List<RoadNodeSave> RoadNodes;
			[Serialize(1)] public Guid OriginGUID;
			[Serialize(2)] public Guid DestGUID;
		}

		public class UniverseSaveData
		{
			[Serialize(0)] public string path;
			[Serialize(1)] public string SaveAs;
			[Serialize(2)] public string FileName;
			[Serialize(3)] public string FogMapName;
			[Serialize(4)] public string PlayerLoyalty;
			[Serialize(5)] public Vector2 campos;
			[Serialize(6)] public float camheight;
			[Serialize(7)] public Vector2 Size;
			[Serialize(8)] public float StarDate;
			[Serialize(9)] public float GameScale;
			[Serialize(10)] public float GamePacing;
			[Serialize(11)] public List<SolarSystemSaveData> SolarSystemDataList;
			[Serialize(12)] public List<EmpireSaveData> EmpireDataList;
			[Serialize(13)] public UniverseData.GameDifficulty gameDifficulty;
			[Serialize(14)] public bool AutoExplore;
			[Serialize(15)] public bool AutoColonize;
			[Serialize(16)] public bool AutoFreighters;
			[Serialize(17)] public bool AutoProjectors;
			[Serialize(18)] public int RemnantKills;
            [Serialize(19)] public int RemnantActivation;
            [Serialize(20)] public bool RemnantArmageddon;
			[Serialize(21)] public float FTLModifier = 1.0f;
            [Serialize(22)] public float EnemyFTLModifier = 1.0f;
			[Serialize(23)] public bool GravityWells;
			[Serialize(24)] public RandomEvent RandomEvent;
			[Serialize(25)] public SerializableDictionary<string, SerializableDictionary<int, Snapshot>> Snapshots;
            [Serialize(26)] public float OptionIncreaseShipMaintenance = GlobalStats.ShipMaintenanceMulti;
            [Serialize(27)] public float MinimumWarpRange = GlobalStats.MinimumWarpRange;
            // removed save field 28
            // @todo Change version tag for savegames so we can remove deleted field ID-s
            [Serialize(29)] public int IconSize;
            [Serialize(30)] public byte TurnTimer;
            [Serialize(31)] public bool preventFederations;
            [Serialize(32)] public float GravityWellRange = GlobalStats.GravityWellRange;
            [Serialize(33)] public bool EliminationMode;
		}

	}
}