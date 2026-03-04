using System;
using System.Linq.Expressions;
using System.Reflection;
using System.Reflection.Emit;
using UnityEngine.Bindings;

namespace MattyFixes.Utils;

public static class ReflectionUtils
{
    internal static Delegate FastGetter([NotNull] this FieldInfo field)
    {
        _ = field ?? throw new ArgumentNullException(nameof(field));
        var methodName = field.ReflectedType!.FullName + ".get_" + field.Name;
        var getterMethod = new DynamicMethod(methodName, field.FieldType, [field.DeclaringType], true);
        var gen = getterMethod.GetILGenerator();
        if (field.IsStatic)
        {
            gen.Emit(OpCodes.Ldsfld, field);
        }
        else
        {
            gen.Emit(OpCodes.Ldarg_0);
            gen.Emit(OpCodes.Ldfld, field);
        }

        gen.Emit(OpCodes.Ret);
        if (field.IsStatic)
            return getterMethod.CreateDelegate(Expression.GetFuncType(field.FieldType));
        return getterMethod.CreateDelegate(Expression.GetFuncType(field.DeclaringType, field.FieldType));
    }

    internal static Delegate FastSetter([NotNull] this FieldInfo field)
    {
        _ = field ?? throw new ArgumentNullException(nameof(field));
        var methodName = field.ReflectedType!.FullName + ".get_" + field.Name;
        var setterMethod = new DynamicMethod(methodName, field.FieldType, [field.DeclaringType, field.FieldType], true);
        var gen = setterMethod.GetILGenerator();
        if (field.IsStatic)
        {
            gen.Emit(OpCodes.Ldarg_1);
            gen.Emit(OpCodes.Stsfld, field);
        }
        else
        {
            gen.Emit(OpCodes.Ldarg_0);
            gen.Emit(OpCodes.Ldarg_1);
            gen.Emit(OpCodes.Stfld, field);
        }

        gen.Emit(OpCodes.Ldarg_1);
        gen.Emit(OpCodes.Ret);

        if (field.IsStatic)
            return setterMethod.CreateDelegate(
                Expression.GetFuncType(field.FieldType, field.FieldType));
        return setterMethod.CreateDelegate(
            Expression.GetFuncType(field.DeclaringType, field.FieldType, field.FieldType));
    }
}
