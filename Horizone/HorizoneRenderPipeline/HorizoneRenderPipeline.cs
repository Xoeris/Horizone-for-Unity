using UnityEngine;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Reflection; 
using Mono.Cecil;
using Mono.Cecil.Cil;
using Mono.Cecil.Rocks;
using Unity.CompilationPipeline.Common.Diagnostics;
using Unity.CompilationPipeline.Common.ILPostProcessing;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;
using Object = System.Object;

namespace HorizoneRenderPipeline
{
    // ===================================================================================
    // 1. UTILS EXTENSION 
    // (This class is just a placeholder. The actual patching happens in HDRPILPP class below)
    // ===================================================================================
    public static class HDUtilsExtension
    {
        internal static bool NewIsSupportedGraphicDevice(GraphicsDeviceType graphicDevice)
        {
            return (graphicDevice == GraphicsDeviceType.Direct3D11 ||
                    graphicDevice == GraphicsDeviceType.Direct3D12 ||
                    graphicDevice == GraphicsDeviceType.PlayStation4 ||
                    graphicDevice == GraphicsDeviceType.PlayStation5 ||
                    graphicDevice == GraphicsDeviceType.PlayStation5NGGC ||
                    graphicDevice == GraphicsDeviceType.XboxOne ||
                    graphicDevice == GraphicsDeviceType.XboxOneD3D12 ||
                    graphicDevice == GraphicsDeviceType.GameCoreXboxOne ||
                    graphicDevice == GraphicsDeviceType.GameCoreXboxSeries ||
                    graphicDevice == GraphicsDeviceType.Metal ||
                    graphicDevice == GraphicsDeviceType.Vulkan ||
                    graphicDevice == GraphicsDeviceType.Switch);
        }

#if UNITY_EDITOR
        // This function can't be in HDEditorUtils because we need it in HDRenderPipeline.cs (and HDEditorUtils is in an editor asmdef)
        internal static bool NewIsSupportedBuildTarget(UnityEditor.BuildTarget buildTarget)
        {
            return (buildTarget == UnityEditor.BuildTarget.StandaloneWindows ||
                buildTarget == UnityEditor.BuildTarget.StandaloneWindows64 ||
                buildTarget == UnityEditor.BuildTarget.StandaloneLinux64 ||
                buildTarget == UnityEditor.BuildTarget.Stadia ||
                buildTarget == UnityEditor.BuildTarget.StandaloneOSX ||
                buildTarget == UnityEditor.BuildTarget.WSAPlayer ||
                buildTarget == UnityEditor.BuildTarget.XboxOne ||
                buildTarget == UnityEditor.BuildTarget.GameCoreXboxOne ||
                buildTarget == UnityEditor.BuildTarget.GameCoreXboxSeries  ||
                buildTarget == UnityEditor.BuildTarget.PS4 ||
                buildTarget == UnityEditor.BuildTarget.PS5 ||
                buildTarget == UnityEditor.BuildTarget.iOS ||
                buildTarget == UnityEditor.BuildTarget.Switch ||
                buildTarget == UnityEditor.BuildTarget.Android ||
                buildTarget == UnityEditor.BuildTarget.VisionOS ||
                buildTarget == UnityEditor.BuildTarget.LinuxHeadlessSimulation);
        }
#endif
    }

    // ===================================================================================
    // 2. CODE GEN HELPERS
    // ===================================================================================
    internal static class CodeGenHelpers
    {
        public const string UnityModuleName = "UnityEngine.CoreModule.dll";
        public const string RuntimeAssemblyName = "Unity.RenderPipelines.HighDefinition.Runtime";

        public static readonly string UnityColor_FullName = typeof(Color).FullName;
        public static readonly string UnityColor32_FullName = typeof(Color32).FullName;
        public static readonly string UnityVector2_FullName = typeof(Vector2).FullName;
        public static readonly string UnityVector3_FullName = typeof(Vector3).FullName;
        public static readonly string UnityVector4_FullName = typeof(Vector4).FullName;
        public static readonly string UnityQuaternion_FullName = typeof(Quaternion).FullName;
        public static readonly string UnityRay_FullName = typeof(Ray).FullName;
        public static readonly string UnityRay2D_FullName = typeof(Ray2D).FullName;

        public static uint Hash(this MethodDefinition methodDefinition)
        {
            var sigArr = Encoding.UTF8.GetBytes($"{methodDefinition.Module.Name} / {methodDefinition.FullName}");
            return (uint)sigArr.GetHashCode();
        }

        public static bool IsSubclassOf(this TypeDefinition typeDefinition, string classTypeFullName)
        {
            if (typeDefinition == null || !typeDefinition.IsClass) return false;

            var baseTypeRef = typeDefinition.BaseType;
            while (baseTypeRef != null)
            {
                if (baseTypeRef.FullName == classTypeFullName) return true;
                try
                {
                    baseTypeRef = baseTypeRef.Resolve().BaseType;
                }
                catch
                {
                    return false;
                }
            }
            return false;
        }

        public static bool IsSerializable(this TypeReference typeReference)
        {
            var typeSystem = typeReference.Module.TypeSystem;

            if (typeReference == typeSystem.Boolean || typeReference == typeSystem.Char || 
                typeReference == typeSystem.SByte || typeReference == typeSystem.Byte ||
                typeReference == typeSystem.Int16 || typeReference == typeSystem.UInt16 || 
                typeReference == typeSystem.Int32 || typeReference == typeSystem.UInt32 || 
                typeReference == typeSystem.Int64 || typeReference == typeSystem.UInt64 ||
                typeReference == typeSystem.Single || typeReference == typeSystem.Double || 
                typeReference == typeSystem.String)
            {
                return true;
            }

            if (typeReference.FullName == UnityColor_FullName || typeReference.FullName == UnityColor32_FullName ||
                typeReference.FullName == UnityVector2_FullName || typeReference.FullName == UnityVector3_FullName ||
                typeReference.FullName == UnityVector4_FullName || typeReference.FullName == UnityQuaternion_FullName ||
                typeReference.FullName == UnityRay_FullName || typeReference.FullName == UnityRay2D_FullName)
            {
                return true;
            }

            if (typeReference.GetEnumAsInt() != null) return true;

            if (typeReference.IsArray) return typeReference.GetElementType().IsSerializable();

            return false;
        }

        public static TypeReference GetEnumAsInt(this TypeReference typeReference)
        {
            if (typeReference.IsArray) return null;

            try
            {
                var typeDef = typeReference.Resolve();
                return typeDef.IsEnum ? typeDef.GetEnumUnderlyingType() : null;
            }
            catch
            {
                return null;
            }
        }

        public static void AddError(this List<DiagnosticMessage> diagnostics, string message)
        {
            diagnostics.Add(new DiagnosticMessage { DiagnosticType = DiagnosticType.Error, MessageData = message });
        }

        public static void AddError(this List<DiagnosticMessage> diagnostics, SequencePoint sequencePoint, string message)
        {
            diagnostics.Add(new DiagnosticMessage
            {
                DiagnosticType = DiagnosticType.Error,
                File = sequencePoint?.Document.Url.Replace($"{Environment.CurrentDirectory}{Path.DirectorySeparatorChar}", ""),
                Line = sequencePoint?.StartLine ?? 0,
                Column = sequencePoint?.StartColumn ?? 0,
                MessageData = $" - {message}"
            });
        }

        public static void AddWarning(this List<DiagnosticMessage> diagnostics, SequencePoint sequencePoint, string message)
        {
            diagnostics.Add(new DiagnosticMessage
            {
                DiagnosticType = DiagnosticType.Warning,
                File = sequencePoint?.Document.Url.Replace($"{Environment.CurrentDirectory}{Path.DirectorySeparatorChar}", ""),
                Line = sequencePoint?.StartLine ?? 0,
                Column = sequencePoint?.StartColumn ?? 0,
                MessageData = $" - {message}"
            });
        }

        public static AssemblyDefinition AssemblyDefinitionFor(ICompiledAssembly compiledAssembly, out PostProcessorAssemblyResolver assemblyResolver)
        {
            assemblyResolver = new PostProcessorAssemblyResolver(compiledAssembly);
            var readerParameters = new ReaderParameters
            {
                SymbolStream = new MemoryStream(compiledAssembly.InMemoryAssembly.PdbData),
                SymbolReaderProvider = new PortablePdbReaderProvider(),
                AssemblyResolver = assemblyResolver,
                ReflectionImporterProvider = new PostProcessorReflectionImporterProvider(),
                ReadingMode = ReadingMode.Immediate
            };

            var assemblyDefinition = AssemblyDefinition.ReadAssembly(new MemoryStream(compiledAssembly.InMemoryAssembly.PeData), readerParameters);
            assemblyResolver.AddAssemblyDefinitionBeingOperatedOn(assemblyDefinition);
            return assemblyDefinition;
        }

        private static void SearchForBaseModulesRecursive(AssemblyDefinition assemblyDefinition, PostProcessorAssemblyResolver assemblyResolver, ref ModuleDefinition unityModule, ref ModuleDefinition hdrpModule, HashSet<string> visited)
        {
            foreach (var module in assemblyDefinition.Modules)
            {
                if (module == null) continue;
                if (unityModule != null && hdrpModule != null) return;

                if (unityModule == null && module.Name == UnityModuleName)
                {
                    unityModule = module;
                    continue;
                }

                if (hdrpModule == null && module.Name == RuntimeAssemblyName)
                {
                    hdrpModule = module;
                    continue;
                }
            }

            if (unityModule != null && hdrpModule != null) return;

            foreach (var assemblyNameReference in assemblyDefinition.MainModule.AssemblyReferences)
            {
                if (assemblyNameReference == null) continue;
                if (visited.Contains(assemblyNameReference.Name)) continue;

                visited.Add(assemblyNameReference.Name);

                var assembly = assemblyResolver.Resolve(assemblyNameReference);
                if (assembly == null) continue;

                SearchForBaseModulesRecursive(assembly, assemblyResolver, ref unityModule, ref hdrpModule, visited);

                if (unityModule != null && hdrpModule != null) return;
            }
        }

        public static (ModuleDefinition UnityModule, ModuleDefinition HDRPModule) FindBaseModules(AssemblyDefinition assemblyDefinition, PostProcessorAssemblyResolver assemblyResolver)
        {
            ModuleDefinition unityModule = null;
            ModuleDefinition hdrpModule = null;
            var visited = new HashSet<string>();
            SearchForBaseModulesRecursive(assemblyDefinition, assemblyResolver, ref unityModule, ref hdrpModule, visited);

            return (unityModule, hdrpModule);
        }
    }

    // ===================================================================================
    // 3. IL POST PROCESSOR
    // ===================================================================================
    class HDRPILPP : ILPostProcessor
    {
        PostProcessorAssemblyResolver m_AssemblyResolver;
        ModuleDefinition m_UnityModule;
        ModuleDefinition m_HDRPModule;
        ModuleDefinition m_MainModule;

        readonly List<DiagnosticMessage> m_Diagnostics = new List<DiagnosticMessage>();

        public override ILPostProcessor GetInstance() => this;

        public override bool WillProcess(ICompiledAssembly compiledAssembly)
        {
            return compiledAssembly.Name == "Unity.RenderPipelines.HighDefinition.Runtime";
        }

        public override ILPostProcessResult Process(ICompiledAssembly compiledAssembly)
        {
            if (!WillProcess(compiledAssembly)) return null;

            m_Diagnostics.Clear();

            var assemblyDefinition = CodeGenHelpers.AssemblyDefinitionFor(compiledAssembly, out m_AssemblyResolver);
            if (assemblyDefinition == null)
            {
                m_Diagnostics.AddError($"Cannot read assembly definition: {compiledAssembly.Name}");
                return new ILPostProcessResult(null, m_Diagnostics);
            }

            (m_UnityModule, m_HDRPModule) = CodeGenHelpers.FindBaseModules(assemblyDefinition, m_AssemblyResolver);

            var mainModule = assemblyDefinition.MainModule;
            if (mainModule != null)
            {
                m_MainModule = mainModule;
            }
            else
            {
                m_Diagnostics.AddError("Main module null!");
                return new ILPostProcessResult(null, m_Diagnostics);
            }

            PostProcessAllTypes(m_Diagnostics, m_MainModule.GetAllTypes(), compiledAssembly.Defines);

            var pe = new MemoryStream();
            var pdb = new MemoryStream();
            var writerParameters = new WriterParameters
            {
                SymbolWriterProvider = new PortablePdbWriterProvider(), SymbolStream = pdb, WriteSymbols = true
            };

            assemblyDefinition.Write(pe, writerParameters);
            return new ILPostProcessResult(new InMemoryAssembly(pe.ToArray(), pdb.ToArray()), m_Diagnostics);
        }

        void PostProcessAllTypes(List<DiagnosticMessage> messages, IEnumerable<TypeDefinition> types, string[] assemblyDefines)
        {
            var typeList = types.ToList();
            
            // We search for HDUtils inside HDRP
            foreach(var type in typeList)
            {
                if(type.Name == "HDUtils" || type.Name == "HDUtilsExtension")
                {
                    foreach(var method in type.Methods)
                    {
                        if(method.Name == "IsSupportedBuildTarget" || method.Name == "IsSupportedGraphicDevice")
                        {
                            // Manual patch: Clear all instructions and force "return true"
                            method.Body.Instructions.Clear();
                            method.Body.Variables.Clear();
                            method.Body.ExceptionHandlers.Clear();
                            
                            var processor = method.Body.GetILProcessor();
                            processor.Emit(OpCodes.Ldc_I4_1); // true
                            processor.Emit(OpCodes.Ret);
                        }
                    }
                }
            }
        }
    }

    // ===================================================================================
    // 4. ASSEMBLY RESOLVER
    // ===================================================================================
    internal class PostProcessorAssemblyResolver : IAssemblyResolver
    {
        private readonly string[] m_AssemblyReferences;
        private readonly Dictionary<string, AssemblyDefinition> m_AssemblyCache = new Dictionary<string, AssemblyDefinition>();
        private readonly ICompiledAssembly m_CompiledAssembly;
        private AssemblyDefinition m_SelfAssembly;

        public PostProcessorAssemblyResolver(ICompiledAssembly compiledAssembly)
        {
            m_CompiledAssembly = compiledAssembly;
            m_AssemblyReferences = compiledAssembly.References;
        }

        public void Dispose() { }

        public AssemblyDefinition Resolve(AssemblyNameReference name) => Resolve(name, new ReaderParameters(ReadingMode.Deferred));

        public AssemblyDefinition Resolve(AssemblyNameReference name, ReaderParameters parameters)
        {
            lock (m_AssemblyCache)
            {
                if (name.Name == m_CompiledAssembly.Name)
                    return m_SelfAssembly;

                var fileName = FindFile(name);
                if (fileName == null) return null;

                var lastWriteTime = File.GetLastWriteTime(fileName);
                var cacheKey = $"{fileName}{lastWriteTime}";
                if (m_AssemblyCache.TryGetValue(cacheKey, out var result))
                    return result;

                parameters.AssemblyResolver = this;
                var ms = MemoryStreamFor(fileName);
                var pdb = $"{fileName}.pdb";
                if (File.Exists(pdb)) parameters.SymbolStream = MemoryStreamFor(pdb);

                var assemblyDefinition = AssemblyDefinition.ReadAssembly(ms, parameters);
                m_AssemblyCache.Add(cacheKey, assemblyDefinition);
                return assemblyDefinition;
            }
        }

        private string FindFile(AssemblyNameReference name)
        {
            var fileName = m_AssemblyReferences.FirstOrDefault(r => Path.GetFileName(r) == $"{name.Name}.dll");
            if (fileName != null) return fileName;
            fileName = m_AssemblyReferences.FirstOrDefault(r => Path.GetFileName(r) == $"{name.Name}.exe");
            if (fileName != null) return fileName;
            return m_AssemblyReferences.Select(Path.GetDirectoryName).Distinct().Select(parentDir => Path.Combine(parentDir, $"{name.Name}.dll")).FirstOrDefault(File.Exists);
        }

        private static MemoryStream MemoryStreamFor(string fileName)
        {
            return Retry(10, TimeSpan.FromSeconds(1), () =>
            {
                byte[] byteArray;
                using var fileStream = new FileStream(fileName, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                byteArray = new byte[fileStream.Length];
                var readLength = fileStream.Read(byteArray, 0, (int)fileStream.Length);
                if (readLength != fileStream.Length) throw new InvalidOperationException("File read length is not full length of file.");
                return new MemoryStream(byteArray);
            });
        }

        private static MemoryStream Retry(int retryCount, TimeSpan waitTime, Func<MemoryStream> func)
        {
            try { return func(); }
            catch (IOException)
            {
                if (retryCount == 0) throw;
                Thread.Sleep(waitTime);
                return Retry(retryCount - 1, waitTime, func);
            }
        }

        public void AddAssemblyDefinitionBeingOperatedOn(AssemblyDefinition assemblyDefinition)
        {
            m_SelfAssembly = assemblyDefinition;
        }
    }

    // ===================================================================================
    // 5. REFLECTION IMPORTER & PROVIDER
    // ===================================================================================
    internal class PostProcessorReflectionImporter : DefaultReflectionImporter
    {
        private const string k_SystemPrivateCoreLib = "System.Private.CoreLib";
        private readonly AssemblyNameReference m_CorrectCorlib;

        public PostProcessorReflectionImporter(ModuleDefinition module) : base(module)
        {
            m_CorrectCorlib = module.AssemblyReferences.FirstOrDefault(a => a.Name == "mscorlib" || a.Name == "netstandard" || a.Name == k_SystemPrivateCoreLib);
        }

        public override AssemblyNameReference ImportReference(AssemblyName reference)
        {
            return m_CorrectCorlib != null && reference.Name == k_SystemPrivateCoreLib ? m_CorrectCorlib : base.ImportReference(reference);
        }
    }

    internal class PostProcessorReflectionImporterProvider : IReflectionImporterProvider
    {
        public IReflectionImporter GetReflectionImporter(ModuleDefinition moduleDefinition)
        {
            return new PostProcessorReflectionImporter(moduleDefinition);
        }
    }
}