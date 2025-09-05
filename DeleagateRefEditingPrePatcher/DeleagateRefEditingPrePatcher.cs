using Mono.Cecil;
using Mono.Cecil.Cil;
using Mono.Cecil.Rocks;
using BepInEx.Preloader.Core.Patching;

namespace DeleagateRefEditing;

[PatcherPluginInfo(MyPluginInfo.PLUGIN_GUID, MyPluginInfo.PLUGIN_NAME, MyPluginInfo.PLUGIN_VERSION)]
public class Patcher : BasePatcher
{
    [TargetAssembly("FrooxEngine.dll")]
    public void PatchAssembly(AssemblyDefinition assembly)
    {
        var module = assembly.MainModule;

        var coremodule = module.TypeSystem.Object.Resolve().Module;

        var syncDelegateType = FindType(module, "FrooxEngine.SyncDelegate`1");
        var iSyncRefType = FindType(module, "FrooxEngine.ISyncRef");
        var DelegateType = FindType(coremodule, "System.Delegate");
        var MethodBaseType = FindType(coremodule, "System.Reflection.MethodBase");
        var MemberInfoType = FindType(coremodule, "System.Reflection.MemberInfo");
        var TypeType = FindType(coremodule, "System.Type");

        var targetMethod = FindMethod(syncDelegateType, "FrooxEngine.ISyncRef.TrySet");
        var GetType = FindMethod(TypeType, "GetType");
        var IsAssignableTo = FindMethod(TypeType, "IsAssignableTo");

        var get_Method = FindProperty(DelegateType, "Method").GetMethod;
        var get_Static = FindProperty(MethodBaseType, "IsStatic").GetMethod;
        var get_DeclaringType = FindProperty(MemberInfoType, "DeclaringType").GetMethod;
        var set_Target = FindProperty(iSyncRefType, "Target").SetMethod;

        var _targetField = FindField(syncDelegateType, "_target");




        var delagateRef = module.ImportReference(DelegateType);
        var MethodBaseRef = module.ImportReference(MethodBaseType);
        var MemberInfoRef = module.ImportReference(MemberInfoType);
        var TypeRef = module.ImportReference(TypeType);
        var GetTypeRef = module.ImportReference(GetType);
        var IsAssignableToRef = module.ImportReference(IsAssignableTo);
        var get_MethodRef = module.ImportReference(get_Method);
        var get_StaticRef = module.ImportReference(get_Static);
        var get_DeclaringTypeRef = module.ImportReference(get_DeclaringType);


        targetMethod.Body = new(targetMethod);
        var body = targetMethod.Body;
        body.InitLocals = false;
        var il = body.GetILProcessor();

        var fail = Instruction.Create(OpCodes.Ldc_I4_0);
        

        il.Emit(OpCodes.Ldarg_0);
        il.Emit(OpCodes.Ldfld, _targetField);
        il.Emit(OpCodes.Brfalse, fail);

        il.Emit(OpCodes.Ldarg_0);
        il.Emit(OpCodes.Ldfld, _targetField);
        il.Emit(OpCodes.Callvirt, get_MethodRef);
        il.Emit(OpCodes.Callvirt, get_StaticRef);
        il.Emit(OpCodes.Brtrue, fail);

        il.Emit(OpCodes.Ldarg_1);
        il.Emit(OpCodes.Callvirt, GetTypeRef);
        il.Emit(OpCodes.Ldarg_0);
        il.Emit(OpCodes.Ldfld, _targetField);
        il.Emit(OpCodes.Callvirt, get_MethodRef);
        il.Emit(OpCodes.Callvirt, get_DeclaringTypeRef);
        il.Emit(OpCodes.Callvirt, IsAssignableToRef);
        il.Emit(OpCodes.Brfalse, fail);

        il.Emit(OpCodes.Ldarg_0);
        il.Emit(OpCodes.Ldarg_1);
        il.Emit(OpCodes.Callvirt, set_Target);

        il.Emit(OpCodes.Ldc_I4_1);
        il.Emit(OpCodes.Ret);

        il.Append(fail);
        il.Emit(OpCodes.Ret);

        body.MaxStackSize = 3;
        body.OptimizeMacros();
    }

    static TypeDefinition FindType(ModuleDefinition module, string fullName)
    {
        if (module.GetType(fullName) is TypeDefinition td) return td;
        throw new InvalidOperationException("Type not found: " + fullName);
    }

    static MethodDefinition FindMethod(TypeDefinition type, string name) => type.Methods.OrderBy(m => m.Parameters.Count).FirstOrDefault(m => m.Name == name) ?? throw new InvalidOperationException($"Method not found: {type.FullName}.{name}");
    static PropertyDefinition FindProperty(TypeDefinition type, string name) => type.Properties.FirstOrDefault(p => p.Name == name) ?? throw new InvalidOperationException($"Property not found: {type.FullName}.{name}");
    static FieldDefinition FindField(TypeDefinition type, string name) => type.Fields.FirstOrDefault(f => f.Name == name) ?? throw new InvalidOperationException($"Field not found: {type.FullName}.{name}");
}