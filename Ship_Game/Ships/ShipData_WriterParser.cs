﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using Microsoft.Xna.Framework;
using Ship_Game.Data;
using Ship_Game.Data.Serialization.Types;
using Ship_Game.Gameplay;

namespace Ship_Game.Ships
{
    // NOTE: public variables are SERIALIZED
    public partial class ShipData
    {
        public void Save(FileInfo file)
        {
            ShipDataWriter sw = CreateShipDataText();
            sw.FlushToFile(file);
        }

        ShipDataWriter CreateShipDataText()
        {
            var sw = new ShipDataWriter();
            sw.Write("Version", CurrentVersion);
            sw.Write("Name", Name);
            sw.Write("Hull", Hull);
            sw.Write("Role", Role);
            sw.Write("ModName", ModName);
            sw.Write("Style", ShipStyle);
            sw.Write("Description", Description);
            sw.Write("Size", $"{GridInfo.Size.X},{GridInfo.Size.Y}");
            
            if (IconPath != BaseHull.IconPath)
                sw.Write("IconPath", IconPath);
            if (SelectionGraphic != BaseHull.SelectIcon)
                sw.Write("SelectIcon", SelectionGraphic);
            if (FixedCost > 0)
                sw.Write("FixedCost", FixedCost);
            if (FixedUpkeep > 0f)
                sw.Write("FixedUpkeep", FixedUpkeep);
            
            if (UnLockable)
                sw.Write("Unlockable", UnLockable);
            if (HullUnlockable)
                sw.Write("HullUnlockable", HullUnlockable);
            if (!AllModulesUnlockable) // default is true
                sw.Write("AllModulesUnlockable", AllModulesUnlockable);

            sw.Write("DefaultAIState", DefaultAIState);
            sw.Write("DefaultCombatState", DefaultCombatState);
            sw.Write("EventOnDeath", EventOnDeath); // "DefeatedMothership" remnant event

            ushort[] slotModuleUIDAndIndex = CreateModuleIndexMapping(ModuleSlots, out Array<string> moduleUIDs);

            var moduleLines = new Array<string>();
            for (int i = 0; i < ModuleSlots.Length; ++i)
            {
                string slotString = DesignSlotString(ModuleSlots[i], slotModuleUIDAndIndex[i]);
                moduleLines.Add(slotString);
            }

            sw.WriteLine("# Maps module UIDs to Index, first UID has index 0");
            sw.Write("ModuleUIDs", string.Join(";", moduleUIDs));
            sw.Write("Modules", moduleLines.Count);
            sw.WriteLine("# gridX,gridY;moduleUIDIndex;sizeX,sizeY;turretAngle;moduleRot;hangarShipUID");
            foreach (string m in moduleLines)
                sw.WriteLine(m);
            return sw;
        }
        
        // X,Y,moduleIdx[,sizeX,sizeY,turretAngle,moduleRot,hangarShipUid]
        static string DesignSlotString(DesignSlot slot, ushort moduleIdx)
        {
            Point gp = slot.Pos;
            var sz = slot.GetSize();
            var ta = slot.TurretAngle;
            var mr = (int)slot.ModuleRot;

            string[] fields = new string[6];
            fields[0] = gp.X + "," + gp.Y;
            fields[1] = moduleIdx.ToString();
            // everything after this is optional
            fields[2] = (sz.X == 1 && sz.Y == 1) ? "" : sz.X + "," + sz.Y;
            fields[3] = ta == 0 ? "" : ta.ToString();
            fields[4] = mr == 0 ? "" : mr.ToString();
            fields[5] = slot.HangarShipUID.IsEmpty() ? "" : slot.HangarShipUID;

            int count = GetMaxValidFields(fields);
            return string.Join(";", fields, 0, count);
        }
        
        // get the max span of valid elements, so we can discard empty ones and save space
        static int GetMaxValidFields(string[] fields)
        {
            int count = fields.Length;
            for (; count > 0; --count)
                if (fields[count - 1] != "")
                    break;
            return count;
        }

        // maps each DesignSlot with a (ModuleUID,ModuleUIDIndex)
        static ushort[] CreateModuleIndexMapping(DesignSlot[] saved, out Array<string> moduleUIDs)
        {
            var slotModuleUIDAndIndex = new ushort[saved.Length];
            var moduleUIDsToIdx = new Map<string, int>();
            moduleUIDs = new Array<string>();
            
            for (int i = 0, count = 0; i < saved.Length; ++i)
            {
                string uid = saved[i].ModuleUID;
                if (moduleUIDsToIdx.TryGetValue(uid, out int moduleUIDIdx))
                {
                    slotModuleUIDAndIndex[i] = (ushort)moduleUIDIdx;
                }
                else
                {
                    slotModuleUIDAndIndex[i] = (ushort)count;
                    moduleUIDs.Add(uid);
                    moduleUIDsToIdx.Add(uid, count);
                    ++count;
                }
            }
            return slotModuleUIDAndIndex;
        }
        
        static ShipData ParseDesign(FileInfo file)
        {
            using (var p = new GenericStringViewParser(file))
            {
                if (!p.ReadLine(out StringView firstLine) && !firstLine.StartsWith("Version"))
                    throw new InvalidDataException($"Ship design must start with a Version=? tag! File={file}");

                firstLine.Next('=');
                int version = firstLine.ToInt();
                if (version != CurrentVersion)
                {
                    if (version == 0)
                        throw new InvalidDataException($"Ship design version is invalid: {firstLine.Text} File={file}");

                    // TODO: convert from this version to newer version
                }
                return new ShipData(p);
            }
        }

        ShipData(GenericStringViewParser p)
        {
            string[] moduleUIDs = null;
            DesignSlot[] modules = null;
            int numModules = 0;

            while (p.ReadLine(out StringView line))
            {
                if (modules == null || moduleUIDs == null)
                {
                    StringView key = line.Next('=');
                    StringView value = line;

                    if      (key == "Name") Name = value.Text;
                    else if (key == "Hull") Hull = value.Text;
                    else if (key == "Role") Enum.TryParse(value.Text, out Role);
                    else if (key == "Style")       ShipStyle = value.Text;
                    else if (key == "Description") Description = value.Text;
                    else if (key == "Size")        GridInfo.Size = PointSerializer.FromString(value);
                    else if (key == "IconPath")    IconPath = value.Text;
                    else if (key == "SelectIcon")  SelectionGraphic = value.Text;
                    else if (key == "FixedCost")   FixedCost = value.ToInt();
                    else if (key == "FixedUpkeep") FixedUpkeep = value.ToFloat();
                    else if (key == "Unlockable")           UnLockable = value.ToBool();
                    else if (key == "HullUnlockable")       HullUnlockable = value.ToBool();
                    else if (key == "AllModulesUnlockable") AllModulesUnlockable = value.ToBool();
                    else if (key == "DefaultAIState") Enum.TryParse(value.Text, out DefaultAIState);
                    else if (key == "DefaultCombatState")    Enum.TryParse(value.Text, out DefaultCombatState);
                    else if (key == "EventOnDeath")   EventOnDeath = value.Text;
                    else if (key == "ModuleUIDs")
                        moduleUIDs = value.Split(';').Select(s => string.Intern(s.Text));
                    else if (key == "Modules")
                        modules = new DesignSlot[value.ToInt()];
                }
                else
                {
                    if (numModules == modules.Length)
                        throw new InvalidDataException($"Ship design module count is incorrect: {p.Name}");

                    DesignSlot slot = ParseDesignSlot(line, moduleUIDs);
                    modules[numModules++] = slot;
                }
            }

            if (!ResourceManager.Hull(Hull, out ShipHull hull))
                throw new InvalidDataException($"Hull {Hull} not found");

            ThrusterList = hull.Thrusters;
            ShipStyle = hull.Style;
            IconPath = hull.IconPath;
            ModelPath = hull.ModelPath;
            Animated = hull.Animated;
            IsShipyard = hull.IsShipyard;
            IsOrbitalDefense = hull.IsOrbitalDefense;

            //if (Name.Contains("Acolyte of Flak II"))
            //    Debugger.Break();

            GridInfo.SurfaceArea = hull.Area;

            ModuleSlots = modules;
            
            UpdateBaseHull();
        }

        static DesignSlot ParseDesignSlot(StringView line, string[] moduleUIDs)
        {
            StringView pt = line.Next(';');
            StringView index = line.Next(';');
            StringView sz = line.Next(';');
            StringView turretAngle = line.Next(';');
            StringView moduleRotation = line.Next(';');
            StringView slotOptions = line.Next(';');

            return new DesignSlot(
                PointSerializer.FromString(pt),
                moduleUIDs[index.ToInt()],
                sz.IsEmpty ? new Point(1,1) : PointSerializer.FromString(sz),
                turretAngle.IsEmpty ? 0 : turretAngle.ToInt(),
                moduleRotation.IsEmpty ? ModuleOrientation.Normal
                                        : (ModuleOrientation)moduleRotation.ToInt(),
                slotOptions.IsEmpty ? null : slotOptions.Text
            );
        }
        
        public static string GetBase64ModulesString(Ship ship)
        {
            ModuleSaveData[] saved = ship.GetModuleSaveData();
            ushort[] slotModuleUIDAndIndex = CreateModuleIndexMapping(saved, out Array<string> moduleUIDs);

            var sw = new ShipDataWriter();
            sw.Write("1\n"); // first line is version

            // module1;module2;module3\n
            for (int i = 0; i < moduleUIDs.Count; ++i)
            {
                sw.Write(moduleUIDs[i]);
                if (i != (moduleUIDs.Count - 1))
                    sw.Write(';');
            }
            sw.Write('\n');

            // number of modules
            sw.WriteLine(saved.Length.ToString());
            
            // each module takes two lines
            // first line is DesignModule, second line is ModuleSaveData fields
            for (int i = 0; i < saved.Length; ++i)
            {
                ModuleSaveData slot = saved[i];
                string slotString = DesignSlotString(slot, slotModuleUIDAndIndex[i]);
                sw.WriteLine(slotString);

                string[] fields = new string[3];
                fields[0] = slot.Health > 0 ? slot.Health.String(1) : "";
                fields[1] = slot.ShieldPower > 0 ? slot.ShieldPower.String(1) : "";
                fields[2] = slot.HangarShip != Guid.Empty ? slot.HangarShip.ToString() : "";
                
                int count = GetMaxValidFields(fields);
                string stateString = string.Join(";", fields, 0, count);
                sw.WriteLine(stateString);
            }

            byte[] ascii = sw.GetASCIIBytes();
            return Convert.ToBase64String(ascii, Base64FormattingOptions.None);
        }

        public static ModuleSaveData[] GetModuleSaveFromBase64String(string base64string)
        {
            byte[] bytes = Convert.FromBase64String(base64string);
            var p = new GenericStringViewParser("save", new StreamReader(new MemoryStream(bytes), Encoding.ASCII));

            int version = p.ReadLine().ToInt();
            if (version != 1)
            {
                Log.Error($"Unsupported ModuleSaveData version: {version}");
                return null;
            }

            string[] moduleUIDs = p.ReadLine().Split(';').Select(s => string.Intern(s.Text));
            int numModules = p.ReadLine().ToInt();
            var modules = new ModuleSaveData[numModules];
            
            for (int i = 0; i < modules.Length; ++i)
            {
                StringView line1 = p.ReadLine();
                DesignSlot s = ParseDesignSlot(line1, moduleUIDs);

                StringView line2 = p.ReadLine();
                StringView hp = line2.Next(';');
                StringView sp = line2.Next(';');
                StringView hs = line2.Next(';');

                var msd = new ModuleSaveData(s,
                    hp.IsEmpty ? 0 : hp.ToFloat(),
                    sp.IsEmpty ? 0 : sp.ToFloat(),
                    hs.IsEmpty ? Guid.Empty : Guid.Parse(hs.ToString())
                );
                modules[i] = msd;
            }

            return modules;
        }
    }
}
