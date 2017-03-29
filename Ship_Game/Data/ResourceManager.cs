using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Audio;
using SgMotion;
using SgMotion.Controllers;
using Ship_Game.Gameplay;
using SynapseGaming.LightingSystem.Core;
using SynapseGaming.LightingSystem.Rendering;
using System;
using System.Collections.Generic;
using System.IO;
using System.Xml.Serialization;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;
using Microsoft.Xna.Framework.Media;

namespace Ship_Game
{
    public sealed class ResourceManager // Refactored by RedFox
    {
        public static Map<string, Texture2D> TextureDict          = new Map<string, Texture2D>();
        public static XmlSerializer WeaponSerializer              = new XmlSerializer(typeof(Weapon));
        public static Map<string, Ship> ShipsDict                 = new Map<string, Ship>();
        public static Map<string, Technology> TechTree            = new Map<string, Technology>(StringComparer.InvariantCultureIgnoreCase);
        private static readonly Array<Model> RoidsModels          = new Array<Model>();
        private static readonly Array<Model> JunkModels           = new Array<Model>();
        private static readonly Array<ToolTip> ToolTips           = new Array<ToolTip>();
        public static Array<Encounter> Encounters                 = new Array<Encounter>();
        public static Map<string, Building> BuildingsDict         = new Map<string, Building>();
        public static Map<string, Good> GoodsDict                 = new Map<string, Good>();
        public static Map<string, Weapon> WeaponsDict             = new Map<string, Weapon>();
        private static Map<string, ShipModule> ShipModulesDict     = new Map<string, ShipModule>();
        public static Map<string, Texture2D> ProjTextDict         = new Map<string, Texture2D>();
        public static Map<string, ModelMesh> ProjectileMeshDict   = new Map<string, ModelMesh>();
        public static Map<string, Model> ProjectileModelDict      = new Map<string, Model>();
        public static bool Initialized                            = false;

        public static Array<RandomItem> RandomItemsList           = new Array<RandomItem>();
        private static Map<string, Troop> TroopsDict              = new Map<string, Troop>();
        private static Array<string>      TroopsDictKeys          = new Array<string>();
        public static IReadOnlyList<string> TroopTypes            => TroopsDictKeys;
        public static Map<string, DiplomacyDialog> DDDict         = new Map<string, DiplomacyDialog>();
        public static Map<string, LocalizationFile> LanguageDict  = new Map<string, LocalizationFile>();

        public static Map<string, Artifact> ArtifactsDict         = new Map<string, Artifact>();
        public static Map<string, ExplorationEvent> EventsDict    = new Map<string, ExplorationEvent>();
        public static Array<Texture2D> BigNebulas                 = new Array<Texture2D>();
        public static Array<Texture2D> MedNebulas                 = new Array<Texture2D>();
        public static Array<Texture2D> SmallNebulas               = new Array<Texture2D>();
        public static Array<Texture2D> SmallStars                 = new Array<Texture2D>();
        public static Array<Texture2D> MediumStars                = new Array<Texture2D>();
        public static Array<Texture2D> LargeStars                 = new Array<Texture2D>();
        public static Array<EmpireData> Empires                   = new Array<EmpireData>();
        public static XmlSerializer HeaderSerializer              = new XmlSerializer(typeof(HeaderData));
        public static Map<string, Model> ModelDict                = new Map<string, Model>();
        public static Map<string, SkinnedModel> SkinnedModels     = new Map<string, SkinnedModel>();
        public static Map<string, ShipData> HullsDict             = new Map<string, ShipData>(StringComparer.InvariantCultureIgnoreCase);

        public static Array<KeyValuePair<string, Texture2D>> FlagTextures = new Array<KeyValuePair<string, Texture2D>>();
        public static Map<string, SoundEffect> SoundEffectDict    = new Map<string, SoundEffect>();

        // Added by McShooterz
        public static HostileFleets HostileFleets                 = new HostileFleets();
        public static ShipNames ShipNames                         = new ShipNames();
        public static AgentMissionData AgentMissionData           = new AgentMissionData();
        public static MainMenuShipList MainMenuShipList           = new MainMenuShipList();
        public static Map<ShipData.RoleName, ShipRole> ShipRoles  = new Map<ShipData.RoleName, ShipRole>();
        public static Map<string, HullBonus> HullBonuses          = new Map<string, HullBonus>();
        public static Map<string, PlanetEdict> PlanetaryEdicts    = new Map<string, PlanetEdict>();
        public static XmlSerializer EconSerializer                = new XmlSerializer(typeof(EconomicResearchStrategy));

        public static Map<string, EconomicResearchStrategy> EconStrats = new Map<string, EconomicResearchStrategy>();

        private static RacialTraits RacialTraits;
        private static DiplomaticTraits DiplomacyTraits;

        // @todo These are all hacks caused by bad design and tight coupling
        public static UniverseScreen UniverseScreen;
        public static ScreenManager ScreenManager;

        // All references to Game1.Instance.Content were replaced by this property
        public static GameContentManager ContentManager => Game1.Instance.Content;

        public static void MarkShipDesignsUnlockable()
        {
            var shipTechs = new Map<Technology, Array<string>>();
            foreach (var techTreeItem in TechTree)
            {
                Technology tech = techTreeItem.Value;
                if (tech.ModulesUnlocked.Count <= 0 && tech.HullsUnlocked.Count <= 0)
                    continue;
                shipTechs.Add(tech, FindPreviousTechs(tech, new Array<string>()));
            }

            foreach (ShipData hull in HullsDict.Values)
            {
                if (hull.Role == ShipData.RoleName.disabled)
                    continue;
                if (hull.Role < ShipData.RoleName.gunboat)
                    hull.unLockable = true;
                foreach (Technology hulltech2 in shipTechs.Keys)
                {
                    foreach (Technology.UnlockedHull hulls in hulltech2.HullsUnlocked)
                    {
                        if (hulls.Name == hull.Hull)
                        {
                            foreach (string tree in shipTechs[hulltech2])
                            {
                                hull.techsNeeded.Add(tree);
                                hull.unLockable = true;
                            }
                            break;
                        }
                    }
                    if (hull.unLockable)
                        break;
                }
            }

            int x = 0;
            var purge = new HashSet<string>();
            foreach (var kv in ShipsDict)
            {
                ShipData shipData = kv.Value.shipData;
                if (shipData == null)
                    continue;
                if (shipData.HullRole == ShipData.RoleName.disabled)
                    continue;

                if (shipData.HullData != null && shipData.HullData.unLockable)
                {
                    foreach (string str in shipData.HullData.techsNeeded)
                        shipData.techsNeeded.Add(str);
                    shipData.hullUnlockable = true;
                }
                else
                {
                    shipData.allModulesUnlocakable = false;
                    shipData.hullUnlockable = false;
                    purge.Add(kv.Key);
                }

                if (shipData.hullUnlockable)
                {
                    shipData.allModulesUnlocakable = true;
                    foreach (ModuleSlotData module in kv.Value.shipData.ModuleSlots)
                    {
                        if (module.InstalledModuleUID == "Dummy")
                            continue;
                        bool modUnlockable = false;
                        foreach (Technology technology in shipTechs.Keys)
                        {
                            foreach (Technology.UnlockedMod mods in technology.ModulesUnlocked)
                            {
                                if (mods.ModuleUID == module.InstalledModuleUID)
                                {
                                    modUnlockable = true;
                                    shipData.techsNeeded.Add(technology.UID);
                                    foreach (string tree in shipTechs[technology])
                                        shipData.techsNeeded.Add(tree);
                                    break;
                                }
                            }
                            if (modUnlockable)
                                break;
                        }
                        if (!modUnlockable)
                        {
                            shipData.allModulesUnlocakable = false;
                            break;
                        }

                    }
                }

                if (shipData.allModulesUnlocakable)
                    foreach (string techname in shipData.techsNeeded)
                    {
                        shipData.TechScore += (int) TechTree[techname].Cost;
                        x++;
                        if (!(shipData.BaseStrength > 0f))
                            CalculateBaseStrength(kv.Value);
                    }
                else
                {
                    shipData.unLockable = false;
                    shipData.techsNeeded.Clear();
                    purge.Add(shipData.Name);
                    shipData.BaseStrength = 0;
                }

            }

            //Log.Info("Designs Bad: " + purge.Count + " : ShipDesigns OK : " + x);
            //foreach (string purger in purge)
            //    Log.Info("These are Designs" + purger);
        }

        public static void LoadItAll()
        {
            Reset();
            Log.Info("Load {0}", GlobalStats.HasMod ? GlobalStats.ModPath : "Vanilla");
            LoadLanguage();
            LoadTroops();
            LoadTextures();
            LoadToolTips();
            LoadHullData();
            LoadWeapons();
            LoadShipModules();
            LoadGoods();
            LoadShips();
            LoadJunk();
            LoadAsteroids();
            LoadProjTexts();
            LoadBuildings();
            LoadProjectileMeshes();
            LoadTechTree();
            LoadRandomItems();
            LoadFlagTextures();
            LoadNebulas();
            LoadSmallStars();
            LoadMediumStars();
            LoadLargeStars();
            LoadEmpires();
            LoadDialogs();
            LoadEncounters();
            LoadExpEvents();
            LoadArtifacts();
            LoadShipRoles();
            LoadPlanetEdicts();
            LoadEconomicResearchStrats();
            LoadBlackboxSpecific();

            HelperFunctions.CollectMemory();
        }

        // Gets FileInfo for Mod or Vanilla file. Mod file is checked first
        // Example relativePath: "Textures/myatlas.xml"
        public static FileInfo GetModOrVanillaFile(string relativePath)
        {
            FileInfo info;
            if (GlobalStats.HasMod)
            {
                info = new FileInfo(GlobalStats.ModPath + relativePath);
                if (info.Exists)
                    return info;
            }
            info = new FileInfo("Content/" + relativePath);
            return info.Exists ? info : null;
        }

        // This first tries to deserialize from Mod folder and then from Content folder
        private static T TryDeserialize<T>(string file) where T : class
        {
            FileInfo info = null;
            if (GlobalStats.HasMod) info = new FileInfo(GlobalStats.ModPath + file);
            if (info == null || !info.Exists) info = new FileInfo("Content/" + file);
            if (!info.Exists)
                return null;
            using (Stream stream = info.OpenRead())
                return (T)new XmlSerializer(typeof(T)).Deserialize(stream);
        }

        // The entity value is assigned only IF file exists and Deserialize succeeds
        private static void TryDeserialize<T>(string file, ref T entity) where T : class
        {
            var result = TryDeserialize<T>(file);
            if (result != null) entity = result;
        }

        // This gathers an union of Mod and Vanilla files. Any vanilla file is replaced by mod files.
        public static FileInfo[] GatherFilesUnified(string dir, string ext)
        {
            if (!GlobalStats.HasMod)
                return Dir.GetFiles("Content/" + dir, ext);

            var infos = new Map<string, FileInfo>();

            string contentPath = Path.GetFullPath("Content/");
            foreach (FileInfo file in Dir.GetFiles("Content/" + dir, ext))
            {
                infos[file.FullName.Substring(contentPath.Length)] = file;
            }

            // now pull everything from the modfolder and replace all matches
            contentPath = Path.GetFullPath(GlobalStats.ModPath);
            foreach (var file in Dir.GetFiles(GlobalStats.ModPath + dir, ext))
            {
                infos[file.FullName.Substring(contentPath.Length)] = file;
            }

            return infos.Values.ToArray();
        }

        // This tries to gather only mod files, or only vanilla files
        // No union/mix is made
        public static FileInfo[] GatherFilesModOrVanilla(string dir, string ext)
        {
            if (GlobalStats.HasMod)
            {
                var files = Dir.GetFiles(GlobalStats.ModPath + dir, ext);
                if (files.Length != 0)
                    return files;
            }
            return Dir.GetFiles("Content/" + dir, ext);
        }

        // Loads a list of entities in a folder
        public static Array<T> LoadEntitiesModOrVanilla<T>(string dir, string where) where T : class
        {
            var result = new Array<T>();
            var files = GatherFilesModOrVanilla(dir, "xml");
            if (files.Length != 0)
            {
                var s = new XmlSerializer(typeof(T));
                foreach (FileInfo info in files)
                    if (LoadEntity(s, info, where, out T entity))
                        result.Add(entity);
            }
            else Log.Error($"{where}: No files in '{dir}'");
            return result;
        }

        // Added by RedFox - Generic entity loading, less typing == more fun
        private static bool LoadEntity<T>(XmlSerializer s, FileInfo info, string id, out T entity) where T : class
        {
            try
            {
                using (FileStream stream = info.OpenRead())
                    entity = (T)s.Deserialize(stream);
                return true;
            }
            catch (Exception e)
            {
                Log.Error(e, $"Deserialize {id} failed");
                entity = null;
                return false;
            }
        }

        private static Array<T> LoadEntities<T>(FileInfo[] files, string id) where T : class
        {
            var list = new Array<T>(files.Length);
            var s = new XmlSerializer(typeof(T));
            foreach (FileInfo info in files)
                if (LoadEntity(s, info, id, out T entity))
                    list.Add(entity);
            return list;
        }

        private static Array<T> LoadEntities<T>(string dir, string id) where T : class
        {
            return LoadEntities<T>(GatherFilesUnified(dir, "xml"), id);
        }

        private static Array<T> LoadVanillaEntities<T>(string dir, string id) where T : class
        {
            return LoadEntities<T>(Dir.GetFiles("Content/" + dir, "xml"), id);
        }

        private static Array<T> LoadModEntities<T>(string dir, string id) where T : class
        {
            return LoadEntities<T>(Dir.GetFiles(GlobalStats.ModPath + dir, "xml"), id);
        }

        private class InfoPair<T> where T : class
        {
            public readonly FileInfo Info;
            public readonly T Entity;
            public InfoPair(FileInfo info, T entity) { Info = info; Entity = entity; }
        }

        private static Array<InfoPair<T>> LoadEntitiesWithInfo<T>(string dir, string id, bool modOnly = false) where T : class
        {
            var files = modOnly ? Dir.GetFiles(GlobalStats.ModPath + dir, "xml") : GatherFilesUnified(dir, "xml");
            var list = new Array<InfoPair<T>>(files.Length);
            var s = new XmlSerializer(typeof(T));
            foreach (FileInfo info in files)
                if (LoadEntity(s, info, id, out T entity))
                    list.Add(new InfoPair<T>(info, entity));
            return list;
        }

        public static float GetTroopCost(string troopType) => TroopsDict[troopType].GetCost();
        public static Troop GetTroopTemplate(string troopType) => TroopsDict[troopType];
        public static Array<Troop> GetTroopTemplates() => new Array<Troop>(TroopsDict.Values);

        public static Troop CopyTroop(Troop t)
        {
            Troop troop = t.Clone();
            troop.StrengthMax = t.StrengthMax > 0 ? t.StrengthMax : t.Strength;
            troop.WhichFrame = (int) RandomMath.RandomBetween(1, t.num_idle_frames - 1);
            troop.SetOwner(t.GetOwner());
            return troop;
        }

        // @todo Remove this
        private static Troop CreateTroop(Troop template, Empire forOwner)
        {
            Troop troop = CopyTroop(template);
            if (forOwner != null)
                troop.Strength += (int)(forOwner.data.Traits.GroundCombatModifier * troop.Strength);
            troop.SetOwner(forOwner);
            return troop;
        }

        public static Troop CreateTroop(string troopType, Empire forOwner)
        {
            Troop troop = CopyTroop(TroopsDict[troopType]);
            if (forOwner != null)
                troop.Strength += (int)(forOwner.data.Traits.GroundCombatModifier * troop.Strength);
            troop.SetOwner(forOwner);
            return troop;
        }


        public static Ship GetShipTemplate(string shipName)
        {
            return ShipsDict[shipName];
        }

        public static string GetShipHull(string shipName)
        {
            return ShipsDict[shipName].GetShipData().Hull;
        }

        // Added by RedFox - Debug, Hangar Ship, and Platform creation
        public static Ship CreateShipAtPoint(string shipName, Empire owner, Vector2 position)
        {
            if (!ShipsDict.TryGetValue(shipName, out Ship template))
            {
                var stackTrace = new Exception();
                MessageBox.Show($"Failed to create new ship '{shipName}'. " +
                                $"This is a bug caused by mismatched or missing ship designs\n\n{stackTrace.StackTrace}",
                    "Ship spawn failed!", MessageBoxButtons.OK);
                return null;
            }

            var ship = new Ship
            {
                shipData     = template.shipData,
                Name         = template.Name,
                BaseStrength = template.BaseStrength,
                BaseCanWarp  = template.BaseCanWarp,
                loyalty      = owner,
                Position     = position
            };

            if (!template.shipData.Animated)
            {
                ship.SetSO(new SceneObject(GetModel(template.ModelPath).Meshes[0])
                    {ObjectType = ObjectType.Dynamic});
            }
            else
            {
                SkinnedModel model = GetSkinnedModel(template.ModelPath);
                ship.SetSO(new SceneObject(model.Model) {ObjectType = ObjectType.Dynamic});
                ship.SetAnimationController(new AnimationController(model.SkeletonBones), model);
            }

            ship.GetTList().Capacity = template.GetTList().Count;
            foreach (Thruster t in template.GetTList())
                ship.AddThruster(t);

            ship.CreateModuleSlotsFromData(template.GetShipData().ModuleSlots);

            // Added by McShooterz: add automatic ship naming
            if (GlobalStats.HasMod)
                ship.VanityName = ShipNames.GetName(owner.data.Traits.ShipType, ship.shipData.Role);

            if (ship.shipData.Role == ShipData.RoleName.fighter)
                ship.Level += owner.data.BonusFighterLevels;

            // during new game creation, universeScreen can still be null
            if (UniverseScreen != null && UniverseScreen.GameDifficulty > UniverseData.GameDifficulty.Normal)
                ship.Level += (int) UniverseScreen.GameDifficulty;

            ship.Initialize();

            var so = ship.GetSO();
            so.World = Matrix.CreateTranslation(new Vector3(ship.Center, 0f));

            var screenManager = UniverseScreen?.ScreenManager ?? ScreenManager;
            lock (GlobalStats.ObjectManagerLocker)
            {
                screenManager.inter.ObjectManager.Submit(so);
            }

            var content = ContentManager;
            var thrustCylinder = content.Load<Model>("Effects/ThrustCylinderB");
            var noiseVolume    = content.Load<Texture3D>("Effects/NoiseVolume");
            var thrusterEffect = content.Load<Effect>("Effects/Thrust");
            foreach (Thruster t in ship.GetTList())
            {
                t.load_and_assign_effects(content, thrustCylinder, noiseVolume, thrusterEffect);
                t.InitializeForViewing();
            }

            owner.AddShip(ship);
            return ship;
        }

        //@bug #1002  cant add a ship to a system in readlock. 
        public static Ship CreateShipAt(string shipName, Empire owner, Planet p, Vector2 deltaPos, bool doOrbit)
        {
            Ship ship = CreateShipAtPoint(shipName, owner, p.Position + deltaPos);
            if (doOrbit)
                ship.DoOrbit(p);

            ship.SetSystem(p.system);
            return ship;
        }

        // Refactored by RedFox - Normal Shipyard ship creation
        public static Ship CreateShipAt(string shipName, Empire owner, Planet p, bool doOrbit)
        {
            return CreateShipAt(shipName, owner, p, Vector2.Zero, doOrbit);
        }

        // Added by McShooterz: for refit to keep name
        // Refactored by RedFox
        public static Ship CreateShipAt(string shipName, Empire owner, Planet p, bool doOrbit, string refitName, int refitLevel)
        {
            Ship ship = CreateShipAt(shipName, owner, p, doOrbit);

            // Added by McShooterz: add automatic ship naming
            ship.VanityName = refitName;
            ship.Level = refitLevel;
            return ship;
        }

        // unused -- Called in fleet creation function, which is in turn not used
        public static Ship CreateShipAtPoint(string shipName, Empire owner, Vector2 p, float facing)
        {
            Ship ship = CreateShipAtPoint(shipName, owner, p);
            ship.Rotation = facing;
            return ship;
        }

        // Unused... Battle mode, eh?
        public static Ship CreateShipForBattleMode(string shipName, Empire owner, Vector2 p)
        {
            return CreateShipAtPoint(shipName, owner, p);
        }

        // Hangar Ship Creation
        public static Ship CreateShipFromHangar(string key, Empire owner, Vector2 p, Ship parent)
        {
            Ship ship = CreateShipAtPoint(key, owner, p);
            if (ship == null) return null;
            ship.Mothership = parent;
            ship.Velocity = parent.Velocity;
            return ship;
        }

        public static Ship CreateTroopShipAtPoint(string shipName, Empire owner, Vector2 point, Troop troop)
        {
            Ship ship = CreateShipAtPoint(shipName, owner, point);
            ship.VanityName = troop.Name;
            ship.TroopList.Add(CopyTroop(troop));
            if (ship.shipData.Role == ShipData.RoleName.troop)
                ship.shipData.ShipCategory = ShipData.Category.Combat;
            return ship;
        }

        // Added by RedFox
        private static void DeleteShipFromDir(string dir, string shipName)
        {
            foreach (FileInfo info in Dir.GetFiles(dir, shipName + ".xml", SearchOption.TopDirectoryOnly))
            {
                // @note ship.Name is always the same as fileNameNoExt 
                //       part of "shipName.xml", so we can skip parsing the XML's
                if (info.NameNoExt() != shipName)
                    continue;

                ShipsDict.Remove(shipName);
                info.Delete();
                return;
            }
        }

        // Refactored by RedFox
        public static void DeleteShip(string shipName)
        {
            DeleteShipFromDir("Content/StarterShips", shipName);
            DeleteShipFromDir("Content/SavedDesigns", shipName);

            string appData = Dir.ApplicationData;
            DeleteShipFromDir(appData + "/StarDrive/Saved Designs", shipName);
            DeleteShipFromDir(appData + "/StarDrive/WIP", shipName);

            foreach (Empire e in EmpireManager.Empires)
                e.UpdateShipsWeCanBuild();
        }

        public static Building CreateBuilding(string whichBuilding)
        {
            Building template = BuildingsDict[whichBuilding];
            Building newB = template.Clone();
            newB.Cost *= UniverseScreen.GamePaceStatic;

            // comp fix to ensure functionality of vanilla buildings
            if (newB.Name == "Outpost" || newB.Name == "Capital City")
            {
                // @todo What is going on here? Is this correct?
                if (!newB.IsProjector && !(newB.ProjectorRange > 0f))
                {
                    newB.ProjectorRange = Empire.ProjectorRadius;
                    newB.IsProjector = true;
                }
                if (!newB.IsSensor && !(newB.SensorRange > 0.0f))
                {
                    newB.SensorRange = 20000.0f;
                    newB.IsSensor = true;
                }
            }
            if (template.isWeapon)
            {
                newB.theWeapon = CreateWeapon(template.Weapon);
            }
            return newB;
        }

        public static EmpireData GetEmpireByName(string name)
        {
            foreach (EmpireData empireData in Empires)
                if (empireData.Traits.Name == name)
                    return empireData;
            return null;
        }

        public static Model GetModel(string modelName, bool throwOnFailure = false)
        {
            Model item;

            // try to get cached value
            lock (ModelDict) if (ModelDict.TryGetValue(modelName, out item)) return item;

            try
            {
                //ContentManager.EnableLoadInfoLog = true;

                // special backwards compatibility with mods...
                // basically, all old mods put their models into "Mod Models/" folder because
                // the old model loading system didn't handle Unified resource paths...
                if (GlobalStats.HasMod && !modelName.StartsWith("Model"))
                {
                    string modModelPath = GlobalStats.ModPath + "Mod Models/" + modelName + ".xnb";
                    if (File.Exists(modModelPath))
                        item = ContentManager.Load<Model>(modModelPath);
                }
                if (item == null)
                    item = ContentManager.Load<Model>(modelName);
            }
            catch (ContentLoadException)
            {
                if (throwOnFailure) throw;
            }
            finally
            {
                ContentManager.EnableLoadInfoLog = false;
            }

            // stick it into Model cache, even if null (prevents further loading)
            lock (ModelDict) ModelDict.Add(modelName, item);
            return item;
        }

        

        // Gets a loaded texture using the given abstract texture path
        public static Texture2D Texture(string texturePath)
        {
            return TextureDict[texturePath];
        }

        private static void LoadTexture(FileInfo info)
        {
            string relPath = info.CleanResPath();
            var tex = ContentManager.Load<Texture2D>(relPath); // 90% of this methods time is spent inside content::Load

            string texName = relPath.Substring("Textures/".Length);
            lock (TextureDict)
            {
                TextureDict[texName] = tex;
            }
        }

        // This method is a hot path during Loading and accounts for ~25% of time spent
        private static void LoadTextures()
        {
            FileInfo[] files = GatherFilesUnified("Textures", "xnb");
        #if true // parallel texture load
            Parallel.For(files.Length, (start, end) => {
                for (int i = start; i < end; ++i)
                    LoadTexture(files[i]);
            });
        #else
            foreach (FileInfo info in files)
                LoadTexture(info);
        #endif

            // check for any duplicate loads:
            var field = typeof(ContentManager).GetField("loadedAssets", BindingFlags.Instance | BindingFlags.NonPublic);
            var assets = field?.GetValue(ContentManager) as Map<string, object>;
            if (assets != null && assets.Count != 0)
            {
                string[] keys = assets.Keys.Where(key => key != null).ToArray();
                string[] names = keys.Select(key => Path.GetDirectoryName(key) + "\\" + Path.GetFileName(key)).ToArray();
                for (int i = 0; i < names.Length; ++i)
                {
                    for (int j = 0; j < names.Length; ++j)
                    {
                        if (i != j && names[i] == names[j])
                        {
                            Log.Warning("!! Duplicate texture load: \n    {0}\n    {1}", keys[i], keys[j]);
                        }
                    }
                }
            }
        }

        // Load texture with its abstract path such as
        // "Explosions/smaller/shipExplosion"
        public static Texture2D LoadTexture(string textureName)
        {
            if (TextureDict.TryGetValue(textureName, out Texture2D tex))
                return tex;
            try
            {
                tex = ContentManager.Load<Texture2D>("Textures/" + textureName);
                TextureDict[textureName] = tex;
            }
            catch (Exception)
            {
            }
            return tex;
        }

        // Load texture for a specific mod, such as modName="Overdrive"
        public static Texture2D LoadModTexture(string modName, string textureName)
        {
            if (TextureDict.TryGetValue(textureName, out Texture2D tex))
                return tex;

            string modTexPath = "Mods/" + modName + "/Textures/" + textureName;
            if (File.Exists(modTexPath + ".xnb"))
                return TextureDict[textureName] = ContentManager.Load<Texture2D>(modTexPath);

            return null;
        }

        public static float GetModuleCost(string uid)
        {
            ShipModule template = ShipModulesDict[uid];
            return template.Cost;
        }

        public static ShipModule GetModuleTemplate(string uid)
        {
            return ShipModulesDict[uid];
        }
        public static bool ModuleExists(string uid) => ShipModulesDict.ContainsKey(uid);
        public static IReadOnlyDictionary<string, ShipModule> ShipModules => ShipModulesDict;
        public static bool TryGetModule(string uid, out ShipModule mod) => ShipModulesDict.TryGetValue(uid, out mod);

        public static RacialTraits RaceTraits
            => RacialTraits ?? (RacialTraits = TryDeserialize<RacialTraits>("RacialTraits/RacialTraits.xml")); 

        public static DiplomaticTraits DiplomaticTraits
            => DiplomacyTraits ?? (DiplomacyTraits = TryDeserialize<DiplomaticTraits>("Diplomacy/DiplomaticTraits.xml"));

        public static SolarSystemData LoadSolarSystemData(string homeSystemName)
            => TryDeserialize<SolarSystemData>("SolarSystems/" + homeSystemName + ".xml");                

        public static Array<SolarSystemData> LoadRandomSolarSystems()
            => LoadEntitiesModOrVanilla<SolarSystemData>("SolarSystems/Random", "LoadSolarSystems");

        public static SkinnedModel GetSkinnedModel(string path)
        {
            if (SkinnedModels.TryGetValue(path, out SkinnedModel model))
                return model;
            // allow this to throw an exception on load error
            return SkinnedModels[path] = ContentManager.Load<SkinnedModel>(path);
        }

        // Refactored by RedFox, gets a new weapon instance based on weapon UID
        public static Weapon CreateWeapon(string uid)
        {
            Weapon template = WeaponsDict[uid];
            return template.Clone();
        }

        // WARNING: DO NOT MODIFY this Weapon instace! (wish C# has const refs like C++)
        public static Weapon GetWeaponTemplate(string uid)
        {
            return WeaponsDict[uid];
        }

        public static Texture2D LoadRandomLoadingScreen(GameContentManager content)
        {
            var files = GatherFilesModOrVanilla("LoadingScreen", "xnb");

            FileInfo file = files[RandomMath.InRange(0, files.Length)];
            return content.Load<Texture2D>(file.CleanResPath());
        }

        // advice is temporary and only sticks around while loading
        public static string LoadRandomAdvice()
        {
            string adviceFile = "Advice/" + GlobalStats.Language + "/Advice.xml";

            var adviceList = TryDeserialize<Array<string>>(adviceFile);
            return adviceList?[RandomMath.InRange(adviceList.Count)] ?? "Advice.xml missing";
        }

        private static void LoadArtifacts() // Refactored by RedFox
        {
            foreach (var arts in LoadEntities<Array<Artifact>>("Artifacts", "LoadArtifacts"))
            {
                foreach (Artifact art in arts)
                {
                    art.Name = string.Intern(art.Name);
                    ArtifactsDict[art.Name] = art;
                }
            }
        }

        private static void LoadBuildings() // Refactored by RedFox
        {
            foreach (Building newB in LoadEntities<Building>("Buildings", "LoadBuildings"))
            {
                BuildingsDict[string.Intern(newB.Name)] = newB;
            }
        }

        private static void LoadDialogs() // Refactored by RedFox
        {
            string dir = "DiplomacyDialogs/" + GlobalStats.Language + "/";
            foreach (var pair in LoadEntitiesWithInfo<DiplomacyDialog>(dir, "LoadDialogs"))
            {
                string nameNoExt = pair.Info.NameNoExt();
                DDDict[nameNoExt] = pair.Entity;
            }
        }

        private static void LoadEmpires() // Refactored by RedFox
        {
            Empires.Clear();

            if (GlobalStats.HasMod && GlobalStats.ActiveModInfo.DisableDefaultRaces)
            {
                Empires.AddRange(LoadModEntities<EmpireData>("Races", "LoadEmpires"));
            }
            else
            {
                Empires.AddRange(LoadEntities<EmpireData>("Races", "LoadEmpires"));
            }
        }

        public static void LoadEncounters() // Refactored by RedFox
        {
            Encounters = LoadEntities<Encounter>("Encounter Dialogs", "LoadEncounters");
        }

        private static void LoadExpEvents() // Refactored by RedFox
        {
            foreach (var pair in LoadEntitiesWithInfo<ExplorationEvent>("Exploration Events", "LoadExpEvents"))
                EventsDict[pair.Info.NameNoExt()] = pair.Entity;
        }

        private static void LoadFlagTextures() // Refactored by RedFox
        {
            foreach (FileInfo info in GatherFilesUnified("Flags", "xnb"))
            {
                var tex = ContentManager.Load<Texture2D>(info.CleanResPath());
                FlagTextures.Add(new KeyValuePair<string, Texture2D>(info.NameNoExt(), tex));
            }
        }

        private static void LoadGoods() // Refactored by RedFox
        {
            foreach (var pair in LoadEntitiesWithInfo<Good>("Goods", "LoadGoods"))
            {
                Good good = pair.Entity;
                good.UID = string.Intern(pair.Info.NameNoExt());
                GoodsDict[good.UID] = good;
            }
        }

        public static void LoadHardcoreTechTree() // Refactored by RedFox
        {
            TechTree.Clear();
            foreach (var pair in LoadEntitiesWithInfo<Technology>("Technology_HardCore", "LoadTechnologyHardcore"))
            {
                TechTree[pair.Info.NameNoExt()] = pair.Entity;
            }
        }

        public static bool TryGetHull(string shipHull, out ShipData hullData)
        {
            return HullsDict.TryGetValue(shipHull, out hullData);
        }

        public static Array<ShipData> LoadHullData() // Refactored by RedFox
        {
            var retList = new Array<ShipData>();

            FileInfo[] hullFiles = GatherFilesUnified("Hulls", "xml");
            Parallel.For(hullFiles.Length, (start, end) =>
            {
                for (int i = start; i < end; ++i)
                {
                    FileInfo info = hullFiles[i];
                    try
                    {
                        string dirName     = info.Directory?.Name ?? "";
                        ShipData shipData  = ShipData.Parse(info);
                        shipData.Hull      = string.Intern(dirName + "/" + shipData.Hull);
                        shipData.ShipStyle = string.Intern(dirName);

                        lock (retList)
                        {
                            HullsDict[shipData.Hull] = shipData;
                            retList.Add(shipData);
                        }
                    }
                    catch (Exception e)
                    {
                        Log.Error(e, $"LoadHullData {info.Name} failed");
                    }
                }
            });
            return retList;
        }

        public static Model GetJunkModel(int idx)
        {
            return JunkModels[idx];
        }
        public static int NumJunkModels => JunkModels.Count;

        public static int NumAsteroidModels => RoidsModels.Count;
        public static Model GetAsteroidModel(int roidId)
        {
            return RoidsModels[roidId];
        }

        // loads models from a model folder that match "modelPrefixNNN.xnb" format, where N is an integer
        private static void LoadNumberedModels(Array<Model> models, string modelFolder, string modelPrefix, string id)
        {
            models.Clear();
            foreach (FileInfo info in GatherFilesModOrVanilla(modelFolder, "xnb"))
            {
                string nameNoExt = info.NameNoExt();
                try
                {
                    // only accept "prefixNN" format, because there are a bunch of textures in the asteroids folder
                    if (!nameNoExt.StartsWith(modelPrefix) || !int.TryParse(nameNoExt.Substring(modelPrefix.Length), out int _))
                        continue;
                    models.Add(ContentManager.Load<Model>(info.CleanResPath()));
                }
                catch (Exception e)
                {
                    Log.Error(e, $"LoadNumberedModels {modelFolder} {nameNoExt} failed");
                }
            }
        }

        private static void LoadJunk() // Refactored by RedFox
        {
            LoadNumberedModels(JunkModels, "Model/SpaceJunk/", "spacejunk", "LoadJunk");
        }
        private static void LoadAsteroids()
        {
            LoadNumberedModels(RoidsModels, "Model/Asteroids/", "asteroid", "LoadAsteroids");
        }

        private static void LoadLanguage() // Refactored by RedFox
        {
            foreach (var loc in LoadVanillaEntities<LocalizationFile>("Localization/English/", "LoadLanguage"))
                Localizer.AddTokens(loc.TokenList);

            if (GlobalStats.NotEnglish)
            {
                foreach (var loc in LoadVanillaEntities<LocalizationFile>($"Localization/{GlobalStats.Language}/", "LoadLanguage"))
                    Localizer.AddTokens(loc.TokenList);
            }

            // Now replace any vanilla tokens with mod tokens
            if (GlobalStats.HasMod)
            {
                foreach (var loc in LoadModEntities<LocalizationFile>("Localization/English/", "LoadLanguage"))
                    Localizer.AddTokens(loc.TokenList);

                if (GlobalStats.NotEnglish)
                {
                    foreach (var loc in LoadModEntities<LocalizationFile>($"Localization/{GlobalStats.Language}/", "LoadLanguage"))
                        Localizer.AddTokens(loc.TokenList);
                }
            }

        }

        private static void LoadLargeStars() // Refactored by RedFox
        {
            foreach (FileInfo info in GatherFilesUnified("LargeStars", "xnb"))
            {
                try
                {
                    LargeStars.Add(ContentManager.Load<Texture2D>(info.CleanResPath()));
                }
                catch (Exception e)
                {
                    Log.Error(e, "LoadLargerStars failed");
                }
            }
        }

        private static void LoadMediumStars() // Refactored by RedFox
        {
            foreach (FileInfo info in GatherFilesUnified("MediumStars", "xnb"))
            {
                try
                {
                    MediumStars.Add(ContentManager.Load<Texture2D>(info.CleanResPath()));
                }
                catch (Exception e)
                {
                    Log.Error(e, "LoadMediumStars failed");
                }
            }
        }

        // Refactored by RedFox
        private static void LoadNebulas()
        {
            foreach (FileInfo info in Dir.GetFiles("Content/Nebulas", "xnb"))
            {
                string nameNoExt = info.NameNoExt();
                var tex = ContentManager.Load<Texture2D>("Nebulas/" + nameNoExt);
                if      (tex.Width == 2048) BigNebulas.Add(tex);
                else if (tex.Width == 1024) MedNebulas.Add(tex);
                else                        SmallNebulas.Add(tex);
            }
        }

        // Refactored by RedFox
        private static void LoadProjectileMesh(string projectileDir, string nameNoExt)
        {
            string path = projectileDir + nameNoExt;
            try
            {
                var projModel = ContentManager.Load<Model>(path);
                ProjectileMeshDict[nameNoExt]  = projModel.Meshes[0];
                ProjectileModelDict[nameNoExt] = projModel;
            }
            catch (Exception e)
            {
                if (e.HResult == -2146233088)
                    return;
                Log.Error(e, $"LoadProjectile {path} failed");
            }
        }

        private static void LoadProjectileMeshes()
        {
            const string projectileDir = "Model/Projectiles/";
            LoadProjectileMesh(projectileDir, "projLong");
            LoadProjectileMesh(projectileDir, "projTear");
            LoadProjectileMesh(projectileDir, "projBall");
            LoadProjectileMesh(projectileDir, "torpedo");
            LoadProjectileMesh(projectileDir, "missile");
            LoadProjectileMesh(projectileDir, "spacemine");
        }


        private static void LoadProjTexts()
        {
            foreach (FileInfo info in GatherFilesUnified("Model/Projectiles/textures", "xnb"))
            {
                var tex = ContentManager.Load<Texture2D>(info.CleanResPath());
                ProjTextDict[info.NameNoExt()] = tex;
            }
        }


        private static void LoadRandomItems()
        {
            RandomItemsList = LoadEntities<RandomItem>("RandomStuff", "LoadRandomItems");
        }

        private static void LoadShipModules()
        {
            foreach (var pair in LoadEntitiesWithInfo<ShipModule_Deserialize>("ShipModules", "LoadShipModules"))
            {
                // Added by gremlin support techlevel disabled folder.
                if (pair.Info.DirectoryName.IndexOf("disabled", StringComparison.OrdinalIgnoreCase) > 0)
                    continue;
                ShipModule_Deserialize data = pair.Entity;

                data.UID = string.Intern(pair.Info.NameNoExt());
                data.IconTexturePath = string.Intern(data.IconTexturePath);
                if (data.WeaponType != null)
                    data.WeaponType = string.Intern(data.WeaponType);

                if (data.IsCommandModule && data.TargetTracking == 0 && data.FixedTracking == 0)
                {
                    data.TargetTracking = (sbyte)((data.XSIZE*data.YSIZE) / 3);
                }

            #if DEBUG
                if (ShipModulesDict.ContainsKey(data.UID))
                    Log.Info("ShipModule UID already found. Conflicting name:  {0}", data.UID);
            #endif
                ShipModulesDict[data.UID] = ShipModule.CreateTemplate(data);
            }

            Log.Info("Num ShipModule_Advanced: {0}", ShipModuleFlyweight.TotalNumModules);

            foreach (var entry in ShipModulesDict)
                entry.Value.SetAttributesNoParent();
        }

        private static Array<Ship> LoadShips(FileInfo[] shipDescriptors)
        {
            var ships = new Array<Ship>();

            RangeAction loadShips = (start, end) =>
            {
                for (int i = start; i < end; ++i)
                {
                    FileInfo info = shipDescriptors[i];
                    if (info.DirectoryName.IndexOf("disabled", StringComparison.OrdinalIgnoreCase) != -1)
                        continue;

                    try
                    {
                        ShipData shipData = ShipData.Parse(info);
                        if (shipData.Role == ShipData.RoleName.disabled)
                            continue;

                        Ship newShip = Ship.CreateShipFromShipData(shipData);
                        newShip.SetShipData(shipData);
                        if (!newShip.InitializeStatus(fromSave: false))
                            continue;

                        lock (ships)
                        {
                            ShipsDict[shipData.Name] = newShip;
                            ships.Add(newShip);
                        }
                    }
                    catch (Exception e)
                    {
                        Log.Error(e, $"LoadShip {info.Name} failed");
                    }
                }
            };
            Parallel.For(shipDescriptors.Length, loadShips);
            //loadShips(0, shipDescriptors.Length); // test without parallel for
            return ships;
        }

        // Refactored by RedFox
        // This is a hotpath during loading and ~50% of time is spent here
        public static void LoadShips()
        {
            ShipsDict.Clear();

            foreach (Ship ship in LoadShips(GatherFilesModOrVanilla("StarterShips", "xml")))
                ship.reserved = true;

            foreach (Ship ship in LoadShips(GatherFilesUnified("SavedDesigns", "xml")))
                ship.reserved = true;

            foreach (Ship ship in LoadShips(Dir.GetFiles(Dir.ApplicationData + "/StarDrive/Saved Designs", "xml")))
                ship.IsPlayerDesign = true;

            foreach (Ship ship in LoadShips(GatherFilesUnified("ShipDesigns", "xml")))
            {
                ship.reserved = true;
                ship.IsPlayerDesign = true;
            }
            
            foreach (var entry in ShipsDict) // Added by gremlin : Base strength Calculator
            {
                CalculateBaseStrength(entry.Value);
            }
        }

        // @todo Move this into Ship class and autocalculate during ship instance init
        public static float CalculateBaseStrength(Ship ship)
        {
            float offense = 0f;
            float defense = 0f;
            bool fighters = false;
            bool weapons = false;

            foreach (ShipModule slot in ship.ModuleSlotList)
            {
                //ShipModule template = GetModuleTemplate(slot.UID);
                weapons  |= slot.InstalledWeapon != null;
                fighters |= slot.hangarShipUID   != null && !slot.IsSupplyBay && !slot.IsTroopBay;

                offense += CalculateModuleOffense(slot);
                defense += CalculateModuleDefense(slot, ship.Size);

                if (ShipModulesDict[slot.UID].WarpThrust > 0)
                    ship.BaseCanWarp = true;
            }

            if (!fighters && !weapons) offense = 0f;
            if (defense > offense) defense = offense;

            return ship.BaseStrength = ship.shipData.BaseStrength = offense + defense;
        }

        // @todo Move this to ShipModule class
        public static float CalculateModuleOffenseDefense(ShipModule module, int slotCount)
        {
            return CalculateModuleDefense(module, slotCount) + CalculateModuleOffense(module);
        }

        // @todo Move this to ShipModule class
        public static float CalculateModuleDefense(ShipModule module, int slotCount)
        {
            if (slotCount <= 0)
                return 0f;

            float def = 0f;
            def += module.shield_power_max * ((module.shield_radius * .05f) / slotCount);
            //(module.shield_power_max+  module.shield_radius +module.shield_recharge_rate) / slotCount ;
            def += module.HealthMax * ((module.ModuleType == ShipModuleType.Armor ? (module.XSIZE) : 1f) / (slotCount * 4));
            return def;
        }

        // @todo Move this to ShipModule class
        public static float CalculateModuleOffense(ShipModule module)
        {
            float off = 0f;
            if (module.InstalledWeapon != null)
            {
                //weapons = true;
                Weapon w = module.InstalledWeapon;

                //Doctor: The 25% penalty to explosive weapons was presumably to note that not all the damage is applied to a single module - this isn't really weaker overall, though
                //and unfairly penalises weapons with explosive damage and makes them appear falsely weaker.
                off += (!w.isBeam ? (w.DamageAmount * w.SalvoCount) * (1f / w.fireDelay) : w.DamageAmount * 18f);

                //Doctor: Guided weapons attract better offensive rating than unguided - more likely to hit. Setting at flat 25% currently.
                if (w.Tag_Guided)
                    off *= 1.25f;

                //Doctor: Higher range on a weapon attracts a small bonus to offensive rating. E.g. a range 2000 weapon gets 5% uplift vs a 5000 range weapon 12.5% uplift. 
                off *= (1 + (w.Range / 40000));

                //Doctor: Here follows multipliers which modify the perceived offensive value of weapons based on any modifiers they may have against armour and shields
                //Previously if e.g. a rapid-fire cannon only did 20% damage to armour, it could have amuch higher off rating than a railgun that had less technical DPS but did double armour damage.
                if (w.EffectVsArmor < 1)
                {
                    if (w.EffectVsArmor > 0.75f)      off *= 0.9f;
                    else if (w.EffectVsArmor > 0.5f)  off *= 0.85f;
                    else if (w.EffectVsArmor > 0.25f) off *= 0.8f;
                    else                              off *= 0.75f;
                }
                if (w.EffectVsArmor > 1)
                {
                    if (w.EffectVsArmor > 2.0f)      off *= 1.5f;
                    else if (w.EffectVsArmor > 1.5f) off *= 1.3f;
                    else                             off *= 1.1f;
                }
                if (w.EffectVSShields < 1)
                {
                    if (w.EffectVSShields > 0.75f)      off *= 0.9f;
                    else if (w.EffectVSShields > 0.5f)  off *= 0.85f;
                    else if (w.EffectVSShields > 0.25f) off *= 0.8f;
                    else                                off *= 0.75f;
                }
                if (w.EffectVSShields > 1)
                {
                    if (w.EffectVSShields > 2f)        off *= 1.5f;
                    else if (w.EffectVSShields > 1.5f) off *= 1.3f;
                    else                               off *= 1.1f;
                }

                //Doctor: If there are manual XML override modifiers to a weapon for manual balancing, apply them.
                off *= w.OffPowerMod;

                if (off > 0f && (w.TruePD || w.Range < 1000))
                {
                    float range = 0f;
                    if (w.Range < 1000)
                        range = (1000f - w.Range) * .01f;
                    off /= (2 + range);
                }
                if (w.EMPDamage > 0) off += w.EMPDamage * (1f / w.fireDelay) * .2f;
            }
            if (module.hangarShipUID != null && !module.IsSupplyBay && !module.IsTroopBay)
            {
                if (ShipsDict.TryGetValue(module.hangarShipUID, out Ship hangarShip))
                {
                    off += (hangarShip.BaseStrength > 0f) ? hangarShip.BaseStrength : CalculateBaseStrength(hangarShip);
                }
                else off += 100f;
            }
            return off;
        }

        private static void LoadSmallStars()
        {
            foreach (FileInfo info in GatherFilesModOrVanilla("SmallStars", "xnb"))
            {
                Texture2D tex = ContentManager.Load<Texture2D>(info.CleanResPath());
                SmallStars.Add(tex);
            }
        }

        private static void LoadTechTree()
        {
            bool modTechsOnly = GlobalStats.HasMod && GlobalStats.ActiveModInfo.clearVanillaTechs;
            var techs = LoadEntitiesWithInfo<Technology>("Technology", "LoadTechTree", modTechsOnly);

            foreach (var pair in techs)
            {
                Technology tech = pair.Entity;
                tech.UID = string.Intern(pair.Info.NameNoExt());
                TechTree[tech.UID] = tech;

                // categorize uncategorized techs
                if (tech.TechnologyType != TechnologyType.General)
                    continue;

                if (tech.BuildingsUnlocked.Count > 0)
                {
                    foreach (Technology.UnlockedBuilding buildingU in tech.BuildingsUnlocked)
                    {
                        if (!BuildingsDict.TryGetValue(buildingU.Name, out Building building))
                            continue;
                        if (building.AllowInfantry || building.PlanetaryShieldStrengthAdded > 0 
                                 || building.CombatStrength > 0 || building.isWeapon 
                                 || building.Strength > 0  || building.IsSensor)
                            tech.TechnologyType = TechnologyType.GroundCombat;
                        else if (building.AllowShipBuilding || building.PlusFlatProductionAmount > 0 
                                 || building.PlusProdPerRichness > 0 || building.StorageAdded > 0 
                                 || building.PlusFlatProductionAmount > 0)
                            tech.TechnologyType = TechnologyType.Industry;
                        else if (building.PlusTaxPercentage > 0 || building.CreditsPerColonist > 0)
                            tech.TechnologyType = TechnologyType.Economic;
                        else if (building.PlusFlatResearchAmount > 0 || building.PlusResearchPerColonist > 0)
                            tech.TechnologyType = TechnologyType.Research;
                        else if (building.PlusFoodPerColonist > 0 || building.PlusFlatFoodAmount > 0 
                                 || building.PlusFoodPerColonist > 0 || building.MaxPopIncrease > 0 
                                 || building.PlusFlatPopulation > 0  || building.Name == "Biosspheres" || building.PlusTerraformPoints > 0)
                            tech.TechnologyType = TechnologyType.Colonization;
                    }
                }
                else if (tech.TroopsUnlocked.Count > 0)
                {
                    tech.TechnologyType = TechnologyType.GroundCombat;
                }
                else if (tech.TechnologyType == TechnologyType.General && tech.BonusUnlocked.Count > 0)
                {
                    foreach (Technology.UnlockedBonus bonus in tech.BonusUnlocked)
                    {
                        if (bonus.Type == "SHIPMODULE" || bonus.Type == "HULL")
                            tech.TechnologyType = TechnologyType.ShipGeneral;
                        else if (bonus.Type == "TROOP")
                            tech.TechnologyType = TechnologyType.GroundCombat;
                        else if (bonus.Type == "BUILDING")
                            tech.TechnologyType = TechnologyType.Colonization;
                        else if (bonus.Type == "ADVANCE")
                            tech.TechnologyType = TechnologyType.ShipGeneral;
                    }
                }
                else if (tech.ModulesUnlocked.Count > 0)
                {
                    foreach (Technology.UnlockedMod moduleU in tech.ModulesUnlocked)
                    {
                        if (!ShipModulesDict.TryGetValue(moduleU.ModuleUID, out ShipModule module))
                            continue;

                        if (module.InstalledWeapon != null || module.MaximumHangarShipSize > 0
                            || module.ModuleType == ShipModuleType.Hangar)
                            tech.TechnologyType = TechnologyType.ShipWeapons;
                        else if (module.ShieldPower > 0 
                                 || module.ModuleType == ShipModuleType.Armor
                                 || module.ModuleType == ShipModuleType.Countermeasure
                                 || module.ModuleType == ShipModuleType.Shield)
                            tech.TechnologyType = TechnologyType.ShipDefense;
                        else
                            tech.TechnologyType = TechnologyType.ShipGeneral;
                    }
                }
                else tech.TechnologyType = TechnologyType.General;

                if (tech.HullsUnlocked.Count > 0)
                {
                    tech.TechnologyType = TechnologyType.ShipHull;
                    foreach (Technology.UnlockedHull hull in tech.HullsUnlocked)
                    {
                        ShipData.RoleName role = HullsDict[hull.Name].Role;
                        if (role == ShipData.RoleName.freighter 
                            || role == ShipData.RoleName.platform
                            || role == ShipData.RoleName.construction 
                            || role == ShipData.RoleName.station)
                            tech.TechnologyType = TechnologyType.Industry;
                    }

                }
            }
        }

        private static void LoadToolTips()
        {
            foreach (var tooltips in LoadEntities<Tooltips>("Tooltips", "LoadToolTips"))
            {
                ToolTips.Capacity = tooltips.ToolTipsList.Count;
                foreach (ToolTip tip in tooltips.ToolTipsList)
                {
                    int idx = tip.TIP_ID - 1;
                    while (ToolTips.Count <= idx) ToolTips.Add(null); // sparse List
                    ToolTips[idx] = tip;
                }
            }
        }
        public static ToolTip GetToolTip(int tipId)
        {
            return ToolTips[tipId - 1];
        }

        private static void LoadTroops()
        {
            foreach (var pair in LoadEntitiesWithInfo<Troop>("Troops", "LoadTroops"))
            {
                Troop troop = pair.Entity;
                troop.Name = string.Intern(pair.Info.NameNoExt());
                TroopsDict[troop.Name] = troop;

                if (troop.StrengthMax <= 0)
                    troop.StrengthMax = troop.Strength;
            }
            TroopsDictKeys = new Array<string>(TroopsDict.Keys);
        }

        
        private static void LoadWeapons() // Refactored by RedFox
        {
            foreach (var pair in LoadEntitiesWithInfo<Weapon>("Weapons", "LoadWeapons"))
            {
                Weapon wep = pair.Entity;
                wep.UID = string.Intern(pair.Info.NameNoExt());
                WeaponsDict[wep.UID] = wep;
            }
        }

        //Added by McShooterz: Load ship roles
        private static void LoadShipRoles()
        {
            foreach (var shipRole in LoadEntities<ShipRole>("ShipRoles", "LoadShipRoles"))
            {
                Enum.TryParse(shipRole.Name, out ShipData.RoleName key);
                ShipRoles[key] = shipRole;
            }
        }

        private static void LoadPlanetEdicts()
        {
            foreach (var planetEdict in LoadEntities<PlanetEdict>("PlanetEdicts", "LoadPlanetEdicts"))
                PlanetaryEdicts[planetEdict.Name] = planetEdict;
        }

        private static void LoadEconomicResearchStrats()
        {
            foreach (var pair in LoadEntitiesWithInfo<EconomicResearchStrategy>("EconomicResearchStrategy", "LoadEconResearchStrats"))
            {
                // the story here: some mods have bugged <Name> refs, so we do manual handholding to fix their bugs...
                pair.Entity.Name = pair.Info.NameNoExt();
                EconStrats[pair.Entity.Name] = pair.Entity;
            }
        }

        // Added by RedFox
        private static void LoadBlackboxSpecific()
        {
            if (GlobalStats.HasMod && GlobalStats.ActiveModInfo.useHullBonuses)
            {
                foreach (var hullBonus in LoadEntities<HullBonus>("HullBonuses", "LoadHullBonuses"))
                    HullBonuses[hullBonus.Hull] = hullBonus;
                GlobalStats.ActiveModInfo.useHullBonuses = HullBonuses.Count != 0;
            }

            TryDeserialize("HostileFleets/HostileFleets.xml",    ref HostileFleets);
            TryDeserialize("ShipNames/ShipNames.xml",            ref ShipNames);
            TryDeserialize("MainMenu/MainMenuShipList.xml",      ref MainMenuShipList);
            TryDeserialize("AgentMissions/AgentMissionData.xml", ref AgentMissionData);

            foreach (FileInfo info in GatherFilesUnified("SoundEffects", "xnb"))
            {
                SoundEffect se = ContentManager.Load<SoundEffect>(info.CleanResPath());
                SoundEffectDict[info.NameNoExt()] = se;
            }
        }

        public static void Reset()
        {
            HullsDict.Clear();
            WeaponsDict.Clear();
            TroopsDict.Clear();
            TroopsDictKeys.Clear();
            BuildingsDict.Clear();
            ShipModulesDict.Clear();
            FlagTextures.Clear();
            TechTree.Clear();
            ArtifactsDict.Clear();
            ShipsDict.Clear();
            SoundEffectDict.Clear();
            TextureDict.Clear();
            ToolTips.Clear();
            GoodsDict.Clear();
            Empires.Clear();       
            Encounters.Clear();
            EventsDict.Clear();
            RandomItemsList.Clear();
            ProjectileMeshDict.Clear();
            ProjTextDict.Clear();

            HostileFleets.Fleets.Clear();
            ShipNames.EmpireEntries.Clear();
            MainMenuShipList.ModelPaths.Clear();
            AgentMissionData = new AgentMissionData();

            // @todo Make this work properly:
            // Game1.GameContent.Unload();
        }

        public static Array<string> FindPreviousTechs(Technology target, Array<string> alreadyFound)
        {
            //this is supposed to reverse walk through the tech tree.
            foreach (var techTreeItem in TechTree)
            {
                Technology tech = techTreeItem.Value;
                foreach (Technology.LeadsToTech leadsto in tech.LeadsTo)
                {
                    //if if it finds a tech that leads to the target tech then find the tech that leads to it. 
                    if (leadsto.UID == target.UID)
                    {
                        alreadyFound.Add(target.UID);
                        return FindPreviousTechs(tech, alreadyFound);
                    }
                }
            }
            return alreadyFound;
        }

        public static Video LoadVideo(GameContentManager content, string videoPath)
        {
            return content.Load<Video>("Video/" + videoPath);
        }
    }
}