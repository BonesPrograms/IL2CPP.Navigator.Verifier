using System;
using System.Collections.Generic;
using Il2CppInterop.Runtime;
using XQuinn.Reflection;
using XQuinn.Runtime.NavigatorEngine;
using Assembly = Il2CppSystem.Reflection.Assembly;
using Type = Il2CppSystem.Type;

namespace XQuinn.Runtime
{
    internal readonly struct HostRegistrationResult
    {
        internal HostRegistrationResult(int gameTypeCount, int unityTypeCount, int cachedTypeCount, int skippedTypeCount)
        {
            GameTypeCount = gameTypeCount;
            UnityTypeCount = unityTypeCount;
            CachedTypeCount = cachedTypeCount;
            SkippedTypeCount = skippedTypeCount;
        }

        internal int GameTypeCount { get; }
        internal int UnityTypeCount { get; }
        internal int CachedTypeCount { get; }
        internal int SkippedTypeCount { get; }
    }

    /// <summary>Registers Vietnam War and curated scene-facing Unity types for Navigator.</summary>
    internal static class HostTypeRegistration
    {
        static readonly (string assemblyName, string typeName)[] s_unityInteractionTypes =
        {
            // Scene objects and the component inheritance chain.
            ("UnityEngine.CoreModule", "UnityEngine.Object"),
            ("UnityEngine.CoreModule", "UnityEngine.Component"),
            ("UnityEngine.CoreModule", "UnityEngine.Behaviour"),
            ("UnityEngine.CoreModule", "UnityEngine.MonoBehaviour"),
            ("UnityEngine.CoreModule", "UnityEngine.GameObject"),
            ("UnityEngine.CoreModule", "UnityEngine.Transform"),
            ("UnityEngine.CoreModule", "UnityEngine.RectTransform"),

            // Render, camera, lighting and effect components.
            ("UnityEngine.CoreModule", "UnityEngine.Camera"),
            ("UnityEngine.CoreModule", "UnityEngine.Light"),
            ("UnityEngine.CoreModule", "UnityEngine.Renderer"),
            ("UnityEngine.CoreModule", "UnityEngine.MeshRenderer"),
            ("UnityEngine.CoreModule", "UnityEngine.SkinnedMeshRenderer"),
            ("UnityEngine.CoreModule", "UnityEngine.LineRenderer"),
            ("UnityEngine.CoreModule", "UnityEngine.TrailRenderer"),
            ("UnityEngine.CoreModule", "UnityEngine.SpriteRenderer"),
            ("UnityEngine.ParticleSystemModule", "UnityEngine.ParticleSystemRenderer"),
            ("UnityEngine.UIModule", "UnityEngine.CanvasRenderer"),
            ("UnityEngine.CoreModule", "UnityEngine.ReflectionProbe"),
            ("UnityEngine.CoreModule", "UnityEngine.LODGroup"),
            ("UnityEngine.ClothModule", "UnityEngine.Cloth"),

            // 3D and 2D physics components.
            ("UnityEngine.PhysicsModule", "UnityEngine.Collider"),
            ("UnityEngine.PhysicsModule", "UnityEngine.BoxCollider"),
            ("UnityEngine.PhysicsModule", "UnityEngine.SphereCollider"),
            ("UnityEngine.PhysicsModule", "UnityEngine.CapsuleCollider"),
            ("UnityEngine.PhysicsModule", "UnityEngine.MeshCollider"),
            ("UnityEngine.PhysicsModule", "UnityEngine.CharacterController"),
            ("UnityEngine.PhysicsModule", "UnityEngine.Rigidbody"),
            ("UnityEngine.PhysicsModule", "UnityEngine.Joint"),
            ("UnityEngine.PhysicsModule", "UnityEngine.FixedJoint"),
            ("UnityEngine.PhysicsModule", "UnityEngine.HingeJoint"),
            ("UnityEngine.PhysicsModule", "UnityEngine.SpringJoint"),
            ("UnityEngine.PhysicsModule", "UnityEngine.ConfigurableJoint"),
            ("UnityEngine.PhysicsModule", "UnityEngine.ConstantForce"),
            ("UnityEngine.Physics2DModule", "UnityEngine.Collider2D"),
            ("UnityEngine.Physics2DModule", "UnityEngine.BoxCollider2D"),
            ("UnityEngine.Physics2DModule", "UnityEngine.CircleCollider2D"),
            ("UnityEngine.Physics2DModule", "UnityEngine.CapsuleCollider2D"),
            ("UnityEngine.Physics2DModule", "UnityEngine.PolygonCollider2D"),
            ("UnityEngine.Physics2DModule", "UnityEngine.EdgeCollider2D"),
            ("UnityEngine.Physics2DModule", "UnityEngine.CompositeCollider2D"),
            ("UnityEngine.Physics2DModule", "UnityEngine.Rigidbody2D"),
            ("UnityEngine.Physics2DModule", "UnityEngine.Joint2D"),

            // Audio, animation, navigation, terrain and other scene components.
            ("UnityEngine.AudioModule", "UnityEngine.AudioSource"),
            ("UnityEngine.AudioModule", "UnityEngine.AudioListener"),
            ("UnityEngine.AudioModule", "UnityEngine.AudioReverbZone"),
            ("UnityEngine.AnimationModule", "UnityEngine.Animator"),
            ("UnityEngine.AnimationModule", "UnityEngine.Animation"),
            ("UnityEngine.ParticleSystemModule", "UnityEngine.ParticleSystem"),
            ("UnityEngine.UIModule", "UnityEngine.Canvas"),
            ("UnityEngine.AIModule", "UnityEngine.AI.NavMeshAgent"),
            ("UnityEngine.AIModule", "UnityEngine.AI.NavMeshObstacle"),
            ("UnityEngine.TerrainModule", "UnityEngine.Terrain"),
            ("UnityEngine.TerrainPhysicsModule", "UnityEngine.TerrainCollider"),
            ("UnityEngine.WindModule", "UnityEngine.WindZone"),
            ("UnityEngine.GridModule", "UnityEngine.Grid"),
            ("UnityEngine.TilemapModule", "UnityEngine.Tilemaps.Tilemap"),
            ("UnityEngine.TilemapModule", "UnityEngine.Tilemaps.TilemapRenderer"),
            ("UnityEngine.DirectorModule", "UnityEngine.Playables.PlayableDirector"),
            ("UnityEngine.VideoModule", "UnityEngine.Video.VideoPlayer"),

            // Event-system and UI components commonly attached to GameObjects.
            ("UnityEngine.UI", "UnityEngine.EventSystems.EventSystem"),
            ("UnityEngine.UI", "UnityEngine.EventSystems.UIBehaviour"),
            ("UnityEngine.UI", "UnityEngine.EventSystems.StandaloneInputModule"),
            ("UnityEngine.UI", "UnityEngine.EventSystems.EventTrigger"),
            ("UnityEngine.UI", "UnityEngine.UI.Graphic"),
            ("UnityEngine.UI", "UnityEngine.UI.Selectable"),
            ("UnityEngine.UI", "UnityEngine.UI.Button"),
            ("UnityEngine.UI", "UnityEngine.UI.Image"),
            ("UnityEngine.UI", "UnityEngine.UI.RawImage"),
            ("UnityEngine.UI", "UnityEngine.UI.Text"),
            ("UnityEngine.UI", "UnityEngine.UI.Slider"),
            ("UnityEngine.UI", "UnityEngine.UI.Toggle"),
            ("UnityEngine.UI", "UnityEngine.UI.Scrollbar"),
            ("UnityEngine.UI", "UnityEngine.UI.ScrollRect"),
            ("UnityEngine.UI", "UnityEngine.UI.InputField"),
            ("UnityEngine.UI", "UnityEngine.UI.Dropdown"),
            ("UnityEngine.UI", "UnityEngine.UI.CanvasScaler"),
            ("UnityEngine.UI", "UnityEngine.UI.GraphicRaycaster"),

            // Values and assets read from or assigned to scene objects/components.
            ("UnityEngine.CoreModule", "UnityEngine.Color"),
            ("UnityEngine.CoreModule", "UnityEngine.Color32"),
            ("UnityEngine.CoreModule", "UnityEngine.Vector2"),
            ("UnityEngine.CoreModule", "UnityEngine.Vector3"),
            ("UnityEngine.CoreModule", "UnityEngine.Vector4"),
            ("UnityEngine.CoreModule", "UnityEngine.Vector2Int"),
            ("UnityEngine.CoreModule", "UnityEngine.Vector3Int"),
            ("UnityEngine.CoreModule", "UnityEngine.Quaternion"),
            ("UnityEngine.CoreModule", "UnityEngine.Matrix4x4"),
            ("UnityEngine.CoreModule", "UnityEngine.Bounds"),
            ("UnityEngine.CoreModule", "UnityEngine.BoundsInt"),
            ("UnityEngine.CoreModule", "UnityEngine.Rect"),
            ("UnityEngine.CoreModule", "UnityEngine.RectInt"),
            ("UnityEngine.CoreModule", "UnityEngine.Ray"),
            ("UnityEngine.PhysicsModule", "UnityEngine.RaycastHit"),
            ("UnityEngine.Physics2DModule", "UnityEngine.RaycastHit2D"),
            ("UnityEngine.CoreModule", "UnityEngine.LayerMask"),
            ("UnityEngine.CoreModule", "UnityEngine.Plane"),
            ("UnityEngine.CoreModule", "UnityEngine.Material"),
            ("UnityEngine.CoreModule", "UnityEngine.Shader"),
            ("UnityEngine.CoreModule", "UnityEngine.Texture"),
            ("UnityEngine.CoreModule", "UnityEngine.Texture2D"),
            ("UnityEngine.CoreModule", "UnityEngine.RenderTexture"),
            ("UnityEngine.CoreModule", "UnityEngine.Mesh"),
            ("UnityEngine.AnimationModule", "UnityEngine.AnimationClip"),
            ("UnityEngine.AnimationModule", "UnityEngine.RuntimeAnimatorController"),
            ("UnityEngine.CoreModule", "UnityEngine.Sprite"),
            ("UnityEngine.PhysicsModule", "UnityEngine.PhysicMaterial"),
            ("UnityEngine.Physics2DModule", "UnityEngine.PhysicsMaterial2D"),
            ("UnityEngine.AudioModule", "UnityEngine.AudioClip"),
            ("UnityEngine.TerrainModule", "UnityEngine.TerrainData"),
            ("UnityEngine.CoreModule", "UnityEngine.SceneManagement.Scene")
        };

        internal static HostRegistrationResult Register(Action<string>? warning = null)
        {
            // The managed bridge types do not receive native class pointers until injection.
            // Register both before resolving their Il2CppSystem.Type objects for the privileged
            // keys; bulk host registration can then treat them like every other native type.
            Types.RegisterInIl2Cpp();
            XQuinn.Runtime.NavigatorBridge.TypeRegister.RegisterInIl2Cpp();
            TypeRegister.CacheType(Il2CppType.Of<Types>(), "Types");
            TypeRegister.CacheType(Il2CppType.Of<XQuinn.Runtime.NavigatorBridge.TypeRegister>(), "TypeRegister");

            Assembly gameAssembly = Assembly.Load("Assembly-CSharp");
            var gameTypes = gameAssembly.GetTypes();
            List<Type> hostTypes = new(gameTypes.Length + s_unityInteractionTypes.Length);
            for (int i = 0; i < gameTypes.Length; i++)
                hostTypes.Add(gameTypes[i]);

            Dictionary<string, Assembly> unityAssemblies = new(StringComparer.Ordinal);
            HashSet<string> unavailableAssemblies = new(StringComparer.Ordinal);
            int unityTypeCount = 0;
            for (int i = 0; i < s_unityInteractionTypes.Length; i++)
            {
                (string assemblyName, string typeName) = s_unityInteractionTypes[i];
                if (unavailableAssemblies.Contains(assemblyName))
                    continue;
                if (!unityAssemblies.TryGetValue(assemblyName, out Assembly? assembly))
                {
                    try
                    {
                        assembly = Assembly.Load(assemblyName);
                    }
                    catch (Il2CppInterop.Runtime.Il2CppException ex) when
                        (ex.Message.Contains("FileNotFoundException", StringComparison.Ordinal))
                    {
                        unavailableAssemblies.Add(assemblyName);
                        warning?.Invoke($"Optional Unity interaction assembly was unavailable: {assemblyName} ({ex.Message})");
                        continue;
                    }
                    unityAssemblies.Add(assemblyName, assembly);
                }

                Type? type = assembly.GetType(typeName, false, false);
                if (type == null)
                {
                    warning?.Invoke($"Unity interaction type was unavailable and could not be cached: {typeName} ({assemblyName})");
                    continue;
                }

                hostTypes.Add(type);
                unityTypeCount++;
            }

            int cachedBefore = TypeRegister.Values.Count;
            List<(string key, Type type)> skipped = TypeRegister.CacheTypesSkipDuplicates(hostTypes, fullname: false);
            TypeRegister.SkippedTypes = skipped;
            return new HostRegistrationResult(
                gameTypes.Length,
                unityTypeCount,
                TypeRegister.Values.Count - cachedBefore,
                skipped.Count);
        }
    }
}