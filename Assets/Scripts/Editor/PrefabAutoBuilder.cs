#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace BossFight2D.EditorTools
{
    public static class PrefabAutoBuilder
    {
        private const string PrefabFolder = "Assets/Prefabs";
        private const string PlayerPrefabPath = PrefabFolder + "/Player.prefab";
        private const string BossPrefabPath = PrefabFolder + "/Boss.prefab";
        private const string PlayerControllerPath = PrefabFolder + "/Player.controller";
        private const string BossControllerPath = PrefabFolder + "/Boss.controller";

        [MenuItem("Tools/BossFight2D/Build Prefabs/Build Both")]
        public static void BuildBoth()
        {
            EnsureFolder("Assets", "Prefabs");
            BuildPlayerPrefab();
            BuildBossPrefab();
            EditorUtility.DisplayDialog("BossFight2D", "Built Player and Boss prefabs.", "OK");
        }

        [MenuItem("Tools/BossFight2D/Build Prefabs/Build Player")] 
        public static void BuildPlayerPrefab()
        {
            EnsureFolder("Assets", "Prefabs");
            var go = new GameObject("Player");
            try
            {
                // Rigidbody2D
                var rb = go.AddComponent<Rigidbody2D>();
                rb.gravityScale = 0f;
                rb.freezeRotation = true;

                // Optional collider (environment)
                var bodyCol = go.AddComponent<CapsuleCollider2D>();
                bodyCol.isTrigger = false;
                bodyCol.size = new Vector2(0.6f, 1.2f);

                // Core scripts
                go.AddComponent<BossFight2D.Player.PlayerController>();
                var health = go.AddComponent<BossFight2D.Player.PlayerHealth>();
                var focus = go.AddComponent<BossFight2D.Player.PlayerFocus>();
                var combat = go.AddComponent<BossFight2D.Player.PlayerCombat>();

                // Animator + controller with Attack trigger
                var animator = go.AddComponent<Animator>();
                var controller = CreateOrGetAnimatorController(PlayerControllerPath);
                EnsureTriggerParam(controller, "Attack");
                animator.runtimeAnimatorController = controller;

                // Child hitbox
                var hitboxGO = new GameObject("PlayerHitbox");
                hitboxGO.transform.SetParent(go.transform);
                hitboxGO.transform.localPosition = new Vector3(0.6f, 0f, 0f);
                var hitCol = hitboxGO.AddComponent<CircleCollider2D>();
                hitCol.isTrigger = true;
                hitCol.radius = 0.45f;

                var hitbox = hitboxGO.AddComponent<BossFight2D.Combat.Hitbox2D>();
                hitbox.defaultDamage = 10;
                hitbox.owner = go;
                hitbox.disableColliderWhenInactive = true;
                hitbox.autoDeactivateSeconds = 0.2f;

                // Wire references


                // Save prefab
                var prefab = PrefabUtility.SaveAsPrefabAsset(go, PlayerPrefabPath);
                Debug.Log($"Saved Player prefab to {PlayerPrefabPath}");
            }
            finally
            {
                GameObject.DestroyImmediate(go);
            }
        }

        [MenuItem("Tools/BossFight2D/Build Prefabs/Build Boss")] 
        public static void BuildBossPrefab()
        {
            EnsureFolder("Assets", "Prefabs");
            var go = new GameObject("Boss");
            try
            {
                // Rigidbody2D
                var rb = go.AddComponent<Rigidbody2D>();
                rb.bodyType = RigidbodyType2D.Kinematic;
                rb.freezeRotation = true;

                // Optional collider (environment)
                var bodyCol = go.AddComponent<BoxCollider2D>();
                bodyCol.isTrigger = false;
                bodyCol.size = new Vector2(1.2f, 1.2f);

                // Core scripts
                var sm = go.AddComponent<BossFight2D.Boss.BossStateMachine>();
                var combat = go.AddComponent<BossFight2D.Boss.BossCombat>();
                go.AddComponent<BossFight2D.Combat.Hurtbox2D>();
                //var health = go.AddComponent<BossFight2D.Boss.BossHealth>();
                //var controller = go.AddComponent<BossFight2D.Boss.BossController>();
                go.AddComponent<Unity.Netcode.NetworkObject>();

                // Animator + controller with Attack trigger
                var animator = go.AddComponent<Animator>();
                var controller = CreateOrGetAnimatorController(BossControllerPath);
                EnsureTriggerParam(controller, "Attack");
                animator.runtimeAnimatorController = controller;

                // Child hitbox
                var hitboxGO = new GameObject("BossHitbox");
                hitboxGO.transform.SetParent(go.transform);
                hitboxGO.transform.localPosition = new Vector3(0.8f, 0f, 0f);
                var hitCol = hitboxGO.AddComponent<CircleCollider2D>();
                hitCol.isTrigger = true;
                hitCol.radius = 0.6f;

                var hitbox = hitboxGO.AddComponent<BossFight2D.Combat.Hitbox2D>();
                hitbox.defaultDamage = 1;
                hitbox.owner = go;
                hitbox.disableColliderWhenInactive = true;
                hitbox.autoDeactivateSeconds = 0.25f;

                // Wire references
                combat.hitbox = hitbox;

                // Save prefab
                var prefab = PrefabUtility.SaveAsPrefabAsset(go, BossPrefabPath);
                Debug.Log($"Saved Boss prefab to {BossPrefabPath}");
            }
            finally
            {
                GameObject.DestroyImmediate(go);
            }
        }

        private static void EnsureFolder(string parent, string child)
        {
            if (!AssetDatabase.IsValidFolder(Path.Combine(parent, child)))
            {
                AssetDatabase.CreateFolder(parent, child);
            }
        }

        private static AnimatorController CreateOrGetAnimatorController(string controllerPath)
        {
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(controllerPath);
            if (controller == null)
            {
                controller = AnimatorController.CreateAnimatorControllerAtPath(controllerPath);
                Debug.Log($"Created AnimatorController at {controllerPath}");
            }
            return controller;
        }

        private static void EnsureTriggerParam(AnimatorController controller, string name)
        {
            if (controller == null) return;
            foreach (var p in controller.parameters)
            {
                if (p.name == name && p.type == AnimatorControllerParameterType.Trigger)
                    return;
            }
            controller.AddParameter(name, AnimatorControllerParameterType.Trigger);
            EditorUtility.SetDirty(controller);
        }
    }
}
#endif