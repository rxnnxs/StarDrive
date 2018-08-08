﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;

namespace Ship_Game
{
    public class UITransitionEffect : UIEffect
    {
        private const float TransitionSpeed = 0.025f;
        private readonly float Offset; // offset multiplier
        private readonly float Direction;

        private readonly Rectangle AnimStart;
        private readonly Rectangle AnimEnd;

        public UITransitionEffect(UIElementV2 e, 
            float distance, float animOffset, float direction) : base(e)
        {
            Offset = animOffset;
            Direction  = direction;
            Animation = direction < 0 ? +1f : 0f;
            AnimStart = e.Rect;
            AnimEnd = AnimStart;
            AnimEnd.X += (int)distance;
        }

        public override bool Update()
        {
            Animation += Direction * TransitionSpeed;
            float animWithOffset = ((Animation - 0.5f * Offset) / 0.5f).Clamped(0f, 1f);

            int dx = (AnimEnd.X - AnimStart.X);
            Element.X = AnimStart.X + animWithOffset * dx;

            if (Direction < 0f && animWithOffset.AlmostEqual(0f) ||
                Direction > 0f && animWithOffset.AlmostEqual(1f))
            {
                GameAudio.PlaySfxAsync("blip_click"); // effect finished!

                return true;
            }
            return false;
        }
    }
}
