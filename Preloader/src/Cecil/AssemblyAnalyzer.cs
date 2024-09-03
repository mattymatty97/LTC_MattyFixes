using Mono.Cecil;
using Mono.Cecil.Cil;

namespace MattyFixes.Preloader.Cecil;

internal static class AssemblyAnalyzer
{
    internal static void ProcessAssembly(AssemblyDefinition assembly, FieldReference itemPropertiesField,
        FieldReference verticalOffsetField, FieldReference localVerticalOffsetField, out long callCount)
    {
        callCount = 0;
        foreach (var moduleDefinition in assembly.Modules)
            ProcessModule(assembly, moduleDefinition, itemPropertiesField, verticalOffsetField,
                localVerticalOffsetField, ref callCount);
    }

    private static void ProcessModule(AssemblyDefinition assembly, ModuleDefinition module,
        FieldReference itemPropertiesField,
        FieldReference verticalOffsetField, FieldReference localVerticalOffsetField, ref long callCount)
    {
        foreach (var typeDefinition in module.Types)
            ProcessType(assembly, typeDefinition, itemPropertiesField, verticalOffsetField, localVerticalOffsetField,
                ref callCount);
    }

    private static void ProcessType(AssemblyDefinition assembly, TypeDefinition type,
        FieldReference itemPropertiesField,
        FieldReference verticalOffsetField, FieldReference localVerticalOffsetField, ref long callCount)
    {
        foreach (var methodDefinition in type.Methods)
            ProcessMethod(assembly, type, methodDefinition, itemPropertiesField, verticalOffsetField,
                localVerticalOffsetField, ref callCount);
    }

    private static void ProcessMethod(AssemblyDefinition assembly, TypeDefinition type, MethodDefinition method,
        FieldReference itemPropertiesField,
        FieldReference verticalOffsetField, FieldReference localVerticalOffsetField, ref long callCount)
    {
        if (!method.HasBody)
            return;
        
        var instructions = method.Body.Instructions;

        for (var i = 0; i < instructions.Count; i++)
        {
            if (instructions[i].OpCode == OpCodes.Ldfld && instructions[i].Operand == itemPropertiesField &&
                instructions[i + 1].OpCode == OpCodes.Ldfld && instructions[i + 1].Operand == verticalOffsetField)
            {
                callCount++;
                instructions[i + 1].OpCode = OpCodes.Nop;
                instructions[i + 1].Operand = null;
                instructions[i].Operand = localVerticalOffsetField;
            } 
        }
    }
}