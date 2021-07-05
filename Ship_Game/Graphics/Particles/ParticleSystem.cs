using System;
using System.Threading;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Graphics.PackedVector;
using Ship_Game.Data;

namespace Ship_Game
{
    public sealed class ParticleSystem : IDisposable
    {
        // Settings class controls the appearance and animation of this particle system.
        ParticleSettings Settings;

        // For loading the effect file and particle texture.
        readonly GameContentManager Content;

        // Custom effect for drawing particles. This computes the particle
        // animation entirely in the vertex shader: no per-particle CPU work required!
        Effect ParticleEffect;
        
        // Shortcuts for accessing frequently changed effect parameters.
        EffectParameter EffectViewParameter;
        EffectParameter EffectProjectionParameter;
        EffectParameter EffectViewportScaleParameter;
        EffectParameter EffectTimeParameter;
        
        // An array of particles, treated as a circular queue.
        ParticleVertex[] Particles;
        
        // A vertex buffer holding our particles. This contains the same data as
        // the particles array, but copied across to where the GPU can access it.
        DynamicVertexBuffer VertexBuffer;

        // Index buffer turns sets of four vertices into particle quads (pairs of triangles).
        IndexBuffer IndexBuffer;

        VertexDeclaration VertexDeclaration;

        // The particles array and vertex buffer are treated as a circular queue.
        // Initially, the entire contents of the array are free, because no particles
        // are in use. When a new particle is created, this is allocated from the
        // beginning of the array. If more than one particle is created, these will
        // always be stored in a consecutive block of array elements. Because all
        // particles last for the same amount of time, old particles will always be
        // removed in order from the start of this active particle region, so the
        // active and free regions will never be intermingled. Because the queue is
        // circular, there can be times when the active particle region wraps from the
        // end of the array back to the start. The queue uses modulo arithmetic to
        // handle these cases. For instance with a four entry queue we could have:
        //
        //      0
        //      1 - first active particle
        //      2 
        //      3 - first free particle
        //
        // In this case, particles 1 and 2 are active, while 3 and 4 are free.
        // Using modulo arithmetic we could also have:
        //
        //      0
        //      1 - first free particle
        //      2 
        //      3 - first active particle
        //
        // Here, 3 and 0 are active, while 1 and 2 are free.
        //
        // But wait! The full story is even more complex.
        //
        // When we create a new particle, we add them to our managed particles array.
        // We also need to copy this new data into the GPU vertex buffer, but we don't
        // want to do that straight away, because setting new data into a vertex buffer
        // can be an expensive operation. If we are going to be adding several particles
        // in a single frame, it is faster to initially just store them in our managed
        // array, and then later upload them all to the GPU in one single call. So our
        // queue also needs a region for storing new particles that have been added to
        // the managed array but not yet uploaded to the vertex buffer.
        //
        // Another issue occurs when old particles are retired. The CPU and GPU run
        // asynchronously, so the GPU will often still be busy drawing the previous
        // frame while the CPU is working on the next frame. This can cause a
        // synchronization problem if an old particle is retired, and then immediately
        // overwritten by a new one, because the CPU might try to change the contents
        // of the vertex buffer while the GPU is still busy drawing the old data from
        // it. Normally the graphics driver will take care of this by waiting until
        // the GPU has finished drawing inside the VertexBuffer.SetData call, but we
        // don't want to waste time waiting around every time we try to add a new
        // particle! To avoid this delay, we can specify the SetDataOptions.NoOverwrite
        // flag when we write to the vertex buffer. This basically means "I promise I
        // will never try to overwrite any data that the GPU might still be using, so
        // you can just go ahead and update the buffer straight away". To keep this
        // promise, we must avoid reusing vertices immediately after they are drawn.
        //
        // So in total, our queue contains four different regions:
        //
        // Vertices between firstActiveParticle and firstNewParticle are actively
        // being drawn, and exist in both the managed particles array and the GPU
        // vertex buffer.
        //
        // Vertices between firstNewParticle and firstFreeParticle are newly created,
        // and exist only in the managed particles array. These need to be uploaded
        // to the GPU at the start of the next draw call.
        //
        // Vertices between firstFreeParticle and firstRetiredParticle are free and
        // waiting to be allocated.
        //
        // Vertices between firstRetiredParticle and firstActiveParticle are no longer
        // being drawn, but were drawn recently enough that the GPU could still be
        // using them. These need to be kept around for a few more frames before they
        // can be reallocated.
        int FirstActiveParticle;
        int FirstNewParticle;
        int FirstFreeParticle;
        int FirstRetiredParticle;
        
        // This is the actual particle count, which is Particles.Length / 4
        int MaxParticles;

        // Store the current time, in seconds.
        float CurrentTime;

        // Count how many times Draw has been called. This is used to know
        // when it is safe to retire old particles back into the free list.
        int DrawCounter;
        readonly float Scale;

        static readonly Random RandomA = new Random();
        static readonly Random RandomB = new Random();
        readonly GraphicsDevice GraphicsDevice;

        struct ParticleVertex
        {
            // Stores which corner of the particle quad this vertex represents.
            public Short2 Corner;
            // Stores the starting position of the particle.
            public Vector3 Position;
            // Stores the starting velocity of the particle.
            public Vector3 Velocity;
            // Four random values, used to make each particle look slightly different.
            public Color Random;
            // The time (in seconds) at which this particle was created.
            public float Time;

            public static readonly VertexElement[] VertexElements =
            {
                new VertexElement(0, 0,  VertexElementFormat.Short2,  VertexElementMethod.Default, VertexElementUsage.Position, 0),
                new VertexElement(0, 4,  VertexElementFormat.Vector3, VertexElementMethod.Default, VertexElementUsage.Position, 1),
                new VertexElement(0, 16, VertexElementFormat.Vector3, VertexElementMethod.Default, VertexElementUsage.Normal, 0),
                new VertexElement(0, 28, VertexElementFormat.Color,   VertexElementMethod.Default, VertexElementUsage.Color, 0),
                new VertexElement(0, 32, VertexElementFormat.Single,  VertexElementMethod.Default, VertexElementUsage.TextureCoordinate, 0)
            };

            public const int SizeInBytes = 36;
        }

        public ParticleSystem(GameContentManager content, ParticleSettings settings, GraphicsDevice device, float scale, int maxParticles)
        {
            GraphicsDevice = device;
            Content        = content;
            Scale          = scale;
            LoadContent(settings, maxParticles);
        }
        
        void LoadContent(ParticleSettings settings, int maxParticles)
        {
            MaxParticles = maxParticles > 0 ? maxParticles : settings.MaxParticles;
            Settings = settings.Clone();
            Settings.MaxParticles = MaxParticles; 

            LoadParticleEffect();

            VertexDeclaration = new VertexDeclaration(GraphicsDevice, ParticleVertex.VertexElements);
            VertexBuffer = new DynamicVertexBuffer(GraphicsDevice, ParticleVertex.SizeInBytes*MaxParticles*4,
                                                   BufferUsage.WriteOnly);
            
            // Allocate the particle array, and fill in the corner fields (which never change).
            Particles = new ParticleVertex[MaxParticles * 4];

            for (int i = 0; i < MaxParticles; i++)
            {
                Particles[i * 4 + 0].Corner = new Short2(-1, -1); // TopLeft
                Particles[i * 4 + 1].Corner = new Short2(+1, -1); // TopRight
                Particles[i * 4 + 2].Corner = new Short2(+1, +1); // BotRight
                Particles[i * 4 + 3].Corner = new Short2(-1, +1); // BotLeft
            }

            // Create and populate the index buffer.
            ushort[] indices = new ushort[MaxParticles * 6];

            for (int i = 0; i < MaxParticles; i++)
            {
                indices[i * 6 + 0] = (ushort)(i * 4 + 0);
                indices[i * 6 + 1] = (ushort)(i * 4 + 1);
                indices[i * 6 + 2] = (ushort)(i * 4 + 2);

                indices[i * 6 + 3] = (ushort)(i * 4 + 0);
                indices[i * 6 + 4] = (ushort)(i * 4 + 2);
                indices[i * 6 + 5] = (ushort)(i * 4 + 3);
            }

            IndexBuffer = new IndexBuffer(GraphicsDevice, typeof(ushort), indices.Length, BufferUsage.WriteOnly);
            IndexBuffer.SetData(indices);
        }

        void LoadParticleEffect()
        {
            var effect = Content.LoadEffect("3DParticles/ParticleEffect.fx");

            // If we have several particle systems, the content manager will return
            // a single shared effect instance to them all. But we want to preconfigure
            // the effect with parameters that are specific to this particular
            // particle system. By cloning the effect, we prevent one particle system
            // from stomping over the parameter settings of another.
            ParticleEffect = effect.Clone(GraphicsDevice);

            EffectParameterCollection parameters = ParticleEffect.Parameters;

            // Look up shortcuts for parameters that change every frame.
            EffectViewParameter           = parameters["View"];
            EffectProjectionParameter     = parameters["Projection"];
            EffectViewportScaleParameter  = parameters["ViewportScale"];
            EffectTimeParameter           = parameters["CurrentTime"];

            // Set the values of parameters that do not change.
            parameters["Duration"].SetValue((float)Settings.Duration.TotalSeconds);
            parameters["DurationRandomness"].SetValue(Settings.DurationRandomness);
            parameters["Gravity"].SetValue(Settings.Gravity);
            parameters["EndVelocity"].SetValue(Settings.EndVelocity);
            parameters["MinColor"].SetValue(Settings.MinColor.ToVector4());
            parameters["MaxColor"].SetValue(Settings.MaxColor.ToVector4());

            parameters["RotateSpeed"].SetValue(new Vector2(Settings.MinRotateSpeed, Settings.MaxRotateSpeed));
            parameters["StartSize"].SetValue(new Vector2(Settings.MinStartSize, Settings.MaxStartSize) * Scale);
            parameters["EndSize"].SetValue(new Vector2(Settings.MinEndSize, Settings.MaxEndSize) * Scale);
            
            // Load the particle texture, and set it onto the effect.
            var texture = Content.Load<Texture2D>("3DParticles/" + Settings.TextureName);
            parameters["Texture"].SetValue(texture);

            // if Duration > 6.66, use StaticParticles
            // if (Settings.MinRotateSpeed > 0f || Settings.MaxRotateSpeed > 0f) use RotatingParticles
            // else NonRotatingParticles
            ParticleEffect.CurrentTechnique = ParticleEffect.Techniques["Particles"];
        }

        public ParticleEmitter NewEmitter(float particlesPerSecond, Vector3 initialPosition)
        {
            return new ParticleEmitter(this, particlesPerSecond, initialPosition);
        }

        public ParticleEmitter NewEmitter(float particlesPerSecond, Vector3 initialPosition, float zAxisMod)
        {
            initialPosition.Z += zAxisMod;
            return new ParticleEmitter(this, particlesPerSecond, initialPosition);
        }

        public ParticleEmitter NewEmitter(float particlesPerSecond, Vector2 initialCenter, float initialZ = 0f)
        {
            return new ParticleEmitter(this, particlesPerSecond, new Vector3(initialCenter, initialZ));
        }

        /// <summary>
        /// Updates the particle system.
        /// </summary>
        public void Update(DrawTimes elapsed)
        {
            CurrentTime += elapsed.RealTime.Seconds;
            RetireActiveParticles();
            FreeRetiredParticles();

            // If we let our timer go on increasing for ever, it would eventually
            // run out of floating point precision, at which point the particles
            // would render incorrectly. An easy way to prevent this is to notice
            // that the time value doesn't matter when no particles are being drawn,
            // so we can reset it back to zero any time the active queue is empty.

            if (FirstActiveParticle == FirstFreeParticle)
                CurrentTime = 0f;

            if (FirstRetiredParticle == FirstActiveParticle)
                DrawCounter = 0;
        }

        /// <summary>
        /// Helper for checking when active particles have reached the end of
        /// their life. It moves old particles from the active area of the queue
        /// to the retired section.
        /// </summary>
        void RetireActiveParticles()
        {
            var particles = Particles;
            if (particles == null)
                return;

            float particleDuration = (float)Settings.Duration.TotalSeconds;

             // wtf?? "StaticParticles" ? for Star particles and Galaxy particles
            if (particleDuration == 6.66f)
                return;

            while (FirstActiveParticle != FirstNewParticle)
            {
                // Is this particle old enough to retire?
                // We multiply the active particle index by four, because each
                // particle consists of a quad that is made up of four vertices.
                ref ParticleVertex particle = ref particles[FirstActiveParticle * 4];
                float particleAge = CurrentTime - particle.Time;
                if (particleAge < particleDuration)
                    break;
                
                // Remember the time at which we retired this particle.
                particle.Time = DrawCounter;
                
                // Move the particle from the active to the retired queue.
                ++FirstActiveParticle;

                if (FirstActiveParticle >= MaxParticles)
                    FirstActiveParticle = 0;
            }
        }
        
        /// <summary>
        /// Helper for checking when retired particles have been kept around long
        /// enough that we can be sure the GPU is no longer using them. It moves
        /// old particles from the retired area of the queue to the free section.
        /// </summary>
        void FreeRetiredParticles()
        {
            var particles = Particles;
            if (particles == null)
                return;

            while (FirstRetiredParticle != FirstActiveParticle)
            {
                // Has this particle been unused long enough that
                // the GPU is sure to be finished with it?
                // We multiply the retired particle index by four, because each
                // particle consists of a quad that is made up of four vertices.
                int age = DrawCounter - (int)particles[FirstRetiredParticle * 4].Time;

                // The GPU is never supposed to get more than 2 frames behind the CPU.
                // We add 1 to that, just to be safe in case of buggy drivers that
                // might bend the rules and let the GPU get further behind.
                if (age < 3)
                    break;
                
                // Move the particle from the retired to the free queue.
                ++FirstRetiredParticle;

                if (FirstRetiredParticle >= MaxParticles)
                    FirstRetiredParticle = 0;
            }
        }
        
        public void Draw(in Matrix view, in Matrix projection)
        {
            var particles = Particles;
            if (particles == null)
                return;

            EffectViewParameter.SetValue(view);
            EffectProjectionParameter.SetValue(projection);

            // Restore the vertex buffer contents if the graphics device was lost.
            if (VertexBuffer.IsContentLost)
            {
                VertexBuffer.SetData(particles);
            }

            // If there are any particles waiting in the newly added queue,
            // we'd better upload them to the GPU ready for drawing.
            if (FirstNewParticle != FirstFreeParticle)
            {
                AddNewParticlesToVertexBuffer();
            }

            // If there are any active particles, draw them now!
            if (FirstActiveParticle != FirstFreeParticle)
            {
                GraphicsDevice device = GraphicsDevice;
                var rs = device.RenderState;
                rs.AlphaBlendEnable       = true;
                rs.AlphaBlendOperation    = BlendFunction.Add;
                rs.SourceBlend            = Settings.SourceBlend;
                rs.DestinationBlend       = Settings.DestinationBlend;
                rs.AlphaTestEnable        = true;
                rs.AlphaFunction          = CompareFunction.Greater;
                rs.ReferenceAlpha         = 0;
                rs.DepthBufferEnable      = true;
                rs.DepthBufferWriteEnable = false;

                // Set an effect parameter describing the viewport size. This is
                // needed to convert particle sizes into screen space point sizes.
                EffectViewportScaleParameter.SetValue(new Vector2(0.5f / device.Viewport.AspectRatio, -0.5f));

                // Set an effect parameter describing the current time. All the vertex
                // shader particle animation is keyed off this value.
                EffectTimeParameter.SetValue(CurrentTime);
                
                // Set the particle vertex and index buffer.
                device.Vertices[0].SetSource(VertexBuffer, 0, ParticleVertex.SizeInBytes);
                device.Indices = IndexBuffer;
                device.VertexDeclaration = VertexDeclaration;

                ParticleEffect.Begin();
                foreach (EffectPass pass in ParticleEffect.CurrentTechnique.Passes)
                {
                    pass.Begin();

                    if (FirstActiveParticle < FirstFreeParticle)
                    {
                        // If the active particles are all in one consecutive range,
                        // we can draw them all in a single call.
                        int numParticles = (FirstFreeParticle - FirstActiveParticle);
                        device.DrawIndexedPrimitives(PrimitiveType.TriangleList, 0,
                                                     FirstActiveParticle * 4, numParticles * 4, // 4 points
                                                     FirstActiveParticle * 6, numParticles * 2); // 2 triangles
                    }
                    else
                    {
                        int numParticles = (MaxParticles - FirstActiveParticle);
                        device.DrawIndexedPrimitives(PrimitiveType.TriangleList, 0,
                                                     FirstActiveParticle * 4, numParticles * 4, // 4 points
                                                     FirstActiveParticle * 6, numParticles * 2); // 2 triangles
                        if (FirstFreeParticle > 0)
                        {
                            device.DrawIndexedPrimitives(PrimitiveType.TriangleList, 0,
                                                         0, FirstFreeParticle * 4,
                                                         0, FirstFreeParticle * 2);
                        }
                    }
                    pass.End();
                }
                ParticleEffect.End();

                rs.DepthBufferWriteEnable = true;
            }
            ++DrawCounter;
        }

        /// <summary>
        /// Helper for uploading new particles from our managed
        /// array to the GPU vertex buffer.
        /// </summary>
        void AddNewParticlesToVertexBuffer()
        {
            var particles = Particles;
            if (particles == null)
                return;

            const int stride = ParticleVertex.SizeInBytes;
            if (FirstNewParticle < FirstFreeParticle)
            {
                // If the new particles are all in one consecutive range,
                // we can upload them all in a single call.
                VertexBuffer.SetData(FirstNewParticle * stride * 4, particles,
                                     FirstNewParticle * 4,
                                     (FirstFreeParticle - FirstNewParticle) * 4,
                                     stride, SetDataOptions.NoOverwrite);
            }
            else
            {
                // If the new particle range wraps past the end of the queue
                // back to the start, we must split them over two upload calls.
                VertexBuffer.SetData(FirstNewParticle * stride * 4, particles,
                                     FirstNewParticle * 4,
                                     (MaxParticles - FirstNewParticle) * 4,
                                     stride, SetDataOptions.NoOverwrite);

                if (FirstFreeParticle > 0)
                {
                    VertexBuffer.SetData(0, particles,
                                         0, FirstFreeParticle * 4,
                                         stride, SetDataOptions.NoOverwrite);
                }
            }

            // Move the particles we just uploaded from the new to the active queue.
            FirstNewParticle = FirstFreeParticle;
        }

        void AddParticle(Random random, Vector3 position, Vector3 velocity)
        {
            // when Graphics device is reset, this particle system will be disposed
            // and Particles will be set to null
            var particles = Particles;
            if (particles == null)
                return;
            
            // Figure out where in the circular queue to allocate the new particle.
            // Need to increment this index thread-safely because multiple threads will be adding particles
            int nextFreeParticle = Interlocked.Add(ref FirstFreeParticle, 1);
            int firstFreeParticle = nextFreeParticle - 1;

            // reset during exact overflow (concurrent increment)
            if (nextFreeParticle == MaxParticles)
                FirstFreeParticle = 0;

            // If there are no free particles, we just have to give up.
            if (firstFreeParticle == FirstRetiredParticle || 
                firstFreeParticle >= MaxParticles) // or we ran into a concurrent increment issue
                return;

            // Adjust the input velocity based on how much
            // this particle system wants to be affected by it.
            velocity *= Settings.EmitterVelocitySensitivity;

            // Add in some random amount of horizontal velocity.
            float horizontalVelocity = Settings.MinHorizontalVelocity.LerpTo(Settings.MaxHorizontalVelocity, (float)random.NextDouble());
            float horizontalAngle = (float)random.NextDouble() * RadMath.TwoPI;
            velocity.X += horizontalVelocity * RadMath.Cos(horizontalAngle);
            velocity.Z += horizontalVelocity * RadMath.Sin(horizontalAngle);

            // Add in some random amount of vertical velocity.
            velocity.Y += Settings.MinVerticalVelocity.LerpTo(Settings.MaxVerticalVelocity, (float)random.NextDouble());
            
            // Choose four random control values. These will be used by the vertex
            // shader to give each particle a different size, rotation, and color.
            var randomValues = new Color((byte)random.Next(255),
                                         (byte)random.Next(255),
                                         (byte)random.Next(255),
                                         (byte)random.Next(255));

            // Fill in the particle vertex structure.
            for (int i = 0; i < 4; i++)
            {
                ref ParticleVertex particle = ref particles[firstFreeParticle * 4 + i];
                particle.Position = position;
                particle.Velocity = velocity;
                particle.Random = randomValues;
                particle.Time = CurrentTime;
            }
        }

        public void AddParticleThreadA(Vector3 position, Vector3 velocity) => AddParticle(RandomA, position, velocity);
        public void AddParticleThreadB(Vector3 position, Vector3 velocity) => AddParticle(RandomB, position, velocity);
        public void AddParticleThread(bool randomA, Vector3 position, Vector3 velocity) => AddParticle(randomA ? RandomA : RandomB, position, velocity);
        

        public void Dispose()
        {
            Particles = null;
            VertexBuffer?.Dispose(ref VertexBuffer);
            IndexBuffer?.Dispose(ref IndexBuffer);
            VertexDeclaration?.Dispose(ref VertexDeclaration);
            GC.SuppressFinalize(this);
        }

        ~ParticleSystem()
        {
            VertexBuffer?.Dispose(ref VertexBuffer);
            IndexBuffer?.Dispose(ref IndexBuffer);
            VertexDeclaration?.Dispose(ref VertexDeclaration);
        }
    }
}