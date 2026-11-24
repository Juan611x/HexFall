using System;
using System.Collections.Generic;
using UnityEngine;
using Random = System.Random;

namespace HexFall.Player
{
    // Horizontal wind currents wrapping the air shield: one band renderer per current (pooled in the prefab), all
    // sharing one material. WindCurrentScheduler decides heights, tilts and lifetimes, WindCurrentArc turns a
    // current's life progress into a travelling arc, and the shield build gates the whole set so the currents only
    // show once the shield is built and fade while it assembles or disassembles. MagicShield drives Step each frame.
    // The rings live in the shield's local space (sphere radius 0.5); per-current values go through a
    // MaterialPropertyBlock so the material stays shared.
    public class WindCurrents : MonoBehaviour
    {
        private const float MeshRadius = 0.5f; // the ring mesh is built at the sphere's radius, scaled per height

        [SerializeField] private Renderer[] rings;
        [SerializeField] private float sphereRadius = 0.5f;
        [SerializeField] private float sweepTurns = 1.1f;
        [SerializeField] private float maxArcLength = 0.4f;

        private static readonly int HeadId = Shader.PropertyToID("_Head");
        private static readonly int TailId = Shader.PropertyToID("_Tail");
        private static readonly int FadeId = Shader.PropertyToID("_Fade");
        private static readonly int SeedId = Shader.PropertyToID("_Seed");

        private Random _random;
        private WindCurrentScheduler _scheduler;
        private MaterialPropertyBlock _block;
        private float[] _startTurn;
        private float[] _tiltAxisDegrees;
        private float[] _seed;
        private float[] _lastHeight;
        private float[] _lastProgress;

        public float BuildProgress { get; private set; }

        public IReadOnlyList<WindCurrentState> States
        {
            get
            {
                EnsureScheduler();
                return _scheduler.Currents;
            }
        }

        private void Awake()
        {
            Hide();
        }

        // Test and prefab seam: rings to drive and the random source (null = a fresh random).
        public void Configure(Renderer[] ringRenderers, Random random)
        {
            rings = ringRenderers;
            _random = random;
            _scheduler = null;
            Hide();
        }

        // New shield: the currents start staggered again.
        public void Restart()
        {
            _scheduler = null;
            EnsureScheduler();
        }

        public void Hide()
        {
            if (rings == null) return;
            foreach (var ring in rings)
                if (ring != null) ring.enabled = false;
        }

        // Advance the currents by deltaTime and show them for the shield's current build progress.
        public void Step(float deltaTime, float buildProgress)
        {
            EnsureScheduler();
            BuildProgress = buildProgress;
            _scheduler.Tick(deltaTime);

            float gate = WindCurrentArc.Gate(buildProgress);
            int count = Math.Min(rings.Length, _scheduler.Currents.Count);
            for (int i = 0; i < count; i++)
            {
                var ring = rings[i];
                if (ring == null) continue;

                var state = _scheduler.Currents[i];
                if (state.Progress < _lastProgress[i] || state.Height != _lastHeight[i]) Respawned(i);
                _lastProgress[i] = state.Progress;
                _lastHeight[i] = state.Height;

                var arc = WindCurrentArc.At(state.Progress, _startTurn[i], sweepTurns, maxArcLength);
                float fade = arc.Fade * gate;
                if (gate > 0f) Place(ring.transform, state, _tiltAxisDegrees[i]);
                ring.enabled = fade > 0.001f;
                if (!ring.enabled) continue;

                ring.GetPropertyBlock(_block);
                _block.SetFloat(HeadId, arc.Head);
                _block.SetFloat(TailId, arc.Tail);
                _block.SetFloat(FadeId, fade);
                _block.SetFloat(SeedId, _seed[i]);
                ring.SetPropertyBlock(_block);
            }
        }

        private void Place(Transform ring, WindCurrentState state, float axisDegrees)
        {
            float scale = WindCurrentArc.RingRadius(state.Height, sphereRadius) / MeshRadius;
            ring.localPosition = new Vector3(0f, state.Height * sphereRadius, 0f);
            ring.localScale = new Vector3(scale, 1f, scale);
            var axis = Quaternion.Euler(0f, axisDegrees, 0f) * Vector3.right; // a random horizontal axis
            ring.localRotation = Quaternion.AngleAxis(state.TiltDegrees, axis);
        }

        // A current just respawned: it gets its own start position on the ring, tilt axis and noise seed.
        private void Respawned(int i)
        {
            _startTurn[i] = (float)_random.NextDouble();
            _tiltAxisDegrees[i] = (float)_random.NextDouble() * 360f;
            _seed[i] = (float)_random.NextDouble() * 100f;
        }

        private void EnsureScheduler()
        {
            if (_scheduler != null) return;
            if (rings == null) throw new InvalidOperationException("WindCurrents needs ring renderers");

            _random = _random ?? new Random();
            _block = _block ?? new MaterialPropertyBlock();
            int count = Mathf.Clamp(rings.Length, 3, 4);
            _scheduler = new WindCurrentScheduler(_random, count, -0.6f, 0.6f, 0.3f, 12f, 1.2f, 2.2f);
            _startTurn = new float[count];
            _tiltAxisDegrees = new float[count];
            _seed = new float[count];
            _lastHeight = new float[count];
            _lastProgress = new float[count];
            for (int i = 0; i < count; i++)
            {
                Respawned(i);
                _lastHeight[i] = _scheduler.Currents[i].Height;
                _lastProgress[i] = _scheduler.Currents[i].Progress;
            }
        }
    }
}
