using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Newtonsoft.Json;
using Ship_Game.Ships;
using System;
using System.Xml.Serialization;

namespace Ship_Game
{
    public sealed class Troop
    {
        [Serialize(0)] public string Name;
        [Serialize(1)] public string RaceType;
        [Serialize(2)] public int first_frame = 1;
        [Serialize(3)] public bool animated;
        [Serialize(4)] public string idle_path;
        [Serialize(5)] public string Icon;
        [Serialize(6)] public string MovementCue;
        [Serialize(7)] public int Level;
        [Serialize(8)] public int AttackTimerBase = 10;
        [Serialize(9)] public int MoveTimerBase = 10;
        [Serialize(10)] public int num_idle_frames;
        [Serialize(11)] public int num_attack_frames;
        [Serialize(12)] public int idle_x_offset;
        [Serialize(13)] public int idle_y_offset;
        [Serialize(14)] public int attack_width = 128;
        [Serialize(15)] public string attack_path;
        [Serialize(16)] public bool facingRight;
        [Serialize(17)] public string Description;
        [Serialize(18)] public string OwnerString;
        [Serialize(19)] public int BoardingStrength;
        [Serialize(20)] public int MaxStoredActions = 1;
        [Serialize(21)] public float MoveTimer;   // FB - use UpdateMoveTimer or ResetMoveTimer
        [Serialize(22)] public float AttackTimer; // FB - use UpdateAttackTimer or ResetAttackTimer
        [Serialize(23)] public float MovingTimer = 1f;
        [Serialize(24)] public int AvailableMoveActions   = 1; // FB - use UpdateMoveActions 
        [Serialize(25)] public int AvailableAttackActions = 1; // FB - use UpdateAttackActions
        [Serialize(26)] public string TexturePath;
        [Serialize(27)] public bool Idle = true;
        [Serialize(28)] public int WhichFrame = 1;
        [Serialize(29)] public float Strength; // FB - Do not modify this directly. use DamageTroop and HealTroop
        [Serialize(30)] public float StrengthMax; 
        [Serialize(31)] public int HardAttack; // FB - use NetHardAttack
        [Serialize(32)] public int SoftAttack; // FB - use NetSoftAttack
        [Serialize(33)] public string Class;
        [Serialize(34)] public int Kills;
        [Serialize(35)] public TargetType TargetType;
        [Serialize(36)] public int Experience;
        [Serialize(37)] public float Cost;
        [Serialize(38)] public string sound_attack;
        [Serialize(39)] public int Range;
        [Serialize(40)] public float Launchtimer = 10f; // FB - use UpdateLaunchTimer or ResetLaunchTimer
        [Serialize(41)] public string Type;

        [XmlIgnore][JsonIgnore] public Planet HostPlanet { get; private set; }
        [XmlIgnore][JsonIgnore] private Empire Owner;
        [XmlIgnore][JsonIgnore] public Ship HostShip { get; private set; }
        [XmlIgnore][JsonIgnore] public Rectangle FromRect { get; private set; }

        [XmlIgnore][JsonIgnore] private float UpdateTimer;        
        [XmlIgnore][JsonIgnore] public string DisplayName    => DisplayNameEmpire(Owner);
        [XmlIgnore] [JsonIgnore] public float ActualCost     => Cost * CurrentGame.Pace;
        [XmlIgnore] [JsonIgnore] public bool CanMove         => AvailableMoveActions > 0;
        [XmlIgnore] [JsonIgnore] public bool CanAttack       => AvailableAttackActions > 0;
        [XmlIgnore] [JsonIgnore] public int ActualHardAttack => (int)(HardAttack + 0.1f * Level * HardAttack);
        [XmlIgnore] [JsonIgnore] public int ActualSoftAttack => (int)(SoftAttack + 0.1f * Level * SoftAttack);
        [XmlIgnore] [JsonIgnore] public Empire Loyalty       => Owner ?? (Owner = EmpireManager.GetEmpireByName(OwnerString));
        [XmlIgnore] [JsonIgnore] public int ActualRange      => Level < 3 ? Range : Range + 1;  // veterans have bigger range

        public string DisplayNameEmpire(Empire empire = null)
        {
            empire = Owner ?? empire;
            if (empire == null || !empire.data.IsRebelFaction) return Name;
            return Localizer.Token(empire.data.TroopNameIndex);
        }

        public Troop Clone()
        {
            var t        = (Troop)MemberwiseClone();
            t.HostPlanet = null;
            t.Owner      = null;
            t.HostShip   = null;
            return t;
        }

        public void DoAttack()
        {
            Idle       = false;
            WhichFrame = first_frame;
        }

        public void UpdateAttackActions(int amount)
        {
            AvailableAttackActions = (AvailableAttackActions + amount).Clamped(0, MaxStoredActions);
        }

        public void UpdateMoveActions(int amount)
        {
            AvailableMoveActions = (AvailableMoveActions + amount).Clamped(0, MaxStoredActions);
        }

        public void UpdateMoveTimer(float amount)
        {
            if (!CanMove) MoveTimer += amount;
        }

        public void UpdateAttackTimer(float amount)
        {
            if (!CanAttack) AttackTimer += amount;
        }

        public void UpdateLaunchTimer(float amount)
        {
            Launchtimer += amount;
        }

        public void ResetMoveTimer()
        {
            MoveTimer = Math.Max(MoveTimerBase - (int)(Level * 0.5), 5);
        }

        public void ResetAttackTimer()
        {
            AttackTimer = Math.Max(AttackTimerBase - (int)(Level * 0.5), 5);
        }

        public void ResetLaunchTimer()
        {
            Launchtimer = MoveTimerBase; // FB -  yup, MoveTimerBase
        }

        private string WhichFrameString => WhichFrame.ToString("00");

        [XmlIgnore][JsonIgnore]
        public SubTexture TextureDefault => ResourceManager.Texture("Troops/"+TexturePath);

        //@HACK the animation index and firstframe value are coming up with bad values for some reason. i could not figure out why
        //so here i am forcing it to draw troop template first frame if it hits a problem. in the update method i am refreshing the firstframe value as well. 
        private SubTexture TextureIdleAnim   => ResourceManager.TextureOrDefault(
            "Troops/" + idle_path+WhichFrameString, 
            "Troops/" + idle_path+ResourceManager.GetTroopTemplate(Name).first_frame.ToString("0000"));

        private SubTexture TextureAttackAnim => ResourceManager.TextureOrDefault(
            "Troops/" + attack_path + WhichFrameString, 
            "Troops/" + idle_path + ResourceManager.GetTroopTemplate(Name).first_frame.ToString("0000"));

        public string StrengthText => $"Strength: {Strength:0.}";

        //@todo split this into methods of animated and non animated. or always draw animated and move the animation logic 
        // to a central location to be used by any animated image. 
        public void Draw(SpriteBatch spriteBatch, Rectangle drawRect)
        {
            if (!facingRight)
            {
                DrawFlip(spriteBatch, drawRect);
                return;
            }
            if (!animated)
            {
                spriteBatch.Draw(TextureDefault, drawRect, Color.White);
                return;
            }
            if (Idle)
            {
                var sourceRect = new Rectangle(idle_x_offset, idle_y_offset, 128, 128);
                spriteBatch.Draw(TextureIdleAnim, drawRect, sourceRect, Color.White);
                return;
            }

            float scale     = drawRect.Width / 128f;
            drawRect.Width  = (int)(attack_width * scale);
            var sourceRect2 = new Rectangle(idle_x_offset, idle_y_offset, attack_width, 128);

            SubTexture attackTexture = TextureAttackAnim;
            if (attackTexture.Height <= 128)
            {
                spriteBatch.Draw(attackTexture, drawRect, sourceRect2, Color.White);
                return;
            }
            sourceRect2.Y      -= idle_y_offset;
            sourceRect2.Height += idle_y_offset;
            Rectangle r         = drawRect;
            r.Y                -= (int)(scale * idle_y_offset);
            r.Height           += (int)(scale * idle_y_offset);
            spriteBatch.Draw(attackTexture, r, sourceRect2, Color.White);
        }

        public void DrawFlip(SpriteBatch spriteBatch, Rectangle drawRect)
        {
            if (!animated)
            {
                spriteBatch.Draw(TextureDefault, drawRect, Color.White, 0f, Vector2.Zero, SpriteEffects.FlipHorizontally, 1f);
                return;
            }
            if (Idle)
            {
                var sourceRect = new Rectangle(idle_x_offset, idle_y_offset, 128, 128);
                spriteBatch.Draw(TextureIdleAnim, drawRect, sourceRect, Color.White, 0f, Vector2.Zero, SpriteEffects.FlipHorizontally, 1f);
                return;
            }
            float scale = drawRect.Width / 128f;
            drawRect.X = drawRect.X - (int)(attack_width * scale - drawRect.Width);
            drawRect.Width = (int)(attack_width * scale);

            Rectangle sourceRect2 = new Rectangle(idle_x_offset, idle_y_offset, attack_width, 128);
            var attackTexture = TextureAttackAnim;
            if (attackTexture.Height <= 128)
            {
                spriteBatch.Draw(attackTexture, drawRect, sourceRect2, Color.White, 0f, Vector2.Zero, SpriteEffects.FlipHorizontally, 1f);
                return;
            }
            sourceRect2.Y      -= idle_y_offset;
            sourceRect2.Height += idle_y_offset;
            Rectangle r         = drawRect;
            r.Height           += (int)(scale * idle_y_offset);
            r.Y                -= (int)(scale * idle_y_offset);
            spriteBatch.Draw(attackTexture, r, sourceRect2, Color.White, 0f, Vector2.Zero, SpriteEffects.FlipHorizontally, 1f);
        }

        public void DrawIcon(SpriteBatch spriteBatch, Rectangle drawRect)
        {
            var iconTexture = ResourceManager.Texture("TroopIcons/" + Icon + "_icon");
            spriteBatch.Draw(iconTexture, drawRect, Color.White);
        }

        public Ship Launch()
        {
            if (HostPlanet == null)
                return null;

            foreach (PlanetGridSquare pgs in HostPlanet.TilesList)
            {
                if (!pgs.TroopsHere.Contains(this))
                    continue;

                pgs.TroopsHere.Clear();
                HostPlanet.TroopsHere.Remove(this);
            }
            Ship retShip = Ship.CreateTroopShipAtPoint(Owner.data.DefaultTroopShip, Owner, HostPlanet.Center, this);
            HostPlanet = null;
            return retShip;
        }

        public void SetFromRect(Rectangle from)
        {
            FromRect = from;
        }

        public void SetOwner(Empire e)
        {
            Owner = e;
            if (e != null)
                OwnerString = e.data.Traits.Name;
        }

        public void SetPlanet(Planet newPlanet)
        {
            HostPlanet = newPlanet;
            if (HostPlanet != null && !HostPlanet.TroopsHere.Contains(this))
            {
                HostPlanet.TroopsHere.Add(this);
            }
        }

        public void SetShip(Ship s)
        {
            HostShip = s;
        }

        public void Update(float elapsedTime)
        {
            Troop troop        = this;
            troop.UpdateTimer -= elapsedTime;
            if (UpdateTimer > 0f)
                return;
            troop.first_frame = ResourceManager.GetTroopTemplate(troop.Name).first_frame;
            int whichFrame    = WhichFrame;
            if (!Idle)
            {
                UpdateTimer = 0.75f / num_attack_frames;                
                whichFrame++;
                if (whichFrame <= num_attack_frames - (first_frame == 1 ? 0 : 1))
                {
                    WhichFrame++;
                    return;
                }

                WhichFrame = first_frame;
                Idle       = true;
            }
            else
            {
                UpdateTimer = 1f / num_idle_frames;
                whichFrame++;
                if (whichFrame <= num_idle_frames - (first_frame == 1 ? 0 : 1))
                {
                    WhichFrame++;
                    return;
                }

                WhichFrame = first_frame;
            }
        }

        //Added by McShooterz
        public void AddKill()
        {
            Kills++;
            Experience++;
            if (Experience != 1 + Level)
                return;
            Experience -= 1 + Level;
            Level++;
        }

        public void DamageTroop(float amount)
        {
            Strength = (Strength - amount).Clamped(0, ActualStrengthMax);
        }

        public void HealTroop(float amount)
        {
            DamageTroop(-amount);
        }

        public float ActualStrengthMax
        {
            get
            {
                if (StrengthMax <= 0)
                    StrengthMax = ResourceManager.GetTroopTemplate(Name).Strength;

                float modifiedStrength = (StrengthMax + Level) * (1 + Owner?.data.Traits.GroundCombatModifier ?? 1f);
                return (float)Math.Round(modifiedStrength, 0);
            }
        }

        public bool AssignTroopToNearestAvailableTile(Troop t, PlanetGridSquare tile, Planet planet )
        {
            Array<PlanetGridSquare> list = new Array<PlanetGridSquare>();
            foreach (PlanetGridSquare pgs in planet.TilesList)
            {
                if (pgs.TroopsHere.Count < pgs.MaxAllowedTroops
                    && (pgs.building == null || pgs.building != null && pgs.building.CombatStrength == 0)
                    && (Math.Abs(tile.x - pgs.x) <= 1 && Math.Abs(tile.y - pgs.y) <= 1))
                    list.Add(pgs);
            }

            if (list.Count <= 0)
                return false;

            int index = (int)RandomMath.RandomBetween(0.0f, list.Count);
            PlanetGridSquare planetGridSquare1 = list[index];
            foreach (PlanetGridSquare planetGridSquare2 in planet.TilesList)
            {
                if (planetGridSquare2 != planetGridSquare1)
                    continue;

                planetGridSquare2.TroopsHere.Add(t);
                planet.TroopsHere.Add(t);
                t.SetPlanet(planet);
                return true;
            }
            return false;

        }

        public bool AssignTroopToTile(Planet planet = null)
        {
            planet = planet ?? HostPlanet;
            var list = new Array<PlanetGridSquare>();
            foreach (PlanetGridSquare planetGridSquare in planet.TilesList)
            {
                if (planetGridSquare.TroopsHere.Count < planetGridSquare.MaxAllowedTroops 
                    && (planetGridSquare.building == null || planetGridSquare.building != null && planetGridSquare.building.CombatStrength == 0))
                    list.Add(planetGridSquare);
            }
            if (list.Count > 0)
            {
                int index = (int)RandomMath.RandomBetween(0.0f, list.Count);
                PlanetGridSquare planetGridSquare = list[index];
                foreach (PlanetGridSquare eventLocation in planet.TilesList)
                {
                    if (eventLocation != planetGridSquare) continue;

                    eventLocation.TroopsHere.Add(this);
                    planet.TroopsHere.Add(this);
                    if (Owner != planet.Owner)
                        Strength = (Strength - planet.TotalInvadeInjure).Clamped(0, ActualStrengthMax);

                    SetPlanet(planet);
                    if (!eventLocation.EventOnTile || eventLocation.NoTroopsOnTile || eventLocation.SingleTroop.Loyalty.isFaction)
                        return true;
                    ResourceManager.Event(eventLocation.building.EventTriggerUID).TriggerPlanetEvent(planet, eventLocation.SingleTroop.Loyalty, eventLocation, Empire.Universe);
                }
            }
            return false;
        }

    }
}