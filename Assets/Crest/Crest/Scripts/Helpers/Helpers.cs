// Crest Ocean System

// Copyright 2021 Wave Harmonic Ltd

namespace Crest
{
    using System.Collections.Generic;
    using System.Reflection;
    using UnityEngine;
    using UnityEngine.Experimental.Rendering;
    using UnityEngine.Rendering;
#if CREST_URP
    using UnityEngine.Rendering.Universal;
#endif
#if CREST_HDRP
    using UnityEngine.Rendering.HighDefinition;
#endif

#if !UNITY_2023_2_OR_NEWER
    using GraphicsFormatUsage = UnityEngine.Experimental.Rendering.FormatUsage;
#endif

    /// <summary>
    /// General purpose helpers which, at the moment, do not warrant a seperate file.
    /// </summary>
    public static class Helpers
    {
        internal static int SiblingIndexComparison(int x, int y) => x.CompareTo(y);

        /// <summary>
        /// Comparer that always returns less or greater, never equal, to get work around unique key constraint
        /// </summary>
        internal static int DuplicateComparison(int x, int y)
        {
            var result = x.CompareTo(y);
            // If non-zero, use result, otherwise return greater (never equal)
            return result != 0 ? result : 1;
        }

        public static BindingFlags s_AnyMethod = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance |
            BindingFlags.Static | BindingFlags.FlattenHierarchy;

        public static T GetCustomAttribute<T>(System.Type type) where T : System.Attribute
        {
            return (T)System.Attribute.GetCustomAttribute(type, typeof(T));
        }

        static WaitForEndOfFrame s_WaitForEndOfFrame = new WaitForEndOfFrame();
        public static WaitForEndOfFrame WaitForEndOfFrame => s_WaitForEndOfFrame;

        static Material s_UtilityMaterial;
        public static Material UtilityMaterial
        {
            get
            {
                if (s_UtilityMaterial == null)
                {
                    s_UtilityMaterial = new Material(Shader.Find("Hidden/Crest/Helpers/Utility"));
                }

                return s_UtilityMaterial;
            }
        }

        // Need to cast to int but no conversion cost.
        // https://stackoverflow.com/a/69148528
        internal enum UtilityPass
        {
            CopyColor,
            CopyDepth,
            ClearDepth,
            ClearStencil,
        }

        /// <summary>
        /// Uses PrefabUtility.InstantiatePrefab in editor and GameObject.Instantiate in standalone.
        /// </summary>
        public static GameObject InstantiatePrefab(GameObject prefab)
        {
#if UNITY_EDITOR
            return (GameObject)UnityEditor.PrefabUtility.InstantiatePrefab(prefab);
#else
            return GameObject.Instantiate(prefab);
#endif
        }

        // Taken from Unity
        // https://docs.unity3d.com/2022.2/Documentation/Manual/BestPracticeUnderstandingPerformanceInUnity5.html
        public static bool StartsWithNoAlloc(this string a, string b)
        {
            int aLen = a.Length;
            int bLen = b.Length;

            int ap = 0; int bp = 0;

            while (ap < aLen && bp < bLen && a[ap] == b[bp])
            {
                ap++;
                bp++;
            }

            return (bp == bLen);
        }

#if UNITY_EDITOR
        public static bool IsPreviewOfGameCamera(Camera camera)
        {
            // StartsWith has GC allocations. It is only used in the editor.
            return camera.cameraType == CameraType.Game && camera.name.StartsWithNoAlloc("Preview");
        }
#endif

        public static bool IsMSAAEnabled(Camera camera)
        {
#if CREST_HDRP
            if (RenderPipelineHelper.IsHighDefinition)
            {
                var hdCamera = HDCamera.GetOrCreate(camera);
                // Scene view camera does appear to support MSAA unlike other RPs.
                // Querying frame settings on the camera will give the correct results - overriden or not.
#if UNITY_2021_2_OR_NEWER
                return hdCamera.msaaSamples != MSAASamples.None;
#else
                return hdCamera.msaaSamples != MSAASamples.None && hdCamera.frameSettings.IsEnabled(FrameSettingsField.MSAA);
#endif
            }
#endif

            var isMSAA = camera.allowMSAA;
#if CREST_URP
            if (RenderPipelineHelper.IsUniversal)
            {
                // MSAA will be the same for every camera if XR rendering.
                isMSAA = isMSAA || XRHelpers.IsRunning;
            }
#endif

#if UNITY_EDITOR
            // Game View Preview ignores allowMSAA.
            isMSAA = isMSAA || IsPreviewOfGameCamera(camera);
            // Scene view doesn't support MSAA.
            isMSAA = isMSAA && camera.cameraType != CameraType.SceneView;
#endif

#if CREST_URP
            if (RenderPipelineHelper.IsUniversal)
            {
                // Keep this check last so it overrides everything else.
                isMSAA = isMSAA && camera.GetUniversalAdditionalCameraData().scriptableRenderer.supportedRenderingFeatures.msaa;
            }
#endif

            // QualitySettings.antiAliasing can be zero.
            return (isMSAA ? QualitySettings.antiAliasing : 1) > 1;
        }

        public static bool IsMotionVectorsEnabled()
        {
#if CREST_HDRP
            if (RenderPipelineHelper.IsHighDefinition)
            {
                // Only check the RP asset for now. This can happen at run-time, but a developer should not change the
                // quality setting when performance matters like gameplay.
                return (GraphicsSettings.currentRenderPipeline as HDRenderPipelineAsset)
                    .currentPlatformRenderPipelineSettings.supportMotionVectors;
            }
#endif // CREST_HDRP

            // Default to false until we support MVs.
            return false;
        }

        public static bool IsIntelGPU()
        {
            // Works for Windows and MacOS. Grabbed from Unity Graphics repository:
            // https://github.com/Unity-Technologies/Graphics/blob/68b0d42c/com.unity.render-pipelines.high-definition/Runtime/RenderPipeline/HDRenderPipeline.PostProcess.cs#L198-L199
            return SystemInfo.graphicsDeviceName.ToLowerInvariant().Contains("intel");
        }

        public static bool MaskIncludesLayer(int mask, int layer)
        {
            // Taken from:
            // http://answers.unity.com/answers/1332280/view.html
            return mask == (mask | (1 << layer));
        }

        public static void Swap<T>(ref T a, ref T b)
        {
            var temp = b;
            b = a;
            a = temp;
        }

        public static void ClearRenderTexture(RenderTexture texture, Color clear, bool depth = true, bool color = true)
        {
            var active = RenderTexture.active;

            // Using RenderTexture.active will not write to all slices.
            Graphics.SetRenderTarget(texture, 0, CubemapFace.Unknown, -1);
            // TODO: Do we need to disable GL.sRGBWrite as it is linear to linear.
            GL.Clear(depth, color, clear);

            // Graphics.SetRenderTarget can be equivalent to setting RenderTexture.active:
            // https://docs.unity3d.com/ScriptReference/Graphics.SetRenderTarget.html
            // Restore previous active texture or it can incur a warning when releasing:
            // Releasing render texture that is set to be RenderTexture.active!
            RenderTexture.active = active;
        }

        // R16G16B16A16_SFloat appears to be the most compatible format.
        // https://docs.unity3d.com/Manual/class-TextureImporterOverride.html#texture-compression-support-platforms
        // https://learn.microsoft.com/en-us/windows/win32/direct3d12/typed-unordered-access-view-loads#supported-formats-and-api-calls
        readonly static GraphicsFormat s_FallbackGraphicsFormat = GraphicsFormat.R16G16B16A16_SFloat;

#if UNITY_2021_3_OR_NEWER
#if CREST_VERIFYRANDOMWRITESUPPORT
        static bool SupportsRandomWriteOnRenderTextureFormat(GraphicsFormat format)
        {
            var rtFormat = GraphicsFormatUtility.GetRenderTextureFormat(format);
            return System.Enum.IsDefined(typeof(RenderTextureFormat), rtFormat)
                && SystemInfo.SupportsRandomWriteOnRenderTextureFormat(rtFormat);
        }
#endif
#endif

        internal static GraphicsFormat GetCompatibleTextureFormat(GraphicsFormat format, GraphicsFormatUsage usage, bool randomWrite = false)
        {
            var useFallback = false;
            var result = SystemInfo.GetCompatibleFormat(format, usage);

            if (result == GraphicsFormat.None)
            {
                Debug.Log($"Crest: The graphics device does not support the render texture format {format}. Will attempt to use fallback.");
                useFallback = true;
            }
            else if (result != format)
            {
                Debug.Log($"Crest: Using render texture format {result} instead of {format}.");
            }

#if UNITY_2021_3_OR_NEWER
#if CREST_VERIFYRANDOMWRITESUPPORT
            if (!useFallback && randomWrite && !SupportsRandomWriteOnRenderTextureFormat(result))
            {
                Debug.Log($"Crest: The graphics device does not support the render texture format {result} with random read/write. Will attempt to use fallback.");
                useFallback = true;
            }
#endif
#endif

            // Check if fallback is compatible before using it.
            if (useFallback && format == s_FallbackGraphicsFormat)
            {
                Debug.Log($"Crest: Fallback {s_FallbackGraphicsFormat} is not supported on this device. Please inform us.");
                useFallback = false;
            }

            if (useFallback)
            {
                result = s_FallbackGraphicsFormat;
            }

            return result;
        }

        public static void SetGlobalKeyword(string keyword, bool enabled)
        {
            if (enabled)
            {
                Shader.EnableKeyword(keyword);
            }
            else
            {
                Shader.DisableKeyword(keyword);
            }
        }

        public static void RenderTargetIdentifierXR(ref RenderTexture texture, ref RenderTargetIdentifier target)
        {
            target = new RenderTargetIdentifier
            (
                texture,
                mipLevel: 0,
                CubemapFace.Unknown,
                depthSlice: -1 // Bind all XR slices.
            );
        }

        public static RenderTargetIdentifier RenderTargetIdentifierXR(int id) => new RenderTargetIdentifier
        (
            id,
            mipLevel: 0,
            CubemapFace.Unknown,
            depthSlice: -1  // Bind all XR slices.
        );

        /// <summary>
        /// Creates an RT reference and adds it to the RTI. Native object behind RT is not created so you can change its
        /// properties before being used.
        /// </summary>
        public static void CreateRenderTargetTextureReference(ref RenderTexture texture, ref RenderTargetIdentifier target)
        {
            // Do not overwrite reference or it will create reference leak.
            if (texture == null)
            {
                // Dummy values. We are only creating an RT reference, not an RT native object. RT should be configured
                // properly before using or calling Create.
                texture = new RenderTexture(0, 0, 0);
            }

            // Always call this in case of recompilation as RTI will lose its reference to the RT.
            RenderTargetIdentifierXR(ref texture, ref target);
        }

        /// <summary>
        /// Creates an RT with an RTD if it does not exist or assigns RTD to RT (RT should be released first). This
        /// prevents reference leaks.
        /// </summary>
        /// <remarks>
        /// Afterwards call <a href="https://docs.unity3d.com/ScriptReference/RenderTexture.Create.html">Create</a> if
        /// necessary or <a href="https://docs.unity3d.com/ScriptReference/RenderTexture-active.html">let Unity handle
        /// it</a>.
        /// </remarks>
        public static void SafeCreateRenderTexture(ref RenderTexture texture, RenderTextureDescriptor descriptor)
        {
            // Do not overwrite reference or it will create reference leak.
            if (texture == null)
            {
                texture = new RenderTexture(descriptor);
            }
            else
            {
                texture.descriptor = descriptor;
            }
        }

        public static bool RenderTargetTextureNeedsUpdating(RenderTexture texture, RenderTextureDescriptor descriptor)
        {
            return
                descriptor.width != texture.width ||
                descriptor.height != texture.height ||
                descriptor.volumeDepth != texture.volumeDepth ||
                descriptor.useDynamicScale != texture.useDynamicScale;
        }

        /// <summary>
        /// Uses Destroy in play mode or DestroyImmediate in edit mode.
        /// </summary>
        public static void Destroy(Object @object)
        {
#if UNITY_EDITOR
            // We must use DestroyImmediate in edit mode. As it apparently has an overhead, use recommended Destroy in
            // play mode. DestroyImmediate is generally recommended in edit mode by Unity:
            // https://docs.unity3d.com/ScriptReference/Object.DestroyImmediate.html
            if (!Application.isPlaying)
            {
                Object.DestroyImmediate(@object);
            }
            else
#endif
            {
                Object.Destroy(@object);
            }
        }

        internal static T[] FindObjectsByType<T>() where T : Object
        {
#if UNITY_2023_3_OR_NEWER
            return Object.FindObjectsByType<T>(FindObjectsSortMode.None);
#else
            return Object.FindObjectsOfType<T>();
#endif
        }

        internal static T FindFirstObjectByType<T>() where T : Object
        {
#if UNITY_2023_3_OR_NEWER
            return Object.FindFirstObjectByType<T>();
#else
            return Object.FindObjectOfType<T>();
#endif
        }

        public static void SetRenderTarget(CommandBuffer buffer, RenderTargetIdentifier target)
        {
            CoreUtils.SetRenderTarget(buffer, target);
        }

        public static void SetRenderTarget(CommandBuffer buffer, RenderTargetIdentifier color, RenderTargetIdentifier depth)
        {
            CoreUtils.SetRenderTarget(buffer, color, depth);
        }

        /// <summary>
        /// Blit using full screen triangle. Supports more features than CommandBuffer.Blit like the RenderPipeline tag
        /// in sub-shaders. Never use for data.
        /// </summary>
        public static void Blit(CommandBuffer buffer, RenderTargetIdentifier target, Material material, int pass = -1, MaterialPropertyBlock properties = null)
        {
            SetRenderTarget(buffer, target);
            buffer.DrawProcedural
            (
                Matrix4x4.identity,
                material,
                pass,
                MeshTopology.Triangles,
                vertexCount: 3,
                instanceCount: 1,
                properties
            );
        }

        /// <summary>
        /// Blit using full screen triangle. Supports more features than CommandBuffer.Blit like the RenderPipeline tag
        /// in sub-shaders. Never use for fullscreen effects.
        /// </summary>
        public static void Blit(CommandBuffer buffer, RenderTexture target, Material material, int pass = -1, int depthSlice = -1, MaterialPropertyBlock properties = null)
        {
            buffer.SetRenderTarget(target, mipLevel: 0, CubemapFace.Unknown, depthSlice);
            buffer.DrawProcedural
            (
                Matrix4x4.identity,
                material,
                pass,
                MeshTopology.Triangles,
                vertexCount: 3,
                instanceCount: 1,
                properties
            );
        }

        public static void SetShaderVector(Material material, int nameID, Vector4 value, bool global = false)
        {
            if (global)
            {
                Shader.SetGlobalVector(nameID, value);
            }
            else
            {
                material.SetVector(nameID, value);
            }
        }

        public static void SetShaderInt(Material material, int nameID, int value, bool global = false)
        {
            if (global)
            {
                Shader.SetGlobalInt(nameID, value);
            }
            else
            {
                material.SetInt(nameID, value);
            }
        }

#if CREST_URP
        readonly static List<bool> s_RenderFeatureActiveStates = new List<bool>();
        readonly static FieldInfo s_RenderDataListField = typeof(UniversalRenderPipelineAsset)
                        .GetField("m_RendererDataList", BindingFlags.NonPublic | BindingFlags.Instance);
        readonly static FieldInfo s_DefaultRendererIndex = typeof(UniversalRenderPipelineAsset)
                        .GetField("m_DefaultRendererIndex", BindingFlags.NonPublic | BindingFlags.Instance);
        readonly static FieldInfo s_RendererIndex = typeof(UniversalAdditionalCameraData)
                        .GetField("m_RendererIndex", BindingFlags.NonPublic | BindingFlags.Instance);

        internal static ScriptableRendererData[] UniversalRendererData(UniversalRenderPipelineAsset asset) =>
            (ScriptableRendererData[])s_RenderDataListField.GetValue(asset);

        internal static int GetRendererIndex(Camera camera)
        {
            var rendererIndex = (int)s_RendererIndex.GetValue(camera.GetUniversalAdditionalCameraData());

            if (rendererIndex < 0)
            {
                rendererIndex = (int)s_DefaultRendererIndex.GetValue(UniversalRenderPipeline.asset);
            }

            return rendererIndex;
        }

        internal static bool IsSSAOEnabled(Camera camera)
        {
            // Get this every time as it could change.
            var renderers = (ScriptableRendererData[])s_RenderDataListField.GetValue(UniversalRenderPipeline.asset);
            var rendererIndex = GetRendererIndex(camera);

            foreach (var feature in renderers[rendererIndex].rendererFeatures)
            {
                if (feature.GetType().Name == "ScreenSpaceAmbientOcclusion")
                {
                    return feature.isActive;
                }
            }

            return false;
        }

        internal static void RenderCameraWithoutCustomPasses(Camera camera)
        {
            // Get this every time as it could change.
            var renderers = (ScriptableRendererData[])s_RenderDataListField.GetValue(UniversalRenderPipeline.asset);
            var rendererIndex = GetRendererIndex(camera);

            foreach (var feature in renderers[rendererIndex].rendererFeatures)
            {
                s_RenderFeatureActiveStates.Add(feature.isActive);
                feature.SetActive(false);
            }

            camera.Render();

            var index = 0;
            foreach (var feature in renderers[rendererIndex].rendererFeatures)
            {
                feature.SetActive(s_RenderFeatureActiveStates[index++]);
            }

            s_RenderFeatureActiveStates.Clear();
        }
#endif
    }

    namespace Internal
    {
        static class Extensions
        {
            // Swizzle
            public static Vector2 XZ(this Vector3 v) => new Vector2(v.x, v.z);
            public static Vector2 XY(this Vector4 v) => new Vector2(v.x, v.y);
            public static Vector2 ZW(this Vector4 v) => new Vector2(v.z, v.w);
            public static Vector3 XNZ(this Vector2 v, float n = 0f) => new Vector3(v.x, n, v.y);
            public static Vector3 XNZ(this Vector3 v, float n = 0f) => new Vector3(v.x, n, v.z);
            public static Vector3 XNN(this Vector3 v, float n = 0f) => new Vector3(v.x, n, n);
            public static Vector3 NNZ(this Vector3 v, float n = 0f) => new Vector3(n, n, v.z);
            public static Vector4 XYNN(this Vector2 v, float n = 0f) => new Vector4(v.x, v.y, n, n);
            public static Vector4 NNZW(this Vector2 v, float n = 0f) => new Vector4(n, n, v.x, v.y);

            public static void SetKeyword(this Material material, string keyword, bool enabled)
            {
                if (enabled)
                {
                    material.EnableKeyword(keyword);
                }
                else
                {
                    material.DisableKeyword(keyword);
                }
            }

            public static void SetKeyword(this ComputeShader shader, string keyword, bool enabled)
            {
                if (enabled)
                {
                    shader.EnableKeyword(keyword);
                }
                else
                {
                    shader.DisableKeyword(keyword);
                }
            }

            public static void SetShaderKeyword(this CommandBuffer buffer, string keyword, bool enabled)
            {
                if (enabled)
                {
                    buffer.EnableShaderKeyword(keyword);
                }
                else
                {
                    buffer.DisableShaderKeyword(keyword);
                }
            }

            public static Color MaybeLinear(this Color color)
            {
                return QualitySettings.activeColorSpace == ColorSpace.Linear ? color.linear : color;
            }

            public static Color MaybeGamma(this Color color)
            {
                return QualitySettings.activeColorSpace == ColorSpace.Linear ? color : color.gamma;
            }

            public static Color FinalColor(this Light light)
            {
                var linear = GraphicsSettings.lightsUseLinearIntensity;
                var color = linear ? light.color.linear : light.color;
                color *= light.intensity;
                if (linear && light.useColorTemperature) color *= Mathf.CorrelatedColorTemperatureToRGB(light.colorTemperature);
                if (!linear) color = color.MaybeLinear();
                return linear ? color.MaybeGamma() : color;
            }

            ///<summary>
            /// Sets the msaaSamples property to the highest supported MSAA level in the settings.
            ///</summary>
            public static void SetMSAASamples(this ref RenderTextureDescriptor descriptor, Camera camera)
            {
                // QualitySettings.antiAliasing is zero when disabled which is invalid for msaaSamples.
                // We need to set this first as GetRenderTextureSupportedMSAASampleCount uses it:
                // https://docs.unity3d.com/ScriptReference/SystemInfo.GetRenderTextureSupportedMSAASampleCount.html
                descriptor.msaaSamples = Helpers.IsMSAAEnabled(camera) ? Mathf.Max(QualitySettings.antiAliasing, 1) : 1;
                descriptor.msaaSamples = SystemInfo.GetRenderTextureSupportedMSAASampleCount(descriptor);
            }

            public static Vector3 LinearVelocity(this Rigidbody rigidbody)
            {
#if UNITY_2023_3_OR_NEWER
                return rigidbody.linearVelocity;
#else
                return rigidbody.velocity;
#endif
            }
        }
    }

}
