/*
 * @author NyaPPu (mnyappu@gmail.com)
 * @version 1.1, 2024.05.13
 */

#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using nadena.dev.modular_avatar.core;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using VRC.SDK3.Avatars.Components;
using VRC.SDKBase;
public class LightControlGenerator : MonoBehaviour, IEditorOnly
{
    public AnimatorController animatorController;
    private ModularAvatarMergeAnimator MAMergeAnimator;

    // public bool useMonochrome;
    public VRCAvatarDescriptor avatarRoot;
    public Transform[] ignores;

    private string savePath;

    public class RendererInfo {
        public string path;
        public bool isLiltoon;
        public bool isPoiyomi;
    }
    private List<RendererInfo> rendererInfos;

    public void Run()
    {
        SetComponent(ref MAMergeAnimator);

        MAMergeAnimator.animator = animatorController;
        savePath = Path.GetDirectoryName(AssetDatabase.GetAssetPath(animatorController));
        SkinnedMeshRenderer[] renderers = avatarRoot.GetComponentsInChildren<SkinnedMeshRenderer>(true);
        rendererInfos = new();
        foreach (SkinnedMeshRenderer renderer in renderers) {
            if (IsIgnore(renderer.transform)) continue;
            string path = GetObjectPath(renderer.transform, avatarRoot.transform);
            RendererInfo ri = new RendererInfo() { path = path, isLiltoon = false, isPoiyomi = false };
            foreach (Material material in renderer.sharedMaterials) {
                // 포이요미면 설정해줘야할 것들이 있다
                if (material.shader.name.IndexOf("poiyomi", StringComparison.OrdinalIgnoreCase) >= 0) {
                    material.SetFloat("_LightingCapEnabled", 1f);
                    material.SetOverrideTag("_LightingCap" + "Animated", "true");
                    material.SetOverrideTag("_LightingMinLightBrightness" + "Animated", "true");
                    material.SetOverrideTag("_LightingMonochromatic" + "Animated", "true");
                    ri.isPoiyomi = true;
                } else {
                    ri.isLiltoon = true;
                }
                // material.SetOverrideTag("_DissolveAlpha" + "Animated", "true");
            }
            rendererInfos.Add(ri);
        }

        while (animatorController.parameters.Length > 0) {
            animatorController.RemoveParameter(animatorController.parameters[0]);
        }
        while (animatorController.layers.Length > 0) {
            animatorController.RemoveLayer(0);
        }
        animatorController.AddParameter("LightLimit", AnimatorControllerParameterType.Float);
        // if (useMonochrome) animatorController.AddParameter("MonochromeLighting", AnimatorControllerParameterType.Float);
        animatorController.AddParameter("MonochromeLighting", AnimatorControllerParameterType.Float);
        animatorController.AddParameter("ResetLighting", AnimatorControllerParameterType.Bool);


        MakeLightLimit();
        MakeMonochromeLighting();
        MakeResetLighting();
    }

    public void MakeLightLimit()
    {
        AnimatorStateMachine animatorStateMachineLightLimit = AddLayer("라이팅 설정/밝기");
        AnimatorState actionStateLightLimit = MakeTimeState(animatorStateMachineLightLimit, "LightLimit");
        AnimationClip clip_LightLimit = new AnimationClip();
        foreach (RendererInfo rendererInfo in rendererInfos) {
            AnimationCurve maxLightCurve = new AnimationCurve();
            AddLinearKey(maxLightCurve, 0f, 0f);
            AddLinearKey(maxLightCurve, 0.25f, 0.5f);
            AddLinearKey(maxLightCurve, 0.5f, 1f);
            AddLinearKey(maxLightCurve, 0.8333333f, 1f);
            AddLinearKey(maxLightCurve, 1f, 2f);
            if (rendererInfo.isLiltoon) {
                clip_LightLimit.SetCurve(rendererInfo.path, typeof(SkinnedMeshRenderer), "material._LightMaxLimit", maxLightCurve);
            }
            if (rendererInfo.isPoiyomi) {
                clip_LightLimit.SetCurve(rendererInfo.path, typeof(SkinnedMeshRenderer), "material._LightingCap", maxLightCurve);
            }

            AnimationCurve minLightCurve = new AnimationCurve();
            AddLinearKey(minLightCurve, 0f, 0f);
            AddLinearKey(minLightCurve, 0.25f, 0f);
            AddLinearKey(minLightCurve, 0.5f, 0.1f);
            AddLinearKey(minLightCurve, 0.8333333f, 1f);
            AddLinearKey(minLightCurve, 1f, 2f);
            if (rendererInfo.isLiltoon) {
                clip_LightLimit.SetCurve(rendererInfo.path, typeof(SkinnedMeshRenderer), "material._LightMinLimit", minLightCurve);
            }
            if (rendererInfo.isPoiyomi) {
                clip_LightLimit.SetCurve(rendererInfo.path, typeof(SkinnedMeshRenderer), "material._LightingMinLightBrightness", minLightCurve);
            }
        }
        AssetDatabase.CreateAsset(clip_LightLimit, savePath+"/LightLimit.anim");
        actionStateLightLimit.motion = clip_LightLimit;
    }

    public void MakeMonochromeLighting()
    {
        AnimatorStateMachine animatorStateMachineMonochromeLighting = AddLayer("라이팅 설정/흑백화");
        AnimatorState actionState = MakeTimeState(animatorStateMachineMonochromeLighting, "MonochromeLighting");
        AnimationClip clip_MonochromeLighting = new AnimationClip();
        foreach (RendererInfo rendererInfo in rendererInfos) {
            AnimationCurve maxLightCurve = new AnimationCurve();
            AddLinearKey(maxLightCurve, 0f, 0f);
            AddLinearKey(maxLightCurve, 1f, 1f);
            if (rendererInfo.isLiltoon) {
                clip_MonochromeLighting.SetCurve(rendererInfo.path, typeof(SkinnedMeshRenderer), "material._MonochromeLighting", maxLightCurve);
            }
            if (rendererInfo.isPoiyomi) {
                clip_MonochromeLighting.SetCurve(rendererInfo.path, typeof(SkinnedMeshRenderer), "material._LightingMonochromatic", maxLightCurve);
            }
        }
        AssetDatabase.CreateAsset(clip_MonochromeLighting, savePath+"/MonochromeLighting.anim");
        actionState.motion = clip_MonochromeLighting;
    }

    public void MakeResetLighting()
    {
        AnimatorStateMachine animatorStateReset = AddLayer("라이팅 설정/초기화");
        AnimatorState idleState = animatorStateReset.AddState("Idle", new Vector3(30f, 60f, 0f));
        animatorStateReset.defaultState = idleState;
        idleState.speed = 1f;
        AnimatorStateTransition anyToIdle = animatorStateReset.AddAnyStateTransition(idleState);
        anyToIdle.AddCondition(AnimatorConditionMode.IfNot, 0f, "ResetLighting");
        anyToIdle.hasExitTime = false;
        anyToIdle.hasFixedDuration = true;
        anyToIdle.duration = 0f;
        anyToIdle.canTransitionToSelf = false;
        AnimatorState resetState = animatorStateReset.AddState("Reset", new Vector3(240f, 20f, 0f));
        resetState.speed = 1f;
        AnimatorStateTransition anyToReset = animatorStateReset.AddAnyStateTransition(resetState);
        anyToReset.AddCondition(AnimatorConditionMode.If, 0f, "ResetLighting");
        anyToReset.hasExitTime = false;
        anyToReset.hasFixedDuration = true;
        anyToReset.duration = 0f;
        anyToReset.canTransitionToSelf = false;
        VRCAvatarParameterDriver resetParameterDriver = resetState.AddStateMachineBehaviour<VRCAvatarParameterDriver>();
        resetParameterDriver.parameters = new List<VRC_AvatarParameterDriver.Parameter>(){
            new VRC_AvatarParameterDriver.Parameter {
                name = "LightLimit",
                type = VRC_AvatarParameterDriver.ChangeType.Set,
                value = 0.5f
            },
        };
        // if (useMonochrome) {
        resetParameterDriver.parameters.Add(
            new VRC_AvatarParameterDriver.Parameter {
                name = "MonochromeLighting",
                type = VRC_AvatarParameterDriver.ChangeType.Set,
                value = 0f
            }
        );
        // }
    }

    public void AddLinearKey(AnimationCurve curve, float time, float value)
    {
        Keyframe key = new Keyframe(time, value)
        {
            weightedMode = WeightedMode.Both,
            inWeight = 0f,
            inTangent = 0f,
            outWeight = 0f,
            outTangent = 0f,
        };
        curve.AddKey(key);
    }

    public void CheckAvatarRoot()
    {
        if (avatarRoot == null) {
            Transform root = GetAvatarRoot(transform);
            if (root != null) avatarRoot = root.GetComponent<VRCAvatarDescriptor>();
        }
    }

    public bool IsIgnore(Transform target)
    {
        foreach (Transform ignore in ignores) {
            if (target.IsChildOf(ignore)) return true;
        }
        return false;
    }

    public static string GetObjectPath(Transform current, Transform root = null)
    {
        System.Text.StringBuilder pathBuilder = new System.Text.StringBuilder();
        if (root == null) root = current.root;
        while (current != null)
        {
            if (current == root) break;
            if (pathBuilder.Length > 0) pathBuilder.Insert(0, "/");
            pathBuilder.Insert(0, current.name);
            current = current.parent;
        }
        return pathBuilder.ToString();
    }

    public Transform GetAvatarRoot(Transform current)
    {
        VRCAvatarDescriptor vrcAD = current.GetComponent<VRCAvatarDescriptor>();
        while (vrcAD == null && current.parent)
        {
            current = current.parent;
            vrcAD = current.GetComponent<VRCAvatarDescriptor>();
        }
        if (vrcAD == null) return null;
        return current;
    }

    public AnimatorStateMachine AddLayer(string name)
    {
        AnimatorStateMachine animatorStateMachine = new AnimatorStateMachine
        {
            name = animatorController.MakeUniqueLayerName(name),
            hideFlags = HideFlags.HideInHierarchy
        };

        animatorController.AddLayer(new AnimatorControllerLayer
        {
            stateMachine = animatorStateMachine,
            name = animatorStateMachine.name,
            defaultWeight = 1f
        });

        AssetDatabase.AddObjectToAsset(animatorStateMachine, animatorController);
        return animatorStateMachine;
    }

    public AnimatorState MakeTimeState(AnimatorStateMachine animatorStateMachine, string name)
    {
        AnimatorState actionState = animatorStateMachine.AddState(name, new Vector3(30f, 190f, 0f));
        animatorStateMachine.defaultState = actionState;
        actionState.speed = 1f;
        actionState.timeParameter = name;
        actionState.timeParameterActive = true;
        return actionState;
    }
    

    public void SetComponent<T>(ref T component) where T : MonoBehaviour
    {
        if (component == null)
        {
            component = GetComponent<T>(); // 컴포넌트를 가져옴
            if (component == null)
            {
                component = gameObject.AddComponent<T>(); // 컴포넌트가 없으면 추가
            }
        }
    }
}

[CustomEditor(typeof(LightControlGenerator))]
public class LightControlGeneratorEditor : Editor
{
    public override void OnInspectorGUI()
    {
        LightControlGenerator component = (LightControlGenerator)target;
        component.CheckAvatarRoot();
        if (component.avatarRoot == null) {
            EditorGUILayout.HelpBox("Avatar Root를 설정하거나 아바타 내에 배치해주세요.", MessageType.Warning);
        }

        base.OnInspectorGUI();

        GUILayout.Space(10);
        if (GUILayout.Button("Generate"))
        {
            component.Run();
        }
    }
}

#endif