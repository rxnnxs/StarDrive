using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Ship_Game;
using Ship_Game.Graphics.Particles;

namespace Ship_Game
{
    public class ParticleEmitter
    {
        readonly IParticleSystem ParticleSystem;
        Vector3 PreviousPosition;
        float TimeBetweenParticles;
        float TimeLeftOver;
        public float Scale;

        // Use ParticleSystem NewEmitter() instead
        internal ParticleEmitter(IParticleSystem ps, float particlesPerSecond, float scale, in Vector3 initialPosition)
        {
            ParticleSystem = ps;
            PreviousPosition = initialPosition;
            TimeBetweenParticles = 1f / particlesPerSecond;
            Scale = scale;
        }

        public void SetParticlesPerSecond(float particlesPerSecond)
        {
            TimeBetweenParticles = 1f / particlesPerSecond;
        }

        public void Update(float elapsedTime)
        {
            Update(elapsedTime, PreviousPosition);
        }

        public void Update(float elapsedTime, in Vector3 newPosition)
        {
            Update(elapsedTime, newPosition, 0, 1f);
        }

        public void Update(float elapsedTime, in Vector3 newPosition, float zVelocity, float scale)
        {
            if (elapsedTime > 0f)
            {
                Vector3 velocity = newPosition - PreviousPosition;
                velocity.Z = zVelocity;
                velocity /= elapsedTime;
                float timeToSpend = TimeLeftOver + elapsedTime;
                float currentTime = -TimeLeftOver;
                while (timeToSpend > TimeBetweenParticles)
                {
                    currentTime += TimeBetweenParticles;
                    timeToSpend -= TimeBetweenParticles;
                    float mu = currentTime / elapsedTime;
                    Vector3 pos = Vector3.Lerp(PreviousPosition, newPosition, mu);
                    ParticleSystem.AddParticle(pos, velocity, scale*Scale, Color.White);
                }
                TimeLeftOver = timeToSpend;
            }
            PreviousPosition = newPosition;
        }

        public void Update(float elapsedTime, in Vector3 newPosition, float scale, Color color)
        {
            if (elapsedTime > 0f)
            {
                Vector3 velocity = (newPosition - PreviousPosition) / elapsedTime;
                float timeToSpend = TimeLeftOver + elapsedTime;
                float currentTime = -TimeLeftOver;                
                while (timeToSpend > TimeBetweenParticles)
                {
                    currentTime += TimeBetweenParticles;
                    timeToSpend -= TimeBetweenParticles;
                    float relTime = currentTime / elapsedTime;
                    Vector3 pos = Vector3.Lerp(PreviousPosition, newPosition, relTime);
                    ParticleSystem.AddParticle(pos, velocity, scale*Scale, color);
                }
                TimeLeftOver = timeToSpend;
            }
            PreviousPosition = newPosition;
        }

        public void UpdateProjectileTrail(float elapsedTime, in Vector3 newPosition, in Vector2 pVel)
        {
            if (elapsedTime > 0f)
            {
                Vector3 velocity = pVel.ToVec3();
                float timeToSpend = TimeLeftOver + elapsedTime;
                float currentTime = - TimeLeftOver;

                while (timeToSpend > TimeBetweenParticles)
                {
                    currentTime += TimeBetweenParticles;
                    timeToSpend -= TimeBetweenParticles;
                    float mu = currentTime / elapsedTime;
                    Vector3 position = Vector3.Lerp(PreviousPosition, newPosition, mu);
                    ParticleSystem.AddParticle(position, velocity, Scale, Color.White);
                }
                TimeLeftOver = timeToSpend;
            }
            PreviousPosition = newPosition;
        }
    }
}