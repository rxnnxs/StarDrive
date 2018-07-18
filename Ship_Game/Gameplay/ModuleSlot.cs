using Microsoft.Xna.Framework;
using System;
using System.Xml.Serialization;

namespace Ship_Game.Gameplay
{
    public sealed class ModuleSlotData
    {
        public Vector2 Position;
        public string InstalledModuleUID;
        public Guid HangarshipGuid;
        public float Health;
        [XmlElement(ElementName = "Shield_Power")]
        public float ShieldPower;
        [XmlElement(ElementName = "ShieldUpChance")]
        public float ShieldUpChance;
        [XmlElement(ElementName = "ShieldPowerBeforeWarp")]
        public float ShieldPowerBeforeWarp;
        [XmlElement(ElementName = "facing")]
        public float Facing;
        [XmlElement(ElementName = "state")]
        public string Orientation;
        public Restrictions Restrictions;
        public string SlotOptions;
        

        public override string ToString() => $"{InstalledModuleUID} {Position} {Facing} {Restrictions}";

        public ModuleOrientation GetOrientation()
        {
            if (Orientation.NotEmpty() && Orientation != "Normal")
                return (ModuleOrientation)Enum.Parse(typeof(ModuleOrientation), Orientation);
            return ModuleOrientation.Normal;
        }
    }
}