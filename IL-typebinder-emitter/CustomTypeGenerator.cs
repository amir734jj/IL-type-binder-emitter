using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;

namespace IL_typebinder_emitter
{
    /// <summary>
    /// Creates a new type dynamically
    /// </summary>
    public class CustomTypeGenerator<TSource, TCommon>
    {
        private readonly TypeBuilder _tb;

        private readonly FieldBuilder _entityFieldBldr;

        private readonly Type _srcType;
        
        private readonly Type _cmType;

        /// <summary>
        /// Initialize custom type builder
        /// </summary>
        public CustomTypeGenerator(Dictionary<string, (Type Type, string SourcePrpName)> members)
        {
            _cmType = typeof(TCommon);
            _srcType = typeof(TSource);
            
            var objType = typeof(object);

            if (!_cmType.IsInterface)
            {
                throw new Exception("Type has to be an interface");
            }

            const string assemblyName = "DynamicAseembly123";
            const string typeSignature = "DynamicType123";

            var assemblyBuilder =
                AssemblyBuilder.DefineDynamicAssembly(new AssemblyName(assemblyName), AssemblyBuilderAccess.RunAndCollect);
            
            
            var moduleBuilder = assemblyBuilder.DefineDynamicModule("Module123");

            _tb = moduleBuilder.DefineType(typeSignature,
                TypeAttributes.Public |
                TypeAttributes.Serializable |
                TypeAttributes.Class |
                TypeAttributes.Sealed |
                TypeAttributes.AutoLayout, objType);

            _tb.AddInterfaceImplementation(_cmType);

            _entityFieldBldr = EmitSourceField();

            _tb.DefineDefaultConstructor(
                MethodAttributes.Public |
                MethodAttributes.SpecialName |
                MethodAttributes.RTSpecialName);

            var constructorBuilder = _tb.DefineConstructor(MethodAttributes.Public,
                CallingConventions.Standard,
                new[] {_srcType});

            constructorBuilder.DefineParameter(0, ParameterAttributes.None, "entity");
            
            var constructorIl = constructorBuilder.GetILGenerator();
            constructorIl.Emit(OpCodes.Ldarg_0);
            constructorIl.Emit(OpCodes.Ldarg_1);
            constructorIl.Emit(OpCodes.Stfld, _entityFieldBldr);
            constructorIl.Emit(OpCodes.Ret);

            foreach (var (commonPrpName, (type, sourcePrpName)) in members)
            {
                EmitProperty(commonPrpName, type, sourcePrpName);
            }
            
            EmittedType = _tb.CreateType();
        }

        public Type EmittedType { get; }

        private FieldBuilder EmitSourceField()
        {
            var entityBldr = _tb.DefineField("_" + "entity", _srcType, FieldAttributes.Private);

            return entityBldr;
        }

        private void EmitProperty(string cPn, Type cmPt, string sPn)
        {
            var srcProp = _srcType.GetProperty(sPn, BindingFlags.Public | BindingFlags.Instance);
            var overrideProp = _cmType.GetProperty(cPn, BindingFlags.Public | BindingFlags.Instance);

            var overrideGetterPropMthdInfo = overrideProp.GetMethod ?? throw new Exception("Missing getter");
            var getterMethodInfo = srcProp.GetMethod ?? throw new Exception("Missing getter!");
            var getPropMthdBldr = _tb.DefineMethod($"get_{cPn}",
                MethodAttributes.Public |
                MethodAttributes.Virtual |
                MethodAttributes.SpecialName |
                MethodAttributes.HideBySig,
                cmPt, Type.EmptyTypes);
            
            var getIl = getPropMthdBldr.GetILGenerator();
            var getPropertyLbl = getIl.DefineLabel();
            var exitGetLbl = getIl.DefineLabel();

            getIl.MarkLabel(getPropertyLbl);
            getIl.Emit(OpCodes.Ldarg_0);
            getIl.Emit(OpCodes.Ldfld, _entityFieldBldr);
            getIl.Emit(OpCodes.Call, getterMethodInfo);
            getIl.Emit(OpCodes.Dup);
            getIl.MarkLabel(exitGetLbl);
            getIl.Emit(OpCodes.Ret);

            var overrideSetterPropMthdInfo = overrideProp.SetMethod ?? throw new Exception("Missing setter");
            var setterMethodInfo = srcProp.SetMethod ?? throw new Exception("Missing setter!");
            var setPropMthdBldr = _tb.DefineMethod($"set_{cPn}",
                MethodAttributes.Public |
                MethodAttributes.Virtual |
                MethodAttributes.SpecialName |
                MethodAttributes.HideBySig,
                null, new[] {cmPt});
         
            var setIl = setPropMthdBldr.GetILGenerator();
            var modifyPropertyLbl = setIl.DefineLabel();
            var exitSetLbl = setIl.DefineLabel();

            setIl.MarkLabel(modifyPropertyLbl);
            setIl.Emit(OpCodes.Ldarg_0);
            getIl.Emit(OpCodes.Ldfld, _entityFieldBldr);
            setIl.Emit(OpCodes.Ldarg_1);
            getIl.Emit(OpCodes.Call, setterMethodInfo);
            setIl.Emit(OpCodes.Nop);
            setIl.MarkLabel(exitSetLbl);
            setIl.Emit(OpCodes.Ret);

            _tb.DefineMethodOverride(getPropMthdBldr, overrideGetterPropMthdInfo);
            _tb.DefineMethodOverride(setPropMthdBldr, overrideSetterPropMthdInfo);
            
            var propertyBldr = _tb.DefineProperty(cPn, PropertyAttributes.None, cmPt, null);
            propertyBldr.SetGetMethod(getPropMthdBldr);
            propertyBldr.SetSetMethod(setPropMthdBldr);
        }
    }
}