﻿using System;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Graphics.PackedVector;
using SDUtils;

namespace Ship_Game.Graphics.Particles;

/// <summary>
/// A single reusable block of particle vertices,
/// each vertex can only be active once, until all
/// vertices have died, at which point this buffer is Exhausted
/// and can be Reset for reuse
/// </summary>
public sealed class ParticleVertexBuffer : IDisposable
{
    // the maximum # of particles per buffer,
    // this must be smaller than 10922 (which is ushort.MaxValue / 6)
    public const int Size = 4096;

    public readonly ParticleVertexBufferShared Shared;

    // A vertex buffer holding our particles. This contains the same data as
    // the particles array, but copied across to where the GPU can access it.
    DynamicVertexBuffer VertexBuffer;

    // all of the particles
    readonly ParticleVertex[] Particles;

    // [active..][pending..][free....]
    int FirstActive;
    int FirstPending;
    int FirstFree;

    // number of active particles that can be drawn
    public int ActiveParticles => FirstFree - FirstActive;

    // number of particles pending upload to the GPU
    public int PendingParticles => FirstFree - FirstPending;

    // there are no more free particle slots in this buffer
    public bool IsFull => FirstFree == Size;

    // all particles have died, this buffer is exhausted and can be recycled
    public bool IsExhausted => FirstActive == Size;

    public ParticleVertexBuffer(ParticleVertexBufferShared shared)
    {
        Shared = shared;
        Particles = new ParticleVertex[Size * 4];
        VertexBuffer = new(shared.Device, ParticleVertex.SizeInBytes*Size*4, BufferUsage.WriteOnly);
    }

    public void Reset()
    {
        FirstActive = 0;
        FirstFree = 0;
    }

    /// <summary>
    /// Tries to add a new particle. Returns false if buffer is full
    /// </summary>
    public bool Add(in ParticleVertex srcVertex)
    {
        if (IsFull)
            return false;

        int newIndex = FirstFree++;
        ref ParticleVertex dstVertex0 = ref Particles[newIndex * 4];
        ref ParticleVertex dstVertex1 = ref Particles[newIndex * 4 + 1];
        ref ParticleVertex dstVertex2 = ref Particles[newIndex * 4 + 2];
        ref ParticleVertex dstVertex3 = ref Particles[newIndex * 4 + 3];
        dstVertex0 = srcVertex;
        dstVertex1 = srcVertex;
        dstVertex2 = srcVertex;
        dstVertex3 = srcVertex;
        dstVertex0.Corner = new Short2(-1, -1); // TopLeft
        dstVertex1.Corner = new Short2(+1, -1); // TopRight
        dstVertex2.Corner = new Short2(+1, +1); // BotRight
        dstVertex3.Corner = new Short2(-1, +1); // BotLeft
        return true;
    }

    // @return true if this buffer is exhausted and should be removed
    public bool Update(float totalSimulationTime, float particleDuration, bool isStatic)
    {
        // check whether active particles have reached end of their lifetime
        if (!isStatic)
        {
            while (FirstActive < FirstFree)
            {
                ref ParticleVertex particle = ref Particles[FirstActive * 4];
                float particleAge = totalSimulationTime - particle.Time;
                if (particleAge < particleDuration)
                    break;
                
                // Move the particle from the active to the retired queue.
                ++FirstActive;
            }
        }
        return IsExhausted;
    }

    public void Draw(Effect effect)
    {
        // nothing to draw?
        int firstActive = FirstActive;
        int firstFree = FirstFree;
        int numParticles = firstFree - firstActive;

        var vbo = VertexBuffer;
        if (numParticles <= 0 || vbo == null)
            return;

        // Restore the vertex buffer contents if the graphics device was lost.
        if (vbo.IsContentLost)
        {
            vbo.SetData(Particles);
        }
        else
        {
            // upload any pending particles data to the GPU
            int firstPending = FirstPending;
            int numPending = firstFree - firstPending;
            if (numPending > 0)
            {
                try
                {
                    const int stride = ParticleVertex.SizeInBytes;
                    vbo.SetData(firstPending * stride * 4, Particles,
                                startIndex: firstPending * 4,
                                elementCount: numPending * 4,
                                stride, SetDataOptions.NoOverwrite);
                }
                catch (Exception e) // if this fails for some reason, just send all data
                {
                    Log.Error(e, $"VertexBuffer.SetData failed: firstPending={firstPending} numPending={numPending}");
                    vbo.SetData(Particles);
                }
            }
        }

        // all particles are now submitted
        FirstPending = firstFree;

        GraphicsDevice device = Shared.Device;
        // Set the particle vertex and index buffer.
        device.Vertices[0].SetSource(vbo, 0, ParticleVertex.SizeInBytes);
        device.Indices = Shared.IndexBuffer;
        device.VertexDeclaration = Shared.VertexDeclaration;

        effect.Begin();
        foreach (EffectPass pass in effect.CurrentTechnique.Passes)
        {
            pass.Begin();
            device.DrawIndexedPrimitives(PrimitiveType.TriangleList, 0,
                                         firstActive * 4, numParticles * 4, // 4 points
                                         firstActive * 6, numParticles * 2); // 2 triangles
            pass.End();
        }
        effect.End();
    }

    ~ParticleVertexBuffer()
    {
        Mem.Dispose(ref VertexBuffer);
    }

    public void Dispose()
    {
        Mem.Dispose(ref VertexBuffer);
        GC.SuppressFinalize(this);
    }
}