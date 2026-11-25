using System.Linq;
using System.Reflection;
using HexFall.Player;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.Rendering;
using UnityEngine;
using UnityEngine.Rendering;

namespace HexFall.UI.Tests
{
    // The horizontal wind currents wrapping the air shield: a pure arc/fade function, a ring band mesh, the
    // HexFall/WindCurrent shader and the WindCurrents adapter driven by WindCurrentScheduler and gated by the build.
    public class WindCurrentsTests
    {
        private const string Dir = "Assets/_Project/VFX/WindShield/";
        private const string ShaderPath = Dir + "Shaders/WindCurrent.shader";
        private const string SharedPath = Dir + "Shaders/WindShieldShared.hlsl";
        private const string CorePath = Dir + "Shaders/WindShieldCore.hlsl";
        private const string MaterialPath = Dir + "Materials/M_WindCurrent.mat";
        private const string MeshPath = Dir + "Models/AirCurrentRing.fbx";
        private const string TracePath = Dir + "Textures/T_AirTrace.png";
        private const string PlayerPrefab = "Assets/_Project/Prefabs/Characters/PlayerWizard.prefab";

        // ---- pure arc / gate -------------------------------------------------------------------------------------

        [Test]
        public void Arc_starts_empty_and_invisible()
        {
            var arc = WindCurrentArc.At(0f, 0.25f);

            Assert.AreEqual(0f, arc.Fade, 1e-5f);
            Assert.AreEqual(0f, arc.Length, 1e-5f);
            Assert.AreEqual(0.25f, arc.Head, 1e-5f);
        }

        [Test]
        public void Arc_is_fully_grown_and_opaque_mid_life()
        {
            var arc = WindCurrentArc.At(0.25f, 0f, 1f, 0.4f);

            Assert.AreEqual(1f, arc.Fade, 1e-4f);
            Assert.AreEqual(0.4f, arc.Length, 1e-4f);
        }

        [Test]
        public void Arc_enters_fast_and_dissipates_slowly()
        {
            float fadeInSpan = Span(p => WindCurrentArc.At(p, 0f).Fade, rising: true);
            float fadeOutSpan = Span(p => WindCurrentArc.At(p, 0f).Fade, rising: false);

            Assert.Less(fadeInSpan * 3f, fadeOutSpan, "the fade-out is at least 3x longer than the entry");
        }

        [Test]
        public void Arc_head_covers_most_of_the_sweep_early_then_drifts()
        {
            float sweep = 1.1f;
            Assert.Greater(WindCurrentArc.At(0.5f, 0f, sweep).Head, 0.8f * sweep, "most of the sweep in the first half of life");
            Assert.Less(WindCurrentArc.At(0.5f, 0f, sweep).Head, WindCurrentArc.At(1f, 0f, sweep).Head, "still drifting");
        }

        [Test]
        public void Arc_fade_never_rises_once_it_starts_dissipating()
        {
            float last = 1f;
            for (float p = 0.3f; p <= 1f; p += 0.01f)
            {
                float fade = WindCurrentArc.At(p, 0f).Fade;
                Assert.LessOrEqual(fade, last + 1e-6f);
                last = fade;
            }
        }

        // Life-progress distance between fade crossing 0.1 and 0.9 on the way up (or down).
        private static float Span(System.Func<float, float> fade, bool rising)
        {
            float low = -1f, high = -1f;
            for (float p = 0f; p <= 1f; p += 0.001f)
            {
                float f = fade(p);
                if (rising)
                {
                    if (low < 0f && f >= 0.1f) low = p;
                    if (high < 0f && f >= 0.9f) high = p;
                }
                else
                {
                    if (p > 0.2f && high < 0f && f <= 0.9f) high = p;
                    if (p > 0.2f && low < 0f && f <= 0.1f) low = p;
                }
            }

            return Mathf.Abs(low - high);
        }

        [Test]
        public void Arc_fades_out_before_the_end_of_life()
        {
            Assert.AreEqual(0f, WindCurrentArc.At(1f, 0f).Fade, 1e-4f);
            Assert.Less(WindCurrentArc.At(0.9f, 0f).Fade, 1f);
        }

        [Test]
        public void Arc_head_sweeps_around_the_sphere_monotonically_and_the_tail_trails_it()
        {
            float lastHead = float.NegativeInfinity;
            for (float p = 0f; p <= 1f; p += 0.02f)
            {
                var arc = WindCurrentArc.At(p, 0.1f, 1.2f, 0.4f);
                Assert.Greater(arc.Head, lastHead);
                lastHead = arc.Head;
                Assert.AreEqual(arc.Head - arc.Length, arc.Tail, 1e-5f);
                Assert.That(arc.Length, Is.InRange(0f, 0.4f + 1e-5f));
                Assert.That(arc.Fade, Is.InRange(0f, 1f));
            }

            Assert.AreEqual(0.1f + 1.2f, WindCurrentArc.At(1f, 0.1f, 1.2f).Head, 1e-4f);
        }

        [Test]
        public void Arc_clamps_out_of_range_progress()
        {
            Assert.AreEqual(WindCurrentArc.At(0f, 0f).Fade, WindCurrentArc.At(-3f, 0f).Fade);
            Assert.AreEqual(WindCurrentArc.At(1f, 0f).Fade, WindCurrentArc.At(7f, 0f).Fade);
            Assert.AreEqual(0f, WindCurrentArc.At(float.NaN, 0f).Fade);
        }

        [Test]
        public void Gate_is_closed_until_the_shield_is_mostly_built_then_opens_to_one()
        {
            Assert.AreEqual(0f, WindCurrentArc.Gate(0f));
            Assert.AreEqual(0f, WindCurrentArc.Gate(0.05f));
            Assert.Greater(WindCurrentArc.Gate(0.5f), 0.3f, "currents show early during the short assembly");
            Assert.AreEqual(1f, WindCurrentArc.Gate(1f), 1e-5f);
            float last = -1f;
            for (float b = 0f; b <= 1f; b += 0.05f)
            {
                float g = WindCurrentArc.Gate(b);
                Assert.GreaterOrEqual(g, last);
                last = g;
            }

            Assert.AreEqual(0f, WindCurrentArc.Gate(float.NaN));
        }

        [Test]
        public void Ring_radius_hugs_the_sphere_slightly_outside_at_every_height()
        {
            Assert.AreEqual(0.5f * 1.06f, WindCurrentArc.RingRadius(0f), 1e-5f);
            Assert.AreEqual(Mathf.Sqrt(0.25f - 0.09f) * 1.06f, WindCurrentArc.RingRadius(0.6f), 1e-5f);
            Assert.AreEqual(WindCurrentArc.RingRadius(0.6f), WindCurrentArc.RingRadius(-0.6f), 1e-6f);
            Assert.GreaterOrEqual(WindCurrentArc.RingRadius(5f), 0f, "never NaN above the pole");
        }

        [Test]
        public void Ring_radius_stays_valid_and_hugs_the_sphere_near_the_poles()
        {
            foreach (float h in new[] { -0.85f, 0.85f })
            {
                float radius = WindCurrentArc.RingRadius(h);
                float sphereAtHeight = Mathf.Sqrt(0.25f - Mathf.Pow(h * 0.5f, 2));
                Assert.IsFalse(float.IsNaN(radius));
                Assert.Greater(radius, 0.2f, "does not collapse at " + h);
                Assert.AreEqual(sphereAtHeight * 1.06f, radius, 1e-5f);
            }
        }

        [Test]
        public void Band_height_shrinks_toward_the_poles_so_it_never_crosses_them()
        {
            Assert.AreEqual(1f, WindCurrentArc.BandScaleY(0f), 1e-6f);
            float last = 1f;
            for (float h = 0f; h <= 1f; h += 0.05f)
            {
                float k = WindCurrentArc.BandScaleY(h);
                Assert.LessOrEqual(k, last + 1e-6f, "monotonic at " + h);
                Assert.AreEqual(k, WindCurrentArc.BandScaleY(-h), 1e-6f, "symmetric");
                Assert.Greater(k, 0f);
                last = k;
            }

            foreach (float h in new[] { 0.85f, -0.85f })
            {
                float top = Mathf.Abs(h) * 0.5f + WindCurrentArc.BandHalfHeight * WindCurrentArc.BandScaleY(h);
                Assert.Less(top, 0.5f * 0.96f, "band edge stays below the pole at " + h);
            }

            Assert.IsFalse(float.IsNaN(WindCurrentArc.BandScaleY(float.NaN)));
        }

        [Test]
        public void Tilt_shrinks_toward_the_poles_so_streaks_stay_streaks()
        {
            Assert.AreEqual(1f, WindCurrentArc.TiltScale(0f), 1e-6f);
            Assert.Less(WindCurrentArc.TiltScale(0.85f), 0.6f);
            Assert.GreaterOrEqual(WindCurrentArc.TiltScale(0.85f), 0f);
            Assert.AreEqual(WindCurrentArc.TiltScale(0.85f), WindCurrentArc.TiltScale(-0.85f), 1e-6f);
            Assert.AreEqual(0f, WindCurrentArc.TiltScale(1.5f), 1e-6f, "never NaN above the pole");
        }

        [Test]
        public void Ring_mesh_half_height_matches_the_constant_the_adapter_uses()
        {
            var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(MeshPath);
            Assert.AreEqual(WindCurrentArc.BandHalfHeight, mesh.bounds.extents.y, 1e-3f);
        }

        // ---- assets ----------------------------------------------------------------------------------------------

        private static Shader LoadShader()
        {
            var shader = AssetDatabase.LoadAssetAtPath<Shader>(ShaderPath);
            Assert.IsNotNull(shader, ShaderPath);
            return shader;
        }

        [Test]
        public void Shader_compiles_without_messages_and_exposes_the_current_properties()
        {
            var shader = LoadShader();
            Assert.AreEqual("HexFall/WindCurrent", shader.name);
            Assert.IsTrue(shader.isSupported, "supported");
            Assert.IsFalse(ShaderUtil.ShaderHasError(shader), "ShaderHasError");
            var messages = ShaderUtil.GetShaderMessages(shader);
            Assert.IsEmpty(messages, string.Join("\n", messages.Select(m => m.severity + ": " + m.message)));
            foreach (var name in new[] { "_BaseColor", "_TraceTex", "_TraceTiling", "_ScrollSpeed", "_Head", "_Tail", "_Fade",
                "_EdgeNoise", "_Seed", "_SnapResolution" })
                Assert.GreaterOrEqual(shader.FindPropertyIndex(name), 0, name);
        }

        [Test]
        public void Snap_function_is_shared_not_duplicated()
        {
            Assert.IsNotNull(AssetDatabase.LoadAssetAtPath<Object>(SharedPath), SharedPath);
            var shared = System.IO.File.ReadAllText(SharedPath);
            StringAssert.Contains("void WindShield_Snap_float", shared);
            StringAssert.DoesNotContain("void WindShield_Snap_float", System.IO.File.ReadAllText(CorePath));
            StringAssert.DoesNotContain("void WindShield_Snap_float", System.IO.File.ReadAllText(ShaderPath));
            StringAssert.Contains("WindShieldShared.hlsl", System.IO.File.ReadAllText(CorePath));
            StringAssert.Contains("WindShieldShared.hlsl", System.IO.File.ReadAllText(ShaderPath));
        }

        [Test]
        public void Twirl_scroll_offset_is_wrapped_per_tile_for_float_precision()
        {
            var core = System.IO.File.ReadAllText(CorePath);
            StringAssert.Contains("frac(time * flowSpeed)", core);
            StringAssert.Contains("frac(-time * flowSpeed * 0.6)", core);
        }

        [Test]
        public void Material_is_near_white_additive_with_the_trace_texture()
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
            Assert.IsNotNull(material, MaterialPath);
            Assert.AreEqual(LoadShader(), material.shader);
            Assert.AreEqual(AssetDatabase.LoadAssetAtPath<Texture2D>(TracePath), material.GetTexture("_TraceTex"));
            var shield = AssetDatabase.LoadAssetAtPath<Material>(Dir + "Materials/M_WindShield.mat").GetColor("_BaseColor");
            Assert.AreEqual(shield, material.GetColor("_BaseColor"), "same near-white as the shield");
            Assert.AreEqual(RenderQueue.Transparent, (RenderQueue)material.renderQueue);
            Assert.AreEqual(Mathf.Round(material.GetFloat("_TraceTiling")), material.GetFloat("_TraceTiling"), "whole tiles wrap the ring");
        }

        [Test]
        public void Ring_mesh_is_a_round_thin_band_with_u_around_and_v_across()
        {
            var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(MeshPath);
            Assert.IsNotNull(mesh, MeshPath);
            Assert.AreEqual(0.5f, mesh.bounds.extents.x, 0.02f, "radius x");
            Assert.AreEqual(0.5f, mesh.bounds.extents.z, 0.02f, "radius z");
            Assert.That(mesh.bounds.extents.y, Is.InRange(0.16f, 0.18f), "a band (0.34 tall, 35 % thicker than T6c) along the vertical axis");
            Assert.GreaterOrEqual(mesh.triangles.Length / 3, 96, "enough segments to look round");
            Assert.LessOrEqual(mesh.triangles.Length / 3, 400, "cheap");
            var uv = mesh.uv;
            Assert.AreEqual(0f, uv.Min(c => c.x), 1e-4f);
            Assert.AreEqual(1f, uv.Max(c => c.x), 1e-4f);
            Assert.AreEqual(0f, uv.Min(c => c.y), 1e-4f);
            Assert.AreEqual(1f, uv.Max(c => c.y), 1e-4f);
            Assert.IsTrue(mesh.vertices.All(v => Mathf.Abs(new Vector2(v.x, v.z).magnitude - 0.5f) < 0.05f), "hugs a 0.5 radius");
        }

        // ---- adapter ---------------------------------------------------------------------------------------------

        private static (GameObject go, WindCurrents currents, Renderer[] rings) NewCurrents(int count = 4)
        {
            var go = new GameObject("WindCurrents");
            var rings = new Renderer[count];
            for (int i = 0; i < count; i++)
            {
                var child = GameObject.CreatePrimitive(PrimitiveType.Quad);
                child.transform.SetParent(go.transform, false);
                rings[i] = child.GetComponent<Renderer>();
                rings[i].sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
            }

            var currents = go.AddComponent<WindCurrents>();
            currents.Configure(rings, new System.Random(5));
            return (go, currents, rings);
        }

        [Test]
        public void Closed_gate_hides_every_ring()
        {
            var (go, currents, rings) = NewCurrents();
            try
            {
                currents.Step(0.1f, 0f);

                Assert.IsTrue(rings.All(r => !r.enabled));
            }
            finally { Object.DestroyImmediate(go); }
        }

        [Test]
        public void Open_gate_shows_the_rings_with_fade_head_and_tail_from_the_scheduler_state()
        {
            var (go, currents, rings) = NewCurrents();
            try
            {
                currents.Step(0.2f, 1f);

                var block = new MaterialPropertyBlock();
                int visible = 0;
                for (int i = 0; i < rings.Length; i++)
                {
                    var state = currents.States[i];
                    var arc = WindCurrentArc.At(state.Progress, 0f);
                    rings[i].GetPropertyBlock(block);
                    Assert.AreEqual(arc.Fade, block.GetFloat("_Fade"), 1e-4f, "fade of ring " + i);
                    Assert.AreEqual(arc.Length, block.GetFloat("_Head") - block.GetFloat("_Tail"), 1e-4f, "arc length of ring " + i);
                    Assert.AreEqual(state.Height * 0.5f, rings[i].transform.localPosition.y, 1e-4f, "height of ring " + i);
                    if (rings[i].enabled) visible++;
                }

                Assert.GreaterOrEqual(visible, 1, "at least one current is on screen mid-life");
            }
            finally { Object.DestroyImmediate(go); }
        }

        [Test]
        public void Gate_scales_the_fade_while_the_shield_assembles_and_disassembles()
        {
            var (go, currents, rings) = NewCurrents();
            try
            {
                currents.Step(0.2f, 1f);
                var block = new MaterialPropertyBlock();
                rings[0].GetPropertyBlock(block);
                float full = block.GetFloat("_Fade");

                currents.Step(0f, 0.7f);
                rings[0].GetPropertyBlock(block);

                Assert.AreEqual(full * WindCurrentArc.Gate(0.7f), block.GetFloat("_Fade"), 1e-4f);
                Assert.Less(block.GetFloat("_Fade"), full);
            }
            finally { Object.DestroyImmediate(go); }
        }

        [Test]
        public void Rings_are_scaled_to_hug_the_sphere_at_their_height_and_tilted_within_the_scheduler_limit()
        {
            var (go, currents, rings) = NewCurrents();
            try
            {
                for (int step = 0; step < 50; step++) currents.Step(0.05f, 1f);

                for (int i = 0; i < rings.Length; i++)
                {
                    var state = currents.States[i];
                    var t = rings[i].transform;
                    Assert.AreEqual(WindCurrentArc.RingRadius(state.Height) / 0.5f, t.localScale.x, 1e-4f, "scale x");
                    Assert.AreEqual(t.localScale.x, t.localScale.z, 1e-5f, "scale z");
                    Assert.LessOrEqual(Quaternion.Angle(Quaternion.identity, t.localRotation), Mathf.Abs(state.TiltDegrees) + 1e-3f, "tilt");
                }
            }
            finally { Object.DestroyImmediate(go); }
        }

        [Test]
        public void Six_rings_cover_heights_up_to_the_wider_band_and_stay_off_the_poles()
        {
            var (go, currents, rings) = NewCurrents(6);
            try
            {
                float minY = 0f, maxY = 0f;
                for (int step = 0; step < 600; step++)
                {
                    currents.Step(0.02f, 1f);
                    for (int i = 0; i < rings.Length; i++)
                    {
                        var t = rings[i].transform;
                        minY = Mathf.Min(minY, t.localPosition.y);
                        maxY = Mathf.Max(maxY, t.localPosition.y);
                        float h = t.localPosition.y / 0.5f;
                        Assert.AreEqual(WindCurrentArc.BandScaleY(h), t.localScale.y, 1e-4f, "band height at " + h);
                        var state = currents.States[i];
                        Assert.LessOrEqual(Quaternion.Angle(Quaternion.identity, t.localRotation),
                            Mathf.Abs(state.TiltDegrees) * WindCurrentArc.TiltScale(state.Height) + 1e-3f, "tilt shrinks near the poles");
                    }
                }

                Assert.AreEqual(6, currents.States.Count);
                Assert.Greater(maxY, 0.7f * 0.5f, "currents start high");
                Assert.Less(minY, -0.7f * 0.5f, "currents start low");
                Assert.LessOrEqual(maxY, 0.85f * 0.5f + 1e-4f);
                Assert.GreaterOrEqual(minY, -0.85f * 0.5f - 1e-4f);
            }
            finally { Object.DestroyImmediate(go); }
        }

        [Test]
        public void Restart_staggers_the_currents_again_and_Hide_turns_the_rings_off()
        {
            var (go, currents, rings) = NewCurrents();
            try
            {
                currents.Step(0.2f, 1f);
                currents.Hide();
                Assert.IsTrue(rings.All(r => !r.enabled));

                currents.Restart();
                var progress = currents.States.Select(s => s.Progress).Distinct().Count();
                Assert.AreEqual(rings.Length, progress, "staggered progress");
            }
            finally { Object.DestroyImmediate(go); }
        }

        [Test]
        public void MagicShield_feeds_the_build_progress_to_the_currents_and_hides_them_when_done()
        {
            var (shieldGo, shield) = NewShield();
            var (go, currents, rings) = NewCurrents();
            try
            {
                currents.transform.SetParent(shieldGo.transform, false);
                using (var so = new SerializedObject(shield))
                {
                    so.FindProperty("currents").objectReferenceValue = currents;
                    so.ApplyModifiedPropertiesWithoutUndo();
                }

                shield.Flash(1f);
                shield.Apply(0.5f, 0.1f);
                Assert.AreEqual(1f, currents.BuildProgress, 1e-5f, "held");

                shield.Apply(1f + 1f, 0.1f); // past disassembly: the live currents keep fading
                Assert.IsTrue(currents.IsLingering, "lingering after the sphere is gone");
                for (int i = 0; i < 40; i++) shield.Apply(2f + i * 0.1f, 0.1f);
                Assert.IsTrue(rings.All(r => !r.enabled), "hidden once they have faded out");
                Assert.IsFalse(currents.IsLingering);
            }
            finally
            {
                Object.DestroyImmediate(go);
                Object.DestroyImmediate(shieldGo);
            }
        }

        // ---- lingering past the sphere (T6b) ---------------------------------------------------------------------

        private static (GameObject shieldGo, MagicShield shield, GameObject go, WindCurrents currents, Renderer[] rings) NewWiredShield(int seed)
        {
            var (shieldGo, shield) = NewShield();
            var (go, currents, rings) = NewCurrents();
            currents.Configure(rings, new System.Random(seed));
            currents.transform.SetParent(shieldGo.transform, false);
            using (var so = new SerializedObject(shield))
            {
                so.FindProperty("currents").objectReferenceValue = currents;
                so.ApplyModifiedPropertiesWithoutUndo();
            }

            return (shieldGo, shield, go, currents, rings);
        }

        // The build timing the shield really uses: hold plus the serialized assemble/disassemble durations.
        private static ShieldBuild BuildOf(MagicShield shield, float hold)
        {
            using (var so = new SerializedObject(shield))
                return new ShieldBuild(hold, so.FindProperty("assembleSeconds").floatValue, so.FindProperty("disassembleSeconds").floatValue);
        }

        private static float RingFade(Renderer ring, MaterialPropertyBlock block)
        {
            if (!ring.enabled) return 0f;
            ring.GetPropertyBlock(block);
            return block.GetFloat("_Fade");
        }

        [Test]
        public void Currents_outlive_the_sphere_then_fade_to_nothing_without_new_births()
        {
            var block = new MaterialPropertyBlock();
            for (int seed = 1; seed <= 8; seed++)
            {
                var (shieldGo, shield, go, currents, rings) = NewWiredShield(seed);
                try
                {
                    const float hold = 0.4f, dt = 1f / 60f;
                    var build = BuildOf(shield, hold);
                    shield.Flash(hold);
                    var heights = rings.Select(r => r.transform.localPosition.y).ToArray();
                    var lastFade = new float[rings.Length];
                    bool visibleAfterSphere = false;
                    for (float t = dt; t < 1.8f; t += dt)
                    {
                        shield.Apply(t, dt);
                        if (build.PhaseAt(t) >= ShieldBuildPhase.Disassembling)
                            for (int i = 0; i < rings.Length; i++)
                                if (rings[i].enabled)
                                    Assert.AreEqual(heights[i], rings[i].transform.localPosition.y, 1e-5f, "no new current after disassembly starts (seed " + seed + ")");
                        if (build.IsDone(t - dt))
                        {
                            for (int i = 0; i < rings.Length; i++)
                            {
                                float fade = RingFade(rings[i], block);
                                Assert.LessOrEqual(fade, lastFade[i] + 1e-5f, "fade is monotonic after the sphere is gone (seed " + seed + ", ring " + i + ")");
                                lastFade[i] = fade;
                                if (fade > 0f) visibleAfterSphere = true;
                            }
                        }
                        else if (build.IsDone(t))
                            for (int i = 0; i < rings.Length; i++) lastFade[i] = RingFade(rings[i], block);

                        if (t < hold)
                            for (int i = 0; i < rings.Length; i++)
                                heights[i] = rings[i].transform.localPosition.y;
                    }

                    Assert.IsTrue(visibleAfterSphere, "some current is still visible after the sphere is gone (seed " + seed + ")");
                    Assert.IsTrue(rings.All(r => !r.enabled), "fully transparent and hidden by 1.8 s (seed " + seed + ")");
                }
                finally
                {
                    Object.DestroyImmediate(go);
                    Object.DestroyImmediate(shieldGo);
                }
            }
        }

        [Test]
        public void A_new_Flash_during_the_linger_restarts_the_currents_cleanly()
        {
            var (shieldGo, shield, go, currents, rings) = NewWiredShield(4);
            try
            {
                shield.Flash(0.4f);
                for (float t = 0.02f; t < 0.9f; t += 0.02f) shield.Apply(t, 0.02f);
                Assert.IsTrue(currents.IsLingering, "setup: lingering");

                shield.Flash(0.4f);

                Assert.IsFalse(currents.IsLingering, "a fresh scheduler is not draining");
                shield.Apply(0.1f, 0.1f);
                Assert.IsTrue(rings.Any(r => r.enabled), "new currents show for the new shield");
                for (float t = 0.2f; t < 2f; t += 0.02f) shield.Apply(t, 0.02f);
                Assert.IsTrue(rings.All(r => !r.enabled), "no ring stuck after the second linger");
            }
            finally
            {
                Object.DestroyImmediate(go);
                Object.DestroyImmediate(shieldGo);
            }
        }

        [Test]
        public void Several_currents_show_during_the_guard_window()
        {
            for (int seed = 1; seed <= 8; seed++)
            {
                var (shieldGo, shield, go, currents, rings) = NewWiredShield(seed);
                try
                {
                    shield.Flash(0.4f);
                    int maxVisible = 0;
                    for (float t = 1f / 60f; t < 0.4f; t += 1f / 60f)
                    {
                        shield.Apply(t, 1f / 60f);
                        maxVisible = Mathf.Max(maxVisible, rings.Count(r => r.enabled));
                    }

                    Assert.GreaterOrEqual(maxVisible, 2, "seed " + seed);
                }
                finally
                {
                    Object.DestroyImmediate(go);
                    Object.DestroyImmediate(shieldGo);
                }
            }
        }

        // ---- parameterless Deflect (the gameplay path) ----------------------------------------------------------

        [Test]
        public void Parameterless_Deflect_before_any_Flash_is_harmless()
        {
            var (shieldGo, shield) = NewShield();
            try
            {
                Assert.DoesNotThrow(() => shield.Deflect());

                shield.Flash(0.4f);
                shield.Apply(0.2f);
                var block = new MaterialPropertyBlock();
                shieldGo.GetComponent<Renderer>().GetPropertyBlock(block);
                var baseColor = AssetDatabase.LoadAssetAtPath<Material>(Dir + "Materials/M_WindShield.mat").GetColor("_BaseColor");
                var color = block.GetColor("_BaseColor");
                Assert.IsFalse(float.IsNaN(color.a));
                Assert.AreEqual(baseColor.a, color.a, 1e-5f, "a deflect before the Flash never boosts the new shield");
            }
            finally { Object.DestroyImmediate(shieldGo); }
        }

        [Test]
        public void Parameterless_Deflect_during_the_hold_boosts_the_alpha_once_the_shield_is_built()
        {
            var (shieldGo, shield) = NewShield();
            try
            {
                shield.Flash(0.4f);
                shield.Deflect(); // Time.time is frozen in edit mode: lands at elapsed 0
                shield.Apply(0.1f);
                var block = new MaterialPropertyBlock();
                shieldGo.GetComponent<Renderer>().GetPropertyBlock(block);
                var baseAlpha = AssetDatabase.LoadAssetAtPath<Material>(Dir + "Materials/M_WindShield.mat").GetColor("_BaseColor").a;
                float alpha = block.GetColor("_BaseColor").a;

                Assert.IsFalse(float.IsNaN(alpha));
                Assert.Greater(alpha, baseAlpha, "boosted");
                Assert.LessOrEqual(alpha, 1f);
            }
            finally { Object.DestroyImmediate(shieldGo); }
        }

        private static (GameObject go, MagicShield shield) NewShield()
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            go.GetComponent<Renderer>().sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>(Dir + "Materials/M_WindShield.mat");
            var shield = go.AddComponent<MagicShield>();
            typeof(MagicShield).GetMethod("Awake", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(shield, null);
            return (go, shield);
        }

        // ---- prefab ----------------------------------------------------------------------------------------------

        [Test]
        public void Player_prefab_wires_six_ring_renderers_under_the_shield()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefab);
            var shield = prefab.GetComponentInChildren<MagicShield>(true);
            Assert.IsNotNull(shield, "MagicShield");

            var currents = shield.GetComponentInChildren<WindCurrents>(true);
            Assert.IsNotNull(currents, "WindCurrents under the shield");
            using (var so = new SerializedObject(shield))
                Assert.AreEqual(currents, so.FindProperty("currents").objectReferenceValue, "MagicShield.currents");

            using (var so = new SerializedObject(currents))
            {
                var rings = so.FindProperty("rings");
                Assert.AreEqual(6, rings.arraySize);
                var material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
                var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(MeshPath);
                for (int i = 0; i < rings.arraySize; i++)
                {
                    var ring = rings.GetArrayElementAtIndex(i).objectReferenceValue as MeshRenderer;
                    Assert.IsNotNull(ring, "ring " + i);
                    Assert.AreEqual(material, ring.sharedMaterial, "material " + i);
                    Assert.AreEqual(mesh, ring.GetComponent<MeshFilter>().sharedMesh, "mesh " + i);
                    Assert.AreEqual(ShadowCastingMode.Off, ring.shadowCastingMode, "no shadows " + i);
                    Assert.IsFalse(ring.receiveShadows, "receive shadows " + i);
                    Assert.IsTrue(ring.transform.IsChildOf(currents.transform), "child " + i);
                }
            }
        }
    }
}
